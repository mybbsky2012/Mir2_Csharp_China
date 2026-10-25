# -*- coding: utf-8 -*-
"""导出候选载入底图缩略图：ChrSel 全部 800x600 帧 + Prguse 900~960 + Title 1290~1300，拼对照图。"""
import struct, gzip
from PIL import Image, ImageDraw

DATA = r"E:\micromir2\Data"


def load_lib(name):
    d = open(r"%s\%s.Lib" % (DATA, name), "rb").read()
    ver, cnt = struct.unpack_from("<ii", d, 0)
    il = list(struct.unpack_from("<%di" % cnt, d, 12))
    return d, cnt, il


def frame(d, il, idx):
    off = il[idx]
    w, h = struct.unpack_from("<hh", d, off)
    if w <= 0 or h <= 0:
        return Image.new("RGBA", (1, 1), (255, 0, 255, 255))
    ln = struct.unpack_from("<i", d, off + 13)[0]
    raw = d[off + 17: off + 17 + ln]
    try:
        px = gzip.decompress(raw)
    except Exception:
        return Image.new("RGBA", (w, h), (255, 0, 0, 255))
    if len(px) < w * h * 4:
        px = px + b"\x00" * (w * h * 4 - len(px))
    return Image.frombytes("RGBA", (w, h), px[:w * h * 4], "raw", "BGRA")


def brightness(im):
    im2 = im.convert("RGB").resize((1, 1))
    r, g, b = im2.getpixel((0, 0))
    return (r + g + b) // 3


cells = []

cd, cc, cil = load_lib("ChrSel")
for i in range(cc):
    off = cil[i]
    w, h = struct.unpack_from("<hh", cd, off)
    if w >= 700 and h >= 500:
        im = frame(cd, cil, i)
        cells.append(("ChrSel[%d]" % i, im, brightness(im)))

pd, pc, pil = load_lib("Prguse")
for i in range(900, 960):
    if i >= pc:
        break
    off = pil[i]
    w, h = struct.unpack_from("<hh", pd, off)
    if w >= 300 and h >= 200:
        im = frame(pd, pil, i)
        cells.append(("Prguse[%d]" % i, im, brightness(im)))

for libname in ("Title",):
    td, tc, til = load_lib(libname)
    for i in range(1280, min(1310, tc)):
        off = til[i]
        w, h = struct.unpack_from("<hh", td, off)
        if w >= 300 and h >= 200:
            im = frame(td, til, i)
            cells.append(("%s[%d]" % (libname, i), im, brightness(im)))

cols = 5
cw, ch = 240, 190
rows = (len(cells) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cw, rows * ch), (40, 40, 40))
dr = ImageDraw.Draw(sheet)
for k, (name, im, br) in enumerate(cells):
    x, y = (k % cols) * cw, (k // cols) * ch
    th = im.copy()
    th.thumbnail((cw - 10, ch - 26))
    sheet.paste(th, (x + 5, y + 20), th)
    dr.text((x + 5, y + 3), "%s  %dx%d  亮=%d" % (name, im.width, im.height, br), fill=(255, 255, 0))

out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\载入底图候选.png"
sheet.save(out)
print("saved:", out, " 共", len(cells), "张候选")
for name, im, br in sorted(cells, key=lambda c: -c[2])[:12]:
    print("%-16s %dx%d 平均亮度=%d" % (name, im.width, im.height, br))
