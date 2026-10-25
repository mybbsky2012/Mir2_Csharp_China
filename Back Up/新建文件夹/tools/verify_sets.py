# -*- coding: utf-8 -*-
import collections
from docx import Document
import openpyxl

D = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2套装数据总表-1到70级-按职业.docx"
X = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\韩Mir2套装数据总表-1到70级-按职业.xlsx"

doc = Document(D)
print("段落:", len(doc.paragraphs), " 表格:", len(doc.tables))
print("--- 标题 ---")
for p in doc.paragraphs:
    if p.style.name.startswith("Heading"):
        print("  ", p.style.name, "|", p.text)

print()
print("--- 全部 ◆ 套装明细节点 ---")
cur = None
for p in doc.paragraphs:
    if p.style.name == "Heading 1":
        cur = p.text
    if p.text.startswith("◆"):
        print("  [%s] %s" % (cur, p.text.replace("◆ ", "")))

print()
wb = openpyxl.load_workbook(X, read_only=True)
print("sheets:", wb.sheetnames)
for sn in wb.sheetnames:
    print("  %s: %d 行" % (sn, wb[sn].max_row))
