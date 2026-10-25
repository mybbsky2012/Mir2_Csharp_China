# -*- coding: utf-8 -*-
"""检查召唤特效帧是否存在于客户端图库：Magic.Lib 1500-1509（召唤骷髅）、Magic2.Lib 0-9（召唤神兽）"""
import struct, gzip, os
from PIL import Image

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools"
DATA = r"E:\micromir2\Data"

def load(path):
    D = open(path, "rb").read()
    ver, count = struct.unpack_from("<ii", D, 0)
    il = list(struct.unpack_from("<%di" % count, D, 12))
    return D, il, ver, count

def frame(D, off):
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    raw = D[off + 17: off + 17 + ln]
    px = gzip.decompress(raw)
    need = w * h * 4
    if len(px) < need:
        px = px + b"\x00" * (need - len(px))
    return Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")

def sheet(name, path, start, n):
    D, il, ver, count = load(path)
    print(f"{name}: 版本={ver}  总帧数={count}  请求帧 {start}..{start+n-1}")
    ims = []
    for i in range(start, start + n):
        if i < 0 or i >= count:
            print(f"   [{i}] ★越界（不存在）")
            continue
        try:
            im = frame(D, il[i])
        except Exception as e:
            print(f"   [{i}] 解码失败 {e}")
            continue
        ims.append((i, im))
    if not ims:
        print("   -> 无可用帧")
        return
    cw = max(im.width for _, im in ims) + 8
    ch = max(im.height for _, im in ims) + 8
    out = Image.new("RGBA", (cw * len(ims), ch), (40, 40, 40, 255))
    for k, (i, im) in enumerate(ims):
        out.paste(im, (k * cw + 4, 4), im)
    p = os.path.join(OUT, f"fx_{name}.png")
    out.save(p)
    print(f"   -> 拼图 {len(ims)} 帧: {p}  ({out.width}x{out.height})")
    print("   尺寸:", ", ".join(f"{i}:{im.width}x{im.height}" for i, im in ims))

sheet("Magic_1500_骷髅", os.path.join(DATA, "Magic.Lib"), 1500, 10)
sheet("Magic2_0_神兽", os.path.join(DATA, "Magic2.Lib"), 0, 10)
