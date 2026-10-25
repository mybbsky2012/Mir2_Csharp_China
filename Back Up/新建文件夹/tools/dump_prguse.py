# -*- coding: utf-8 -*-
"""导出 Prguse.Lib 里几张疑似 LOADING/背景的帧，拼成对照图，便于挑选。"""
import struct, gzip
from PIL import Image, ImageDraw

LIB = r"E:\micromir2\Data\Prguse.Lib"
D = open(LIB, "rb").read()
VER, COUNT = struct.unpack_from("<ii", D, 0)
IL = list(struct.unpack_from("<%di" % COUNT, D, 12))


def frame(idx):
    off = IL[idx]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    raw = D[off + 17: off + 17 + ln]
    px = gzip.decompress(raw)
    if len(px) < w * h * 4:
        px = px + b"\x00" * (w * h * 4 - len(px))
    return Image.frombytes("RGBA", (w, h), px[:w * h * 4], "raw", "BGRA")


probes = [65, 938, 939, 940, 944, 948]
ims = [frame(i) for i in probes]
for i, im in zip(probes, ims):
    im.convert("RGB").save(r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\prguse_%d.png" % i)
    print("saved prguse_%d.png %s" % (i, im.size))
