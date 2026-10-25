# -*- coding: utf-8 -*-
from docx import Document
import openpyxl

D = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2装备数据总表-按职业等级整理.docx"
X = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2装备数据总表-按职业等级整理.xlsx"

doc = Document(D)
print("段落数:", len(doc.paragraphs), " 表格数:", len(doc.tables))
print("--- 各级标题 ---")
for p in doc.paragraphs:
    if p.style.name.startswith("Heading"):
        print(" ", p.style.name, "|", p.text)
print()
print("--- 表格规模（前25张）---")
for i, t in enumerate(doc.tables[:25]):
    print("  表%d: %d行 x %d列 | 表头=%s" % (i, len(t.rows), len(t.columns),
          " / ".join(c.text for c in t.rows[0].cells)))
print()
print("--- 抽样：某张表的行 ---")
t = doc.tables[3]
for r in t.rows[:4]:
    print("   ", " | ".join(c.text for c in r.cells))

print()
wb = openpyxl.load_workbook(X, read_only=True)
print("XLSX sheets:", wb.sheetnames)
for sn in wb.sheetnames:
    ws = wb[sn]
    print("  %s: %d 行 x %d 列" % (sn, ws.max_row, ws.max_column))
ws = wb["战士"]
for i, row in enumerate(ws.iter_rows(min_row=1, max_row=4, values_only=True)):
    print("   战士:", row)
