# -*- coding: utf-8 -*-
from docx import Document
D=r"C://Users//Administrator//WorkBuddy//2026-10-01-17-23-59//韩Mir2装备数据总表-按职业等级整理.docx"
d=Document(D)
import collections
c=collections.Counter()
for t in d.tables:/n    for r in t.rows[1:]:
        c[r.cells[3].text if len(r.cells)>3 else ""]+=1
print("归属分布:",dict(c))
print("属性需求段落数:",len([p for p in d.paragraphs if "属性需求" in p.text]))
print("末3表:",[(" ".join(x.text for x in t.rows[0].cells),len(t.rows)) for t in d.tables[-3:]])
