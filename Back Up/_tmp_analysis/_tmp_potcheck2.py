# -*- coding: utf-8 -*-
import csv
import sys
from collections import Counter

sys.stdout.reconfigure(encoding="utf-8")

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"

with open(p, encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

print("columns:", list(rows[0].keys()))
pots = [x for x in rows if x["ItemType"].strip() == "药水"]
print("potions:", len(pots))
print("shapes:", dict(Counter(x["ItemShape"].strip() for x in pots)))
print("reqclass values:", dict(Counter(x.get("RequiredClass", "?").strip() for x in pots)))
print()

hdr = "{:<24}{:>7}{:>8}{:>8}{:>7}{:>8}{:>9}{:>8}".format(
    "name", "shape", "HP", "MP", "stack", "reqclass", "reqtype", "reqamt")
print(hdr)
print("-" * len(hdr))

for x in pots:
    print("{:<24}{:>7}{:>8}{:>8}{:>7}{:>8}{:>9}{:>8}".format(
        x["ItemName"].strip(), x["ItemShape"].strip(), x["StatHP"].strip(), x["StatMP"].strip(),
        x["ItemStackSize"].strip(), x.get("RequiredClass", "?").strip(),
        x.get("RequiredType", "?").strip(), x.get("RequiredAmount", "?").strip()))
