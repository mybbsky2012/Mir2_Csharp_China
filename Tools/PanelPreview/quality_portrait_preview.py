# -*- coding: utf-8 -*-
"""内观（角色面板立绘）品质光效 预览：现状 vs 加强版
  现状   = DrawPortrait 现参数：染色 alpha45 + 小火(7颗/高18/透明80) + 8 火星
  加强版 = 染色加深 + 身后柔光晕 + 火焰更高更旺 + 火星更多（对比用，代码确认后再同步）
"""
import math
from PIL import Image, ImageFilter, ImageDraw, ImageChops, ImageFont
import numpy as np

SRC = r"C:\Users\Administrator\.workbuddy\clipboard-images\clipboard-2026-10-23T17-35-16-354Z-782a53e3.png"
OUT = r"D:\mir2-20241027\Tools\PanelPreview\quality_portrait_preview.png"

GRADES = [
    ("宝物 · 紫", (150, 70, 235)),
    ("圣物 · 蓝", (50, 130, 250)),
    ("神物 · 金", (255, 165, 25)),
]
BG = (20, 19, 24)          # 内观面板是深色底
PANEL_BG = (26, 25, 31)    # 角色框底色

# 现状 DrawPortrait 参数
CUR = dict(tint=45, halo=0, flame_n=7, flame_h=18, flame_a=80, sparks=8)
# 加强版参数（预览用）
NEW = dict(tint=95, halo=70, flame_n=14, flame_h=36, flame_a=170, sparks=16)


def build_mask(img):
    arr = np.asarray(img.convert("RGB")).astype(np.int16)
    lum = arr.max(axis=2)
    m = (lum > 66).astype(np.uint8)
    mi = Image.fromarray(m * 255, "L").filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
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
    return Image.fromarray(out, "L").filter(ImageFilter.MaxFilter(3))


def portrait_panel(img, mask, colour, label, p, seed):
    """p = 参数 dict；模拟 DrawPortrait 的分层效果"""
    w, h = img.size
    pad = 40
    pw, ph = w + pad * 2, h + pad * 2
    rng = np.random.default_rng(seed)

    arr = np.asarray(img.convert("RGB")).astype(np.float32)
    lum = arr.max(axis=2)
    body = (np.asarray(mask) > 128).astype(np.float32)

    def tint(alpha_img, mul=1.0):
        layer = Image.new("RGBA", (w, h), colour + (255,))
        layer.putalpha(Image.fromarray(np.clip(alpha_img, 0, 255).astype(np.uint8), "L"))
        return layer

    # ---- 本体染色（加法混合：只有亮部被点亮，亮度越高染得越透）----
    strength = np.clip((lum - 60) / 110.0, 0, 1) * body
    body_tint = tint(strength * p["tint"])

    # ---- 身后柔光晕（加强版才有）----
    halo = None
    if p["halo"]:
        halo_ring = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(17)),
                                        mask.filter(ImageFilter.MinFilter(5)))
        halo = tint(halo_ring.filter(ImageFilter.GaussianBlur(4.5)), p["halo"] / 255.0)

    # ---- 武器火焰（贴武器线冒火，参数随 p）----
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
            a = int(p["flame_a"] * (1 - t) ** 0.75) + 12
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

    weapons = [
        ((0.13, 0.88), (0.30, 0.44), 8),   # 左手弯刀
        ((0.72, 0.78), (0.94, 0.48), 8),   # 右手长刃
    ]
    for (x0, y0), (x1, y1), n in weapons:
        ax, ay = x0 * w, y0 * h
        bx, by = x1 * w, y1 * h
        for i in range(n):
            t = i / max(1, n - 1)
            px = ax + (bx - ax) * t + rng.uniform(-2.5, 2.5)
            py = ay + (by - ay) * t + rng.uniform(-2.0, 2.0)
            lick(px, py, p["flame_h"] * (0.6 + rng.random() * 0.7),
                 2.0 + rng.random() * 2.4, (rng.random() - 0.5) * 0.3)

    sparks = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sparks)
    for (x0, y0), (x1, y1), n in weapons:
        for _ in range(p["sparks"]):
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

    # ---- 合成：内观面板底色 + 人物 ----
    panel = Image.new("RGB", (pw, ph), PANEL_BG)
    d0 = ImageDraw.Draw(panel)
    d0.rectangle([pad - 6, pad - 6, pad + w + 6, pad + h + 6], outline=(70, 66, 80), width=2)
    panel.paste(img, (pad, pad))
    view = panel.convert("RGBA")
    position = (pad, pad)

    def over(layer, base):
        tmp = Image.new("RGBA", (pw, ph), (0, 0, 0, 0))
        tmp.paste(layer, position, layer)
        return Image.alpha_composite(base, tmp)

    if halo is not None:
        view = over(halo, view)
    view = over(body_tint, view)
    view = over(flame_total, view)

    result = view.convert("RGB")
    d = ImageDraw.Draw(result)
    try:
        font = ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc", 16)
    except Exception:
        font = ImageFont.load_default()
    d.text((10, 8), label, fill=colour, font=font)
    return result


def main():
    img = Image.open(SRC).convert("RGB")
    mask = build_mask(img)

    rows = [
        ("现状（太淡）", CUR, 41),
        ("加强版", NEW, 77),
    ]
    panels = []
    for row_label, p, seed_base in rows:
        for i, (l, c) in enumerate(GRADES):
            panels.append(portrait_panel(img, mask, c, "%s · %s" % (row_label, l), p, seed_base + i))

    W = sum(p.width for p in panels[:3]) + 12 * 2
    row_h = max(p.height for p in panels[:3])
    H = row_h * 2 + 26
    canvas = Image.new("RGB", (W, H), BG)
    for idx, p in enumerate(panels):
        r, c2 = divmod(idx, 3)
        canvas.paste(p, (c2 * (p.width + 12), r * (row_h + 26)))
    canvas.save(OUT)
    print("saved:", OUT, canvas.size)


if __name__ == "__main__":
    main()
