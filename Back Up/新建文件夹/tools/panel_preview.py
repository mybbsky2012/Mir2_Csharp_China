# -*- coding: utf-8 -*-
"""生成内挂面板新布局预览图（264x272，2 倍放大），用于核对勾选项位置。"""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\内挂面板新布局预览.png"

SCALE = 2
W, H = 264, 272

FONT_PATH = r"C:\Windows\Fonts\msyh.ttc"
font = ImageFont.truetype(FONT_PATH, 12 * SCALE)
font_small = ImageFont.truetype(FONT_PATH, 10 * SCALE)

img = Image.new("RGB", (W * SCALE, H * SCALE), (18, 18, 20))
d = ImageDraw.Draw(img)


def s(v):
    return v * SCALE


# 面板背景 + 金边
d.rectangle([s(2), s(2), s(W - 3), s(H - 3)], fill=(26, 24, 22), outline=(150, 118, 52), width=s(1))
# 标题栏分隔线（y=28）
d.line([s(6), s(28), s(W - 7), s(28)], fill=(120, 96, 48), width=1)
d.text((s(8), s(6)), "内挂设置", font=font, fill=(226, 200, 132))
# 关闭叉
d.text((s(238), s(5)), "X", font=font, fill=(196, 176, 120))

# (x, y, 文本, 是否勾选, 是否左列)
BOXES = [
    (28, 42, "内挂总开关", True, True),
    (28, 66, "自动攻击", True, True),
    (28, 90, "自动走位", True, True),
    (28, 114, "自动捡物", True, True),
    (28, 138, "自动喝血药", False, True),
    (28, 162, "自动喝蓝药", False, True),
    (28, 186, "自动技能", True, True),
    (28, 210, "毒符互换", True, True),
    (132, 42, "免蜡", False, False),
    (132, 66, "穿人/怪/NPC", False, False),
    (132, 90, "免助跑", False, False),
    (132, 114, "超负重", False, False),
    (132, 138, "泰山(不后仰)", False, False),
    (132, 162, "走位跑步", True, False),
    (132, 186, "自动躲避", False, False),
]

for (x, y, text, checked, left) in BOXES:
    # 勾选框 16x12
    d.rectangle([s(x), s(y), s(x + 16), s(y + 12)], outline=(140, 118, 60), fill=(12, 12, 12))
    if checked:
        d.line([s(x + 3), s(y + 6), s(x + 6), s(y + 9)], fill=(120, 230, 120), width=s(1) + 1)
        d.line([s(x + 6), s(y + 9), s(x + 13), s(y + 2)], fill=(120, 230, 120), width=s(1) + 1)
    color = (218, 216, 210) if left else (196, 176, 140)
    d.text((s(x + 20), s(y - 2)), text, font=font, fill=color)

# 底部提示
d.text((s(10), s(240)), "右列开关需服务器开放，未开放时置灰", font=font_small, fill=(150, 142, 120))

img.save(OUT)
print("saved:", OUT)
