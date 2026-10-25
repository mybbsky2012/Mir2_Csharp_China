# -*- coding: utf-8 -*-
"""
解析 Mir2 Lib(v3) 指定帧，导出 PNG 并统计每帧 alpha 覆盖/亮度。
用于判断毒云 1280-1289 这些帧里，哪几帧是"起手/消散(淡)"、哪几帧是"盛放(实)"。
"""
import struct, zlib, os, sys

LIB = r"E:\micromir2\Data\Magic2.Lib"
OUT = os.path.dirname(os.path.abspath(__file__))
FRAMES = list(range(1280, 1300))


def read_images(path):
    with open(path, "rb") as f:
        data = f.read()

    def i32(o):
        return struct.unpack_from("<i", data, o)[0]

    ver = i32(0)
    count = i32(4)
    p = 8
    frameSeek = 0
    if ver >= 3:
        frameSeek = i32(8)
        p = 12
    indexList = list(struct.unpack_from("<%di" % count, data, p))

    def read_image(idx):
        o = indexList[idx]
        w, h, x, y, sx, sy = struct.unpack_from("<6h", data, o)
        shadow = data[o + 12]
        length = struct.unpack_from("<i", data, o + 13)[0]
        payload = data[o + 17: o + 17 + length]
        raw = b""
        if length > 0 and w > 0 and h > 0:
            for wbits in (31, -15, 15):
                try:
                    raw = zlib.decompress(payload, wbits)
                    break
                except Exception:
                    continue
        return dict(w=w, h=h, x=x, y=y, shadow=shadow, length=length, raw=raw)

    return ver, count, read_image


def write_png(path, w, h, bgra):
    """bgra: bytearray of w*h*4"""
    raw = bytearray()
    stride = w * 4
    for yy in range(h):
        raw.append(0)
        raw += bgra[yy * stride:(yy + 1) * stride]

    def chunk(tag, payload):
        c = struct.pack(">I", len(payload)) + tag + payload
        c += struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)
        return c

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def compose_sheet(frames, path):
    """把所有帧横向拼成一张对比图（黑底），方便肉眼对比。"""
    imgs = [f["bgra"] for f in frames]
    W = sum(f["w"] for f in frames)
    H = max(f["h"] for f in frames)
    canvas = bytearray(W * H * 4)
    ox = 0
    for f in frames:
        w, h, bgra = f["w"], f["h"], f["bgra"]
        for yy in range(h):
            for xx in range(w):
                si = (yy * w + xx) * 4
                a = bgra[si + 3]
                if a == 0:
                    continue
                di = (yy * W + ox + xx) * 4
                canvas[di] = bgra[si]
                canvas[di + 1] = bgra[si + 1]
                canvas[di + 2] = bgra[si + 2]
                canvas[di + 3] = a
        ox += w
    write_png(path, W, H, canvas)


ver, count, read_image = read_images(LIB)
print("Lib version=%d count=%d" % (ver, count))

prev = None
for idx in FRAMES:
    im = read_image(idx)
    w, h, raw = im["w"], im["h"], im["raw"]
    n = w * h
    if n == 0 or len(raw) < n * 4:
        print("%4d  w=%-4d h=%-4d  <EMPTY/短数据 raw=%d>" % (idx, w, h, len(raw)))
        continue
    total_a = 0
    solid = 0      # alpha >= 128
    anypx = 0      # alpha > 0
    lum = 0
    for i in range(n):
        a = raw[i * 4 + 3]
        b = raw[i * 4]
        g = raw[i * 4 + 1]
        r = raw[i * 4 + 2]
        total_a += a
        if a > 0:
            anypx += 1
            lum += (r + g + b) // 3
        if a >= 128:
            solid += 1
    avg_a = total_a / n
    fill = anypx * 100.0 / n
    sol = solid * 100.0 / n
    avg_lum = (lum / anypx) if anypx else 0
    diff = ""
    if prev is not None:
        d = sum(abs(raw[i] - prev[i]) for i in range(0, min(len(raw), len(prev)), 4))
        diff = "  Δ=%d" % d
    print("%4d  w=%-4d h=%-4d  avgA=%6.1f  fill=%5.1f%%  solid=%5.1f%%  lum=%5.1f%s"
          % (idx, w, h, avg_a, fill, sol, avg_lum, diff))
    im["bgra"] = raw
    prev = raw

# 导出拼图
sheet = []
for idx in FRAMES:
    im = read_image(idx)
    if im["w"] and im["h"] and len(im["raw"]) >= im["w"] * im["h"] * 4:
        im["bgra"] = im["raw"]
        sheet.append(im)
if sheet:
    compose_sheet(sheet, os.path.join(OUT, "poison_1280_1299.png"))
    print("sheet ->", os.path.join(OUT, "poison_1280_1299.png"))
