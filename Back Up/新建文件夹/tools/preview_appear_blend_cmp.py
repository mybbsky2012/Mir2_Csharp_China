# -*- coding: utf-8 -*-
"""把出场帧在『草地背景』上分别按 普通alpha / 加法混合 渲染，对比颜色观感。
加法模拟: out = clamp(src*srcAlpha + dst)  （DXManager DrawBlend: SourceAlpha / Dest=One）
普通alpha: out = src*srcAlpha + dst*(1-srcAlpha)
"""
import struct, gzip, os
from PIL import Image, ImageDraw, ImageFont

DATA = r"E:\micromir2\Data\Monster"
OUT  = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\sheets"
BG   = (58, 92, 48)          # 近似草地绿

def load(path):
    D = open(path, "rb").read()
    ver, count = struct.unpack_from("<ii", D, 0)
    il = list(struct.unpack_from("<%di" % count, D, 12))
    return D, il, count

def frame(D, off):
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    need = w * h * 4
    if len(px) < need:
        px = px + b"\x00" * (need - len(px))
    return Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")

def compose(frames, mode, scale=2):
    """叠加多帧（每帧按自己锚点简单居中叠）"""
    if not frames:
        return None
    W = max(f.width for f in frames)
    H = max(f.height for f in frames)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for f in frames:
        ox = (W - f.width) // 2
        oy = (H - f.height) // 2
        canvas.alpha_composite(f, (ox, oy))
    # 合成到不透明背景
    res = Image.new("RGB", (W, H), BG)
    cp = canvas.load(); rp = res.load()
    for y in range(H):
        for x in range(W):
            r, g, b, a = cp[x, y]
            br, bg, bb = BG
            al = a / 255.0
            if mode == "add":
                rr = min(255, int(r * al + br)); gg = min(255, int(g * al + bg)); bb2 = min(255, int(b * al + bb))
            else:
                rr = int(r * al + br * (1 - al)); gg = int(g * al + bg * (1 - al)); bb2 = int(b * al + bb * (1 - al))
            rp[x, y] = (rr, gg, bb2)
    if scale != 1:
        res = res.resize((W * scale, H * scale), Image.NEAREST)
    return res

def load_range(libname, start, end):
    D, il, count = load(os.path.join(DATA, libname))
    out = []
    for i in range(max(0, start), min(end, count - 1) + 1):
        try:
            im = frame(D, il[i])
        except Exception:
            continue
        if im.width <= 6 or im.height <= 6:
            continue
        out.append((i, im))
    return out

GROUPS = [
    ("170.Lib", 0, 9,   "神兽170 帧0-9 (方向0)"),
    ("036.Lib", 224, 233, "骷髅036 帧224-233"),
    ("172.Lib", 224, 233, "月灵172 帧224-233"),
]

try:
    fnt = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 18)
except Exception:
    fnt = ImageFont.load_default()

tiles = []
for lib, a, b, label in GROUPS:
    fr = load_range(lib, a, b)
    n = len(fr)
    # 取中间帧做颜色观感对比（粒子最多的一帧）
    mid = fr[n // 2][1]
    add = compose([mid], "add", 3)
    alp = compose([mid], "alpha", 3)
    tiles.append((label, add, alp, mid))

PAD = 16
HDR = 30
W = sum(t[1].width for t in tiles) + PAD * (len(tiles) + 1)
H = max(t[1].height for t in tiles) * 2 + HDR * 2 + PAD * 4
canvas = Image.new("RGB", (W, H), (24, 26, 32))
d = ImageDraw.Draw(canvas)

x = PAD
d.text((PAD, 6), "上排=加法混合(当前)  下排=普通alpha  背景=草地绿(58,92,48)", fill=(255, 210, 120), font=fnt)
y0 = HDR + PAD
y1 = y0 + max(t[1].height for t in tiles) + HDR + PAD
for label, add, alp, mid in tiles:
    d.text((x, y0 - HDR + 4), label + " add", fill=(180, 230, 255), font=fnt)
    canvas.paste(add, (x, y0))
    d.text((x, y1 - HDR + 4), label + " alpha", fill=(255, 200, 200), font=fnt)
    canvas.paste(alp, (x, y1))
    x += add.width + PAD

out = os.path.join(OUT, "神兽_透明度对比_加vs普通.png")
os.makedirs(OUT, exist_ok=True)
canvas.save(out)
print("已生成:", out, canvas.size)
