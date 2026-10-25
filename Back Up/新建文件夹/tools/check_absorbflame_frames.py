# -*- coding: utf-8 -*-
"""核对 Magic_32bit.Lib 中吸魔炎风(1581-1614)与吸魔炎风-秘籍(1630-1665)的特效帧"""
import struct, gzip, zlib, sys
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8")

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"
raw = open(LIB, "rb").read()


class R:
    def __init__(self, d):
        self.d = d
        self.p = 0

    def i32(self):
        v = struct.unpack_from("<i", self.d, self.p)[0]
        self.p += 4
        return v

    def i16(self):
        v = struct.unpack_from("<h", self.d, self.p)[0]
        self.p += 2
        return v

    def u8(self):
        v = self.d[self.p]
        self.p += 1
        return v


r = R(raw)
ver = r.i32()
count = r.i32()
frame_seek = r.i32() if ver >= 3 else 0
index_list = [r.i32() for _ in range(count)]
print(f"ver={ver} 总帧数={count} frameSeek={frame_seek}")


def load_frame(idx):
    r.p = index_list[idx]
    w = r.i16(); h = r.i16()
    x = r.i16(); y = r.i16()
    sx = r.i16(); sy = r.i16()
    shadow = r.u8()
    length = r.i32()
    has_mask = (shadow >> 7) == 1
    data = raw[r.p:r.p + length]
    r.p += length
    if has_mask:
        mw = r.i16(); mh = r.i16(); mx = r.i16(); my = r.i16()
        mlen = r.i32()
        r.p += mlen
    try:
        px = gzip.decompress(data)
    except Exception:
        px = zlib.decompress(data)
    if len(px) < w * h * 4:
        px = px + b"\x00" * (w * h * 4 - len(px))
    img = Image.frombytes("RGBA", (w, h), px[:w * h * 4])
    b, g, rr, a = img.split()
    return Image.merge("RGBA", (rr, g, b, a)), (w, h, x, y, "mask" if has_mask else "")


RANGES = [("吸魔炎风 AbsorbFlame", 1581, 1614), ("吸魔炎风-秘籍 AbsorbFlameRare", 1630, 1665)]
CELL = 72
rows = []
all_tiles = []
for name, a, b in RANGES:
    tiles = []
    for i in range(a, b + 1):
        img, meta = load_frame(i)
        tiles.append((i, img))
        # 统计非透明像素
        alpha = img.split()[3]
        data = list(alpha.get_flattened_data()) if hasattr(alpha, "get_flattened_data") else list(alpha.getdata())
        nz = sum(1 for v in data if v > 0)
        maxa = max(data) if data else 0
        print(f"  {name} 帧{i}: {meta[0]}x{meta[1]} offset=({meta[2]},{meta[3]}) {meta[4]} 非透明像素={nz} 最大alpha={maxa}")
    all_tiles.append((f"{name}  ({a}-{b}, {len(tiles)}帧)", tiles))

cols = 12
out_rows = []
for title, tiles in all_tiles:
    out_rows.append((title, tiles))

maxw = max(len(t) for _, t in out_rows)
sheet = Image.new("RGBA", (maxw * CELL, len(out_rows) * (CELL + 22)), (24, 24, 28, 255))
for ri, (title, tiles) in enumerate(out_rows):
    for ci, (fi, img) in enumerate(tiles):
        small = img.resize((CELL, CELL), Image.NEAREST) if img.width and img.height else Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
        # 居中放置
        sheet.paste(small, (ci * CELL, ri * (CELL + 22)), small)
    print(f"[行] {title}  帧数={len(tiles)}")

sheet.save(r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\吸魔炎风帧核对.png")
print("saved ->", r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\吸魔炎风帧核对.png")
