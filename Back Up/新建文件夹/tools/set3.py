# -*- coding: utf-8 -*-
import csv, io, collections

SRC = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

hits = [r for r in rows if "套装" in (r.get("ItemToolTip") or "")]
print("ItemToolTip 含『套装』的条目:", len(hits))
for r in hits[:40]:
    print("[%s] %s | 套装=%s | Lv=%s | %s" % (
        r["ItemIndex"], r["ItemName"], r["ItemSet"], r["ItemRequiredAmount"],
        r["ItemToolTip"].replace("\n", " ⏎ ")))

print()
print("=== 所有非空 ItemToolTip 里含其他关键词的 ===")
for kw in ["套装属性", "属性:"]:
    n = len([r for r in rows if kw in (r.get("ItemToolTip") or "")])
    print("  %s -> %d" % (kw, n))
