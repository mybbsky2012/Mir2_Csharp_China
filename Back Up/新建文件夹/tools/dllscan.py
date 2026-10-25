# -*- coding: utf-8 -*-
import re, io, os

base = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server"
files = ["Server.Library.dll", "Server.dll", "Shared.dll"]
kw = ["SetBuffs", "SetInfo", "ItemSet", "套装", "Set.txt", ".txt"]

for fn in files:
    p = os.path.join(base, fn)
    if not os.path.exists(p):
        continue
    data = open(p, "rb").read()
    # 提取 UTF-16LE 字符串
    try:
        txt16 = data.decode("utf-16-le", errors="ignore")
    except Exception:
        txt16 = ""
    # 提取 ASCII
    txt8 = re.sub(rb"[^\x20-\x7e]", b"\n", data).decode("ascii", errors="ignore")
    hits = set()
    for k in kw:
        for m in re.finditer(re.escape(k), txt16):
            s = txt16[max(0, m.start()-60):m.start()+60].replace("\n", " ")
            hits.add("U16:" + s.strip())
        for m in re.finditer(re.escape(k), txt8):
            s = txt8[max(0, m.start()-60):m.start()+60].replace("\n", " ")
            hits.add("A8:" + s.strip())
    print("=====", fn, "hits:", len(hits))
    for h in sorted(hits)[:40]:
        print("   ", h[:150])
