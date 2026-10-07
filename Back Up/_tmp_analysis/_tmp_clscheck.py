# -*- coding: utf-8 -*-
import csv
from collections import Counter

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"

with open(p, encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

print("ItemRequiredClass 取值分布:")
for k, v in Counter(x["ItemRequiredClass"].strip() for x in rows).most_common():
    print("  {!r:<14} {}".format(k, v))

print()
print("ItemRequiredGender 取值分布:")
for k, v in Counter(x["ItemRequiredGender"].strip() for x in rows).most_common():
    print("  {!r:<14} {}".format(k, v))

print()
print("样例（武器前 8 行）:")
for x in [y for y in rows if y["ItemType"].strip() == "武器"][:8]:
    print("  {:<16} cls={!r:<10} gender={!r:<10} reqtype={!r:<8} amt={:<5} shape={} dur={}".format(
        x["ItemName"].strip(), x["ItemRequiredClass"].strip(), x["ItemRequiredGender"].strip(),
        x["ItemRequiredType"].strip(), x["ItemRequiredAmount"], x["ItemShape"], x["ItemDurability"]))
