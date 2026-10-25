# -*- coding: utf-8 -*-
"""诊断 Magic_32bit.Lib 指定帧：尺寸 / 压缩长度 / 解压后长度 / alpha 覆盖。"""
import struct, zlib, os

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"

with open(LIB, "rb") as f:
    data = f.read()

i32 = lambda o: struct.unpack_from("<i", data, o)[0]
ver = i32(0); count = i32(4); p = 12 if ver >= 3 else 8
INDEX = list(struct.unpack_from("<%di" % count, data, p))
print("ver=%d count=%d" % (ver, count))

def frame(i):
    o = INDEX[i]
    w, h, x, y, sx, sy = struct.unpack_from("<6h", data, o)
    shadow = data[o + 12]
    length = struct.unpack_from("<i", data, o + 13)[0]
    raw = b""; how = "-"
    if w > 0 and h > 0 and length > 0:
        payload = data[o + 17: o + 17 + length]
        for name, wb in (("gz", 31), ("raw", -15), ("zlib", 15)):
            try:
                raw = zlib.decompress(payload, wb); how = name; break
            except Exception:
                continue
    return w, h, shadow, length, raw, how

prev = None
import sys
rng = sys.argv[1:] or ["1296","1385"]
for n in range(int(rng[0]), int(rng[1]) + 1):
    w, h, shadow, length, raw, how = frame(n)
    need = w * h * 4
    if raw:
        alpha = sum(raw[i] for i in range(3, len(raw), 4)) / max(1, w * h)
        nz = sum(1 for i in range(3, len(raw), 4) if raw[i] > 0)
        fill = nz * 100.0 / max(1, w * h)
    else:
        alpha = fill = 0.0
    flag = ""
    if w > 4 and raw and len(raw) != need:
        flag = "  <<<LEN!=w*h*4"
    if w > 4 and not raw:
        flag = "  <<<解压失败"
    print("%4d w=%-4d h=%-4d sh=%-3d len=%-7d raw=%-8d %-4s avgA=%6.1f fill=%5.1f%%%s"
          % (n, w, h, shadow, length, len(raw), how, alpha, fill, flag))
