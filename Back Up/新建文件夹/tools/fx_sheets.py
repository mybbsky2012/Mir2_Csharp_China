# -*- coding: utf-8 -*-
"""导出 Magic/Magic2 图库的特效帧段，便于挑选召唤出场特效"""
import struct, gzip, os, sys
from PIL import Image

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools"
DATA = r"E:\micromir2\Data"

def load(path):
    D = open(path, "rb").read()
    ver, count = struct.unpack_from("<ii", D, 0)
    il = list(struct.unpack_from("<%di" % count, D, 12))
    return D, il, count

def frame(D, off):
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    need = w * h * 4
    if len(px) < need:
        px = px + b"\x00" * (need - len(px))
    im = Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")
    return im, x, y

def sheet(libname, start, end, cols=10, scale=1):
    path = os.path.join(DATA, libname + ".Lib")
    D, il, count = load(path)
    lo, hi = max(0, start), min(end, count - 1)
    rows = []
    for i in range(lo, hi + 1):
        try:
            im, x, y = frame(D, il[i])
        except Exception:
            continue
        if im.width <= 6 or im.height <= 6:
            continue
        if scale != 1:
            im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        rows.append((i, im))
    if not rows:
        print(f"{libname} {start}-{end}: 无帧")
        return
    ch = max(im.height for _, im in rows) + 18
    cw = max(im.width for _, im in rows) + 8
    ncol = min(cols, len(rows))
    nrow = (len(rows) + ncol - 1) // ncol
    out = Image.new("RGB", (cw * ncol, ch * nrow), (45, 45, 48))
    from PIL import ImageDraw
    dr = ImageDraw.Draw(out)
    for k, (i, im) in enumerate(rows):
        cx, cy = (k % ncol) * cw, (k // ncol) * ch
        out.paste(im, (cx + 4, cy + 16), im)
        dr.text((cx + 5, cy + 2), str(i), fill=(255, 220, 120))
    p = os.path.join(OUT, f"sheets/{libname}_{start}_{end}.png")
    os.makedirs(os.path.dirname(p), exist_ok=True)
    out.save(p)
    print(f"{libname} {lo}-{hi}: {len(rows)} 帧 -> {p} ({out.width}x{out.height})")

for a, b in [(1500, 1549), (1550, 1599)]:
    sheet("Magic", a, b)
sheet("Magic2", 0, 49)
