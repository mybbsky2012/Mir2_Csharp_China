# -*- coding: utf-8 -*-
"""羽化装备 · 100 进制新编码（100 套方案）效果图。

新编码规则：段基 + 职业/类别 × 100 + 序号
  盔甲  1000 段：1000 + 职业*100 + 档位   → 通用 1001~1026 / 战 1101~1109 / 法 1201~ / 道 1301~ / 刺 1401~ / 弓 1501~
  武器  2000 段：2000 + 职业*100 + 序号   → 序号 = 素材文件在该目录的排序位（1 起）
  坐骑  3000 段：3000 + 序号              → UpMount 00~11
  鱼竿  4000 段：4000 + 序号              → UpFishing 00/01（客户端按玩家性别选 M/F 文件）

输出两张：
  羽化盔甲·新编码效果图.png        （6 行 × 档位，含新 Shape 号 + 内观立绘）
  羽化武器·坐骑·鱼竿·新编码效果图.png（武器 6 行 + 坐骑 1 行 + 鱼竿 1 行）
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib_util
from PIL import Image, ImageDraw, ImageFont

BASE = r"E:\micromir2\Data\HumUp"
OUT_DIR = r"D:\mir2-20241027\Tools\输出\羽化新编码预览"

FONT = r"C:\Windows\Fonts\msyh.ttc"
FONT_B = r"C:\Windows\Fonts\msyhbd.ttc"

BG = (250, 250, 252)
GRID = (225, 228, 235)
TEXT = (60, 60, 70)
SUBTLE = (130, 130, 140)
ACCENT = (86, 110, 200)
ACCENT2 = (34, 139, 87)

# 职业 → 盔甲/头发/武器素材目录 + 内观库
ROWS = [
    dict(name="通用\n(战法道)", idx=0, armour="UpCArmour", hair="UpCHair", weapon="UpCWeapon",
         state_m="StateItem_Common_Man", state_f="StateItem_Common_Woman",
         m_frame=2450, f_frame=2460, w_frame=2430),
    dict(name="战士", idx=1, armour="UpWarArmour", hair="UpWarHair", weapon="UpWarWeapon",
         state_m="StateItem_Warrior_Man", state_f="StateItem_Warrior_Woman",
         m_frame=2450, f_frame=2460, w_frame=2430),
    dict(name="法师", idx=2, armour="UpWizArmour", hair="UpWizHair", weapon="UpWizWeapon",
         state_m="StateItem_Wizard_Man", state_f="StateItem_Wizard_Woman",
         m_frame=2451, f_frame=2461, w_frame=2432),
    dict(name="道士", idx=3, armour="UpTaoArmour", hair="UpTaoHair", weapon="UpTaoWeapon",
         state_m="StateItem_Tao_Man", state_f="StateItem_Tao_Woman",
         m_frame=2452, f_frame=2462, w_frame=2434),
    dict(name="刺客", idx=4, armour="UpAssArmour", hair="UpAssHair", weapon="UpAssWeaponL",
         state_m="StateItem_Assassin_Man", state_f="StateItem_Assassin_Woman",
         m_frame=2453, f_frame=2463, w_frame=2436),
    dict(name="弓箭", idx=5, armour="UpArcArmour", hair="UpArcHair", weapon="UpArcWeapon",
         state_m="StateItem_Archer_Man", state_f="StateItem_Archer_Woman",
         m_frame=2454, f_frame=2464, w_frame=3395),
]

STAND_FRAME_MALE = 32       # 职业 8 帧/方向布局 dir4 第一帧
STAND_FRAME_COMMON = 16     # 通用库 4 帧/方向布局 dir4 第一帧


def lib_files(directory):
    """目录里按名称排序的 .Lib 文件（去掉扩展名）。"""
    p = os.path.join(BASE, directory)
    return [f[:-4] for f in sorted(os.listdir(p))
            if f.lower().endswith(".lib") and ".bak" not in f.lower()]


def frame_at(path, idx):
    """取帧；索引越界/空帧返回 None。"""
    if idx is None or idx < 0:
        return None
    return lib_util.frame(path, idx)


def stand_frame(path):
    """该库的正面站立帧号：先按 8 帧/方向取 32，空则按通用 4 帧/方向取 16。"""
    r = frame_at(path, 32)
    if r is not None and r[0].getbbox() is not None:
        return 32
    return 16


def compose(layers, pad=6):
    minx = min(x for _, x, _ in layers) - pad
    miny = min(y for _, _, y in layers) - pad
    maxx = max(x + im.width for im, x, _ in layers) + pad
    maxy = max(y + im.height for im, _, y in layers) + pad
    canvas = Image.new("RGBA", (maxx - minx, maxy - miny), (0, 0, 0, 0))
    for img, x, y in layers:
        canvas.alpha_composite(img, (x - minx, y - miny))
    return canvas


def armour_look(row, tier, male):
    """地图小人：盔甲 + 头发（同档）。"""
    suffix = "%02d.Lib" % tier
    apath = os.path.join(BASE, row["armour"], suffix)
    if not os.path.exists(apath):
        return None
    hpath = os.path.join(BASE, row["hair"], suffix)
    if not os.path.exists(hpath):
        hpath = None

    total = lib_util.count(apath)
    base_f = stand_frame(apath)
    idx = base_f + ((total // 2) if not male else 0)

    layers = []
    r = frame_at(apath, idx)
    if r is None:
        return None
    layers.append(r)
    if hpath:
        hr = frame_at(hpath, idx)
        if hr is not None:
            layers.append(hr)
    img = compose(layers)
    return img.resize((img.width * 2, img.height * 2), Image.NEAREST)


def state_look(row, male):
    """内观立绘：盔甲立绘 + 武器立绘（内观不受编码影响，固定帧）。"""
    lib = os.path.join(BASE, (row["state_m"] if male else row["state_f"]) + ".Lib")
    frame = row["m_frame"] if male else row["f_frame"]
    layers = []
    a = frame_at(lib, frame)
    if a is None:
        return None
    layers.append(a)
    w = frame_at(lib, row["w_frame"])
    if w is not None:
        layers.append(w)
    return compose(layers, pad=10)


def weapon_look(row, seq, male, zoom=2):
    """羽化小人（本职业盔甲+头发 01）手持该武器的完整外观（与客户端同帧合成）。"""
    files = row["_weapon_files"]
    fname = files[seq - 1]

    apath = os.path.join(BASE, row["armour"], "01.Lib")
    hpath = os.path.join(BASE, row["hair"], "01.Lib")

    body_frame = stand_frame(apath)
    total = lib_util.count(apath)
    idx = body_frame + ((total // 2) if not male else 0)

    layers = []
    r = frame_at(apath, idx)
    if r is None:
        return None
    layers.append(r)
    hr = frame_at(hpath, idx)
    if hr is not None:
        layers.append(hr)

    for d in (row["weapon"], row.get("weapon2")):
        if not d:
            continue
        wpath = os.path.join(BASE, d, fname + ".Lib")
        if not os.path.exists(wpath):
            continue
        widx = 32
        wr = frame_at(wpath, widx)
        if wr is None or wr[0].getbbox() is None:
            wr = frame_at(wpath, 16)
            widx = 16 if wr is not None else widx
        if wr is not None:
            layers.append(wr)
    if len(layers) < 2:
        return None
    img = compose(layers, pad=10)
    return img.resize((img.width * zoom, img.height * zoom), Image.NEAREST)


def mount_look(seq, zoom=2):
    fname = "%02d" % (seq - 1)
    path = os.path.join(BASE, "UpMount", fname + ".Lib")
    if not os.path.exists(path):
        return None
    r = frame_at(path, 32)
    if r is None:
        return None
    img = compose([r], pad=8)
    return img.resize((img.width * zoom, img.height * zoom), Image.NEAREST)


def fishing_look(seq, male, zoom=3):
    fname = "%02d%s" % (seq - 1, "M" if male else "F")
    path = os.path.join(BASE, "UpFishing", fname + ".Lib")
    if not os.path.exists(path):
        return None
    r = frame_at(path, 32)
    if r is None:
        r = frame_at(path, 16)
    if r is None:
        return None
    img = compose([r], pad=8)
    return img.resize((img.width * zoom, img.height * zoom), Image.NEAREST)


def fit(img, box_w, box_h):
    x = (box_w - img.width) // 2
    y = (box_h - img.height) // 2
    return img, x, y


def f(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)


# ════════════════════════════ 图 1：盔甲 ════════════════════════════

def sheet_armour():
    for row in ROWS:
        row["_armour_files"] = lib_files(row["armour"])
    max_tiers = max(len(lib_files(r["armour"])) - 1 for r in ROWS)   # 00=素体，不算档

    CELL_W, ROW_H, LABEL_W, STATE_W = 150, 320, 96, 200
    HEADER_H, MARGIN = 110, 24
    table_w = LABEL_W + max_tiers * CELL_W + 2 * STATE_W + 30
    width = MARGIN * 2 + table_w
    height = MARGIN + HEADER_H + len(ROWS) * ROW_H + 74

    sheet = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(sheet)

    draw.text((MARGIN, MARGIN), "羽化盔甲 · 新编码效果图（100 进制 · 100 套方案）", font=f(30, True), fill=TEXT)
    draw.text((MARGIN, MARGIN + 42),
              "新编码：Shape = 1000 + 职业×100 + 档位   （通用 1001~、战士 1101~、法师 1201~、道士 1301~、刺客 1401~、弓箭 1501~）"
              "    素体 = 00，档位从 01 起",
              font=f(14), fill=ACCENT)

    top = MARGIN + HEADER_H
    draw.text((MARGIN + 12, top + 12), "职业", font=f(17, True), fill=SUBTLE)
    draw.text((MARGIN + LABEL_W, top + 34), "← 档位 1~%d（外观各不相同）｜内观不随编码变化，仍按装备自己的 Image 取帧 →" % max_tiers,
              font=f(13), fill=SUBTLE)
    for t in range(max_tiers):
        cx = MARGIN + LABEL_W + t * CELL_W
        draw.text((cx + 6, top + 8), "%d档" % (t + 1), font=f(15, True), fill=TEXT)
    sx = MARGIN + LABEL_W + max_tiers * CELL_W + 8
    draw.text((sx + 10, top + 12), "内观·男", font=f(17, True), fill=TEXT)
    draw.text((sx + STATE_W + 10, top + 12), "内观·女", font=f(17, True), fill=TEXT)
    draw.line([(MARGIN, top + HEADER_H - 16), (width - MARGIN, top + HEADER_H - 16)], fill=GRID, width=2)

    for ri, row in enumerate(ROWS):
        ry = top + ri * ROW_H
        n_tier = len(row["_armour_files"]) - 1
        draw.text((MARGIN + 12, ry + ROW_H // 2 - 30), row["name"], font=f(20, True), fill=TEXT, spacing=6)
        lo, hi = 1000 + row["idx"] * 100 + 1, 1000 + row["idx"] * 100 + n_tier
        draw.text((MARGIN + 12, ry + ROW_H // 2 + 16), "%d~%d" % (lo, hi), font=f(12), fill=ACCENT)

        for t in range(max_tiers):
            cx = MARGIN + LABEL_W + t * CELL_W
            if ri % 2 == 1:
                draw.rectangle([cx, ry, cx + CELL_W - 1, ry + ROW_H - 1], fill=(245, 246, 250))
            tier = t + 1
            shape = 1000 + row["idx"] * 100 + tier
            if tier <= n_tier:
                m = armour_look(row, tier, True)
                fl = armour_look(row, tier, False)
                cell_h = ROW_H - 40
                if m:
                    img, ix, iy = fit(m, CELL_W // 2 - 6, cell_h)
                    sheet.paste(img, (cx + ix, ry + 22 + iy), img)
                if fl:
                    img, ix, iy = fit(fl, CELL_W // 2 - 6, cell_h)
                    sheet.paste(img, (cx + CELL_W // 2 + ix, ry + 22 + iy), img)
                draw.text((cx + 6, ry + 4), str(shape), font=f(12), fill=ACCENT)
            draw.line([(cx + CELL_W - 1, ry), (cx + CELL_W - 1, ry + ROW_H)], fill=GRID, width=1)

        for k, male in ((0, True), (1, False)):
            st = state_look(row, male)
            bx = sx + k * STATE_W
            if st is not None:
                img, ix, iy = fit(st, STATE_W - 20, ROW_H - 30)
                sheet.paste(img, (bx + ix, ry + iy), img)
            draw.line([(bx, ry), (bx, ry + ROW_H)], fill=GRID, width=1)
        draw.line([(MARGIN, ry + ROW_H), (width - MARGIN, ry + ROW_H)], fill=GRID, width=1)

    ny = top + len(ROWS) * ROW_H + 16
    draw.text((MARGIN, ny), "注：通用库素材 00~25 共 25 档、职业库 00~09 共 9 档 —— 素材档位远多于现行放行的 8 档，", font=f(14), fill=SUBTLE)
    draw.text((MARGIN, ny + 22), "     改编码后 tier 上限可同步放宽（客户端 tier = Shape 个两位数）。档位格空白 = 素材缺该档。", font=f(14), fill=SUBTLE)

    out = os.path.join(OUT_DIR, "羽化盔甲·新编码效果图.png")
    sheet.save(out)
    print("已生成:", out, sheet.size)


# ════════════════════════════ 图 2：武器 + 坐骑 + 鱼竿 ════════════════════════════

def sheet_weapon_mount_fishing():
    for row in ROWS:
        row["_weapon_files"] = lib_files(row["weapon"])
        if row["idx"] == 4:  # 刺客双持
            row["weapon2"] = "UpAssWeaponR"

    LABEL_W, CELL_W, ROW_H = 96, 130, 210
    HEADER_H, MARGIN = 96, 24
    max_wep = max(len(r["_weapon_files"]) for r in ROWS)
    MOUNT_CELL, MOUNT_ROW = 300, 300
    FISH_CELL, FISH_ROW = 230, 260

    w1 = LABEL_W + max_wep * CELL_W + 30
    w2 = LABEL_W + 12 * MOUNT_CELL + 30
    w3 = LABEL_W + 2 * FISH_CELL + 30
    width = MARGIN * 2 + max(w1, w2, w3)
    height = (MARGIN + HEADER_H + 6 * ROW_H            # 武器
              + 40 + MOUNT_ROW                          # 坐骑
              + 40 + FISH_ROW                           # 鱼竿
              + 74)

    sheet = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(sheet)

    y = MARGIN
    draw.text((MARGIN, y), "羽化武器 / 坐骑 / 鱼竿 · 新编码效果图", font=f(30, True), fill=TEXT)
    draw.text((MARGIN, y + 42),
              "武器 Shape = 2000 + 职业×100 + 序号（2001~ 通用 / 2101~ 战 / 2201~ 法 / 2301~ 道 / 2401~ 刺 / 2501~ 弓）"
              "    坐骑 = 3000 + 序号    鱼竿 = 4000 + 序号（按玩家性别自动选 0NM / 0NF）",
              font=f(14), fill=ACCENT)
    y += HEADER_H

    # ── 武器区 ──
    draw.text((MARGIN + 8, y + 4), "武器 2000 段", font=f(20, True), fill=ACCENT)
    y += 36
    draw.text((MARGIN + LABEL_W, y - 2), "羽化小人（本职业 01 甲）+ 手持该武器（与客户端同帧合成），2 倍放大；刺客为左右手双持", font=f(13), fill=SUBTLE)
    y += 26
    for ri, row in enumerate(ROWS):
        ry = y + ri * ROW_H
        n = len(row["_weapon_files"])
        draw.text((MARGIN + 12, ry + ROW_H // 2 - 24), row["name"], font=f(17, True), fill=TEXT, spacing=4)
        draw.text((MARGIN + 12, ry + ROW_H // 2 + 4), "2001~%d" % (2000 + row["idx"] * 100 + n), font=f(11), fill=ACCENT)
        for t in range(max_wep):
            cx = MARGIN + LABEL_W + t * CELL_W
            if ri % 2 == 1:
                draw.rectangle([cx, ry, cx + CELL_W - 1, ry + ROW_H - 1], fill=(245, 246, 250))
            if t < n:
                img = weapon_look(row, t + 1, True)
                if img:
                    im, ix, iy = fit(img, CELL_W - 10, ROW_H - 34)
                    sheet.paste(im, (cx + 5 + ix, ry + 6 + iy), im)
                draw.text((cx + 6, ry + 4), str(2000 + row["idx"] * 100 + t + 1), font=f(11), fill=ACCENT)
                draw.text((cx + 6, ry + ROW_H - 20), row["_weapon_files"][t], font=f(10), fill=SUBTLE)
            draw.line([(cx + CELL_W - 1, ry), (cx + CELL_W - 1, ry + ROW_H)], fill=GRID, width=1)
        draw.line([(MARGIN, ry + ROW_H), (width - MARGIN, ry + ROW_H)], fill=GRID, width=1)
    y += 6 * ROW_H + 40

    # ── 坐骑区 ──
    draw.text((MARGIN + 8, y + 4), "坐骑 3000 段", font=f(20, True), fill=ACCENT2)
    draw.text((MARGIN + LABEL_W, y + 8), "Shape = 3000 + 序号 → UpMount\\0N.Lib，正面骑乘帧，2 倍放大", font=f(13), fill=SUBTLE)
    y += 40
    mounts = lib_files("UpMount")
    for t, fname in enumerate(mounts):
        cx = MARGIN + LABEL_W + t * MOUNT_CELL
        if t % 2 == 1:
            draw.rectangle([cx, y, cx + MOUNT_CELL - 1, y + MOUNT_ROW - 1], fill=(245, 246, 250))
        img = mount_look(t + 1)
        if img:
            im, ix, iy = fit(img, MOUNT_CELL - 14, MOUNT_ROW - 40)
            sheet.paste(im, (cx + 7 + ix, y + 6 + iy), im)
        draw.text((cx + 8, y + 6), str(3000 + t + 1), font=f(13, True), fill=ACCENT2)
        draw.text((cx + 8, y + MOUNT_ROW - 24), fname + ".Lib", font=f(11), fill=SUBTLE)
        draw.line([(cx + MOUNT_CELL - 1, y), (cx + MOUNT_CELL - 1, y + MOUNT_ROW)], fill=GRID, width=1)
    y += MOUNT_ROW + 40

    # ── 鱼竿区 ──
    draw.text((MARGIN + 8, y + 4), "鱼竿 4000 段", font=f(20, True), fill=ACCENT2)
    draw.text((MARGIN + LABEL_W, y + 8), "Shape = 4000 + 序号 → UpFishing\\0N{M|F}.Lib，按持竿玩家性别选文件", font=f(13), fill=SUBTLE)
    y += 40
    fishes = [x for x in lib_files("UpFishing")]
    labels = []
    for seq in (1, 2):
        for male in (True, False):
            labels.append((seq, male))
    for t, (seq, male) in enumerate(labels):
        cx = MARGIN + LABEL_W + t * FISH_CELL
        if t % 2 == 1:
            draw.rectangle([cx, y, cx + FISH_CELL - 1, y + FISH_ROW - 1], fill=(245, 246, 250))
        img = fishing_look(seq, male)
        if img:
            im, ix, iy = fit(img, FISH_CELL - 14, FISH_ROW - 44)
            sheet.paste(im, (cx + 7 + ix, y + 6 + iy), im)
        draw.text((cx + 8, y + 6), str(4000 + seq), font=f(13, True), fill=ACCENT2)
        draw.text((cx + 8, y + FISH_ROW - 26),
                  "%s (%s)" % (fishes[seq - 1] + ("M" if male else "F"), "男" if male else "女"),
                  font=f(11), fill=SUBTLE)
        draw.line([(cx + FISH_CELL - 1, y), (cx + FISH_CELL - 1, y + FISH_ROW)], fill=GRID, width=1)

    ny = height - 52
    draw.text((MARGIN, ny), "注：武器库「序号」= 素材文件在目录中的排序位（格内灰字为对应文件名）；坐骑/鱼竿暂未接入客户端加载，", font=f(14), fill=SUBTLE)
    draw.text((MARGIN, ny + 22), "     本图按素材实际内容排出编码表。武器正面帧取 dir4 第一帧（通用库空帧时回落 dir4 前 4 帧/方向布局的第 16 帧）。", font=f(14), fill=SUBTLE)

    out = os.path.join(OUT_DIR, "羽化武器·坐骑·鱼竿·新编码效果图.png")
    sheet.save(out)
    print("已生成:", out, sheet.size)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    sheet_armour()
    sheet_weapon_mount_fishing()
