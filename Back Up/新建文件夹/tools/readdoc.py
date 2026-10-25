# -*- coding: utf-8 -*-
from docx import Document
p = r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/20260915Mir2Server/Envir/SetBuffs属性说明.docx"
d = Document(p)
for i, para in enumerate(d.paragraphs):
    if para.text.strip():
        print(para.text)
print("--- tables:", len(d.tables))
for t in d.tables:
    for r in t.rows:
        print(" | ".join(c.text for c in r.cells))
