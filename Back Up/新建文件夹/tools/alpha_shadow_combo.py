# -*- coding: utf-8 -*-
import struct, gzip, zlib, sys
sys.stdout.reconfigure(encoding='utf-8')

LIB = r'E:/micromir2/Data/Magic_32bit.Lib'
raw = open(LIB,'rb').read()
ver = struct.unpack_from('<i', raw, 0)[0]
count = struct.unpack_from('<i', raw, 4)[0]
idx = [struct.unpack_from('<i', raw, 8+4*i)[0] for i in range(count)]

def stats(i):
    q = idx[i]
    w,h,x,y = struct.unpack_from('<4h', raw, q)
    ln = struct.unpack_from('<i', raw, q+13)[0]
    d = raw[q+17:q+17+ln]
    if w==0 or h==0: return None
    try: px = gzip.decompress(d)
    except Exception:
        try: px = zlib.decompress(d)
        except Exception: return None
    need = w*h*4
    if len(px) < need: return None
    tot = 0; nz = 0; full = 0; hist = [0]*6
    for k in range(3, need, 4):
        a = px[k]
        if a:
            nz += 1
            tot += a
            if a == 255: full += 1
            hist[min(5, a*6//256)] += 1
    if nz == 0: return ('BLANK', w, h, 0, 0, 0, hist)
    return ('OK', w, h, tot/nz, full/nz*100, nz/(w*h)*100, hist)

print('idx  state   size        avgA   full%   cover%   alpha直方图(0-42/43-85/86-127/128-170/171-212/213-255 占比%)')
for i in list(range(1390, 1412)) + [1430, 1450, 1470, 1490, 1510, 1530] + [1546, 1547, 1548, 1549, 1550] + [1561, 1562]:
    s = stats(i)
    if s is None:
        print('%4d  EMPTY/OOR' % i); continue
    st,w,h,avg,full,cover,hist = s
    hs = ' '.join('%5.1f' % (v*100.0/max(1,sum(hist))) for v in hist)
    print('%4d  %-7s %4dx%-5d %6.1f %6.1f %7.1f   %s' % (i, st, w, h, avg, full, cover, hs))
