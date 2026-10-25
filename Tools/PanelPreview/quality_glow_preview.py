# -*- coding: utf-8 -*-
"""品质外观效果图 v4
  衣服 + 头盔 = 品质色发光（装备本体染色 + 外发光晕）
  武器       = 魔幻火焰（贴武器轮廓的火舌 + 上升火星粒子）
"""
import math
from PIL import Image, ImageFilter, ImageDraw, ImageChops, ImageFont
import numpy as np

SRC = r"C:\Users\Administrator\.workbuddy\clipboard-images\clipboard-2026-10-23T17-35-16-354Z-782a53e3.png"
OUT = r"D:\mir2-20241027\Tools\PanelPreview\quality_glow_preview.png"
ZOOM = r"D:\mir2-20241027\Tools\PanelPreview\quality_glow_zoom.png"

GRADES = [
    ("宝物 · 紫", (150, 70, 235)),
    ("圣物 · 蓝", (50, 130, 250)),
    ("神物 · 金", (255, 165, 25)),
]
BG = (12, 11, 14)


def build_mask(img):
    """人物剪影：亮度阈值 + 开运算 + 最大连通块 + 外扩 1px"""
    arr = np.asarray(img.convert("RGB")).astype(np.int16)
    lum = arr.max(axis=2)
    m = (lum > 66).astype(np.uint8)

    mi = Image.fromarray(m * 255, "L")
    mi = mi.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    m = (np.asarray(mi) > 128).astype(np.uint8)

    h, w = m.shape
    seen = np.zeros((h, w), np.uint8)
    best, best_size = [], 0
    for sy in range(h):
        for sx in range(w):
            if not m[sy, sx] or seen[sy, sx]:
                continue
            stack = [(sy, sx)]
            seen[sy, sx] = 1
            comp = []
            while stack:
                y, x = stack.pop()
                comp.append((y, x))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and m[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = 1
                        stack.append((ny, nx))
            if len(comp) > best_size:
                best, best_size = comp, len(comp)

    out = np.zeros((h, w), np.uint8)
    for (y, x) in best:
        out[y, x] = 255
    mi = Image.fromarray(out, "L").filter(ImageFilter.MaxFilter(3))
    return mi


def grade_panel(img, mask, colour, label, seed):
    w, h = img.size
    pad = 42
    pw, ph = w + pad * 2, h + pad * 2
    rng = np.random.default_rng(seed)

    def tint(alpha_img, mul=1.0):
        layer = Image.new("RGBA", (w, h), colour + (255,))
        arr = np.asarray(alpha_img).astype(np.float32) * mul
        layer.putalpha(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "L"))
        return layer

    arr = np.asarray(img.convert("RGB")).astype(np.float32)
    lum = arr.max(axis=2)

    # ---------- 衣服 + 头盔：装备本体染色发光 ----------
    # 只给「人物范围内且够亮」的装备像素上色，亮度越高染得越透（暗部保留细节）
    body = (np.asarray(mask) > 128).astype(np.float32)
    strength = np.clip((lum - 60) / 120.0, 0, 1) * body
    body_tint = tint(Image.fromarray((strength * 150).astype(np.uint8), "L"), 0.75)

    # 外发光晕（贴轮廓往外散）
    halo_ring = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(15)),
                                    mask.filter(ImageFilter.MinFilter(5)))
    halo = tint(halo_ring.filter(ImageFilter.GaussianBlur(3.4)), 0.7)

    # 贴边亮线（让轮廓更利落）
    edge = ImageChops.subtract(mask, mask.filter(ImageFilter.MinFilter(3)))
    rim = tint(edge, 0.9)

    # ---------- 武器：魔幻火焰 ----------
    flame = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fd = ImageDraw.Draw(flame)

    def lick(x, y, height, width, lean, hot=110):
        steps = max(4, int(height))
        for s in range(steps):
            t = s / steps
            yy = y - s
            xx = x + lean * s + 2.0 * math.sin(t * 3.8 + lean * 9)
            profile = (1 - t) ** 0.5 * (0.6 + 0.6 * math.sin(math.pi * min(1.0, t * 1.3)))
            ww = width * max(0.0, profile)
            if ww < 0.7:
                continue
            a = int(220 * (1 - t) ** 0.75) + 12
            if t < 0.2:
                c = tuple(min(255, v + hot) for v in colour)
            elif t < 0.6:
                c = colour
            else:
                c = tuple(int(v * 0.8) for v in colour)
            fd.line([(xx - ww, yy), (xx + ww, yy)], fill=c + (a,))
            if t < 0.55:
                cw = max(0.6, ww * 0.33)
                fd.line([(xx - cw, yy), (xx + cw, yy)],
                        fill=tuple(min(255, v + hot - 20) for v in colour) + (min(255, a + 30),))

    # 武器区域（左手弯刀 / 右手长刃），沿线多点冒火
    weapons = [
        ((0.13, 0.88), (0.30, 0.44), 16),   # 左手弯刀：从下往上
        ((0.72, 0.78), (0.94, 0.48), 16),   # 右手长刃
    ]
    for (x0, y0), (x1, y1), n in weapons:
        ax, ay = x0 * w, y0 * h
        bx, by = x1 * w, y1 * h
        for i in range(n):
            t = i / (n - 1)
            px = ax + (bx - ax) * t + rng.uniform(-2.5, 2.5)
            py = ay + (by - ay) * t + rng.uniform(-2.0, 2.0)
            lick(px, py, 10 + rng.random() * 26, 2.0 + rng.random() * 2.4,
                 (rng.random() - 0.5) * 0.3)

    # 火星粒子（魔幻感）：向上飘的小亮点
    sparks = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sparks)
    for (x0, y0), (x1, y1), n in weapons:
        for _ in range(34):
            t = rng.random()
            px = (x0 * w + (x1 * w - x0 * w) * t) + rng.uniform(-9, 9)
            py = (y0 * h + (y1 * h - y0 * h) * t) - rng.uniform(0, 46)
            r = 0.7 + rng.random() * 1.5
            a = int(120 + rng.random() * 120)
            c = tuple(min(255, v + 90) for v in colour)
            sd.ellipse([px - r, py - r, px + r, py + r], fill=c + (a,))

    flame = flame.filter(ImageFilter.GaussianBlur(0.8))
    glow = flame.filter(ImageFilter.GaussianBlur(5)).point(lambda v: int(v * 0.65))
    flame_total = Image.alpha_composite(glow, flame)
    flame_total = Image.alpha_composite(flame_total, sparks.filter(ImageFilter.GaussianBlur(0.5)))

    # ---------- 合成 ----------
    panel = Image.new("RGB", (pw, ph), BG)
    # 背景压暗：原图背景是噪点纹理，直接贴会显得脏（人物保留原样）
    dim = Image.new("RGB", (w, h), BG)
    soft = mask.filter(ImageFilter.GaussianBlur(1.2))
    base = Image.composite(img, Image.blend(img, dim, 0.82), soft)
    panel.paste(base, (pad, pad))
    view = panel.convert("RGBA")
    position = (pad, pad)

    def over(layer, base):
        tmp = Image.new("RGBA", (pw, ph), (0, 0, 0, 0))
        tmp.paste(layer, position, layer)
        return Image.alpha_composite(base, tmp)

    view = over(halo, view)        # 身后光晕
    view = over(body_tint, view)   # 装备本体染色发光
    view = over(rim, view)         # 贴边亮线
    view = over(flame_total, view) # 武器火焰 + 火星

    result = view.convert("RGB")
    d = ImageDraw.Draw(result)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc", 17)
    except Exception:
        font = ImageFont.load_default()
    d.text((12, 10), label, fill=colour, font=font)
    return result


def main():
    img = Image.open(SRC).convert("RGB")
    mask = build_mask(img)
    print("mask cover:", (np.asarray(mask) > 128).sum(), "of", img.size[0] * img.size[1])

    panels = [grade_panel(img, mask, c, l, 17 + i) for i, (l, c) in enumerate(GRADES)]
    W = sum(p.width for p in panels) + 12 * (len(panels) - 1)
    H = max(p.height for p in panels)
    canvas = Image.new("RGB", (W, H), (8, 8, 10))
    x = 0
    for p in panels:
        canvas.paste(p, (x, 0))
        x += p.width + 12
    canvas.save(OUT)
    canvas.crop((0, 0, panels[0].width, H)).resize((panels[0].width * 2, H * 2), Image.LANCZOS).save(ZOOM)
    print("saved:", OUT, canvas.size)


if __name__ == "__main__":
    main()
