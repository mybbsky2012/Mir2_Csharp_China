# -*- coding: utf-8 -*-
"""羽化装备（Shape 1000 段）效果图生成器。

每个职业一行（通用/战士/法师/道士/刺客/弓箭），列 = 档位（一~八）：
  · 外观：地图上的地图小人（Up*Armour\\0N.Lib 站立正面帧 + 同档头发），男女各一，2 倍放大
  · 内观：装备栏立绘（StateItem_<职业>_<性别>.Lib 固定帧 + 武器帧），每行男女各一份

素材根：E:\\micromir2\\Data\\HumUp（与客户端 Settings.HumUpPath 对应）
输出：Tools/输出/shape1000预览/羽化装备Shape1000段效果图.png
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib_util
from PIL import Image, ImageDraw, ImageFont

BASE = r"E:\micromir2\Data\HumUp"
OUT_DIR = r"D:\mir2-20241027\Tools\输出\shape1000预览"
OUT_FILE = os.path.join(OUT_DIR, "羽化装备Shape1000段效果图.png")

FONT = r"C:\Windows\Fonts\msyh.ttc"
FONT_B = r"C:\Windows\Fonts\msyhbd.ttc"

BG = (250, 250, 252)
GRID = (225, 228, 235)
TEXT = (60, 60, 70)
SUBTLE = (130, 130, 140)
ACCENT = (86, 110, 200)

# 职业 → 素材目录 / 内观库 / 内观固定帧（与 PlayerObject.GetHumUpStateFrames 一致）
ROWS = [
    dict(name="通用\n(战法道)", armour="UpCArmour", hair="UpCHair",
         state_m="StateItem_Common_Man", state_f="StateItem_Common_Woman",
         m_frame=2450, f_frame=2460, w_frame=2430, shapes=[1001]),
    dict(name="战士", armour="UpWarArmour", hair="UpWarHair",
         state_m="StateItem_Warrior_Man", state_f="StateItem_Warrior_Woman",
         m_frame=2450, f_frame=2460, w_frame=2430, shapes=[1011, 1012, 1013, 1014, 1015, 1016, 1017, 1018]),
    dict(name="法师", armour="UpWizArmour", hair="UpWizHair",
         state_m="StateItem_Wizard_Man", state_f="StateItem_Wizard_Woman",
         m_frame=2451, f_frame=2461, w_frame=2432, shapes=[1021, 1022, 1023, 1024, 1025, 1026, 1027, 1028]),
    dict(name="道士", armour="UpTaoArmour", hair="UpTaoHair",
         state_m="StateItem_Tao_Man", state_f="StateItem_Tao_Woman",
         m_frame=2452, f_frame=2462, w_frame=2434, shapes=[1031, 1032, 1033, 1034, 1035, 1036, 1037, 1038]),
    dict(name="刺客", armour="UpAssArmour", hair="UpAssHair",
         state_m="StateItem_Assassin_Man", state_f="StateItem_Assassin_Woman",
         m_frame=2453, f_frame=2463, w_frame=2436, shapes=[1041, 1042, 1043, 1044, 1045, 1046, 1047, 1048]),
    dict(name="弓箭", armour="UpArcArmour", hair="UpArcHair",
         state_m="StateItem_Archer_Man", state_f="StateItem_Archer_Woman",
         m_frame=2454, f_frame=2464, w_frame=3395, shapes=[1051, 1052, 1053, 1054, 1055, 1056, 1057, 1058]),
]

TIERS = ["一档", "二档", "三档", "四档", "五档", "六档", "七档", "八档"]
# 站立动作 dir4（面向镜头）第一帧：HumUpPlayer Frame(0,6,2)，每方向 8 槽 → 4*8=32
STAND_FRAME = 32
ZOOM = 2  # 地图小人放大倍数（最近邻）

CELL_W = 196      # 档位格宽
ROW_H = 350       # 行高（由内观立绘高度决定）
LABEL_W = 110     # 行标签宽
STATE_W = 210     # 内观立绘格宽
HEADER_H = 96
MARGIN = 24


def compose(layers, pad=6):
    """按各自 (fx,fy) 落点叠层，返回紧贴内容的画布。layers=[(img,x,y)]"""
    minx = min(x for _, x, _ in layers) - pad
    miny = min(y for _, _, y in layers) - pad
    maxx = max(x + img.width for img, x, _ in layers) + pad
    maxy = max(y + img.height for img, _, y in layers) + pad
    canvas = Image.new("RGBA", (maxx - minx, maxy - miny), (0, 0, 0, 0))
    for img, x, y in layers:
        canvas.alpha_composite(img, (x - minx, y - miny))
    return canvas


def armour_look(row, tier, male):
    """地图小人：盔甲 + 头发（同档）。返回合成图（ZOOM 倍放大）。"""
    suffix = "%02d.Lib" % tier
    apath = os.path.join(BASE, row["armour"], suffix)
    hpath = os.path.join(BASE, row["hair"], suffix)

    half = lib_util.count(apath) // 2
    idx = STAND_FRAME + (half if not male else 0)

    layers = []
    r = lib_util.frame(apath, idx)
    if r is None:
        return None
    layers.append(r)
    hr = lib_util.frame(hpath, idx)
    if hr is not None:
        layers.append(hr)

    img = compose(layers)
    return img.resize((img.width * ZOOM, img.height * ZOOM), Image.NEAREST)


def state_look(row, male):
    """内观立绘：盔甲立绘 + 武器立绘（各自 fx,fy 落点）。"""
    lib = os.path.join(BASE, (row["state_m"] if male else row["state_f"]) + ".Lib")
    frame = row["m_frame"] if male else row["f_frame"]

    layers = []
    a = lib_util.frame(lib, frame)
    if a is None:
        return None
    layers.append(a)
    w = lib_util.frame(lib, row["w_frame"])
    if w is not None:
        layers.append(w)
    return compose(layers, pad=10)


def fit(img, box_w, box_h):
    """居中放入给定盒子（返回 (img, x, y)，保持原尺寸不缩放）。"""
    x = (box_w - img.width) // 2
    y = (box_h - img.height) // 2
    return img, x, y


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    f_title = ImageFont.truetype(FONT_B, 30)
    f_head = ImageFont.truetype(FONT_B, 17)
    f_label = ImageFont.truetype(FONT_B, 20)
    f_sub = ImageFont.truetype(FONT, 13)
    f_note = ImageFont.truetype(FONT, 14)

    n_tier = 8
    table_w = LABEL_W + n_tier * CELL_W + 2 * STATE_W + 30
    width = MARGIN * 2 + table_w
    height = MARGIN + HEADER_H + len(ROWS) * ROW_H + 64

    sheet = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(sheet)

    # ── 标题 ──
    draw.text((MARGIN, MARGIN), "羽化装备 · Shape 1000 段效果图（外观 + 内观）", font=f_title, fill=TEXT)
    draw.text((MARGIN, MARGIN + 42),
              "外观 = 地图上的地图小人（Up*Armour\\0N.Lib 站立正面帧，男/女，2 倍放大）    "
              "内观 = 装备栏立绘（StateItem_<职业>_<性别>.Lib 固定帧 + 武器帧）",
              font=f_note, fill=SUBTLE)

    top = MARGIN + HEADER_H

    # ── 表头 ──
    x0 = MARGIN
    draw.text((x0 + 12, top + 14), "职业", font=f_head, fill=SUBTLE)
    for t, name in enumerate(TIERS):
        cx = x0 + LABEL_W + t * CELL_W
        draw.text((cx + CELL_W // 2 - 46, top + 8), name, font=f_head, fill=TEXT)
        # 该档位通用 shape 编号示例（以战士行为例写在格子下沿会太挤，只在表头给区间）
    draw.text((x0 + LABEL_W, top + 34),
              "Shape = 1000 + 职业索引×10 + 档位（1011~1018 战士 / 1021~1028 法师 / 1031~1038 道士 / 1041~1048 刺客 / 1051~1058 弓箭，1001 通用）",
              font=f_sub, fill=ACCENT)
    sx = x0 + LABEL_W + n_tier * CELL_W + 8
    draw.text((sx + 10, top + 8), "内观·男", font=f_head, fill=TEXT)
    draw.text((sx + STATE_W + 10, top + 8), "内观·女", font=f_head, fill=TEXT)
    draw.text((sx + 10, top + 34), "不分档位，帧号 = 装备自己的 Image", font=f_sub, fill=SUBTLE)

    line_y = top + HEADER_H - 14
    draw.line([(x0, line_y), (width - MARGIN, line_y)], fill=GRID, width=2)

    # ── 每职业一行 ──
    for ri, row in enumerate(ROWS):
        ry = top + ri * ROW_H

        # 行标签
        draw.text((x0 + 12, ry + ROW_H // 2 - 26), row["name"], font=f_label, fill=TEXT, spacing=6)
        shapes_txt = "\n".join(str(s) for s in row["shapes"][:4]) + ("\n..." if len(row["shapes"]) > 4 else "")
        draw.text((x0 + 12, ry + ROW_H - 74), shapes_txt, font=ImageFont.truetype(FONT, 12), fill=SUBTLE, spacing=2)

        # 档位格：外观小人
        for t in range(n_tier):
            cx = x0 + LABEL_W + t * CELL_W
            if ri % 2 == 1:
                draw.rectangle([cx, ry, cx + CELL_W - 1, ry + ROW_H - 1], fill=(245, 246, 250))
            shape = row["shapes"][t] if t < len(row["shapes"]) else None
            tier = t + 1
            m = armour_look(row, tier, True) if shape else None
            f = armour_look(row, tier, False) if shape else None
            cell_h = ROW_H - 20
            if m is not None:
                img, ix, iy = fit(m, CELL_W // 2 - 6, cell_h)
                sheet.paste(img, (cx + ix, ry + 10 + iy), img)
            if f is not None:
                img, ix, iy = fit(f, CELL_W // 2 - 6, cell_h)
                sheet.paste(img, (cx + CELL_W // 2 + ix, ry + 10 + iy), img)
            if shape:
                draw.text((cx + 6, ry + 4), str(shape), font=ImageFont.truetype(FONT, 12), fill=ACCENT)
            draw.line([(cx + CELL_W - 1, ry), (cx + CELL_W - 1, ry + ROW_H)], fill=GRID, width=1)

        # 内观立绘
        sx = x0 + LABEL_W + n_tier * CELL_W + 8
        for k, male in ((0, True), (1, False)):
            st = state_look(row, male)
            bx = sx + k * STATE_W
            if st is not None:
                img, ix, iy = fit(st, STATE_W - 20, ROW_H - 30)
                sheet.paste(img, (bx + ix, ry + iy), img)
            draw.line([(bx, ry), (bx, ry + ROW_H)], fill=GRID, width=1)

        draw.line([(x0, ry + ROW_H), (width - MARGIN, ry + ROW_H)], fill=GRID, width=1)

    # ── 底注 ──
    ny = top + len(ROWS) * ROW_H + 14
    draw.text((MARGIN, ny),
              "说明：外观档位由 Shape 个位决定（1~8，素材 Up*Armour\\00 素体 ~ 08）；内观不受档位影响，"
              "按装备自己的 Image 在 StateItem_<职业>_<性别>.Lib 里取帧，Image 为空帧时回落到本职业固定帧。",
              font=f_note, fill=SUBTLE)

    sheet.save(OUT_FILE)
    print("已生成:", OUT_FILE, sheet.size)


if __name__ == "__main__":
    main()
