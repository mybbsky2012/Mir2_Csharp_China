# -*- coding: utf-8 -*-
"""核对韩版原表 mirsound.lst 里技能音效段引用的文件是否存在于资源包。"""
import re, os, collections

BS = chr(92)
LST = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"

names = {f.lower() for f in os.listdir(D)}
rows = open(LST, encoding='euc-kr', errors='replace').read().splitlines()

refs = collections.OrderedDict()
for ln in rows:
    parts = re.split(r'[:\t]', ln.replace(' ', ''))
    if len(parts) < 2:
        continue
    try:
        i = int(parts[0])
    except ValueError:
        continue
    f = parts[-1].replace('wav' + BS, '').replace('wav/', '').strip()
    if not f:
        continue
    if i not in refs:
        refs[i] = f

sk = {i: f for i, f in refs.items() if 10000 <= i < 12000}
miss = [(i, f) for i, f in sorted(sk.items()) if f.lower() not in names]
has = [(i, f) for i, f in sorted(sk.items()) if f.lower() in names]

print("韩版原表技能段(10000~11999): 条目 %d，文件存在 %d，缺失 %d" % (len(sk), len(has), len(miss)))
print()
print("=== 存在的（前 40）===")
for i, f in has[:40]:
    print("   idx%-6d MagicID %-3d +%d -> %s" % (i, (i - 10000) // 10, (i - 10000) % 10, f))
print()
print("=== 缺失的（全部）===")
for i, f in miss:
    print("   idx%-6d MagicID %-3d +%d -> %-22s 缺失" % (i, (i - 10000) // 10, (i - 10000) % 10, f))

# 道士段明细
print()
print("=== 道士段 10600~10900 明细 ===")
for i in sorted(k for k in sk if 10595 <= k <= 10920):
    f = sk[i]
    ok = '有' if f.lower() in names else '缺'
    print("   idx%-6d MagicID %-3d +%d -> %-22s %s" % (i, (i - 10000) // 10, (i - 10000) % 10, f, ok))
