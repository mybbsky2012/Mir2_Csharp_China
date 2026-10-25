# -*- coding: utf-8 -*-
"""导出 E:\micromir2\Data 的 ChrSel[17..19] 与 Prguse[939..949]，拼成预览图，检查是否真的可见。"""
import struct, gzip, sys
from PIL import Image, ImageDraw

DATA = r"E:\micromir2\Data"


def load_lib(name):
    p = "%s\\%s.Lib" % (DATA, name)
    d = open(p, "rb").read()
    ver, cnt = struct.unpack_from("<ii", d, 0)
    il = list(struct.unpack_from("<%di" % cnt, d, 12))
    return d, cnt, il


def frame(d, il, idx):
    off = il[idx]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", d, off)
    ln = struct.unpack_from("<i", d, off + 13)[0]
    raw = d[off + 17: off + 17 + ln]
    if w <= 0 or h <= 0:
        return Image.new("RGBA", (1, 1), (255, 0, 255, 255))
    try:
        px = gzip.decompress(raw)
    except Exception:
        return Image.new("RGBA", (w, h), (255, 0, 0, 255))
    if len(px) < w * h * 4:
        px = px + b"\x00" * (w * h * 4 - len(px))
    return Image.frombytes("RGBA", (w, h), px[:w * h * 4], "raw", "BGRA")


def stats(im):
    ex = im.getextrema()
    return " ".join("%s:%d~%d" % (ch, lo, hi) for ch, (lo, hi) in zip("RGBA", ex))


cd, cc, cil = load_lib("ChrSel")
pd, pc, pil = load_lib("Prguse")

print("== ChrSel 17~19 ==")
for i in (17, 18, 19):
    im = frame(cd, cil, i)
    print("ChrSel[%d] %s  %s" % (i, im.size, stats(im)))

print("== Prguse 939~949 ==")
for i in range(939, 950):
    im = frame(pd, pil, i)
    print("Prguse[%d] %s  %s" % (i, im.size, stats(im)))

# 合成预览：按载入画面的实际摆法（黑底 + 居中 800x600 标志 + 中下方 Loading 动画）
SW, SH = 1024, 768
canvas = Image.new("RGB", (SW, SH), (0, 0, 0))
logo = frame(cd, cil, 18).convert("RGBA")
canvas.paste(logo, ((SW - logo.width) // 2, (SH - logo.height) // 2), logo)

frames = [frame(pd, pil, i) for i in range(940, 949)]
strip = Image.new("RGBA", (92 * len(frames), 20), (0, 0, 0, 0))
for k, fr in enumerate(frames):
    strip.paste(fr, (k * 92, 0), fr)
canvas.paste(strip, ((SW - strip.width) // 2, SH // 2 + 40), strip)

out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\载入画面素材实测.png"
canvas.save(out)
print("saved:", out)
