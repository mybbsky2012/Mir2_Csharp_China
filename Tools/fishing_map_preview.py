#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 Mir2 地图渲染成 PNG 预览：陆地 / 水面 / 已有钓鱼格，用于选址。

用法: python Tools/fishing_map_preview.py D:/123/Maps/0.map out.png
"""
import sys
import zlib
import struct
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fishing_zone import MapFile, BASE, CELL, HIGH_FLAG, LOW_FLAG, LIGHT_OFF

LAND = (206, 202, 192)
WATER = (36, 72, 122)        # HighWall 且无 LowWall 位 —— 与已有钓点同型的水面
WATER2 = (86, 150, 90)       # LowWall 位（另一类不可走地形）
WALL = (150, 140, 130)       # 其他阻挡
FISH = (60, 220, 230)
GRID = (120, 120, 130)


def write_png(path, w, h, rows):
    raw = b''.join(b'\x00' + rows[y] for y in range(h))

    def chunk(tag, data):
        return (struct.pack('>I', len(data)) + tag + data +
                struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n' +
           chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)) +
           chunk(b'IDAT', zlib.compress(raw, 6)) +
           chunk(b'IEND', b''))
    open(path, 'wb').write(png)


def render(m, out, scale=1, crop=None, grid_every=50):
    W, H = m.W, m.H
    x0, y0, x1, y1 = crop if crop else (0, 0, W - 1, H - 1)
    x0, x1 = max(0, x0), min(W - 1, x1)
    y0, y1 = max(0, y0), min(H - 1, y1)
    cw, ch = x1 - x0 + 1, y1 - y0 + 1
    W2, H2 = cw * scale, ch * scale
    raw = m.raw
    grid = [[LAND] * W2 for _ in range(H2)]
    for x in range(x0, x1 + 1):
        col = (x - x0) * scale
        for y in range(y0, y1 + 1):
            p = BASE + (x * H + y) * CELL
            hi = struct.unpack_from('<i', raw, p)[0] ^ 0xAA38AA38
            lo = struct.unpack_from('<h', raw, p + 6)[0] ^ m.xor
            L = raw[p + LIGHT_OFF]
            if hi & HIGH_FLAG and not lo & LOW_FLAG:
                c = WATER
            elif lo & LOW_FLAG:
                c = WATER2
            else:
                c = WALL if hi & HIGH_FLAG else LAND
            if 100 <= L <= 119:
                c = FISH
            row = (y - y0) * scale
            for dy in range(scale):
                line = grid[row + dy]
                for dx in range(scale):
                    line[col + dx] = c
    # 网格线（按地图绝对坐标对齐）
    if grid_every:
        gx = (x0 // grid_every + 1) * grid_every
        while gx <= x1:
            cx = (gx - x0) * scale
            for y in range(H2):
                grid[y][cx] = GRID
            gx += grid_every
        gy = (y0 // grid_every + 1) * grid_every
        while gy <= y1:
            cy = (gy - y0) * scale
            line = grid[cy]
            for x in range(W2):
                line[x] = GRID
            gy += grid_every
    rows = [b''.join(bytes(px) for px in row) for row in grid]
    write_png(out, W2, H2, rows)
    print('已生成 %s  %dx%d  覆盖地图 (%d,%d)~(,%d,%d)' % (out, W2, H2, x0, y0, x1, y1))


if __name__ == '__main__':
    src = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else os.path.splitext(src)[0] + '_preview.png'
    scale = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    crop = None
    if len(sys.argv) > 4:
        crop = tuple(int(t) for t in sys.argv[4].split(','))
    render(MapFile(src), dst, scale, crop)
