# -*- coding: utf-8 -*-
import csv, io, collections

SRC = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

by_set = collections.defaultdict(list)
for r in rows:
    s = r["ItemSet"].strip()
    if s in ("非套装", "", "None"):
        continue
    by_set[s].append(r)

# 对比：非套装物品的 tooltip 分布（看哪些是"通用说明"）
alltt = collections.Counter((r.get("ItemToolTip") or "").strip() for r in rows if r["ItemSet"].strip() in ("非套装", "", "None"))
print("=== 非套装物品的 tooltip Top15 ===")
for k, v in alltt.most_common(15):
    print("   %4d  %r" % (v, k[:70]))

print()
print("=== 各套装物品的 tooltip 去重 ===")
for s, lst in sorted(by_set.items(), key=lambda x: -len(x[1])):
    tts = collections.Counter((r.get("ItemToolTip") or "").strip() for r in lst)
    nonempty = {k: v for k, v in tts.items() if k}
    print("\n【%s】%d件, 非空说明 %d 种" % (s, len(lst), len(nonempty)))
    for k, v in list(nonempty.items())[:6]:
        print("   x%-3d %r" % (v, k[:120]))
