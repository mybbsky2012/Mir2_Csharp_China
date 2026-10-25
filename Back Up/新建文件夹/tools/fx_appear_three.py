# -*- coding: utf-8 -*-
"""把三个召唤物的『召唤初现』出场帧合成一张对照图，供进游戏后比对。"""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\sheets"
ROWS = [
    ("召唤出场_神兽170.png", "神兽  170.Lib  动作27 = 帧0-9 / 200ms/帧  ->  红色碎片汇聚成型"),
    ("召唤出场_骷髅036.png", "骷髅  036.Lib  动作27 = 帧0-3 / 450ms/帧  ->  自地面浮现起身"),
    ("召唤出场_月灵172.png", "月灵  172.Lib  动作27 = 帧458-467 / 200ms/帧  ->  同族汇聚"),
]

try:
    f1 = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 20)
except Exception:
    f1 = ImageFont.load_default()

ims = []
for fn, lab in ROWS:
    p = os.path.join(OUT, fn)
    if os.path.exists(p):
        ims.append((Image.open(p).convert("RGB"), lab))
    else:
        print("缺失:", p)

if not ims:
    raise SystemExit("没有可用的帧图")

HDR = 36
W = max(i.width for i, _ in ims) + 20
H = sum(i.height + HDR + 14 for i, _ in ims) + 14
canvas = Image.new("RGB", (W, H), (22, 24, 30))
d = ImageDraw.Draw(canvas)
y = 14
for im, lab in ims:
    d.text((14, y + 6), lab, fill=(255, 205, 110), font=f1)
    y += HDR
    canvas.paste(im, (10, y))
    y += im.height + 14

out = os.path.join(OUT, "召唤出场效果对照_盛大原版.png")
canvas.save(out)
print("已生成:", out, canvas.size)
