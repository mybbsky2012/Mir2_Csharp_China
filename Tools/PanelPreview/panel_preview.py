# -*- coding: utf-8 -*-
"""导出 Title.Lib 第200张（索引199，264x272）内挂面板底图，并按 AutoPlayDialog 的坐标叠字，
用于目视确认「物品过滤 / 数字显血」这一行不压装饰、两列标签不重叠。只读，不改游戏文件。"""
import gzip
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

LIB = r"E:\micromir2\Data\Title.Lib"
FRAME = 199
OUT = r"D:\mir2-20241027\Back Up\manual-20261019_2355-数字显血\内挂面板预览.png"
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
font_path = r"C:\Windows\Fonts\simsun.ttc"
font = ImageFont.truetype(font_path, 11 * SCALE)

rows_left = ["内挂总开关", "自动攻击", "自动走位", "自动捡物", "自动喝血药",
             "自动喝蓝药", "自动技能", "毒符互换", "物品过滤"]
rows_right = ["免蜡", "穿人/怪/NPC", "免助跑", "超负重", "泰山(不后仰)",
              "走位跑步", "自动躲避", "隔位刺杀", "数字显血"]


def draw_box(x, y, text, ticked=False, colour=(255, 210, 60)):
    X, Y = x * SCALE, y * SCALE
    dr.rectangle([X, Y, X + 16 * SCALE - 1, Y + 12 * SCALE - 1], outline=(120, 120, 120))
    if ticked:
        dr.line([X + 2 * SCALE, Y + 6 * SCALE, X + 6 * SCALE, Y + 10 * SCALE], fill=(255, 255, 255), width=SCALE)
        dr.line([X + 6 * SCALE, Y + 10 * SCALE, X + 13 * SCALE, Y + 2 * SCALE], fill=(255, 255, 255), width=SCALE)
    dr.text((X + 15 * SCALE, Y - 2 * SCALE), text, font=font, fill=colour)


ys = [42, 66, 90, 114, 138, 162, 186, 210, 225]
for i, y in enumerate(ys):
    draw_box(28, y, rows_left[i], ticked=(i == 8))
for i, y in enumerate(ys):
    draw_box(132, y, rows_right[i], ticked=(i == 8))

# 提示行（y=252）
dr.text((0, 252 * SCALE), "右列开关需服务器开放，最下一行为本地设置".center(24), font=font, fill=(200, 200, 200))
dr.text((0, 5 * SCALE), "内挂设置", font=font, fill=(255, 220, 120))

img.save(OUT)
print("saved", OUT, img.size)
