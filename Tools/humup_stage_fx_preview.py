# -*- coding: utf-8 -*-
"""羽化登仙 重数特效预览 GIF 生成器（只读 Effect.lib / UpCArmour，不改任何游戏文件）。

复刻游戏内绘制规则（Client/MirObjects/Effect.cs + MLibrary.Draw）：
  特效帧画在 Owner.DrawLocation（人物锚点）+ 帧自身 fx/fy 偏移处。
  脚下特效 drawBehind=true → 先画（人物底层）；
  龙     drawBehind=false → 后画（人物上层，游戏内为 Blend 加法混合，GIF 用普通 alpha 模拟）。

输出：Tools/输出/羽化重数特效预览/*.gif
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib_util as lu
from PIL import Image, ImageDraw, ImageChops

EFFECT = r"E:\micromir2\Data\Effect.lib"
BODY_LIB = r"E:\micromir2\Data\HumUp\UpCArmour\00.Lib"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "输出", "羽化重数特效预览")

BODY_FRAME = 0          # 朝南站姿
FRAME_MS = 150          # GIF 每帧时长（游戏内光环 160ms/帧、龙 130ms/帧，取中间值）

# (名称, 脚下段(start, count), 龙段(start, count))——11-13 定稿：4-5 重与 1-3 重同款，960 弃用
GROUPS = [
    ("1-5重", (680, 20), (714, 36)),
    ("6-8重", (1210, 20), (1240, 31)),
    ("9-10重(现状)", (990, 20), (1020, 32)),
]


def paste(img, src, additive=True):
    """按游戏规则贴帧：锚点 + 帧自身 fx/fy。
    additive=True 模拟 DrawBlend（Blend=true，result=dst+src）——黑色部分不显形，
    这样预览才不会出现「素材黑底变实体黑块」的假象。"""
    im, fx, fy = src
    if not additive:
        img.alpha_composite(im, (fx, fy))
        return
    # 先按 alpha 预乘（alpha=0 的像素不参与加法），再用 ImageChops.add 截断到 255
    r, g, b, a = im.split()
    src_rgb = Image.merge("RGB", (r, g, b))
    mask = a
    src_rgb = Image.composite(src_rgb, Image.new("RGB", im.size, (0, 0, 0)), mask)
    patch = Image.new("RGB", im.size, (0, 0, 0))
    patch.paste(src_rgb, (0, 0))
    box = (fx, fy, fx + im.size[0], fy + im.size[1])
    region = img.crop(box).convert("RGB")
    merged = ImageChops.add(region, patch)
    img.paste(merged.convert("RGBA"), (fx, fy))


def make_gif(path, segments, scale=1):
    """segments: [(list_of_frames, draw_before_body)]，frame = (RGBA, fx, fy)
    画布按 segments+素体的并集包围盒自动计算，锚点随之内移（1:1，不裁切）。"""
    body = lu.frame(BODY_LIB, BODY_FRAME)
    boxes = [bbox(f) for f, _ in segments]
    if body:
        boxes.append(bbox([body]))
    minx = min(b[0] for b in boxes); miny = min(b[1] for b in boxes)
    maxx = max(b[2] for b in boxes); maxy = max(b[3] for b in boxes)
    margin = 30
    canvas = (maxx - minx + margin * 2, maxy - miny + margin * 2)
    anchor = (margin - minx, margin - miny)

    max_len = max(len(f) for f, _ in segments)
    before = [f for f, pre in segments if pre]
    after = [f for f, pre in segments if not pre]

    frames = []
    for k in range(max_len):
        img = Image.new("RGBA", canvas, (20, 20, 26, 255))   # 深色底：光效看得清
        d = ImageDraw.Draw(img)
        # 参考线：锚点（人物脚点）
        d.line([(anchor[0] - 8, anchor[1]), (anchor[0] + 8, anchor[1])], fill=(255, 255, 0, 180), width=1)
        d.line([(anchor[0], anchor[1] - 8), (anchor[0], anchor[1] + 2)], fill=(255, 255, 0, 180), width=1)
        ox, oy = anchor
        for seg in before:
            f = seg[k % len(seg)]
            paste(img, (f[0], f[1] + ox, f[2] + oy))
        if body:
            paste(img, (body[0], body[1] + ox, body[2] + oy), additive=False)
        for seg in after:
            f = seg[k % len(seg)]
            paste(img, (f[0], f[1] + ox, f[2] + oy))
        if scale != 1:
            img = img.resize((canvas[0] * scale, canvas[1] * scale), Image.NEAREST)
        frames.append(img.convert("P", palette=Image.ADAPTIVE))

    frames[0].save(path, save_all=True, append_images=frames[1:],
                   duration=FRAME_MS, loop=0, disposal=2)
    print("写出", path, canvas)


def group_size(fx_start, count):
    """计算一段特效的包围盒（fx/fy 相对锚点）。"""
    minx = miny = 10 ** 9
    maxx = maxy = -10 ** 9
    for i in range(fx_start, fx_start + count):
        f = lu.frame(EFFECT, i)
        if f is None:
            continue
        im, fx, fy = f
        minx = min(minx, fx); miny = min(miny, fy)
        maxx = max(maxx, fx + im.size[0]); maxy = max(maxy, fy + im.size[1])
    return minx, miny, maxx, maxy


def bbox(frames):
    minx = miny = 10 ** 9
    maxx = maxy = -10 ** 9
    for im, fx, fy in frames:
        minx = min(minx, fx); miny = min(miny, fy)
        maxx = max(maxx, fx + im.size[0]); maxy = max(maxy, fy + im.size[1])
    return minx, miny, maxx, maxy


def main():
    os.makedirs(OUT, exist_ok=True)
    margin = 30
    body = lu.frame(BODY_LIB, BODY_FRAME)

    for name, foot, dragon in GROUPS:
        foot_frames = [f for f in (lu.frame(EFFECT, i) for i in range(foot[0], foot[0] + foot[1])) if f]
        dragon_frames = [f for f in (lu.frame(EFFECT, i) for i in range(dragon[0], dragon[0] + dragon[1])) if f]

        make_gif(os.path.join(OUT, f"{name}_合成.gif"),
                 [(foot_frames, True), (dragon_frames, False)])
        make_gif(os.path.join(OUT, f"{name}_脚下{foot[0]}.gif"),
                 [(foot_frames, True)])
        make_gif(os.path.join(OUT, f"{name}_龙{dragon[0]}.gif"),
                 [(dragon_frames, False)])

    print("完成 →", OUT)


if __name__ == "__main__":
    main()
