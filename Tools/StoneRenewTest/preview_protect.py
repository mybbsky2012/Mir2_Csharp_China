# -*- coding: utf-8 -*-
"""内挂面板「保护页」布局预览：Prguse.Lib 索引 256（308x483）底图 + 按 AutoPlayDialog 坐标叠字，
目视确认新增的「自动续气血石」行（y=358）不压装饰、不与提示行（y=398）重叠。只读，不改游戏文件。"""
import gzip
import struct

from PIL import Image, ImageDraw, ImageFont

LIB = r"E:\micromir2\Data\Prguse.Lib"
FRAME = 256
OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tmp\autoplay-protect.png"
SCALE = 2

data = open(LIB, "rb").read()
ver, cnt, total = struct.unpack_from("<iii", data, 0)
offs = struct.unpack_from("<%dI" % cnt, data, 12)
o = offs[FRAME]
w, h, ox, oy, sx, sy, st, ln = struct.unpack_from("<hhhhhhBi", data, o)
raw = gzip.decompress(data[o + 17:o + 17 + ln])
img = Image.frombytes("RGBA", (w, h), raw, "raw", "BGRA")
img = img.transpose(Image.FLIP_TOP_BOTTOM).convert("RGBA")
base = Image.new("RGBA", img.size, (24, 18, 12, 255))
base.alpha_composite(img)
img = base.resize((w * SCALE, h * SCALE), Image.NEAREST)

dr = ImageDraw.Draw(img)
font = ImageFont.truetype(r"C:\Windows\Fonts\simsun.ttc", 11 * SCALE)
small = ImageFont.truetype(r"C:\Windows\Fonts\simsun.ttc", 9 * SCALE)

# 参考：内容区边界（代码注释：内容区 y=60~430）
dr.rectangle([0, 60 * SCALE, w * SCALE - 1, 430 * SCALE], outline=(0, 160, 0), width=1)
dr.text((2, 432 * SCALE), "内容区下界 430", font=small, fill=(0, 190, 0))


def box(x, y, text, ticked=False, colour=(240, 232, 210)):
    X, Y = x * SCALE, y * SCALE
    dr.rectangle([X, Y, X + 16 * SCALE - 1, Y + 12 * SCALE - 1], outline=(120, 120, 120))
    if ticked:
        dr.line([X + 2 * SCALE, Y + 6 * SCALE, X + 6 * SCALE, Y + 10 * SCALE], fill=(255, 255, 255), width=SCALE)
        dr.line([X + 6 * SCALE, Y + 10 * SCALE, X + 13 * SCALE, Y + 2 * SCALE], fill=(255, 255, 255), width=SCALE)
    dr.text((X + 15 * SCALE, Y - 2 * SCALE), text, font=font, fill=colour)


def num(x, y, wpx, text):
    X, Y = x * SCALE, y * SCALE
    dr.rectangle([X, Y, X + wpx * SCALE - 1, Y + 17 * SCALE - 1], outline=(146, 132, 100))
    dr.text((X + 3 * SCALE, Y + 1 * SCALE), text, font=font, fill=(235, 228, 205))


def section(y, text):
    tw = dr.textlength(text, font=font)
    dr.text(((w * SCALE - tw) / 2, y * SCALE), text, font=font, fill=(238, 214, 150))


def header(x, y, text):
    dr.text((x * SCALE, y * SCALE), text, font=small, fill=(188, 178, 158))


def hint(y, text):
    dr.text((2 * SCALE, y * SCALE), text, font=small, fill=(155, 145, 128))


# ── 保护页 ──
box(14, 64, "自动喝血药（自动打怪）")
box(14, 86, "自动喝蓝药（自动打怪）")
section(108, "药水明细（打怪看总闸）")
for x, t in [(14, "启用"), (84, "百分比"), (120, "使用物品(可输入)"), (228, "间隔(毫秒)")]:
    header(x, 124, t)

rows = [("血量低于", "50", "太阳水", "5000"), ("魔法低于", "30", "魔法药(中量)", "1000"),
        ("血量低于", "50", "强效金创药", "5000"), ("魔法低于", "50", "魔法药(小量)", "1000")]
for i, (lbl, pct, item, itv) in enumerate(rows):
    y = 142 + i * 24
    box(14, y, lbl, ticked=(i < 2))
    num(84, y - 3, 30, pct)
    num(120, y - 3, 96, item)
    num(228, y - 3, 52, itv)

section(240, "传送保护（勾选即生效）")
for x, t in [(14, "启用"), (84, "百分比"), (120, "使用物品(可输入)")]:
    header(x, 256, t)
box(14, 274, "血量低于")
num(84, 271, 30, "15")
num(120, 271, 96, "小退")

section(300, "装备保护")
for x, t in [(14, "启用"), (82, "持久低于"), (130, "使用物品(可输入)")]:
    header(x, 316, t)
box(14, 334, "全部装备")
num(84, 331, 26, "5")
dr.text((113 * SCALE, 333 * SCALE), "%", font=font, fill=(245, 238, 220))
num(126, 331, 100, "修复神水")

# ★ 新增行
box(14, 358, "自动续气血石(气血/魔血/幻血)", ticked=True, colour=(255, 230, 120))
dr.rectangle([14 * SCALE - 2, 358 * SCALE - 2, 250 * SCALE, 372 * SCALE], outline=(255, 60, 60), width=1)
dr.text((252 * SCALE, 358 * SCALE), "← 新增", font=small, fill=(255, 90, 90))

hint(388, "喝药两开关只在打怪时生效，其余勾了即生效。装备耐久低自动修，")
hint(403, "宝石位石头用完自动换（玩家/英雄都管）。")

dr.text((5 * SCALE, 5 * SCALE), "内挂设置", font=font, fill=(255, 220, 120))
for i, t in enumerate(["战斗", "保护", "其他"]):
    x = (10 + i * 96) * SCALE
    dr.text((x + 30 * SCALE, 40 * SCALE), t, font=font, fill=(255, 244, 210) if i == 1 else (172, 162, 144))

img.save(OUT)
print("saved", OUT, img.size)
