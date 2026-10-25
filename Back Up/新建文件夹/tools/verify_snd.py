# -*- coding: utf-8 -*-
import os, collections
from docx import Document
import openpyxl

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59"
D = os.path.join(OUT, "韩Mir2音效系统规则与写入指南.docx")
X = os.path.join(OUT, "韩Mir2音效索引与规则表.xlsx")

d = Document(D)
print("段落数:", len(d.paragraphs), " 表格数:", len(d.tables))
print("文件大小: %.0f KB" % (os.path.getsize(D) / 1024))
print()
print("=== 所有标题（一级/二级）===")
for p in d.paragraphs:
    t = p.text.strip()
    if not t:
        continue
    sz = p.runs[0].font.size.pt if p.runs and p.runs[0].font.size else 0
    if sz >= 12:
        print("  " * (0 if sz >= 15 else 1), t[:80])
print()
print("=== 各表格尺寸 ===")
for i, t in enumerate(d.tables):
    print("  表%2d  %2d 行 x %d 列   表头: %s" % (i + 1, len(t.rows), len(t.columns),
          " | ".join(c.text for c in t.rows[0].cells)[:90]))
print()
wb = openpyxl.load_workbook(X)
print("=== Excel sheets ===")
for ws in wb.worksheets:
    print("  %-20s %6d 行 x %d 列   %s" % (ws.title, ws.max_row, ws.max_column,
          " | ".join(str(ws.cell(1, c).value) for c in range(1, min(ws.max_column, 7) + 1))[:100]))
print()
# 抽检技能表与怪物表
ws = wb["技能音效表"]
print("技能表抽检:", [ws.cell(r, 1).value for r in (2, 3, 30)])
ws = wb["怪物音效表"]
for r in range(2, ws.max_row + 1):
    if ws.cell(r, 1).value == "Skeleton":
        print("Skeleton 行:", [ws.cell(r, c).value for c in range(1, ws.max_column + 1)])
        break
ws = wb["问题清单"]
print("问题清单前3行:", [[ws.cell(r, c).value for c in range(1, 7)] for r in (2, 3, 4)])
