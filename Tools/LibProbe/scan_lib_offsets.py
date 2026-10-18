"""扫描 .Lib，列出「视觉内容相对控件 Location 偏移较大」的图。
偏移 = 可见像素包围盒左上角 (l,t)；只要 l/t>0，控件命中框就比视觉内容偏左上 (l,t)。
"""
import gzip
import struct
import sys
import os

def scan(path, min_off=3, max_index=None):
    buf = open(path, "rb").read()
    pos = 0
    ver, count = struct.unpack_from("<ii", buf, pos)
    pos += 8
    if ver >= 3:
        pos += 4
    offsets = struct.unpack_from("<%di" % count, buf, pos)
    total = count if max_index is None else min(count, max_index)
    hits = []
    for i in range(total):
        off = offsets[i]
        try:
            w, h, X, Y, sx, sy = struct.unpack_from("<hhhhhh", buf, off)
            length = struct.unpack_from("<i", buf, off + 13)[0]
        except Exception:
            continue
        if w <= 0 or h <= 0 or w > 2000 or h > 2000:
            continue
        start = off + 17
        blob = buf[start:start + length]
        if len(blob) < 20:
            continue
        try:
            raw = gzip.decompress(blob)
        except Exception:
            continue
        if len(raw) < w * h * 4:
            continue
        l, t, r, b = w, h, 0, 0
        found = False
        step = 1 if w * h <= 250000 else 4
        for y in range(0, h, step):
            row = y * w * 4
            for x in range(0, w, step):
                if raw[row + x * 4] != 0:
                    found = True
                    if x < l: l = x
                    if x >= r: r = x + step
                    if y < t: t = y
                    if y >= b: b = y + step
        if not found:
            continue
        if l >= min_off or t >= min_off:
            hits.append((i, w, h, l, t, r - l, b - t))
    return count, hits


if __name__ == "__main__":
    for path in sys.argv[1:]:
        if not os.path.exists(path):
            print("skip", path)
            continue
        count, hits = scan(path, 3)
        print("=== %s  count=%d  偏移>=3 的图: %d 张 ===" % (os.path.basename(path), count, len(hits)))
        for h in hits[:25]:
            print("   idx=%-5d w=%-4d h=%-4d  视觉起点(%d,%d)  可见 %dx%d" % h)
        if len(hits) > 25:
            print("   ... 其余 %d 张" % (len(hits) - 25))
