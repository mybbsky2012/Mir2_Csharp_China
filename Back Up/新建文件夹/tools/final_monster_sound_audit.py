# -*- coding: utf-8 -*-
"""最终量化：每个怪物、每个动作偏移，客户端实际会播放什么。

链路：
  BaseSound = 图档号 * 10
  index = BaseSound + 偏移
  1) 索引表有该 index  -> 用表里的文件
  2) 表里没有          -> 兜底名 {图档:03d}-{偏移}.wav
逐个判定这个文件属于哪个图档（用索引表反查），得出「正确 / 张冠李戴 / 静音」。
"""
import os, re, sys, hashlib, collections
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from read_xlsx import read

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
LST = r"E:\micromir2\Sound\SoundList.lst"
XLSX = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2音效索引与规则表.xlsx"

exists = set(os.listdir(D))

# 索引表
lst = {}
for ln in open(LST, encoding='utf-8', errors='replace').read().splitlines():
    p = ln.replace(' ', '').replace('\t', ':').split(':')
    if len(p) < 2:
        continue
    try:
        i = int(p[0])
    except ValueError:
        continue
    lst.setdefault(i, p[-1].strip())

# 每个文件被哪个图档（们）在索引表里引用
file_owner = collections.defaultdict(set)
for i, f in lst.items():
    if f and i < 20000:
        file_owner[f.lower()].add(i // 10)

# 怪物清单
mon = []
for r in dict(read(XLSX))['怪物音效表'][1:]:
    if len(r) < 2:
        continue
    try:
        img = int(r[1])
    except (ValueError, TypeError):
        continue
    mon.append((r[0], img))
mon.sort(key=lambda x: x[1])

OFFNAME = {0: '出现/待机', 1: '平时音', 2: '攻击', 3: '挥动/行走', 4: '受击',
           5: '死亡', 6: '攻击2', 7: '攻击3', 8: '攻击4', 9: '攻击5'}

stat = collections.Counter()
wrong_examples = []
mon_bad = collections.Counter()
for name, img in mon:
    base = img * 10
    for o in range(10):
        idx = base + o
        if idx in lst:
            f = lst[idx]
            if not f:
                stat['表内空条目 -> 静音'] += 1
                mon_bad[img] += 1
            elif f not in exists:
                stat['表内文件缺失 -> 静音'] += 1
                mon_bad[img] += 1
            else:
                stat['表命中 -> 正常'] += 1
        else:
            fb = "%03d-%d.wav" % (img, o) if idx <= 20000 else "M%d-%d.wav" % ((idx - 20000) // 10, idx % 10)
            if fb not in exists:
                stat['兜底文件也不存在 -> 静音'] += 1
                mon_bad[img] += 1
            else:
                owners = file_owner.get(fb.lower(), set())
                if img in owners:
                    stat['兜底命中本怪 -> 正常'] += 1
                else:
                    stat['兜底撞别的怪 -> 张冠李戴'] += 1
                    mon_bad[img] += 1
                    if len(wrong_examples) < 18 and o in (1, 2, 4, 5):
                        wrong_examples.append((name, img, o, fb, sorted(owners)))

tot = sum(stat.values())
print("=== 客户端实际播放结果（%d 个怪 × 10 动作 = %d 槽位）===" % (len(mon), tot))
for k, v in stat.most_common():
    print("   %-28s %5d  (%5.1f%%)" % (k, v, v * 100.0 / tot))
print()
bad_mons = [i for i, c in mon_bad.items() if c > 0]
print("至少一个动作出问题的怪: %d / %d (%.1f%%)" % (len(bad_mons), len(mon), len(bad_mons) * 100.0 / len(mon)))
print()
print("=== 「张冠李戴」实例（攻击/受击/死亡/平时音）===")
for name, img, o, fb, owners in wrong_examples:
    print("   %-16s 图档%4d  %-6s(+%d) 播 %-14s  该文件属于图档 %s" %
          (name, img, OFFNAME[o], o, fb, owners or '（未被索引表引用=纯粹的历史遗留文件）'))
