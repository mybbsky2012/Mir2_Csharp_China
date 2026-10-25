#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Mir2 地图钓鱼点扫描器（只读，不修改任何文件）

原理：与服务端 Server/MirEnvir/Map.cs 的 LoadMapCellsv1 完全一致——
      地图每格 15 字节，第 13 字节是 light（光照），light 在 100~119 之间
      表示该格是钓鱼点，FishingAttribute = light - 100。

用法：
    python map_fishing_scan.py <地图目录> [--csv 输出.csv]
"""
import os
import struct
import sys
import csv
from collections import Counter, defaultdict


def find_type(b):
    if len(b) < 20:
        return -1
    if b[2] == 0x43 and b[3] == 0x23:
        return 100
    if b[0] == 0:
        return 5
    if b[0] == 0x0F and b[5] == 0x53 and b[14] == 0x33:
        return 6
    if b[0] == 0x15 and b[4] == 0x32 and b[6] == 0x41 and b[19] == 0x31:
        return 4
    if b[0] == 0x10 and b[2] == 0x61 and b[7] == 0x31 and b[14] == 0x31:
        return 1
    if (b[4] == 0x0F) or (b[4] == 0x03 and b[18] == 0x0D and b[19] == 0x0A):
        w = b[0] | (b[1] << 8)
        h = b[2] | (b[3] << 8)
        return 3 if len(b) > 52 + w * h * 14 else 2
    if b[0] == 0x0D and b[1] == 0x4C and b[7] == 0x20 and b[11] == 0x6D:
        return 7
    return 0


def scan_v1(b):
    """返回 (width, height, [(x,y,attr,blocked)])"""
    w = struct.unpack_from('<h', b, 21)[0]
    xor = struct.unpack_from('<h', b, 23)[0]
    h = struct.unpack_from('<h', b, 25)[0]
    width, height = w ^ xor, h ^ xor
    cells = []
    off = 54
    for x in range(width):
        for y in range(height):
            if off + 15 > len(b):
                return width, height, cells
            high = struct.unpack_from('<i', b, off)[0] ^ 0xAA38AA38
            low = struct.unpack_from('<h', b, off + 6)[0] ^ xor
            blocked = bool(high & 0x20000000) or bool(low & 0x8000)
            light = b[off + 13]
            if 100 <= light <= 119:
                cells.append((x, y, light - 100, blocked))
            off += 15
    return width, height, cells


def scan_v0(b):
    width = struct.unpack_from('<h', b, 0)[0]
    height = struct.unpack_from('<h', b, 2)[0]
    cells = []
    off = 52
    for x in range(width):
        for y in range(height):
            if off + 14 > len(b):
                return width, height, cells
            blocked = bool(struct.unpack_from('<h', b, off)[0] & 0x8000) or \
                      bool(struct.unpack_from('<h', b, off + 2)[0] & 0x8000)
            light = b[off + 13]
            if 100 <= light <= 119:
                cells.append((x, y, light - 100, blocked))
            off += 14
    return width, height, cells


def clusters(cells, grid=20):
    """把钓鱼格按 grid x grid 分块，返回 [(bx,by,count,minx,miny,maxx,maxy)]"""
    buckets = defaultdict(list)
    for x, y, a, blocked in cells:
        buckets[(x // grid, y // grid)].append((x, y))
    out = []
    for (bx, by), pts in buckets.items():
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        out.append((bx, by, len(pts), min(xs), min(ys), max(xs), max(ys)))
    out.sort(key=lambda t: -t[2])
    return out


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    mapdir = sys.argv[1]
    csv_path = None
    if '--csv' in sys.argv:
        csv_path = sys.argv[sys.argv.index('--csv') + 1]

    files = sorted(f for f in os.listdir(mapdir) if f.lower().endswith('.map'))
    if not files:
        print('目录里没有 .map 文件:', mapdir)
        return 1

    all_rows = []
    for fn in files:
        path = os.path.join(mapdir, fn)
        b = open(path, 'rb').read()
        t = find_type(b)
        if t == 1:
            w, h, cells = scan_v1(b)
        elif t == 0:
            w, h, cells = scan_v0(b)
        else:
            print('%-18s 格式 v%-3s 不支持（本工具仅支持 v0/v1），跳过  %d 字节' % (fn, t, len(b)))
            continue

        print('=' * 64)
        print('%-18s 格式 v%d  %dx%d  共 %d 格' % (fn, t, w, h, w * h))
        if not cells:
            print('  >>> 没有钓鱼点（light 100~119 的格子为 0）')
            continue

        attr = Counter(c[2] for c in cells)
        blocked = sum(1 for c in cells if c[3])
        print('  >>> 钓鱼格 %d 个，其中被墙/无地板占用的 %d 个' % (len(cells), blocked))
        print('  水域类型(FishingAttribute) 分布：' +
              '，'.join('%d 类 ×%d' % (k, v) for k, v in sorted(attr.items())))
        print('  坐标范围：X %d~%d，Y %d~%d' %
              (min(c[0] for c in cells), max(c[0] for c in cells),
               min(c[1] for c in cells), max(c[1] for c in cells)))
        cl = clusters(cells)
        print('  主要水域区块（按格数排序，前 10）：')
        for bx, by, n, minx, miny, maxx, maxy in cl[:10]:
            attrs = sorted(set(c[2] for c in cells if c[0] // 20 == bx and c[1] // 20 == by))
            print('    X %3d~%-3d  Y %3d~%-3d  %4d 格  类型 %s' %
                  (minx, maxx, miny, maxy, n, ','.join(map(str, attrs))))
        for x, y, a, blocked in cells:
            all_rows.append([fn, x, y, a, 1 if blocked else 0])

    if csv_path and all_rows:
        with open(csv_path, 'w', newline='', encoding='utf-8-sig') as f:
            wr = csv.writer(f)
            wr.writerow(['map', 'x', 'y', 'fishing_attribute', 'blocked'])
            wr.writerows(all_rows)
        print('\n明细已导出：%s（%d 行）' % (csv_path, len(all_rows)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
