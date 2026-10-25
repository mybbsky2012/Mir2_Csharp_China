# -*- coding: utf-8 -*-
# 把 Magic_32bit.Lib 影华连击 1391 + d*20 的 8 个方向块各取中间帧，拼成一张 PNG 看朝向
import struct, zlib, gzip, sys

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"
raw = open(LIB, 'rb').read()
ver, cnt = struct.unpack_from('<ii', raw, 0)
idx = struct.unpack_from('<%di' % cnt, raw, 8)

def frame(i):
    if i < 0 or i >= cnt:
        return None
    q = idx[i]
    if q <= 0 or q + 17 > len(raw):
        return None
    w, h, x, y = struct.unpack_from('<4h', raw, q)
    if w <= 0 or h <= 0:
        return None
    ln = struct.unpack_from('<i', raw, q + 13)[0]
    if ln <= 0 or q + 17 + ln > len(raw):
        return None
    d = raw[q + 17:q + 17 + ln]
    try:
        d = gzip.decompress(d)
    except Exception:
        try:
            d = zlib.decompress(d)
        except Exception:
            try:
                d = zlib.decompress(d, -15)
            except Exception:
                return None
    if len(d) < w * h * 4:
        return None
    return w, h, d

# 每个方向块取第 8 帧（刀光最明显的时刻）
MIR_DIRS = ['Up', 'UpRight', 'Right', 'DownRight', 'Down', 'DownLeft', 'Left', 'UpLeft']

CELL_W, CELL_H = 200, 200
COLS = 4
ROWS = 2
canvas = bytearray(CELL_W * COLS * CELL_H * ROWS * 4)

for d in range(8):
    fi = 1391 + d * 20 + 8
    fr = frame(fi)
    cx = (d % COLS) * CELL_W
    cy = (d // COLS) * CELL_H
    if fr is None:
        print('dir %d frame %d EMPTY/FAIL' % (d, fi))
        continue
    w, h, data = fr
    print('dir %-9s frame %d  %dx%d' % (MIR_DIRS[d], fi, w, h))
    # 居中贴到 cell
    ox = cx + max(0, (CELL_W - w) // 2)
    oy = cy + max(0, (CELL_H - h) // 2)
    for yy in range(min(h, CELL_H)):
        sy = oy + yy
        if sy >= cy + CELL_H:
            break
        row = data[yy * w * 4:(yy + 1) * w * 4]
        for xx in range(min(w, CELL_W)):
            sx = ox + xx
            if sx >= cx + CELL_W:
                break
            a = row[xx * 4 + 3]
            if a == 0:
                continue
            # 白底 alpha 合成
            r, g, b = row[xx * 4], row[xx * 4 + 1], row[xx * 4 + 2]
            di = (sy * (CELL_W * COLS) + sx) * 4
            canvas[di + 0] = (r * a + 255 * (255 - a)) // 255
            canvas[di + 1] = (g * a + 255 * (255 - a)) // 255
            canvas[di + 2] = (b * a + 255 * (255 - a)) // 255
            canvas[di + 3] = 255

# 每个 cell 画个简单边框（灰）
def setpx(x, y, v):
    if 0 <= x < CELL_W * COLS and 0 <= y < CELL_H * ROWS:
        i = (y * (CELL_W * COLS) + x) * 4
        canvas[i] = canvas[i + 1] = canvas[i + 2] = v
        canvas[i + 3] = 255

for d in range(8):
    cx = (d % COLS) * CELL_W
    cy = (d // COLS) * CELL_H
    for x in range(CELL_W):
        setpx(cx + x, cy + 0, 128)
        setpx(cx + x, cy + CELL_H - 1, 128)
    for y in range(CELL_H):
        setpx(cx + 0, cy + y, 128)
        setpx(cx + CELL_W - 1, cy + y, 128)
    # 中心十字（标出人物位置）
    for k in range(-30, 31):
        setpx(cx + CELL_W // 2 + k, cy + CELL_H // 2, 200)
        setpx(cx + CELL_W // 2, cy + CELL_H // 2 + k, 200)

W_T = CELL_W * COLS
H_T = CELL_H * ROWS
# 写 PNG
def write_png(path, w, h, rgba):
    raw_rows = bytearray()
    for y in range(h):
        raw_rows.append(0)
        raw_rows += rgba[y * w * 4:(y + 1) * w * 4]
    comp = zlib.compress(bytes(raw_rows), 6)
    def chunk(t, d):
        c = t + d
        return struct.pack('>I', len(d)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', comp)
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)

out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tmp\shadowcombo\dirs.png"
import os
os.makedirs(os.path.dirname(out), exist_ok=True)
write_png(out, W_T, H_T, canvas)
print('saved', out, W_T, 'x', H_T)
print('排布：上行 = Up(0), UpRight(1), Right(2), DownRight(3)   下行 = Down(4), DownLeft(5), Left(6), UpLeft(7)')
