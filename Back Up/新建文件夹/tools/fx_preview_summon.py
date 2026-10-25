# -*- coding: utf-8 -*-
"""召唤出场特效候选预览图：骷髅 3 个候选 + 神兽现用帧"""
import struct, gzip, os
from PIL import Image, ImageDraw, ImageFont

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\sheets"
DATA = r"E:\micromir2\Data"
FONT = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 22)

def load(p):
    D = open(p, "rb").read()
    ver, count = struct.unpack_from("<ii", D, 0)
    return D, list(struct.unpack_from("<%di" % count, D, 12))

def frame(D, il, i):
    off = il[i]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    need = w * h * 4
    if len(px) < need:
        px = px + b"\x00" * (need - len(px))
    return Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")

mD, mI = load(os.path.join(DATA, "Magic.Lib"))
m2D, m2I = load(os.path.join(DATA, "Magic2.Lib"))
m3D, m3I = load(os.path.join(DATA, "Magic3.Lib"))

ROWS = [
    ("骷髅候选A（现用）  Magic.Lib 1500-1509  蓝白光点汇聚", mD, mI, 1500, 1509),
    ("骷髅候选B          Magic.Lib 1520-1529  白色烟尘", mD, mI, 1520, 1529),
    ("骷髅候选C          Magic3.Lib 1571-1578 地面火柱", m3D, m3I, 1571, 1578),
    ("神兽（现用，即你说的红色汇聚）  Magic2.Lib 0-8 橙红花瓣汇聚", m2D, m2I, 0, 8),
]

CELL_H = 200
CELL_W = 200
LBL = 44
W = CELL_W * 10 + 40
H = LBL + CELL_H * len(ROWS) + 30
out = Image.new("RGB", (W, H), (34, 34, 38))
dr = ImageDraw.Draw(out)

for r, (label, D, il, a, b) in enumerate(ROWS):
    y0 = LBL + r * CELL_H
    dr.text((24, y0 - 34), label, font=FONT, fill=(255, 224, 130))
    for k, i in enumerate(range(a, b + 1)):
        cx = 20 + k * CELL_W
        try:
            im = frame(D, il, i)
        except Exception:
            continue
        if im.width > CELL_W - 12 or im.height > CELL_H - 24:
            sc = min((CELL_W - 12) / im.width, (CELL_H - 24) / im.height)
            im = im.resize((max(1, int(im.width * sc)), max(1, int(im.height * sc))), Image.NEAREST)
        out.paste(im, (cx + 6, y0 + 8), im)
        dr.text((cx + 6, y0 + CELL_H - 20), str(i), font=ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 14), fill=(150, 150, 150))
    dr.line([(20, y0 + CELL_H - 2), (W - 20, y0 + CELL_H - 2)], fill=(70, 70, 74), width=1)

p = os.path.join(OUT, "召唤出场特效候选.png")
out.save(p)
print("saved", p, out.size)
