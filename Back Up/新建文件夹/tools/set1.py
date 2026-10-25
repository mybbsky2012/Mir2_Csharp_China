# -*- coding: utf-8 -*-
import csv, io, collections

SRC = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

sets = collections.Counter(r["ItemSet"].strip() for r in rows)
print("ItemSet 取值数:", len(sets))
print("---- 非『非套装』的取值 ----")
n = 0
for k, v in sets.most_common():
    if k in ("非套装", "", "None"):
        continue
    print("  %-30s %d" % (k, v))
    n += 1
print("非空套装名数量:", n, " 总条目:", sum(v for k, v in sets.items() if k not in ("非套装", "", "None")))
print()
print("『非套装』:", sets.get("非套装", 0), " 其他:", total_other if False else "")
