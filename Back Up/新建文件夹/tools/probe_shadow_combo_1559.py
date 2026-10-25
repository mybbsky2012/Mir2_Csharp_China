# -*- coding: utf-8 -*-
import struct, gzip, zlib, sys
sys.stdout.reconfigure(encoding='utf-8')

LIBS = {
 'Magic_32bit': r'E:/micromir2/Data/Magic_32bit.Lib',
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

ver, count, frame = loader(LIBS['Magic_32bit'])
print('Magic_32bit ver=%d count=%d' % (ver, count))
print('--- 1388 .. 1605 ---')
for i in range(1388, 1606):
    st, w, h = frame(i)
    mark = ''
    if st == 'OK':
        mark = '  <<<'
    print('%5d %-6s %4dx%-5d%s' % (i, st, w, h, mark))
