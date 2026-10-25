# -*- coding: utf-8 -*-
"""逐个怪物判定：索引表给 +2(攻击) / +4(受击) / +5(死亡) 指向的音频，内容对不对。"""
import os, re, hashlib, collections

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
LST = r"E:\micromir2\Sound\SoundList.lst"

SEM = [
    (re.compile(r'atk1|attack1', re.I), '攻击1'),
    (re.compile(r'atk2|attack2', re.I), '攻击2'),
    (re.compile(r'atk3|attack3', re.I), '攻击3'),
    (re.compile(r'atk4|attack4', re.I), '攻击4'),
    (re.compile(r'dmg|damage|behit|struck|_hit', re.I), '受击'),
    (re.compile(r'die|death', re.I), '死亡'),
    (re.compile(r'stand|move|walk|idle', re.I), '待机'),
]

h2names = collections.defaultdict(list)
for f in os.listdir(D):
    if f.lower().endswith('.wav') and os.path.getsize(os.path.join(D, f)) > 100:
        h2names[hashlib.md5(open(os.path.join(D, f), 'rb').read()).hexdigest()].append(f)

sem_of = {}
for h, names in h2names.items():
    s = set()
    for nm in names:
        for rx, lab in SEM:
            if rx.search(nm):
                s.add(lab)
    for nm in names:
        sem_of[nm] = s

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

stat = collections.Counter()
examples = collections.defaultdict(list)
for M in range(200):
    got = {}
    for o in (2, 4, 5):
        f = lst.get(M * 10 + o)
        if f:
            got[o] = sem_of.get(f, set())
    if not got:
        continue
    ok2 = '攻击1' in got.get(2, set())
    ok4 = '受击' in got.get(4, set())
    ok5 = '死亡' in got.get(5, set())
    have = [o for o in (2, 4, 5) if got.get(o)]
    if not have:
        continue
    good = sum([ok2, ok4, ok5])
    if good == len(have):
        stat['全部正确'] += 1
    elif good == 0:
        stat['全部不对'] += 1
        if len(examples['全部不对']) < 8:
            examples['全部不对'].append((M, {o: '/'.join(sorted(got[o])) or '?' for o in have}))
    else:
        stat['部分不对'] += 1
        if len(examples['部分不对']) < 8:
            examples['部分不对'].append((M, {o: '/'.join(sorted(got[o])) or '?' for o in have}))

print("按「偏移槽位内容是否正确」逐怪统计（只看内容可判定的怪）:")
for k, v in stat.most_common():
    print("   %s: %d 个图档" % (k, v))
for k, rows in examples.items():
    print()
    print("【%s】举例:" % k)
    for M, got in rows:
        print("   图档 %3d : %s" % (M, got))
