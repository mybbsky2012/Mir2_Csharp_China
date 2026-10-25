# -*- coding: utf-8 -*-
"""
重建 PrguseEx.Lib（整宽底栏填充图）—— v2 修正版。

v1 的错误：把原版帧 2 的左段（银珠盘+MIR 匾额）和右段（按钮栏）一起烤进了填充图，
而 MainDialog 本体（居中的 1024 底栏）自己还会画一份 → 全部装饰出现两份，画面重叠错乱。

v2 修正：填充图只保留「纯栏杆中段」做镜像平铺，拉通整个窗口宽度；
珠盘、匾额、按钮栏全部交还给 MainDialog 本体照常绘制，不再重复。
"""
import struct, gzip, io
import numpy as np
from PIL import Image

SRC_LIB = r"E:\micromir2\Data\Prguse.Lib"
OUT_LIB = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\PrguseEx.Lib"
WIDTHS = [1280, 1366, 1600, 1920, 2560]
SEG_X0, SEG_X1 = 300, 600          # 帧 2 的纯栏杆段（无装饰）
RAIL_TOP = 88                       # 栏杆及以上才保留（215 之下全是透明，这里保守取 88）

def read_frame(path, idx):
    D = open(path, "rb").read()
    VER, COUNT, _ = struct.unpack_from("<iii", D, 0)
    IL = struct.unpack_from("<%di" % COUNT, D, 12)
    off = IL[idx]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    return Image.frombytes("RGBA", (w, h), px[: w * h * 4], "raw", "BGRA")

def build_full_bar(seg, W):
    """镜像平铺纯栏杆段到宽度 W，高 152（与原帧同高，保证纵向对齐）。"""
    out = Image.new("RGBA", (W, 152), (0, 0, 0, 0))
    x, flip = 0, False
    while x < W:
        tile = seg.transpose(Image.FLIP_LEFT_RIGHT) if flip else seg
        out.paste(tile, (x, 0))
        x += seg.width
        flip = not flip
    return out

def pack_lib(frames):
    """v3 格式：12 字节头 + 帧偏移表 + 帧区（帧头 6*int16 + shadow + len + gzip BGRA）。"""
    index_list = []
    blob = io.BytesIO()
    offset = 12 + 4 * len(frames)
    for im in frames:
        index_list.append(offset)
        raw = im.tobytes("raw", "BGRA")
        comp = gzip.compress(raw, 6)
        blob.write(struct.pack("<hhhhhhB", im.width, im.height, 0, 0, 0, 0, 0))
        blob.write(struct.pack("<i", len(comp)))
        blob.write(comp)
        offset += 13 + 4 + len(comp)
    head = struct.pack("<iii", 3, len(frames), 12 + 4 * len(frames))
    il = struct.pack("<%di" % len(frames), *index_list)
    return head + il + blob.getvalue()

frame2 = read_frame(SRC_LIB, 2)
seg = frame2.crop((SEG_X0, 0, SEG_X1, 152))
print("源帧2: %dx%d，取纯栏杆段 [%d:%d]" % (frame2.width, frame2.height, SEG_X0, SEG_X1))

frames = [build_full_bar(seg, W) for W in WIDTHS]
data = pack_lib(frames)
open(OUT_LIB, "wb").write(data)
print("已写出 %s（%d 字节）" % (OUT_LIB, len(data)))

# ---- 自校验：按客户端读取逻辑解回 ----
D = open(OUT_LIB, "rb").read()
VER, COUNT, _ = struct.unpack_from("<iii", D, 0)
IL = struct.unpack_from("<%di" % COUNT, D, 12)
print("自校验: VER=%d COUNT=%d" % (VER, COUNT))
prev = None
for i, W in enumerate(WIDTHS):
    off = IL[i]
    w, h = struct.unpack_from("<hh", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    im = Image.frombytes("RGBA", (w, h), px[: w * h * 4], "raw", "BGRA")
    a = np.array(im)[:, :, 3]
    opaque_rows = np.where((a > 10).any(axis=1))[0]
    ok = "OK" if (w == W and h == 152 and opaque_rows.min() >= RAIL_TOP) else "异常!"
    print("  帧%d: %dx%d 不透明行 %d-%d %s" % (i, w, h, opaque_rows.min(), opaque_rows.max(), ok))
    # 预览图：棋盘底 + 模拟 MainDialog 居中叠加位置
    bg = Image.new("RGBA", (W, 152), (90, 140, 90, 255))
    bg.alpha_composite(im)
    bg.save("ex_v2_%d.png" % W)
print("预览图已保存 ex_v2_*.png（绿底模拟游戏画面）")
