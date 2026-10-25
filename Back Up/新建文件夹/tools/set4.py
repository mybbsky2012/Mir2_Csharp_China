# -*- coding: utf-8 -*-
import csv, io, collections, re

SRC = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

by_set = collections.defaultdict(list)
for r in rows:
    s = r["ItemSet"].strip()
    if s in ("非套装", "", "None"):
        continue
    by_set[s].append(r)

print("%-10s %5s %6s %-22s %s" % ("套装", "件数", "有说明", "等级", "套装属性"))
print("-" * 120)
no_tt = []
for s, lst in sorted(by_set.items(), key=lambda x: -len(x[1])):
    tts = [r["ItemToolTip"] for r in lst if "套装属性" in (r.get("ItemToolTip") or "")]
    lvs = sorted(set(int(r["ItemRequiredAmount"]) if r["ItemRequiredType"] == "Level" else -1 for r in lst))
    bonus = ""
    if tts:
        bonus = tts[0].replace("套装属性:", "").replace("\r\n", " / ").strip()
        # 检查是否所有件文案一致
        uniq = set(t.replace("套装属性:", "").replace("\r\n", " / ").strip() for t in tts)
        if len(uniq) > 1:
            bonus += "  <<<文案有 %d 种>>>" % len(uniq)
    else:
        no_tt.append(s)
    print("%-10s %5d %6d %-22s %s" % (s, len(lst), len(tts), str(lvs), bonus))

print()
print("无套装说明的套装:", no_tt)
print()
print("=== 逐套装的部位构成 ===")
for s, lst in sorted(by_set.items(), key=lambda x: -len(x[1])):
    pos = collections.Counter(r["ItemType"] for r in lst if r["ItemType"] != "杂物")
    cls = collections.Counter(r["ItemRequiredClass"] for r in lst)
    print("%-10s 部位=%s 职业=%s" % (s, dict(pos), dict(cls)))
