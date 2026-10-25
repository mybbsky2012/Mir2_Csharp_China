# -*- coding: utf-8 -*-
import collections
from docx import Document

D = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2装备数据总表-按职业等级整理.docx"
d = Document(D)

c = collections.Counter()
for t in d.tables:
    for r in t.rows[1:]:
        if len(r.cells) > 3:
            c[r.cells[3].text] += 1
print("归属分布:", dict(c))

print("属性需求 band 段落:", len([p for p in d.paragraphs if p.text.startswith("▍属性需求")]))
print("末3表:", [(" ".join(x.text for x in t.rows[0].cells), len(t.rows)) for t in d.tables[-3:]])

found = False
for t in d.tables:
    for r in t.rows[1:]:
        if len(r.cells) > 4 and "≥" in r.cells[0].text:
            print("属性需求样例:", [x.text for x in r.cells])
            found = True
            break
    if found:
        break
print("▍/◆ 分组段落数:", len([p for p in d.paragraphs if p.text.startswith(("▍", "◆"))]))
