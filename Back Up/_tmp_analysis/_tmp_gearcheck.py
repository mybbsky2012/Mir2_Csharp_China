# -*- coding: utf-8 -*-
import csv
from collections import defaultdict

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"

with open(p, encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

def i(x, k, d=0):
    try:
        return int(str(x.get(k, "")).strip() or d)
    except Exception:
        return d

# 代码里用到的 ItemType
wanted = ["武器", "盔甲", "头盔", "项链", "手镯", "戒指", "腰带", "靴子", "守护石"]

classes = {"战士": 1, "法师": 2, "道士": 4, "刺客": 8, "弓箭": 16}

print("== 各 ItemType 的物品总数 ==")
counts = defaultdict(int)
for x in rows:
    counts[x["ItemType"].strip()] += 1
for t in wanted:
    print("  {:<8} {}".format(t, counts.get(t, 0)))

print()
print("== 每个部位 x 每个职业，RequiredType=Level 且 RequiredAmount<=45 的候选数 ==")
print("（假人等级 32~45，要求等级必须 <=45 才穿得上）")
print()

# CSV 中 RequiredType 的取值
rt = defaultdict(int)
for x in rows:
    rt[x["ItemRequiredType"].strip()] += 1
print("ItemRequiredType 取值:", dict(rt))
print()

hdr = "{:<8}".format("部位") + "".join("{:>8}".format(c) for c in classes)
print(hdr)
print("-" * len(hdr))

for t in wanted:
    line = "{:<8}".format(t)
    for cname, cflag in classes.items():
        n = 0
        for x in rows:
            if x["ItemType"].strip() != t:
                continue
            rc = i(x, "ItemRequiredClass")
            if (rc & cflag) == 0:
                continue
            rtype = x["ItemRequiredType"].strip()
            amt = i(x, "ItemRequiredAmount")
            if rtype == "Level" and amt > 45:
                continue
            n += 1
        line += "{:>8}".format(n)
    print(line)

print()
print("== 取样：战士 各部位的候选（按 MaxAC+MaxDC 排序取前3） ==")
for t in wanted:
    cands = []
    for x in rows:
        if x["ItemType"].strip() != t:
            continue
        if (i(x, "ItemRequiredClass") & 1) == 0:
            continue
        rtype = x["ItemRequiredType"].strip()
        amt = i(x, "ItemRequiredAmount")
        if rtype == "Level" and amt > 45:
            continue
        score = i(x, "StatMaxDC") + i(x, "StatMaxAC") * 2 + i(x, "StatMaxMAC") * 2
        cands.append((score, x))
    cands.sort(key=lambda z: -z[0])
    names = ", ".join("{} (Lv{}, 分{})".format(
        c[1]["ItemName"].strip(), c[1]["ItemRequiredAmount"], c[0]) for c in cands[:3])
    print("  {:<8} 共{:<4} -> {}".format(t, len(cands), names or "无"))
