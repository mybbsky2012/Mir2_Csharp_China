# -*- coding: utf-8 -*-
"""导出 Prguse.Lib 第257张（索引256，308x483）作为内挂面板底图，按 AutoPlayDialog 的新坐标叠字，
目视确认：标题栏/关闭叉位置、19 项两列不重叠、标签不出黑框、提示行不压底部装饰。只读。"""
import gzip
import struct

from PIL import Image, ImageDraw, ImageFont

LIB = r"E:\micromir2\Data\Prguse.Lib"
FRAME = 256
OUT = r"D:\mir2-20241027\Back Up\内挂面板预览_Prguse256.png"
SCALE = 2

data = open(LIB, "rb").read()
ver, cnt, total = struct.unpack_from("<iii", data, 0)
offs = struct.unpack_from("<%dI" % cnt, data, 12)
o = offs[FRAME]
w, h, ox, oy, sx, sy, st, ln = struct.unpack_from("<hhhhhhBi", data, o)
raw = gzip.decompress(data[o + 17:o + 17 + ln])
img = Image.frombytes("RGBA", (w, h), raw, "raw", "BGRA")
img = img.transpose(Image.FLIP_TOP_BOTTOM)
img = img.resize((w * SCALE, h * SCALE), Image.NEAREST)

dr = ImageDraw.Draw(img)
font = ImageFont.truetype(r"C:\Windows\Fonts\simsun.ttc", 11 * SCALE)

left = [("内挂总开关", 48), ("自动攻击", 84), ("自动走位", 120), ("自动捡物", 156),
        ("魔法锁定", 192), ("自动喝血药", 228), ("自动喝蓝药", 264), ("自动技能", 300),
        ("毒符互换", 336), ("物品过滤", 372)]
right = [("走位跑步", 48), ("隔位刺杀", 84), ("自动躲避", 120), ("免蜡", 156),
         ("穿人/怪/NPC", 192), ("免助跑", 228), ("超负重", 264), ("泰山(不后仰)", 300),
         ("数字显血", 336)]


def draw_box(x, y, text, ticked=False):
    X, Y = x * SCALE, y * SCALE
    dr.rectangle([X, Y, X + 16 * SCALE - 1, Y + 12 * SCALE - 1], outline=(140, 140, 140))
    if ticked:
        dr.line([X + 2 * SCALE, Y + 6 * SCALE, X + 6 * SCALE, Y + 10 * SCALE], fill=(255, 255, 255), width=SCALE)
        dr.line([X + 6 * SCALE, Y + 10 * SCALE, X + 13 * SCALE, Y + 2 * SCALE], fill=(255, 255, 255), width=SCALE)
    dr.text((X + 15 * SCALE, Y - 2 * SCALE), text, font=font, fill=(255, 210, 60))


for text, y in left:
    draw_box(28, y, text, ticked=(y == 192))
for text, y in right:
    draw_box(160, y, text)

dr.text((8 * SCALE, 5 * SCALE), "内挂设置", font=font, fill=(255, 220, 120))
# 关闭按钮占位（Prguse2 360，约 29x22）
dr.rectangle([279 * SCALE, 4 * SCALE, (279 + 29) * SCALE, (4 + 22) * SCALE], outline=(255, 80, 80))
dr.text((28 * SCALE, 400 * SCALE), "右列开关需服务器开放，其余为本地设置", font=font, fill=(200, 200, 200))

img.save(OUT)
print("saved", OUT, img.size)
