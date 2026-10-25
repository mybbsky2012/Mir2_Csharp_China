# -*- coding: utf-8 -*-
import csv, collections, io

PATH = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
SLOTS = ["武器", "盔甲", "头盔", "项链", "戒指", "手镯", "腰带", "靴子", "护身符", "守护石", "面甲"]

with io.open(PATH, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

def lv(r):
    try:
        return int(r["ItemRequiredAmount"])
    except:
        return -1

for s in SLOTS:
    sub = [r for r in rows if r["ItemType"] == s]
    if not sub:
        continue
    lvs = sorted(set(lv(r) for r in sub))
    print("== %s (n=%d) 等级取值: %s" % (s, len(sub), lvs[:40]))

print()
print("=== 各部位 x 职业 交叉数量 ===")
cls = ["战士", "法师", "道士", "刺客", "弓箭", "战法道", "战士刺客", "全职业"]
print("部位".ljust(8) + "".join(c.ljust(10) for c in cls))
for s in SLOTS:
    sub = [r for r in rows if r["ItemType"] == s]
    if not sub:
        continue
    cnt = collections.Counter(r["ItemRequiredClass"] for r in sub)
    print(s.ljust(8) + "".join(str(cnt.get(c, 0)).ljust(10) for c in cls))
