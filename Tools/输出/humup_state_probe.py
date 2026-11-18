import struct, sys

def read_lib(path):
    with open(path, 'rb') as f:
        data = f.read()
    version, count = struct.unpack_from('<ii', data, 0)
    off = 8
    if version >= 3:
        off += 4  # frameSeek
    idx = list(struct.unpack_from('<%di' % count, data, off))
    off += 4 * count
    frames = []
    for i in range(count):
        p = idx[i]
        w, h, x, y, sx, sy = struct.unpack_from('<hhhhhh', data, p)
        shadow = data[p + 12]
        length = struct.unpack_from('<i', data, p + 13)[0]
        frames.append((w, h, x, y, sx, sy, shadow, length))
    return version, count, frames

for name in ['StateItem_Warrior_Man', 'StateItem_Common_Man', 'StateItem_Wizard_Man', 'StateItem_Archer_Man']:
    p = 'E:/micromir2/Data/HumUp/%s.Lib' % name
    try:
        v, n, fr = read_lib(p)
    except Exception as e:
        print(name, 'ERROR', e); continue
    print('%s  version=%d  frames=%d' % (name, v, n))
    for i in [2305, 2308, 2309, 2310, 2311, 2312, 2313, 2314, 2320, 2428, 2430, 2432, 2436, 2450, 2451, 2453, 2454, 2460, 2461, 2463, 2464, 3390, 3395, 3782]:
        if i < n:
            w, h, x, y, sx, sy, sh, ln = fr[i]
            mark = '空' if w == 0 or h == 0 or ln <= 0 else '%dx%d' % (w, h)
            print('   [%4d] %-10s x=%d y=%d' % (i, mark, x, y))
