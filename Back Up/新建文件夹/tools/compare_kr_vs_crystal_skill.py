# -*- coding: utf-8 -*-
"""对比：韩版原表 mirsound.lst 的技能映射 vs Crystal 的兜底命名。
   索引换算：韩版 10000+ID*10+v   <->   Crystal 20000+Spell*10+v
"""
import re, os, collections

BS = chr(92)
LST = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
XLSX = "韩Mir2音效索引与规则表.xlsx"

names = {f.lower() for f in os.listdir(D)}

# 韩版映射
kr = {}
for ln in open(LST, encoding='euc-kr', errors='replace').read().splitlines():
    parts = re.split(r'[:\t]', ln.replace(' ', ''))
    if len(parts) < 2:
        continue
    try:
        i = int(parts[0])
    except ValueError:
        continue
    if not (10000 <= i < 20000):
        continue
    f = parts[-1].replace('wav' + BS, '').replace('wav/', '').strip()
    if i not in kr:
        kr[i] = f

# 技能名
import sys
sys.path.insert(0, 'tools')
from read_xlsx import read
spells = {}
for r in dict(read(XLSX))['技能音效表'][1:]:
    if len(r) < 3:
        continue
    try:
        n = int(r[1])
    except (ValueError, TypeError):
        continue
    spells[n] = r[0]

print("韩版技能段条目: %d，覆盖 Magic ID: %d 个" % (len(kr), len({(i - 10000) // 10 for i in kr})))
print()

same = diff = miss = 0
rows = []
for i in sorted(kr):
    f = kr[i]
    magic = (i - 10000) // 10
    var = (i - 10000) % 10
    fb = "M%d-%d.wav" % (magic, var)
    if not f:
        continue
    if f.lower() not in names:
        miss += 1
        continue
    if f.lower() == fb.lower():
        same += 1
    else:
        diff += 1
        rows.append((magic, var, f, fb, spells.get(magic, '?')))

print("存在且与兜底一致: %d" % same)
print("存在但与兜底不同: %d" % diff)
print("文件缺失: %d" % miss)
print()
print("=== 差异清单（韩版实际指向 != 兜底）===")
print("%-8s %-4s %-22s %-22s %s" % ("MagicID", "变体", "韩版指向", "Crystal兜底", "技能名"))
for magic, var, f, fb, nm in rows:
    print("%-8d +%-3d %-22s %-22s %s" % (magic, var, f, fb, nm))
