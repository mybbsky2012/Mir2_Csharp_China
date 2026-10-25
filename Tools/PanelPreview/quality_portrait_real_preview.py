# -*- coding: utf-8 -*-
"""内观品质光效 v2 预览：用「真实内观截图」做底，验证修正效果
  原图  = 用户实测截图（光斑飘在左上、衣服上紫色噪点）
  修正版 = 剪影采样修正 + 双层外发光 + 染色降到 70 + 火焰贴武器
"""
import math
from collections import deque
from PIL import Image, ImageFilter, ImageDraw, ImageChops, ImageFont
import numpy as np

SRC = r"C:\Users\Administrator\.workbuddy\clipboard-images\clipboard-2026-10-23T18-00-59-287Z-b3d558a1.png"
OUT = r"D:\mir2-20241027\Tools\PanelPreview\quality_portrait_real_v2.png"
SCALE = 4

GRADES = [
    ("宝物 · 紫", (150, 70, 235)),
    ("神物 · 金", (255, 165, 25)),
]
BG = (18, 17, 21)

# 左剑、右剑刃线（原图归一化坐标，从剑柄往剑尖）
BLADES = [
    ((0.285, 0.505), (0.365, 0.795)),
    ((0.660, 0.575), (0.755, 0.665)),
]


def char_mask(img):
    """深色底泛洪去背景，保留主体"""
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    lum = a.max(axis=2)
    h, w = lum.shape
    bg = np.zeros((h, w), bool)
    dq = deque()
    for x in range(w):
        for y in (0, h - 1):
            if lum[y, x] < 68 and not bg[y, x]:
                bg[y, x] = True
                dq.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if lum[y, x] < 68 and not bg[y, x]:
                bg[y, x] = True
                dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not bg[ny, nx] and lum[ny, nx] < 68:
                bg[ny, nx] = True
                dq.append((ny, nx))
    m = (~bg).astype(np.uint8) * 255
    mi = Image.fromarray(m, "L").filter(ImageFilter.MedianFilter(3))
    return mi


def biggest(mask):
    m = (np.asarray(mask) > 128).astype(np.uint8)
    h, w = m.shape
    seen = np.zeros((h, w), np.uint8)
    best, bs = [], 0
    for sy in range(h):
        for sx in range(w):
            if not m[sy, sx] or seen[sy, sx]:
                continue
            st = [(sy, sx)]
            seen[sy, sx] = 1
            comp = []
            while st:
                y, x = st.pop()
                comp.append((y, x))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = 1
                        st.append((ny, nx))
            if len(comp) > bs:
                best, bs = comp, len(comp)
    out = np.zeros((h, w), np.uint8)
    for y, x in best:
        out[y, x] = 255
    return Image.fromarray(out, "L").filter(ImageFilter.MaxFilter(3))


def render(base, mask, colour, label, seed, deep=False):
    w, h = base.size
    rng = np.random.default_rng(seed)
    arr = np.asarray(base.convert("RGB")).astype(np.float32)
    lum = arr.max(axis=2)
    body = (np.asarray(mask) > 128).astype(np.float32)

    def layer(alpha_np):
        lay = Image.new("RGBA", (w, h), colour + (255,))
        lay.putalpha(Image.fromarray(np.clip(alpha_np, 0, 255).astype(np.uint8), "L"))
        return lay

    # 双层外发光（半径 3/6 原图 px）；加深版 alpha 上调
    a_in, a_out = (0.47, 0.27) if deep else (0.33, 0.16)
    ring_in = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(3 * 2 + 1)),
                                  mask.filter(ImageFilter.MinFilter(3)))
    ring_out = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(6 * 2 + 1)),
                                   mask.filter(ImageFilter.MaxFilter(3 * 2 + 1)))
    glow_in = layer(np.asarray(ring_in.filter(ImageFilter.GaussianBlur(1.2))) * a_in)
    glow_out = layer(np.asarray(ring_out.filter(ImageFilter.GaussianBlur(3.0))) * a_out)

    # 本体染色：现在 70 / 加深 95
    tint_a = 95 if deep else 70
    strength = np.clip((lum - 70) / 130.0, 0, 1) * body * tint_a
    tint = layer(strength)

    # 武器火焰：沿剑刃线冒火；加深版火更高更旺
    flame_a = 200 if deep else 150
    h_lo, h_hi = (9, 17) if deep else (7, 14)
    flame = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fd = ImageDraw.Draw(flame)

    def lick(x, y, height, width, lean, hot=100):
        steps = max(4, int(height))
        for s in range(steps):
            t = s / steps
            yy = y - s
            xx = x + lean * s + 2.0 * math.sin(t * 3.6 + lean * 8)
            profile = (1 - t) ** 0.5 * (0.62 + 0.6 * math.sin(math.pi * min(1.0, t * 1.3)))
            ww = width * max(0.0, profile)
            if ww < 0.6:
                continue
            a = int(flame_a * (1 - t) ** 0.8) + 10
            c = tuple(min(255, v + hot) for v in colour) if t < 0.22 else (
                colour if t < 0.6 else tuple(int(v * 0.82) for v in colour))
            fd.line([(xx - ww, yy), (xx + ww, yy)], fill=c + (a,))

    n = 14 if deep else 11
    for (x0, y0), (x1, y1) in BLADES:
        ax, ay, bx, by = x0 * w, y0 * h, x1 * w, y1 * h
        for i in range(n):
            t = i / (n - 1)
            px = ax + (bx - ax) * t + rng.uniform(-1.2, 1.2) * SCALE
            py = ay + (by - ay) * t + rng.uniform(-1.0, 1.0) * SCALE
            lick(px, py, (h_lo + rng.random() * (h_hi - h_lo)) * SCALE, (1.6 + rng.random() * 1.6) * SCALE,
                 (rng.random() - 0.5) * 0.25)

    sparks = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sparks)
    for (x0, y0), (x1, y1) in BLADES:
        for _ in range(16 if deep else 12):
            t = rng.random()
            px = (x0 * w + (x1 * w - x0 * w) * t) + rng.uniform(-4, 4) * SCALE
            py = (y0 * h + (y1 * h - y0 * h) * t) - rng.uniform(0, 11 if deep else 9) * SCALE
            r = (0.6 + rng.random() * 0.9) * SCALE
            c = tuple(min(255, v + 90) for v in colour)
            sd.ellipse([px - r, py - r, px + r, py + r], fill=c + (int(150 + rng.random() * 90),))

    flame = flame.filter(ImageFilter.GaussianBlur(0.6 * SCALE / 4 + 0.4))
    fglow = flame.filter(ImageFilter.GaussianBlur(2.2 * SCALE / 4 + 1)).point(lambda v: int(v * 0.6))
    fire = Image.alpha_composite(fglow, flame)
    fire = Image.alpha_composite(fire, sparks.filter(ImageFilter.GaussianBlur(0.4 * SCALE / 4)))

    out = base.convert("RGBA")
    for lay in (glow_out, glow_in, tint, fire):
        out = Image.alpha_composite(out, lay)

    panel = Image.new("RGB", (w, h), BG)
    panel.paste(out.convert("RGB"), (0, 0))
    d = ImageDraw.Draw(panel)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc", 20)
    except Exception:
        font = ImageFont.load_default()
    d.text((10, 6), label, fill=colour, font=font)
    return panel


def main():
    src = Image.open(SRC).convert("RGB")
    big = src.resize((src.width * SCALE, src.height * SCALE), Image.LANCZOS)
    mask = biggest(char_mask(big))
    print("mask px:", int((np.asarray(mask) > 128).sum()))

    panels = []
    d0 = None
    # 上排：现在版（紫/金）  下排：加深版（紫/金）
    rows = [(False, "现在版"), (True, "加深版")]
    cell = []
    for deep, tag in rows:
        for i, (l, c) in enumerate(GRADES):
            cell.append(render(big, mask, c, "%s · %s" % (tag, l), 5 + i + (40 if deep else 0), deep))
    cw, ch = cell[0].size
    canvas = Image.new("RGB", (cw * 2 + 12, ch * 2 + 12), (10, 10, 12))
    for idx, p in enumerate(cell):
        r, c2 = divmod(idx, 2)
        canvas.paste(p, (c2 * (cw + 12), r * (ch + 12)))
    canvas.save(OUT)
    print("saved:", OUT, canvas.size)


if __name__ == "__main__":
    main()
