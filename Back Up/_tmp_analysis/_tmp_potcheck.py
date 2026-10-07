# -*- coding: utf-8 -*-
import csv

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"

with open(p, encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

print("total items:", len(rows))

pots = [x for x in rows if x["ItemType"].strip() == "药水"]
print("potion items:", len(pots))
print()

hdr = "{:<26}{:>6}{:>8}{:>8}{:>6}{:>7}".format("name", "shape", "HP", "MP", "stack", "weight")
print(hdr)
print("-" * len(hdr))

for x in pots:
    print("{:<26}{:>6}{:>8}{:>8}{:>6}{:>7}".format(
        x["ItemName"].strip(), x["ItemShape"], x["StatHP"], x["StatMP"],
        x["ItemStackSize"], x["ItemWeight"]))

# 统计每个 shape 的数量
from collections import Counter
print()
print("shape counts:", dict(Counter(x["ItemShape"] for x in pots)))
