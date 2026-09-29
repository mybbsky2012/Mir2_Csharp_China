# -*- coding: utf-8 -*-
"""
列出微端包里「不该有」的文件（= 玩家运行时按需下载进来的）。

微端包只应包含：
  顶层   : 程序文件（Client.*、*.dll、*.exe、*.ini、*.pdb、*.ico）、microclient.flag
  DirectX/**、Localization/**
  Data/*.Lib（仅顶层，不递归）
  Data/Shaders/**
  Sound/SoundList.lst
  以及几个空目录占位

其余一律是运行时下载的资源，属于本地缓存，不该进包。
"""

import io
import os
import sys

MICRO = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client_Micro'

TOP_EXT = {'.dll', '.exe', '.ini', '.pdb', '.json', '.ico', '.flag', '.txt', '.log'}
TOP_EXACT = {'microclient.flag'}


def should_keep(rel):
    rel = rel.replace('\\', '/')
    parts = rel.split('/')
    name = parts[-1]
    low = rel.lower()

    # 顶层的程序与配置文件
    if len(parts) == 1:
        if name == 'microclient.flag':
            return True
        # 运行日志不算包的组成部分，但也不是下载缓存，单独归类
        if name in ('MicroClient.log', 'Error.txt'):
            return 'log'
        ext = os.path.splitext(name)[1].lower()
        return ext in TOP_EXT

    # 这几个目录整体保留
    if parts[0] in ('DirectX', 'Localization'):
        return True

    if parts[0] == 'Data':
        # Data 顶层图库
        if len(parts) == 2:
            return name.lower().endswith('.lib')
        # 着色器
        if parts[1] == 'Shaders':
            return True
        return False

    if parts[0] == 'Sound':
        return name == 'SoundList.lst'

    return False


def main():
    out = io.open(sys.stdout.fileno(), 'w', encoding='utf-8', buffering=1, closefd=False)

    keep = 0
    extra = []
    logs = []
    dirs = 0

    for root, dirnames, filenames in os.walk(MICRO):
        dirs += len(dirnames)
        for f in filenames:
            full = os.path.join(root, f)
            rel = os.path.relpath(full, MICRO)
            verdict = should_keep(rel)
            if verdict is True:
                keep += 1
            elif verdict == 'log':
                logs.append(rel)
            else:
                try:
                    size = os.path.getsize(full)
                except OSError:
                    size = 0
                extra.append((size, rel))

    extra.sort(reverse=True)

    out.write('微端包目录: %s\n' % MICRO)
    out.write('保留文件  : %d\n' % keep)
    out.write('多余文件  : %d\n' % len(extra))
    out.write('日志文件  : %d %s\n' % (len(logs), logs))
    out.write('\n--- 多余的（运行时下载进来的） ---\n')
    total = 0
    for size, rel in extra:
        total += size
        out.write('%10.2f MB  %s\n' % (size / 1048576.0, rel))
    out.write('\n合计 %.1f MB\n' % (total / 1048576.0))

    if len(extra) <= 40:
        out.write('\n--- 建议删除清单（%d 个） ---\n' % len(extra))
        for _, rel in extra:
            out.write(os.path.join(MICRO, rel.replace('/', os.sep)) + '\n')
        for rel in logs:
            out.write(os.path.join(MICRO, rel.replace('/', os.sep)) + '\n')

    return 0


if __name__ == '__main__':
    sys.exit(main())
