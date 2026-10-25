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

print("套装数:", len(by_set))
for s, lst in sorted(by_set.items(), key=lambda x: -len(x[1])):
    types = collections.Counter(x["ItemType"] for x in lst)
    cls = collections.Counter(x["ItemRequiredClass"] for x in lst)
    lvs = sorted(set(int(x["ItemRequiredAmount"]) if x["ItemRequiredType"] == "Level" else -1 for x in lst))
    print("\n【%s】%d件" % (s, len(lst)))
    print("   部位:", dict(types))
    print("   职业:", dict(cls))
    print("   等级:", lvs)
    names = [x["ItemName"] for x in lst][:14]
    print("   样例:", "、".join(names))
