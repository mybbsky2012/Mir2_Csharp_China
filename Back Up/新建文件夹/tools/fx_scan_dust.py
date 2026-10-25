# -*- coding: utf-8 -*-
"""按色系扫描特效图库，找出可能的「尘土旋风(土黄) / 红色汇聚」帧段"""
import struct, gzip, os, colorsys
from PIL import Image

DATA = r"E:\micromir2\Data"

def load(path):
    D = open(path, "rb").read()
    ver, count = struct.unpack_from("<ii", D, 0)
    il = list(struct.unpack_from("<%di" % count, D, 12))
    return D, il, count

def stats(D, off):
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    need = w * h * 4
    if len(px) < need:
        return None
    area = w * h
    nr = ng = nb = n = 0
    for p in range(0, need, 16):  # 抽样 1/4
        b, g, r, a = px[p], px[p+1], px[p+2], px[p+3]
        if a < 40:
            continue
        if r + g + b < 60:
            continue
        nr += r; ng += g; nb += b; n += 1
    if n < area * 0.02:
        return None
    r, g, b = nr / n, ng / n, nb / n
    hh, ss, vv = colorsys.rgb_to_hsv(r/255, g/255, b/255)
    return hh*360, ss, vv, n/area

LIBS = ["Magic", "Magic2", "Magic3", "Effect"]
BUCKETS = {"土黄(尘土)": (15, 60), "红/橙红": (-25, 18), "灰白(烟)": None}

for lib in LIBS:
    path = os.path.join(DATA, lib + ".Lib")
    if not os.path.exists(path):
        continue
    D, il, count = load(path)
    print(f"\n===== {lib}.Lib  {count} 帧 =====")
    runs = []
    cur = []
    for i in range(count):
        try:
            s = stats(D, il[i])
        except Exception:
            s = None
        if s and (20 <= s[0] <= 60) and s[1] > 0.25 and s[2] > 0.35 and s[3] > 0.05:
            cur.append(i)
        else:
            if len(cur) >= 4:
                runs.append((cur[0], cur[-1]))
            cur = []
    if len(cur) >= 4:
        runs.append((cur[0], cur[-1]))
    print("  土黄/棕色 连续帧段:", runs[:40])
