# -*- coding: utf-8 -*-
"""分析本批新特效帧段：空帧 / 有效帧 / 尺寸，用于确定播放序列"""
import sys, struct, gzip, zlib
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image

LIBS = {
    'Magic_32bit': r'E:/micromir2/Data/Magic_32bit.Lib',
    'Magic2': r'E:/micromir2/Data/Magic2.Lib',
}
RANGES = [
    ('Magic_32bit', 712, 752, '强击 720-724 / 强击-秘籍 720-744'),
    ('Magic_32bit', 1618, 1696, '吸魔炎风-秘籍 1630-1688'),
    ('Magic_32bit', 1385, 1580, '影华连击 1390-1546 / 秘笈 1390-1570 / 收招 1560-1566'),
    ('Magic2', 1272, 1306, '毒雾 1280-1299'),
]


def loader(path):
    raw = open(path, 'rb').read()
    count = struct.unpack_from('<i', raw, 4)[0]
    idx = [struct.unpack_from('<i', raw, 8 + 4 * i)[0] for i in range(count)]

    def frame(i):
        if i < 0 or i >= count:
            return None
        q = idx[i]
        w, h, x, y = struct.unpack_from('<4h', raw, q)
        sh = raw[q + 12]
        ln = struct.unpack_from('<i', raw, q + 13)[0]
        d = raw[q + 17:q + 17 + ln]
        q += 17 + ln
        if (sh >> 7) == 1:
            mln = struct.unpack_from('<i', raw, q + 8)[0]
            q += 12 + mln
        if w == 0 or h == 0:
            return ('EMPTY', 0, 0, x, y)
        try:
            px = gzip.decompress(d)
        except Exception:
            px = zlib.decompress(d)
        im = Image.frombytes('RGBA', (w, h), px[:w * h * 4])
        b, g, r, a = im.split()
        im = Image.merge('RGBA', (r, g, b, a))
        nz = sum(1 for v in im.split()[3].get_flattened_data() if v > 0)
        return ('OK' if nz > 0 else 'BLANK', w, h, x, y)

    return count, frame


cache = {}
for lib, a, b, label in RANGES:
    if lib not in cache:
        cache[lib] = loader(LIBS[lib])
    count, frame = cache[lib]
    print(f'=== {label}  [{lib} 共 {count} 帧]')
    for i in range(a, b + 1):
        r = frame(i)
        if r is None:
            print(f'  {i}: <越界>')
            continue
        st, w, h, x, y = r
        mark = '' if st == 'OK' else ('  <<< ' + st)
        print(f'  {i}: {st:6s} {w:3d}x{h:3d} off=({x},{y}){mark}')
    print()
