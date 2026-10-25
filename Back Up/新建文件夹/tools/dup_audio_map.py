# -*- coding: utf-8 -*-
"""用「内容相同的文件」把数字命名与语义命名对上，借此判断索引表偏移是否正确。"""
import os, re, hashlib, collections

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"

h2f = collections.defaultdict(list)
for f in os.listdir(D):
    if not f.lower().endswith('.wav'):
        continue
    p = os.path.join(D, f)
    if os.path.getsize(p) < 100:
        continue
    h2f[hashlib.md5(open(p, 'rb').read()).hexdigest()].append(f)

dups = {h: v for h, v in h2f.items() if len(v) > 1}
print("内容完全相同的文件组: %d 组" % len(dups))

pat = re.compile(r'(atk\d|attack\d|dmg|damage|die|death|stand|struck|hit)', re.I)
pairs = []
for h, v in dups.items():
    desc = [x for x in v if pat.search(x)]
    num = [x for x in v if not pat.search(x)]
    if desc and num:
        pairs.append((desc, num))
print("含「语义名 + 数字名」的组: %d 组" % len(pairs))
for desc, num in pairs[:30]:
    print("   %-46s ←→ %s" % (','.join(desc), ','.join(num)))

# 反向：数字名之间的重复（同一批音效被多个编号共用）
numonly = [(v) for v in dups.values() if all(not pat.search(x) for x in v)]
print()
print("纯数字名重复组: %d 组，前 12 组：" % len(numonly))
for v in numonly[:12]:
    print("   %s" % ','.join(v))
