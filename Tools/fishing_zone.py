#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
钓场工具 —— 分析 / 修改 Mir2 (Map 2010 Ver 1.0) 地图的钓鱼水面标记

原理：钓鱼格的唯一判定是 cell 的 light 字节在 100~119 之间
      服务端 Server/MirEnvir/Map.cs  -> Cells[x,y].FishingAttribute = light - 100
      客户端 Client/MirObjects/MapCode.cs -> MapCells[x,y].FishingCell = true
      light - 100 同时是该水域对应的掉落组编号（Envir/Drops/NNFishing.txt）

文件结构（v1 / Map 2010 Ver 1.0）：
      0  ~ 19   标题 "Map 2010 Ver 1.0"
      21 ~ 22  Width  (int16, 需与 23~24 的 xor 异或)
      23 ~ 24  xor key
      25 ~ 26  Height (int16, 同样异或)
      54 起     每格 15 字节, 顺序 for x { for y {} }  -> base = 54 + (x*Height + y)*15
        +0..3  int32 ^ 0xAA38AA38, bit 0x20000000 = HighWall
        +6..7  int16 ^ xor,        bit 0x8000     = LowWall(无地板/水面)
        +8     门序号
        +13    light  <- 钓鱼标记就写在这里

用法：
  python Tools/fishing_zone.py report  D:/123/Maps/0.map
  python Tools/fishing_zone.py report  D:/123/Maps/0.map --min-area 200
  python Tools/fishing_zone.py paint   D:/123/Maps/0.map --rect 300,20,420,90 --dry-run
  python Tools/fishing_zone.py paint   D:/123/Maps/0.map --rect 300,20,420,90 --apply
  python Tools/fishing_zone.py restore D:/123/Maps/0.map
"""
import struct
import sys
import os
import shutil
import argparse
from collections import deque

CELL = 15
BASE = 54
LIGHT_OFF = 13
HIGH_FLAG = 0x20000000
LOW_FLAG = 0x8000


class MapFile:
    def __init__(self, path):
        self.path = path
        self.raw = bytearray(open(path, 'rb').read())
        b = self.raw
        if not (b[0] == 0x10 and b[2] == 0x61 and b[7] == 0x31 and b[14] == 0x31):
            raise SystemExit('不是 Map 2010 Ver 1.0 格式: %s' % path)
        self.xor = struct.unpack_from('<h', b, 23)[0]
        self.W = struct.unpack_from('<h', b, 21)[0] ^ self.xor
        self.H = struct.unpack_from('<h', b, 25)[0] ^ self.xor
        expect = BASE + self.W * self.H * CELL
        if len(b) != expect:
            raise SystemExit('文件大小与尺寸不符: 期望 %d 实际 %d (W=%d H=%d)' %
                             (expect, len(b), self.W, self.H))

    def off(self, x, y):
        return BASE + (x * self.H + y) * CELL

    def is_high(self, x, y):
        o = self.off(x, y)
        return (struct.unpack_from('<i', self.raw, o)[0] ^ 0xAA38AA38) & HIGH_FLAG != 0

    def is_low(self, x, y):
        o = self.off(x, y)
        return (struct.unpack_from('<h', self.raw, o + 6)[0] ^ self.xor) & LOW_FLAG != 0

    def light(self, x, y):
        return self.raw[self.off(x, y) + LIGHT_OFF]

    def set_light(self, x, y, v):
        self.raw[self.off(x, y) + LIGHT_OFF] = v & 0xFF

    def save(self, backup=True):
        if backup:
            bak = self.path + '.bak_fishing'
            if not os.path.exists(bak):
                shutil.copy2(self.path, bak)
                print('已备份 -> %s' % bak)
        with open(self.path, 'wb') as f:
            f.write(self.raw)
        print('已写入 %s (%d 字节)' % (self.path, len(self.raw)))


def build_sets(m):
    """返回 (水面格集合, 钓鱼格字典, 可走格集合)
    水面 = HighWall 位且无 LowWall 位 —— 与本图已有钓点同型的地形"""
    water = set()
    fish = {}
    walk = set()
    raw = m.raw
    xr = m.xor
    W, H = m.W, m.H
    p = BASE
    for x in range(W):
        for y in range(H):
            hi = struct.unpack_from('<i', raw, p)[0] ^ 0xAA38AA38
            lo = struct.unpack_from('<h', raw, p + 6)[0] ^ xr
            is_high = (hi & HIGH_FLAG) != 0
            is_low = (lo & LOW_FLAG) != 0
            L = raw[p + LIGHT_OFF]
            if is_high and not is_low:
                water.add((x, y))
                if 100 <= L <= 119:
                    fish[(x, y)] = L - 100
            elif is_high or is_low:
                pass  # 其他阻挡（城墙/带 low 位地形），不动
            else:
                walk.add((x, y))
            p += CELL
    return water, fish, walk


def can_fish_from(water, walk, wx, wy):
    """从水域格 (wx,wy) 反推：哪些陆地格站着能钓到它（必须正好第3格，中间可挡）"""
    out = []
    for dx, dy, name in ((0, -1, '上'), (1, 0, '右'), (0, 1, '下'), (-1, 0, '左')):
        px, py = wx - dx * 3, wy - dy * 3
        if (px, py) in walk:
            out.append((px, py, name))
    return out


def cmd_report(m, min_area):
    water, fish, walk = build_sets(m)
    print('地图 %s  %dx%d' % (os.path.basename(m.path), m.W, m.H))
    print('  阻挡格(水/墙/建筑) %d   可走格 %d' % (len(water), len(walk)))
    print('  已有钓鱼格 %d' % len(fish))

    # 连通水域（4 邻接）
    seen = set()
    regions = []
    for start in water:
        if start in seen:
            continue
        q = deque([start])
        seen.add(start)
        cells = []
        while q:
            cx, cy = q.popleft()
            cells.append((cx, cy))
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (cx + dx, cy + dy)
                if n in water and n not in seen:
                    seen.add(n)
                    q.append(n)
        if len(cells) < min_area:
            continue
        xs = [c[0] for c in cells]
        ys = [c[1] for c in cells]
        tagged = sum(1 for c in cells if c in fish)
        stand = set()
        for c in cells:
            for s in can_fish_from(water, walk, c[0], c[1]):
                stand.add(s)
        regions.append({
            'cells': cells, 'n': len(cells),
            'bbox': (min(xs), min(ys), max(xs), max(ys)),
            'tagged': tagged, 'stand': len(stand),
        })
    regions.sort(key=lambda r: -r['n'])
    print('\n  水域区块（>=%d 格），按面积排序：' % min_area)
    print('  %-3s %-26s %8s %8s %10s' % ('#', '范围 X,Y ~ X,Y', '面积', '已成钓点', '可钓站位'))
    for i, r in enumerate(regions[:25]):
        b = r['bbox']
        print('  %-3d %-26s %8d %8d %10d' % (
            i, '(%d,%d) ~ (%d,%d)' % b, r['n'], r['tagged'], r['stand']))
    if len(regions) > 25:
        print('  ... 共 %d 块（只列面积前 25）' % len(regions))
    return regions


def cmd_paint(m, rect, value, water_only, dry, apply_, largest_only=True):
    from collections import deque
    x1, y1, x2, y2 = rect
    x1, x2 = min(x1, x2), max(x1, x2)
    y1, y2 = min(y1, y2), max(y1, y2)
    water, fish, walk = build_sets(m)
    targets = set()
    skipped_walk = 0
    for x in range(x1, x2 + 1):
        for y in range(y1, y2 + 1):
            if not (0 <= x < m.W and 0 <= y < m.H):
                continue
            if water_only and (x, y) not in water:
                skipped_walk += 1
                continue
            if m.light(x, y) != value:
                targets.add((x, y))
    # 连通分量过滤：只染最大的一片，避免把区域内零散的小水池也标上
    dropped = 0
    if largest_only and targets:
        tset = targets
        seen = set()
        best = []
        for start in tset:
            if start in seen:
                continue
            comp = []
            q = deque([start])
            seen.add(start)
            while q:
                cx, cy = q.popleft()
                comp.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nb = (cx + dx, cy + dy)
                    if nb in tset and nb not in seen:
                        seen.add(nb)
                        q.append(nb)
            if len(comp) > len(best):
                best = comp
        dropped = len(targets) - len(best)
        targets = set(best)
    changed = len(targets)
    sample = sorted(targets)[:8]
    print('区域 (%d,%d) ~ (%d,%d)  目标 light=%d  掉落组=%d' %
          (x1, y1, x2, y2, value, value - 100))
    print('  将修改 %d 格；非水面跳过 %d 格；零散小块剔除 %d 格；区域总格数 %d' %
          (changed, skipped_walk, dropped, (x2 - x1 + 1) * (y2 - y1 + 1)))
    if sample:
        print('  示例：' + ', '.join('(%d,%d)' % s for s in sample))
    if dry:
        print('  [dry-run] 未写入文件')
        return
    if not apply_:
        print('  未写入。确认无误后加 --apply 才会真正修改。')
        return
    for x, y in targets:
        m.set_light(x, y, value)
    m.save()
    # 回读校验
    m2 = MapFile(m.path)
    ok = sum(1 for x, y in targets if m2.light(x, y) == value)
    print('  回读校验：%d/%d 格 light=%d' % (ok, len(targets), value))


def cmd_restore(path):
    bak = path + '.bak_fishing'
    if not os.path.exists(bak):
        raise SystemExit('没有备份文件 %s' % bak)
    shutil.copy2(bak, path)
    print('已从备份还原 %s' % path)


def main():
    ap = argparse.ArgumentParser(description='Mir2 地图钓场工具')
    ap.add_argument('cmd', choices=['report', 'paint', 'restore'])
    ap.add_argument('map')
    ap.add_argument('--rect', help='x1,y1,x2,y2')
    ap.add_argument('--value', type=int, default=100,
                    help='light 值 100~118（对应 00~18Fishing.txt），默认 100')
    ap.add_argument('--all-cells', action='store_true', help='连陆地格一起标记（默认只标水面）')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--apply', action='store_true')
    ap.add_argument('--min-area', type=int, default=400)
    a = ap.parse_args()

    if a.cmd == 'restore':
        cmd_restore(a.map)
        return

    m = MapFile(a.map)
    if a.cmd == 'report':
        cmd_report(m, a.min_area)
        return

    if not a.rect:
        raise SystemExit('paint 需要 --rect x1,y1,x2,y2')
    rect = tuple(int(t) for t in a.rect.split(','))
    if len(rect) != 4:
        raise SystemExit('--rect 格式应为 x1,y1,x2,y2')
    if not (100 <= a.value <= 119):
        raise SystemExit('--value 必须在 100~119 之间')
    if a.value == 119:
        print('警告：119 对应 19 号掉落组，但 Envir/Drops 只有 00~18Fishing.txt，119 会钓不到东西')
    cmd_paint(m, rect, a.value, not a.all_cells, a.dry_run, a.apply)


if __name__ == '__main__':
    main()
