#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Mir2 Crystal 服务端 —— 脚本命令 / GM 命令提取器

用途：
    从源码里重新提取 NPC 脚本命令、脚本变量、GM 命令清单，
    用于核对《NPC脚本命令与GM命令手册.md》是否与代码一致，
    或在你自己加了新命令之后重新生成清单。

用法：
    python extract_commands.py           # 打印统计 + 输出清单到 out/
    python extract_commands.py --list    # 只打印命令名列表

输出：
    out/01_检查命令.txt      检查命令（#IF 下用）
    out/02_动作命令.txt      动作命令（#ACT 下用）
    out/03_脚本变量.txt      脚本变量（<$XXX>）
    out/04_GM命令.txt        GM 命令（@ 开头）

数据来源：
    Server/MirObjects/NPC/NPCSegment.cs    ← 脚本命令 + 变量
    Server/MirObjects/PlayerObject.cs      ← GM 命令
"""

import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'out')

SEG = os.path.join(ROOT, 'Server', 'MirObjects', 'NPC', 'NPCSegment.cs')
PLAYER = os.path.join(ROOT, 'Server', 'MirObjects', 'PlayerObject.cs')

CASE_RE = re.compile(r'^\s*case\s+"([^"]+)"\s*:')


def load(path):
    if not os.path.exists(path):
        sys.exit('找不到文件: %s' % path)
    with io.open(path, encoding='utf-8-sig', errors='ignore') as f:
        return f.read().splitlines()


def find_line(lines, needle, start=0, end=None):
    """返回第一个包含 needle 的行号（0 基），找不到返回 -1"""
    if end is None:
        end = len(lines)
    for i in range(start, end):
        if needle in lines[i]:
            return i
    return -1


def collect_cases(lines, start, end):
    """在 [start, end) 行范围内收集所有 case 及其后续代码（直到下一个 case/default）"""
    rows = []
    i = start
    while i < end:
        m = CASE_RE.match(lines[i])
        if m:
            name = m.group(1)
            body = []
            j = i + 1
            while j < end and not CASE_RE.match(lines[j]) and not re.match(r'\s*default\s*:', lines[j]):
                s = lines[j].strip()
                if s:
                    body.append(s)
                j += 1
            rows.append((name, body))
            i = j
        else:
            i += 1
    return rows


def brief(body, keep=14):
    """从 case 代码里挑出有信息量的行"""
    keys = []
    for b in body:
        if ('parts.Length' in b or 'Add(new ' in b or 'TryParse' in b
                or 'ReceiveChat(' in b or 'HasFlag' in b or '=' in b and 'newValue' in b):
            keys.append(b)
        if len(keys) >= keep:
            break
    return keys


def extract_segment_commands():
    lines = load(SEG)

    pc = find_line(lines, 'public void ParseCheck')
    pa = find_line(lines, 'public void ParseAct')
    if pc < 0 or pa < 0:
        sys.exit('未能在 NPCSegment.cs 中定位 ParseCheck / ParseAct')

    chk_sw = find_line(lines, 'switch (parts[0].ToUpper())', pc, pa)
    act_sw = find_line(lines, 'switch (parts[0].ToUpper())', pa)

    # 动作 switch 内部还有第二个 switch：脚本变量
    var_sw = find_line(lines, 'switch (innerMatch)', act_sw)
    if var_sw < 0:
        var_sw = act_sw + 1
    # 变量 switch 的结束：下一个同名 switch（或下一个方法定义 / 文件末尾）
    var_end = find_line(lines, 'switch (innerMatch)', var_sw + 1)
    if var_end < 0:
        var_end = find_line(lines, 'private void Act(', var_sw)
    if var_end < 0:
        var_end = len(lines)

    checks = collect_cases(lines, chk_sw + 1, pa)
    acts = collect_cases(lines, act_sw + 1, var_sw)
    variables = collect_cases(lines, var_sw + 1, var_end)
    # 变量名只保留 <$XXX> 形态，并按名字去重（同名变量可能在不同分支重复出现）
    seen = set()
    uniq = []
    for n, b in variables:
        if not re.match(r'^[A-Z_0-9()]+$', n):
            continue
        if n in seen:
            continue
        seen.add(n)
        uniq.append((n, b))
    variables = uniq

    return checks, acts, variables


def extract_gm_commands():
    lines = load(PLAYER)
    start = find_line(lines, 'else if (message.StartsWith("@"))')
    if start < 0:
        sys.exit('未能定位 GM 命令入口（PlayerObject.Chat）')
    sw = find_line(lines, 'switch (parts[0].ToUpper())', start)
    end = find_line(lines, 'foreach (string command in Envir.CustomCommands)', sw)
    if end < 0:
        end = len(lines)
    return collect_cases(lines, sw + 1, end)


def dump(path, title, rows, full_body=False):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    buf = ['# %s' % title, '# 共 %d 条' % len(rows), '']
    for name, body in rows:
        buf.append('### %s' % name)
        for b in (body if full_body else brief(body)):
            buf.append('    ' + b)
        buf.append('')
    with io.open(path, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write('\n'.join(buf))


def main():
    checks, acts, variables = extract_segment_commands()
    gm = extract_gm_commands()

    print('=' * 54)
    print('检查命令（#IF 下用） : %3d 条' % len(checks))
    print('动作命令（#ACT 下用）: %3d 条' % len(acts))
    print('脚本变量 <$XXX>      : %3d 个' % len(variables))
    print('GM 命令 @XXX         : %3d 条' % len(gm))
    print('=' * 54)

    if '--list' in sys.argv:
        print('\n[检查命令]\n' + ', '.join(n for n, _ in checks))
        print('\n[动作命令]\n' + ', '.join(n for n, _ in acts))
        print('\n[脚本变量]\n' + ', '.join(n for n, _ in variables))
        print('\n[GM 命令]\n' + ', '.join(n for n, _ in gm))
        return

    dump(os.path.join(OUT, '01_检查命令.txt'), 'NPC 检查命令（#IF）', checks)
    dump(os.path.join(OUT, '02_动作命令.txt'), 'NPC 动作命令（#ACT）', acts)
    dump(os.path.join(OUT, '03_脚本变量.txt'), 'NPC 脚本变量 <$XXX>', variables, True)
    dump(os.path.join(OUT, '04_GM命令.txt'), 'GM 命令（@ 开头）', gm, True)

    print('\n清单已输出到: %s' % OUT)


if __name__ == '__main__':
    main()
