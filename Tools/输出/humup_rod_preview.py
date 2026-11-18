import struct, gzip, io, os
from PIL import Image

BASE = 'E:/micromir2/Data/HumUp'

def read_lib(path):
    with open(path, 'rb') as f:
        data = f.read()
    version, count = struct.unpack_from('<ii', data, 0)
    off = 8
    if version >= 3:
        off += 4
    idx = list(struct.unpack_from('<%di' % count, data, off))
    return data, idx

def frame(data, idx, i):
    p = idx[i]
    w, h, x, y, sx, sy = struct.unpack_from('<hhhhhh', data, p)
    shadow = data[p + 12]
    length = struct.unpack_from('<i', data, p + 13)[0]
    if w <= 0 or h <= 0 or length <= 0:
        return None, (x, y)
    raw = gzip.decompress(data[p + 17:p + 17 + length])
    return Image.frombytes('RGBA', (w, h), raw, 'raw', 'BGRA'), (x, y)

def compose(libname, body, weapon, out):
    data, idx = read_lib('%s/%s.Lib' % (BASE, libname))
    canvas = Image.new('RGBA', (300, 320), (46, 46, 54, 255))
    ax, ay = 100, 10
    for f in (body, weapon):
        if f is None: continue
        if f >= len(idx): continue
        img, (x, y) = frame(data, idx, f)
        if img is None:
            print('  空帧', f, 'in', libname); continue
        canvas.alpha_composite(img, (ax + x, ay + y))
    canvas.save(out)
    return canvas

rows = []
pairs = [
    ('战士男', 'StateItem_Warrior_Man', 2450, 2310),
    ('法师男', 'StateItem_Wizard_Man', 2451, 2310),
    ('道士男', 'StateItem_Tao_Man', 2452, 2310),
    ('刺客男', 'StateItem_Assassin_Man', 2453, 2310),
    ('弓手男', 'StateItem_Archer_Man', 2454, 2310),
]
tiles = []
for label, lib, body, rod in pairs:
    try:
        im = compose(lib, body, rod, 'D:/mir2-20241027/Tools/输出/state_rod_%s.png' % label)
        tiles.append((label, im))
    except Exception as e:
        print(label, 'ERROR', e)

if tiles:
    sheet = Image.new('RGBA', (300 * len(tiles), 320), (24, 24, 28, 255))
    for i, (label, im) in enumerate(tiles):
        sheet.alpha_composite(im, (300 * i, 0))
    sheet.save('D:/mir2-20241027/Tools/输出/羽化内观_持竿_预览.png')
    print('sheet', sheet.size)
