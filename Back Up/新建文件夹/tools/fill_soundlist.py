# -*- coding: utf-8 -*-
"""重写 SoundList.lst：补齐每个图档的 0~9 号动作槽，根除「兜底命名撞车」。

为什么需要
----------
客户端 SoundManager.PlaySound 在索引表里找不到 index 时，会自己编一个文件名
    {index/10:000}-{index%10}.wav        (index < 20000)
而韩版资源包里同名的 `0NN-o.wav` 是**另一个怪**的音频，一命中就张冠李戴。
只要索引表里对每个 index 都有条目（哪怕是空条目），这条兜底路径就永不触发。

本怪「可用音频集合」怎么取
--------------------------
不能假定文件号 = 图档*10+200（狼系实际用 900-*.wav）。正确做法是**从本怪自己在
索引表里的条目反推**：把本怪 0~9 号索引上已经指向、且资源包里真实存在的文件收进集合 S。
S 为空时才用同族替代表，再为空就写空条目（静音）。

写回策略
--------
- 已有条目且文件存在        -> 原样保留（含指向别的怪做变体复用的，那是有意为之）
- 已有条目但值是空          -> 用 S 补上（原版留白 = 静音，是「怪物出场没叫声」的主因）
- 已有条目但文件不存在      -> 用 S 里的替代
- 完全没有该索引            -> 用 S 里的替代；S 空则写空条目挡住兜底

索引 0 例外
-----------
SoundList.None = 0 / SoundList.Music = 0，这个索引被用作「无音效」标记，
必须永远留空，不能让它指向任何怪物采样。

大小写
------
包内存在 470-1.WAV 这类大写扩展名文件。Windows 文件系统与 HTTP 服务都不区分大小写，
所以判存必须按小写比较，否则会误判为「文件不存在」而把好好的指向改掉。
"""
import os, re, sys, collections

BASE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE)
from read_xlsx import read

SOUND = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
# 基准必须是**韩版原始** SoundList.lst，不能是上一次生成的结果，
# 否则脚本会基于自己的输出再跑一遍，错误会被固化、无法修正。
SRC_LST = os.path.join(BASE, "SoundList.orig.lst")
XLSX = os.path.join(os.path.dirname(BASE), "韩Mir2音效索引与规则表.xlsx")
OUT_LST = os.path.join(BASE, "SoundList.new.lst")

MAX_IMG = 999
OFFSETS = list(range(10))

# 缺某偏移时，按动作语义就近复用本怪其它采样的优先次序
PREFER = {
    0: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
    1: [1, 0, 2, 3, 4, 5, 6, 7, 8, 9],
    2: [2, 6, 7, 1, 0, 3, 4, 5, 8, 9],
    3: [3, 1, 0, 2, 4, 5, 6, 7, 8, 9],
    4: [4, 1, 2, 0, 3, 5, 6, 7, 8, 9],
    5: [5, 4, 1, 2, 0, 3, 6, 7, 8, 9],
    6: [6, 2, 7, 1, 0, 3, 4, 5, 8, 9],
    7: [7, 2, 6, 1, 0, 3, 4, 5, 8, 9],
    8: [8, 6, 2, 7, 1, 0, 3, 4, 5, 9],
    9: [9, 6, 2, 7, 1, 0, 3, 4, 5, 8],
}

# 同族替代（只在资源包里真的没有任何音频时启用；保守，只列同种族的）
DONOR = {
    49: 48,   # SandWorm 沙虫        -> VisceralWorm 内脏虫
    67: 56,   # MutatedHugger 变异拥抱者 -> Hugger 拥抱者
}


def main():
    exist = set(os.listdir(SOUND))
    # Windows 文件系统 / HTTP 服务都不区分大小写：判存一律按小写比较
    exist_lower = {f.lower() for f in exist}

    def has(fn):
        return bool(fn) and fn.lower() in exist_lower

    # 全服怪物清单（图档号），仅用于统计与同族替代
    mon_imgs = []
    for r in dict(read(XLSX))['怪物音效表'][1:]:
        if len(r) < 2:
            continue
        try:
            mon_imgs.append(int(r[1]))
        except (ValueError, TypeError):
            continue

    # 覆盖整个 0~9999 段：图档 0~999 全部显式定义，
    # 这样即使有清单之外的怪，也绝不会再掉进客户端的兜底命名。
    imgs = list(range(0, MAX_IMG + 1))

    # 原表
    with open(SRC_LST, 'rb') as fp:
        raw = fp.read()
    text = raw.decode('utf-8-sig')
    lines = text.splitlines()
    has_bom = raw.startswith(b'\xef\xbb\xbf')

    present_line = {}          # index -> 行号（首次出现）
    present_val = {}           # index -> 值
    for n, ln in enumerate(lines):
        p = ln.replace(' ', '').replace('\t', ':').split(':')
        if len(p) < 2:
            continue
        try:
            i = int(p[0])
        except ValueError:
            continue
        if i not in present_val:
            present_val[i] = p[-1].strip()
            present_line[i] = n

    # 本怪可用集合 S
    # 1) 原表本段条目里已经指向、且磁盘上真实存在的文件
    # 2) 同「文件前缀」的其它偏移 —— 原表常漏写若干偏移，但磁盘上文件是齐的
    #    （图档 21 的文件前缀是 410，磁盘上 410-0/1/2/3/4/5/6/7/8 都在，
    #     原表只写了 1/2/4/5，把 410-0 捞回来正好补上「出现音」）
    def own_set(img):
        s = {}
        prefixes = set()
        for o in OFFSETS:
            v = present_val.get(img * 10 + o)
            if has(v):
                s[o] = v
                m = re.match(r'^(\d+)-(\d)\.[a-z0-9]+$', v, re.I)
                if m:
                    prefixes.add(m.group(1))
        for pre in prefixes:
            for o in OFFSETS:
                if o in s:
                    continue
                cand = "%s-%d.wav" % (pre, o)
                if has(cand):
                    s[o] = cand
        return s

    S = {i: own_set(i) for i in imgs}

    st = collections.Counter()
    changed = []
    appended = []

    for img in imgs:
        s = S[img]
        if not s and img in DONOR:
            s = S.get(DONOR[img], {})
            if s:
                st['用同族替代'] += len([1 for o in OFFSETS])
        for o in OFFSETS:
            idx = img * 10 + o
            cur = present_val.get(idx)
            if idx == 0:
                # SoundList.None / Music 专用：显式留空（既不指向音频，也不让客户端走兜底）
                new = ''
                st['索引0留空'] += 1
            elif has(cur):
                st['保留原指向'] += 1
                continue
            else:
                if cur == '' and idx in present_val:
                    st['填补原版留白'] += 1
                pick = next((x for x in PREFER[o] if x in s), None)
                new = s[pick] if pick is not None else ''
                if new:
                    st['补本怪音频'] += 1
                else:
                    st['补空条目挡兜底'] += 1
            if idx in present_line:
                changed.append((present_line[idx], idx, new))
            else:
                appended.append((idx, new))

    # 就地改写
    for ln_no, idx, new in changed:
        lines[ln_no] = "%04d: %s" % (idx, new) if new else "%04d:" % idx

    appended.sort()
    out = list(lines)
    while out and out[-1].strip() == '':
        out.pop()
    out.append('')
    out.append(';;====== 自动补齐：让每个图档的 0~9 号动作槽都有明确指向 ======')
    out.append(';;原因：客户端在索引表里找不到 index 时会把文件名猜成 "{图档:03d}-{偏移}.wav"，')
    out.append(';;      而韩版资源包里同名文件属于别的怪，一命中就张冠李戴（怪物发出别人的声音）。')
    out.append(';;做法：本怪自己的采样按动作语义就近复用；本怪完全没有音频时留空条目（=静音），')
    out.append(';;      这样那条兜底路径永远不会再触发。原表已有的指向一律不动。')
    out.append(';;由 tools/fill_soundlist.py 自动生成，请勿手工编辑本段。')
    for idx, val in appended:
        out.append("%04d: %s" % (idx, val) if val else "%04d:" % idx)
    out.append('')

    with open(OUT_LST, 'w', encoding='utf-8-sig' if has_bom else 'utf-8', newline='\r\n') as fp:
        fp.write('\n'.join(out))

    print("覆盖图档 %d 个（其中真实怪物 %d 个）" % (len(imgs), len(mon_imgs)))
    for k, v in st.most_common():
        print("   %-14s %5d" % (k, v))
    print("就地改写 %d 行，追加 %d 行" % (len(changed), len(appended)))
    print("输出 %s  (%d 字节)  原表 %d 字节" % (OUT_LST, os.path.getsize(OUT_LST), os.path.getsize(SRC_LST)))


if __name__ == '__main__':
    main()
