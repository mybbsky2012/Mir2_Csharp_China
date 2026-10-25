# -*- coding: utf-8 -*-
import os
DB = r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.MirADB"
D = open(DB,'rb').read()

def show(nm, off, back=180, fwd=260):
    print("="*80)
    print("角色:", nm, " @", off)
    s = max(0, off-back); e = min(len(D), off+fwd)
    seg = D[s:e]
    for k in range(0, len(seg), 32):
        chunk = seg[k:k+32]
        hexs = ' '.join('%02x'%c for c in chunk)
        txt = ''.join(chr(c) if 32 <= c < 127 else '.' for c in chunk)
        print("  %6d  %-96s  %s" % (s+k, hexs, txt))

for nm, off in [("一刀剁了你", 7717), ("道爷爷", 17649)]:
    show(nm, off)
