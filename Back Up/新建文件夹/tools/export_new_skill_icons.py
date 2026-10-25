# -*- coding: utf-8 -*-
"""导出 MagIcon2.Lib 第 2N / 2N+1 帧，用于核对新增技能图标（客户端画 Icon*2 帧）"""
import struct, gzip, io, sys, zlib
from PIL import Image

sys.stdout.reconfigure(encoding="utf-8")

LIB = r"E:\micromir2\Data\MagIcon2.Lib"

NEW = [
    (114, "强击-秘籍"),
    (119, "吸魔炎风"),
    (124, "吸魔炎风-秘籍"),
    (120, "万效符"),
    (125, "万效符-秘籍"),
    (128, "灵魂火符(万效)"),
    (129, "灵魂火符(万效-秘)"),
    (115, "血龙兽-秘籍"),
    (113, "月华乱舞-秘籍"),
    (118, "影华连击"),
    (123, "影华连击-秘笈"),
    (121, "落雷杀"),
    (126, "落雷杀-秘笈"),
    (127, "灭杀"),
    (110, "拔刀术-秘籍"),
    (87,  "月影雾-秘籍"),
    (111, "爆闪-秘籍"),
    (116, "气流术-秘籍"),
]

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
print("ver", ver, "frames", count)


def load_frame(idx):
    r.p = index_list[idx]
    w = r.i16(); h = r.i16()
    x = r.i16(); y = r.i16()
    sx = r.i16(); sy = r.i16()
    shadow = r.u8()
    length = r.i32()
    data = raw[r.p:r.p + length]
    px = gzip.decompress(data)
    if len(px) < w * h * 4:
        px = px + b"\x00" * (w * h * 4 - len(px))
    img = Image.frombytes("RGBA", (w, h), px[:w * h * 4])
    # 客户端是 A8R8G8B8；PIL 需要 BGRA->RGBA
    b, g, rr, a = img.split()
    return Image.merge("RGBA", (rr, g, b, a))


CELL = 64
cols = 6
rows = (len(NEW) + cols - 1) * 2
sheet = Image.new("RGBA", (cols * CELL, rows * CELL // 2 + 40), (30, 30, 36, 255))

tiles = []
for icon, name in NEW:
    a = load_frame(icon * 2)
    b = load_frame(icon * 2 + 1)
    tiles.append((icon, name, a, b))

out = Image.new("RGBA", (cols * CELL, ((len(tiles) + cols - 1) // cols) * (CELL + 18)), (30, 30, 36, 255))
for i, (icon, name, a, b) in enumerate(tiles):
    cx = (i % cols) * CELL
    cy = (i // cols) * (CELL + 18)
    f = a.copy()
    f2 = b.copy()
    out.paste(f.resize((CELL // 2, CELL // 2), Image.NEAREST), (cx, cy), f.resize((CELL // 2, CELL // 2), Image.NEAREST))
    out.paste(f2.resize((CELL // 2, CELL // 2), Image.NEAREST), (cx + CELL // 2, cy), f2.resize((CELL // 2, CELL // 2), Image.NEAREST))
    print(f"  Icon={icon:3} 帧{icon*2}/{icon*2+1}  {name}  {a.size}")

out.save(r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\新技能图标核对.png")
print("saved")
