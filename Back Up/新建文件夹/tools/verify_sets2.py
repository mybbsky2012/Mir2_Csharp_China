# -*- coding: utf-8 -*-
import openpyxl
X = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2套装数据总表-1到70级-按职业.xlsx"
wb = openpyxl.load_workbook(X, read_only=True)

def sets_of(sn):
    ws = wb[sn]
    out = []
    for row in ws.iter_rows(min_row=2, values_only=True):
        if row[0] and row[0] not in [o[0] for o in out]:
            out.append((row[0], row[1], row[2]))
    return out

w = sets_of("战士"); g = sets_of("弓手"); c = sets_of("刺客")
print("战士", len(w), " 弓手", len(g), " 刺客", len(c))
wn = [x[0] for x in w]; gn = [x[0] for x in g]; cn = [x[0] for x in c]
print("弓手有、战士无:", [x for x in gn if x not in wn])
print("刺客有、战士无:", [x for x in cn if x not in wn])
print("战士有、弓手无:", [x for x in wn if x not in gn])
print()
print("--- 弓手套装 ---")
for x in g:
    print("  ", x)
print()
ws = wb["套装总览"]
print("--- 套装总览 ---")
for row in ws.iter_rows(min_row=1, values_only=True):
    print("  ", row)
