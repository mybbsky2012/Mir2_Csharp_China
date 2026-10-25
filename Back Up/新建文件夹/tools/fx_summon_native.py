# -*- coding: utf-8 -*-
"""渲染召唤物的『出场动作』帧段，验证是否就是盛大原版效果"""
import struct, gzip, os
from PIL import Image, ImageDraw

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools"
DATA = r"E:\micromir2\Data\Monster"

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

def sheet(libfile, start, end, tag, cols=12, scale=2):
    path = os.path.join(DATA, libfile)
    D, il, count = load(path)
    lo, hi = max(0, start), min(end, count - 1)
    rows = []
    for i in range(lo, hi + 1):
        try:
            im, x, y = frame(D, il[i])
        except Exception as e:
            continue
        if im.width <= 6 or im.height <= 6:
            continue
        if scale != 1:
            im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        rows.append((i, im))
    if not rows:
        print(f"{tag}: 无帧")
        return
    ch = max(im.height for _, im in rows) + 20
    cw = max(im.width for _, im in rows) + 10
    ncol = min(cols, len(rows))
    nrow = (len(rows) + ncol - 1) // ncol
    out = Image.new("RGB", (cw * ncol, ch * nrow), (45, 45, 48))
    dr = ImageDraw.Draw(out)
    for k, (i, im) in enumerate(rows):
        cx, cy = (k % ncol) * cw, (k // ncol) * ch
        out.paste(im, (cx + 5, cy + 18), im)
        dr.text((cx + 6, cy + 3), str(i), fill=(255, 220, 120))
    p = os.path.join(OUT, f"sheets/{tag}.png")
    os.makedirs(os.path.dirname(p), exist_ok=True)
    out.save(p)
    print(f"{tag}: {len(rows)} 帧 -> {p} ({out.width}x{out.height})")

sheet("036.Lib", 0, 11, "召唤出场_骷髅036")
sheet("170.Lib", 0, 15, "召唤出场_神兽170")
sheet("172.Lib", 456, 468, "召唤出场_月灵172")
