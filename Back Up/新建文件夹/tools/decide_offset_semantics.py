# -*- coding: utf-8 -*-
"""最终判定：索引表里「每个偏移」实际拿到的，是什么动作的声音。

做法：
 1. 按内容(md5)给文件分组；同名内容里只要出现语义命名（atk1/dmg/die/stand…），
    就说明这组音频的语义。
 2. 走一遍 SoundList.lst，把「索引 -> 文件」换成「索引 -> 语义」。
 3. 按「索引 % 10（偏移）」统计语义分布，看客户端拿到的动作对不对。
"""
import os, re, hashlib, collections

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
LST = r"E:\micromir2\Sound\SoundList.lst"

SEM = [
    (re.compile(r'atk1|attack1', re.I), '攻击1'),
    (re.compile(r'atk2|attack2', re.I), '攻击2'),
    (re.compile(r'atk3|attack3', re.I), '攻击3'),
    (re.compile(r'atk4|attack4', re.I), '攻击4'),
    (re.compile(r'dmg|damage|behit|_hit|struck|struck', re.I), '受击'),
    (re.compile(r'die|death', re.I), '死亡'),
    (re.compile(r'stand|move|walk|idle', re.I), '待机/行走'),
]

md5map = collections.defaultdict(list)
for f in os.listdir(D):
    if not f.lower().endswith('.wav'):
        continue
    p = os.path.join(D, f)
    if os.path.getsize(p) < 100:
        continue
    md5map[hashlib.md5(open(p, 'rb').read()).hexdigest()].append(f)

file_sem = {}
known = 0
for h, names in md5map.items():
    sems = set()
    for nm in names:
        for rx, label in SEM:
            if rx.search(nm):
                sems.add(label)
    for nm in names:
        file_sem[nm] = sems
    if sems:
        known += 1
print("有语义线索的音频组: %d / %d" % (known, len(md5map)))

lst = {}
for ln in open(LST, encoding='utf-8', errors='replace').read().splitlines():
    p = ln.replace(' ', '').replace('\t', ':').split(':')
    if len(p) <= 1:
        continue
    try:
        i = int(p[0])
    except ValueError:
        continue
    if i not in lst:
        lst[i] = p[-1]

OFF = {0: '出现/召唤', 1: '被推动·远程默认·(代码里死亡也用)', 2: '★攻击', 3: '挥刀/行走',
       4: '★受击', 5: '★死亡', 6: '攻击2', 7: '攻击3', 8: '攻击4/走跑跳', 9: '冲刺/攻击5'}
tally = collections.defaultdict(collections.Counter)
for i, f in lst.items():
    if i >= 20000 or not f:
        continue
    sems = file_sem.get(f)
    if not sems:
        continue
    for s in sems:
        tally[i % 10][s] += 1

print()
print("客户端偏移 -> 实际拿到的音频语义（按内容去重判定）")
for o in range(10):
    c = tally.get(o)
    if not c:
        continue
    tot = sum(c.values())
    top = '、'.join('%s %d' % (k, v) for k, v in c.most_common(4))
    print("  +%d  %-32s 共%4d 条：%s" % (o, OFF[o], tot, top))

print()
print("抽几个怪看 .lst 的实际指向：")
for M, name in [(43, '羊'), (25, '多钩猫'), (20, '骷髅'), (26, '钉耙猫')]:
    segs = []
    for o in range(6):
        f = lst.get(M * 10 + o)
        sem = '/'.join(sorted(file_sem.get(f, []))) if f else ('空' if f == '' else '无条目')
        segs.append('+%d:%s(%s)' % (o, f or '—', sem or '?'))
    print("  图档 %3d %-6s %s" % (M, name, ' '.join(segs)))
