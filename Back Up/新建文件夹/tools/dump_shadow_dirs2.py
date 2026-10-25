# -*- coding: utf-8 -*-
# Magic_32bit.Lib 影华连击：8 个方向块各取 3 个代表帧，按真实绘制偏移(x,y)拼图，确认方向块朝向
import struct, zlib, gzip, os

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"
raw = open(LIB, 'rb').read()
cnt = struct.unpack_from('<i', raw, 4)[0]
idx = struct.unpack_from('<%di' % cnt, raw, 8)

def frame(i):
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
        px = gzip.decompress(d)
    except Exception:
        try:
            px = zlib.decompress(d)
        except Exception:
            return None
    if len(px) < w * h * 4:
        return None
    return w, h, x, y, px

MIR_DIRS = ['Up(0)', 'UpRight(1)', 'Right(2)', 'DownRight(3)', 'Down(4)', 'DownLeft(5)', 'Left(6)', 'UpLeft(7)']

CW, CH = 560, 560           # cell size; origin = (280, 460) = 人物脚下
ORX, ORY = 280, 460
COLS, ROWS = 4, 2
W_T, H_T = CW * COLS, CH * ROWS
canvas = bytearray(W_T * H_T * 4)

def setpx(x, y, r, g, b, a):
    if 0 <= x < W_T and 0 <= y < H_T:
        i = (y * W_T + x) * 4
        if a >= 250:
            canvas[i] = r; canvas[i+1] = g; canvas[i+2] = b; canvas[i+3] = 255
        else:
            # 简单 over 合成（白底）
            dr = canvas[i]; dg = canvas[i+1]; db = canvas[i+2]; da = canvas[i+3]
            if da == 0:
                dr = dg = db = 255
            canvas[i]   = (r * a + dr * (255 - a)) // 255
            canvas[i+1] = (g * a + dg * (255 - a)) // 255
            canvas[i+2] = (b * a + db * (255 - a)) // 255
            canvas[i+3] = 255

for d in range(8):
    cx = (d % COLS) * CW
    cy = (d // COLS) * CH
    for k in (4, 8, 12):
        fi = 1391 + d * 20 + k
        fr = frame(fi)
        if fr is None:
            print('dir %s frame %d EMPTY/FAIL' % (MIR_DIRS[d], fi))
            continue
        w, h, x, y, px = fr
        print('dir %-12s frame %d  %dx%d off(%d,%d)' % (MIR_DIRS[d], fi, w, h, x, y))
        for yy in range(h):
            py = cy + ORY + y + yy
            row = px[yy * w * 4:(yy + 1) * w * 4]
            for xx in range(w):
                a = row[xx * 4 + 3]
                if a == 0:
                    continue
                setpx(cx + ORX + x + xx, py, row[xx*4], row[xx*4+1], row[xx*4+2], a)

# 分隔线 + 中心十字
def line(x0, y0, x1, y1, v):
    if x0 == x1:
        for y in range(min(y0,y1), max(y0,y1)+1):
            setpx(x0, y, v, v, v, 255)
    else:
        for x in range(min(x0,x1), max(x0,x1)+1):
            setpx(x, y0, v, v, v, 255)

for c in range(COLS + 1):
    line(c * CW, 0, c * CW, H_T - 1, 100)
for r in range(ROWS + 1):
    line(0, r * CH, W_T - 1, r * CH, 100)
for d in range(8):
    cx = (d % COLS) * CW + ORX
    cy = (d // COLS) * CH + ORY
    for k in range(-40, 41):
        setpx(cx + k, cy, 220, 220, 220, 255)
        setpx(cx, cy + k, 220, 220, 220, 255)

def write_png(path, w, h, rgba):
    rows = bytearray()
    for y in range(h):
        rows.append(0)
        rows += rgba[y * w * 4:(y + 1) * w * 4]
    comp = zlib.compress(bytes(rows), 6)
    def chunk(t, d):
        c = t + d
        return struct.pack('>I', len(d)) + c + struct.pack('>I', zlib.crc32(c) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', comp)
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)

out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tmp\shadowcombo\dirs2.png"
os.makedirs(os.path.dirname(out), exist_ok=True)
write_png(out, W_T, H_T, canvas)
print('saved', out)
