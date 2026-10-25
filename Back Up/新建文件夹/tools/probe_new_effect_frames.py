# -*- coding: utf-8 -*-
"""核对新增特效帧段：找出空帧，推断 8方向×20帧 的分组结构"""
import sys, struct, gzip, zlib
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image

LIBS = {
    'Magic_32bit': r'E:\micromir2\Data\Magic_32bit.Lib',
    'Magic2': r'E:\micromir2\Data\Magic2.Lib',
}
RANGES = [
    ('Magic_32bit', 1380, 1570, '影华连击 1390-1559'),
    ('Magic_32bit', 1830, 2100, '影华连击-秘笈 1840-2089'),
    ('Magic_32bit', 1760, 1820, '落雷杀 1770-1809'),
    ('Magic2', 1270, 1310, '毒雾 1280-1299'),
    ('Magic2', 1645, 1672, '毒雾旧帧 1650'),
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
    empties = []
    line = []
    for i in range(a, b + 1):
        st, w, h, x, y = frame(i)
        if st != 'OK':
            empties.append(i)
        line.append(f'{i}:{st[0]}' + (f'{w}x{h}({x},{y})' if st == 'OK' else ''))
    # 紧凑输出：按 10 个一组
    for k in range(0, len(line), 5):
        print('   ' + '  '.join(line[k:k + 5]))
    print(f'   >>> 非OK帧: {empties}')
    print()
