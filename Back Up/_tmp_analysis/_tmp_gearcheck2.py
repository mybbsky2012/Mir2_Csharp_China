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
GEN = {"男性": 1, "女性": 2, "性别不限": 3}

wanted = ["武器", "盔甲", "头盔", "项链", "手镯", "戒指", "腰带", "靴子", "守护石"]
classes = {"战士": 1, "法师": 2, "道士": 4, "刺客": 8, "弓箭": 16}

print("== 候选数（等级 32~45 的假人，要求等级 <=45）==")
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
            if (CLS.get(x["ItemRequiredClass"].strip(), 0) & cflag) == 0:
                continue
            rtype = x["ItemRequiredType"].strip()
            amt = i(x, "ItemRequiredAmount")
            if rtype == "Level" and amt > 45:
                continue
            n += 1
        line += "{:>8}".format(n)
    print(line)

print()
print("== 战士 各部位 top3 候选（分 = MaxDC + MaxAC*2 + MaxMAC*2 + HP/2）==")
for t in wanted:
    cands = []
    for x in rows:
        if x["ItemType"].strip() != t:
            continue
        if (CLS.get(x["ItemRequiredClass"].strip(), 0) & 1) == 0:
            continue
        rtype = x["ItemRequiredType"].strip()
        amt = i(x, "ItemRequiredAmount")
        if rtype == "Level" and amt > 45:
            continue
        score = i(x, "StatMaxDC") + i(x, "StatMaxAC") * 2 + i(x, "StatMaxMAC") * 2 + i(x, "StatHP") / 2
        cands.append((score, x))
    cands.sort(key=lambda z: -z[0])
    s = ", ".join("{} (Lv{}, 分{:.0f})".format(
        c[1]["ItemName"].strip(), c[1]["ItemRequiredAmount"], c[0]) for c in cands[:3])
    print("  {:<8} 共{:<5} -> {}".format(t, len(cands), s or "无"))

print()
print("== 潜在坑：有耐久(durability)为 0 的装备吗？==")
for t in wanted:
    bad = [x for x in rows if x["ItemType"].strip() == t and i(x, "ItemDurability") == 0]
    print("  {:<8} dur=0 的条目 {} 个 {}".format(t, len(bad), [b["ItemName"].strip() for b in bad[:5]]))
