# -*- coding: utf-8 -*-
import csv, collections, io, sys

PATH = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"

with io.open(PATH, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

print("总行数:", len(rows))
print("字段数:", len(rows[0].keys()))
print()

def dist(col):
    c = collections.Counter(r[col] for r in rows)
    return c

for col in ["ItemType", "ItemRequiredClass", "ItemRequiredType", "ItemGrade", "ItemRequiredGender"]:
    print("=== %s ===" % col)
    for k, v in dist(col).most_common():
        print("  %r : %d" % (k, v))
    print()

# 装备类目下的等级分布
print("=== 各 ItemType 的 ItemRequiredAmount 范围 ===")
bytype = collections.defaultdict(list)
for r in rows:
    try:
        lv = int(r["ItemRequiredAmount"])
    except:
        lv = -1
    bytype[r["ItemType"]].append(lv)
for t, lvs in sorted(bytype.items(), key=lambda x: -len(x[1])):
    print("  %s: n=%d min=%d max=%d" % (t, len(lvs), min(lvs), max(lvs)))
