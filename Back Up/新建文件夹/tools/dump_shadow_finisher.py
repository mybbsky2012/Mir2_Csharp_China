# -*- coding: utf-8 -*-
# 导出 Magic_32bit.Lib 1561-1566（影华连击-秘籍 收招/爆炸 素材），按真实偏移拼图
import struct, zlib, gzip, os

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"
raw = open(LIB, 'rb').read()
cnt = struct.unpack_from('<i', raw, 4)[0]
idx = struct.unpack_from('<%di' % cnt, raw, 8)

def frame(i):
    q = idx[i]
    w, h, x, y = struct.unpack_from('<4h', raw, q)
    if w <= 0 or h <= 0:
        return None
    ln = struct.unpack_from('<i', raw, q + 13)[0]
    d = raw[q + 17:q + 17 + ln]
    try:
        px = gzip.decompress(d)
    except Exception:
        try:
            px = zlib.decompress(d)
        except Exception:
            return None
    if len(px) < w * h * 4:
        return None
    return w, h, x, y, px

FRAMES = [1561, 1562, 1563, 1564, 1565, 1566]
CW, CH = 400, 320
ORX, ORY = 200, 250
COLS, ROWS = 3, 2
W_T, H_T = CW * COLS, CH * ROWS
canvas = bytearray(W_T * H_T * 4)
for i in range(W_T * H_T):
    canvas[i * 4] = canvas[i * 4 + 1] = canvas[i * 4 + 2] = 255
    canvas[i * 4 + 3] = 255

def blend(px_, x, y, r, g, b, a):
    if not (0 <= x < W_T and 0 <= y < H_T):
        return
    i = (y * W_T + x) * 4
    canvas[i] = (r * a + canvas[i] * (255 - a)) // 255
    canvas[i + 1] = (g * a + canvas[i + 1] * (255 - a)) // 255
    canvas[i + 2] = (b * a + canvas[i + 2] * (255 - a)) // 255

for n, fi in enumerate(FRAMES):
    fr = frame(fi)
    if fr is None:
        print(fi, 'EMPTY'); continue
    w, h, x, y, px = fr
    print(fi, '%dx%d off(%d,%d)' % (w, h, x, y))
    cx = (n % COLS) * CW
    cy = (n // COLS) * CH
    for yy in range(h):
        for xx in range(w):
            a = px[(yy * w + xx) * 4 + 3]
            if a == 0:
                continue
            blend(px, cx + ORX + x + xx, cy + ORY + y + yy,
                  px[(yy * w + xx) * 4], px[(yy * w + xx) * 4 + 1], px[(yy * w + xx) * 4 + 2], a)

def setpx(x, y, v):
    if 0 <= x < W_T and 0 <= y < H_T:
        i = (y * W_T + x) * 4
        canvas[i] = canvas[i + 1] = canvas[i + 2] = v

for c in range(COLS + 1):
    for y in range(H_T):
        setpx(c * CW, y, 100)
for rr in range(ROWS + 1):
    for x in range(W_T):
        setpx(x, rr * CH, 100)
for n in range(6):
    cx = (n % COLS) * CW + ORX
    cy = (n // COLS) * CH + ORY
    for k in range(-30, 31):
        setpx(cx + k, cy, 210); setpx(cx, cy + k, 210)

def write_png(path, w, h, rgba):
    rows = bytearray()
    for y in range(h):
        rows.append(0)
        rows += rgba[y * w * 4:(y + 1) * w * 4]
    comp = zlib.compress(bytes(rows), 6)
    def chunk(t, d):
        c = t + d
        return struct.pack('>I', len(d)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + chunk(b'IDAT', comp) + chunk(b'IEND', b'')
    open(path, 'wb').write(png)

out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tmp\shadowcombo\finisher.png"
os.makedirs(os.path.dirname(out), exist_ok=True)
write_png(out, W_T, H_T, canvas)
print('saved', out)
