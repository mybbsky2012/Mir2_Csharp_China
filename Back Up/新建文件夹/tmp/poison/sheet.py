# -*- coding: utf-8 -*-
"""把指定帧区间拼成对比图，看每帧实际内容。用法: python sheet.py 输出名 起始 结束 [起始2 结束2 ...]"""
import struct, zlib, os, sys

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"
OUT = os.path.dirname(os.path.abspath(__file__))

def load(path):
    with open(path, "rb") as f:
        data = f.read()
    i32 = lambda o: struct.unpack_from("<i", data, o)[0]
    ver = i32(0); count = i32(4); p = 12 if ver >= 3 else 8
    idx = list(struct.unpack_from("<%di" % count, data, p))
    return data, idx

DATA, INDEX = load(LIB)

def frame(i):
    o = INDEX[i]
    w, h, x, y, sx, sy = struct.unpack_from("<6h", DATA, o)
    length = struct.unpack_from("<i", DATA, o + 13)[0]
    raw = b""
    if w > 0 and h > 0 and length > 0:
        payload = DATA[o + 17: o + 17 + length]
        for wb in (31, -15, 15):
            try:
                raw = zlib.decompress(payload, wb); break
            except Exception:
                continue
    return w, h, x, y, raw

def png(path, w, h, bgra):
    raw = bytearray()
    for yy in range(h):
        raw.append(0); raw += bgra[yy * w * 4:(yy + 1) * w * 4]
    def ch(tag, pl):
        return struct.pack(">I", len(pl)) + tag + pl + struct.pack(">I", zlib.crc32(tag + pl) & 0xFFFFFFFF)
    d = b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    d += ch(b"IDAT", zlib.compress(bytes(raw), 9)) + ch(b"IEND", b"")
    open(path, "wb").write(d)

def build(nums, path):
    fr = []
    for n in nums:
        w, h, x, y, raw = frame(n)
        if w > 4 and h > 1 and len(raw) >= w * h * 4:
            fr.append((n, w, h, x, y, raw))
    if not fr:
        print("no frames"); return
    W = sum(f[1] for f in fr); H = max(f[2] for f in fr)
    canvas = bytearray(W * H * 4)
    ox = 0
    for n, w, h, x, y, raw in fr:
        for yy in range(h):
            for xx in range(w):
                si = (yy * w + xx) * 4
                a = raw[si + 3]
                if a == 0: continue
                di = (yy * W + ox + xx) * 4
                canvas[di] = raw[si]; canvas[di+1] = raw[si+1]; canvas[di+2] = raw[si+2]; canvas[di+3] = a
        ox += w
    png(path, W, H, canvas)
    print(" ->", path, "(%d frames: %s)" % (len(fr), ",".join(str(f[0]) for f in fr)))

args = sys.argv[1:]
name = args[0]
nums = []
for i in range(1, len(args), 2):
    nums += list(range(int(args[i]), int(args[i+1]) + 1))
build(nums, os.path.join(OUT, name))
