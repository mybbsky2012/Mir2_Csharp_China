# -*- coding: utf-8 -*-
import struct, gzip, zlib, sys
sys.stdout.reconfigure(encoding='utf-8')

LIBS = {
 'Magic_32bit': r'E:/micromir2/Data/Magic_32bit.Lib',
 'Magic':       r'E:/micromir2/Data/Magic.Lib',
 'Magic2':      r'E:/micromir2/Data/Magic2.Lib',
}

def loader(path):
    raw = open(path,'rb').read()
    ver = struct.unpack_from('<i', raw, 0)[0]
    count = struct.unpack_from('<i', raw, 4)[0]
    idx = [struct.unpack_from('<i', raw, 8+4*i)[0] for i in range(count)]
    def frame(i):
        if i < 0 or i >= count: return ('OOR',0,0)
        q = idx[i]
        w,h,x,y = struct.unpack_from('<4h', raw, q)
        sh = raw[q+12]
        ln = struct.unpack_from('<i', raw, q+13)[0]
        d = raw[q+17:q+17+ln]
        if w==0 or h==0: return ('EMPTY',0,0)
        try: px = gzip.decompress(d)
        except Exception:
            try: px = zlib.decompress(d)
            except Exception: return ('BADZIP',w,h)
        need = w*h*4
        if len(px) < need: return ('SHORT',w,h)
        nz = 0
        for k in range(3, need, 4):
            if px[k]: nz += 1
        return ('OK' if nz>0 else 'BLANK', w, h)
    return ver, count, frame

RANGES = [
 ('Magic_32bit', 718, 748, '强击 721-724 / 强击-秘籍 721-744'),
 ('Magic_32bit', 1628, 1692, '吸魔炎风 1631-1688'),
 ('Magic_32bit', 1388, 1412, '影华连击 1391+'),
 ('Magic_32bit', 1556, 1570, '影华连击-秘笈 收招 1561-1566'),
 ('Magic_32bit', 1698, 1736, '万效符 1701-1719'),
 ('Magic_32bit', 1764, 1800, '落雷杀 1771-1793'),
 ('Magic_32bit', 1806, 1828, '灭杀 1810-1824'),
 ('Magic_32bit', 758, 786, '月华乱舞-秘籍 761+'),
 ('Magic2',      1276, 1302, '毒雾 1280-1299'),
 ('Magic',       1626, 1640, '吸魔炎风 旧 Magic.Lib 1630'),
]
cache = {}
for lib,a,b,label in RANGES:
    if lib not in cache: cache[lib] = loader(LIBS[lib])
    ver,count,frame = cache[lib]
    print("="*80)
    print("%s  [%s 共 %d 帧]" % (label, lib, count))
    for i in range(a,b+1):
        st,w,h = frame(i)
        mark = '' if st=='OK' else '  <<<< '+st
        print("  %5d: %-7s %3dx%-3d%s" % (i,st,w,h,mark))
