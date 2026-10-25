# -*- coding: utf-8 -*-
"""把试听页转成自包含单文件（音频以 data URI 内嵌）"""
import base64, os, re

BASE = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59"
D = os.path.join(BASE, "_work", "audio_audition")
src = os.path.join(D, "\u9053\u58eb\u6280\u80fd\u97f3\u6548\u8bd5\u542c.html")
html = open(src, encoding="utf-8").read()

# 收集所有出现的 wav 名
names = sorted(set(re.findall(r'[A-Za-z0-9_\-]+\.(?:wav|WAV|mp3)', html)), key=str.lower)
print("内嵌音频数:", len(names))

inject = ["<script>window.__B64__ = {"]
tot = 0
for n in names:
    p = os.path.join(D, n)
    if not os.path.exists(p):
        print("  跳过(不存在):", n); continue
    b = base64.b64encode(open(p, "rb").read()).decode()
    tot += len(b)
    inject.append('"%s":"data:audio/wav;base64,%s",' % (n, b))
inject.append("};</script>")
blob = "\n".join(inject)

# 在自定义 <script> 之前插入 __B64__，并把 new Audio(f) 改为取 data URI
html = html.replace("<script>\nconst DATA =", blob + "\n<script>\nconst DATA =")
html = html.replace("const a = new Audio(f);", "const a = new Audio((window.__B64__ && window.__B64__[f]) || f);")

out = os.path.join(D, "\u9053\u58eb\u6280\u80fd\u97f3\u6548\u8bd5\u542c-\u5355\u6587\u4ef6\u7248.html")
open(out, "w", encoding="utf-8").write(html)
print("OK ->", out)
print("size:", round(os.path.getsize(out) / 1048576, 2), "MB")
