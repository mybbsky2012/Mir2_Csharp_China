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

CLS = {"战士": 1, "法师": 2, "道士": 4, "刺客": 8, "弓箭": 16,
       "战法道": 1 | 2 | 4, "战士刺客": 1 | 8, "全职业": 31}

wanted = ["武器", "盔甲", "头盔", "项链", "手镯", "戒指", "腰带", "靴子", "守护石"]

def score(x):
    return (i(x, "StatMaxDC") + i(x, "StatMaxMC") + i(x, "StatMaxSC")
            + i(x, "StatMaxAC") * 2 + i(x, "StatMaxMAC") * 2 + i(x, "StatHP") / 2)

print("== 战士：每个部位，RequiredAmount 落在 [25,45] 的 top3 ==")
for t in wanted:
    cands = []
    for x in rows:
        if x["ItemType"].strip() != t:
            continue
        if (CLS.get(x["ItemRequiredClass"].strip(), 0) & 1) == 0:
            continue
        if x["ItemRequiredType"].strip() != "Level":
            continue
        amt = i(x, "ItemRequiredAmount")
        if not (25 <= amt <= 45):
            continue
        cands.append((score(x), x))
    cands.sort(key=lambda z: -z[0])
    s = ", ".join("{} (Lv{}, 分{:.0f})".format(c[1]["ItemName"].strip(), c[1]["ItemRequiredAmount"], c[0])
                  for c in cands[:3])
    print("  {:<8} 共{:<5} -> {}".format(t, len(cands), s or "无"))

print()
print("== 战士：所有部位全部候选里，属性分 >120 的「异常强」条目 ==")
seen = []
for x in rows:
    if x["ItemType"].strip() not in wanted:
        continue
    if (CLS.get(x["ItemRequiredClass"].strip(), 0) & 1) == 0:
        continue
    if x["ItemRequiredType"].strip() != "Level":
        continue
    if i(x, "ItemRequiredAmount") > 45:
        continue
    sc = score(x)
    if sc > 120:
        seen.append((sc, x["ItemType"].strip(), x["ItemName"].strip(), i(x, "ItemRequiredAmount")))
seen.sort(key=lambda z: -z[0])
for sc, t, n, lv in seen[:25]:
    print("  {:<8} {:<24} Lv{:<4} 分{:.0f}".format(t, n, lv, sc))
print("  合计 {} 条".format(len(seen)))

print()
print("== 守护石：RequiredAmount 分布 ==")
d = defaultdict(int)
for x in rows:
    if x["ItemType"].strip() == "守护石":
        d[i(x, "ItemRequiredAmount")] += 1
print("  ", dict(sorted(d.items())))

print()
print("== 腰带 / 靴子：RequiredAmount 分布 ==")
for t in ["腰带", "靴子", "头盔"]:
    d = defaultdict(int)
    for x in rows:
        if x["ItemType"].strip() == t:
            d[i(x, "ItemRequiredAmount")] += 1
    print("  {:<6} {}".format(t, dict(sorted(d.items()))))

print()
print("== 法师/道士/刺客/弓箭：RequiredAmount 落在 [25,45] 的候选数 ==")
for cname, cflag in [("法师", 2), ("道士", 4), ("刺客", 8), ("弓箭", 16)]:
    line = "  {:<4}".format(cname)
    for t in wanted:
        n = 0
        for x in rows:
            if x["ItemType"].strip() != t:
                continue
            if (CLS.get(x["ItemRequiredClass"].strip(), 0) & cflag) == 0:
                continue
            if x["ItemRequiredType"].strip() != "Level":
                continue
            if 25 <= i(x, "ItemRequiredAmount") <= 45:
                n += 1
        line += " {}= {}".format(t, n)
    print(line)
