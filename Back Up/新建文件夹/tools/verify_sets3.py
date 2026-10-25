# -*- coding: utf-8 -*-
from docx import Document
D = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2套装数据总表-1到70级-按职业.docx"
d = Document(D)
ps = [p.text for p in d.paragraphs]
grab = False
n = 0
for t in ps:
    if t.startswith("◆ 昆仑套装"):
        grab = True
    if grab:
        print(t)
        n += 1
        if n > 16:
            break
print()
print("=== 无说明提示出现的次数:", sum(1 for t in ps if "未提供" in t))
print("=== 套装属性行样例 ===")
for t in ps:
    if t.startswith("★ 套装属性") and "档" in t:
        print("  ", t)
print()
print("=== 表格数:", len(d.tables), " ===")
# 抽一张昆仑明细表
for tb in d.tables:
    hdr = " ".join(c.text for c in tb.rows[0].cells)
    if hdr.startswith("部位 等级") and len(tb.rows) > 25:
        for r in tb.rows[:4]:
            print("   ", " | ".join(c.text for c in r.cells))
        print("    ... 共 %d 行" % len(tb.rows))
        break
