# -*- coding: utf-8 -*-
"""
万效符特效亮度对比预览
按客户端真实混合公式合成：
  加法混合(SourceBlend=SourceAlpha, DestBlend=One)
  out = dst + tex.rgb * (tex.a * vtx.a)
对比 顶点色 alpha=255(旧) 与 alpha=100(新)
"""
import sys, struct, gzip, zlib, random
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image, ImageDraw

LIB = r'E:\micromir2\Data\Magic_32bit.Lib'
OUT = r'C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\万效符亮度对比.png'


def load_frames(path):
    raw = open(path, 'rb').read()
    count = struct.unpack_from('<i', raw, 4)[0]
    idx = [struct.unpack_from('<i', raw, 8 + 4 * i)[0] for i in range(count)]

    def frame(i):
        q = idx[i]
        w, h, x, y = struct.unpack_from('<4h', raw, q)
        sh = raw[q + 12]
        ln = struct.unpack_from('<i', raw, q + 13)[0]
        d = raw[q + 17:q + 17 + ln]
        q += 17 + ln
        if (sh >> 7) == 1:
            mln = struct.unpack_from('<i', raw, q + 8)[0]
            q += 12 + mln
        if w == 0 or h == 0:
            return None
        try:
            px = gzip.decompress(d)
        except Exception:
            px = zlib.decompress(d)
        im = Image.frombytes('RGBA', (w, h), px[:w * h * 4])
        b, g, r, a = im.split()
        return Image.merge('RGBA', (r, g, b, a)), (x, y)

    return frame


def make_background(w, h):
    """模拟游戏内偏亮的草地场景（地面亮 → 加法混合最容易过曝）"""
    img = Image.new('RGB', (w, h))
    px = img.load()
    rnd = random.Random(7)
    for y in range(h):
        for x in range(w):
            base = 74 + int(26 * (y / h))
            n = rnd.randint(-12, 12)
            px[x, y] = (max(0, base - 18 + n), max(0, base + 14 + n), max(0, base - 34 + n))
    d = ImageDraw.Draw(img)
    for i in range(0, w, 64):
        d.line([(i, 0), (i - 60, h)], fill=(96, 118, 70), width=2)
    return img


def add_char(img, cx, cy):
    """画一个约 50px 高的角色剪影，用于判断比例"""
    d = ImageDraw.Draw(img)
    d.ellipse([cx - 9, cy - 52, cx + 9, cy - 34], fill=(38, 40, 52))
    d.polygon([(cx - 12, cy - 34), (cx + 12, cy - 34), (cx + 15, cy - 2), (cx - 15, cy - 2)], fill=(46, 52, 78))
    d.line([(cx - 15, cy), (cx + 15, cy)], fill=(30, 32, 40), width=3)


def composite(bg, fg, cx, cy, vtx_a):
    """加法混合：out = dst + tex.rgb * tex.a * vtx_a"""
    fw, fh = fg.size
    ox, oy = cx - fw // 2, cy - fh // 2
    bgc = bg.copy()
    bp = bgc.load()
    fp = fg.load()
    k = vtx_a / 255.0
    bw, bh = bgc.size
    for y in range(fh):
        ty = oy + y
        if ty < 0 or ty >= bh:
            continue
        for x in range(fw):
            tx = ox + x
            if tx < 0 or tx >= bw:
                continue
            r, g, b, a = fp[x, y]
            if a == 0:
                continue
            s = (a / 255.0) * k
            dr, dg, db = bp[tx, ty]
            bp[tx, ty] = (min(255, int(dr + r * s)), min(255, int(dg + g * s)), min(255, int(db + b * s)))
    return bgc


def main():
    frame = load_frames(LIB)
    W, H = 620, 470
    bg0 = make_background(W, H)
    add_char(bg0, W // 2, H - 60)

    rows = []
    for label, fi, vtx in [('旧：顶点色 alpha=255（纯白，过曝）', 1712, 255),
                           ('新：顶点色 alpha=100（约 39%）', 1712, 100)]:
        fg, _ = frame(fi)
        rows.append((label, composite(bg0, fg, W // 2, H - 60 - 55, vtx)))

    pad = 30
    out = Image.new('RGB', (W, (H + pad) * 2), (20, 20, 24))
    d = ImageDraw.Draw(out)
    for i, (label, im) in enumerate(rows):
        y = i * (H + pad)
        out.paste(im, (0, y))
        d.text((8, y + H + 8), label, fill=(255, 240, 180))
    out.save(OUT)
    print('saved:', OUT)


main()
