# -*- coding: utf-8 -*-
"""
韩Mir2 套装数据整理：按 职业 -> 等级 聚合正式套装，输出 Word + Excel
数据源: Exports/2_物品数据.csv (117版导出)
套装构成 = ItemSet 字段; 套装加成 = ItemToolTip 中的「套装属性」
"""
import csv, io, os, re, collections
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

SRC = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv"
OUT_DIR = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59"
DOCX_OUT = os.path.join(OUT_DIR, "韩Mir2套装数据总表-1到70级-按职业.docx")
XLSX_OUT = os.path.join(OUT_DIR, "韩Mir2套装数据总表-1到70级-按职业.xlsx")

CLASS_GROUPS = [
    ("战士", ["战士", "战法道", "战士刺客", "全职业"]),
    ("道士", ["道士", "战法道", "全职业"]),
    ("法师", ["法师", "战法道", "全职业"]),
    ("刺客", ["刺客", "战士刺客", "全职业"]),
    ("弓手", ["弓箭", "全职业"]),
]
GROUP_LABEL = {"战士": "战士专属", "法师": "法师专属", "道士": "道士专属",
               "刺客": "刺客专属", "弓箭": "弓手专属", "战法道": "战法道通用",
               "战士刺客": "战士刺客通用", "全职业": "全职业通用"}
DATA_CLASS = {"战士": "战士", "法师": "法师", "道士": "道士", "刺客": "刺客", "弓手": "弓箭"}

EQUIP_TYPES = ["武器", "盔甲", "头盔", "项链", "戒指", "手镯", "腰带", "靴子",
               "护身符", "守护石", "面甲", "坐骑", "铃铛", "蝴蝶结"]
SLOT_ORDER = ["武器", "盔甲", "头盔", "项链", "戒指", "手镯", "腰带", "靴子",
              "护身符", "守护石", "面甲", "坐骑", "铃铛", "蝴蝶结"]

STAT_COLS = [("StatMinAC", "StatMaxAC", "防御"), ("StatMinMAC", "StatMaxMAC", "魔御"),
             ("StatMinDC", "StatMaxDC", "攻击"), ("StatMinMC", "StatMaxMC", "魔法"),
             ("StatMinSC", "StatMaxSC", "道术"), ("Stat准确", None, "准确"),
             ("Stat敏捷", None, "敏捷"), ("StatHP", None, "生命"), ("StatMP", None, "法力"),
             ("Stat攻击速度", None, "攻速"), ("Stat幸运", None, "幸运"),
             ("Stat强度", None, "强度"), ("Stat神圣", None, "神圣"),
             ("Stat反弹伤害", None, "反弹"), ("Stat魔法躲避", None, "魔法躲避"),
             ("Stat毒物躲避", None, "毒物躲避"), ("Stat暴击率", None, "暴击率"),
             ("Stat暴击伤害", None, "暴击伤害"), ("Stat防御强化", None, "防御强化"),
             ("Stat魔法防御强化", None, "魔御强化"), ("Stat攻击强化", None, "攻击强化"),
             ("Stat魔法强化", None, "魔法强化"), ("Stat道术强化", None, "道术强化"),
             ("Stat攻速强化", None, "攻速强化"), ("Stat生命值强化", None, "生命强化"),
             ("Stat法力值强化", None, "法力强化"), ("Stat生命偷取", None, "吸血")]
SPECIAL_COLS = [("SpecialParalize", "麻痹"), ("SpecialTeleport", "传送"), ("SpecialClearRing", "净化"),
                ("SpecialProtection", "护身"), ("SpecialRevival", "复活"), ("SpecialMuscle", "力量"),
                ("SpecialFlame", "火焰"), ("SpecialHealing", "治愈"), ("SpecialProbe", "探测"),
                ("SpecialSkill", "技能"), ("SpecialNoDuraLoss", "不损持久"), ("SpecialBlink", "闪现")]


def to_int(v):
    try:
        return int(str(v).strip())
    except Exception:
        return 0


def attr_text(r):
    parts = []
    for c, c2, label in STAT_COLS:
        v1 = to_int(r.get(c, 0)); v2 = to_int(r.get(c2, 0)) if c2 else 0
        if v1 == 0 and v2 == 0:
            continue
        if c2 and v2 and v2 != v1:
            parts.append("%s%d-%d" % (label, v1, v2))
        elif c2:
            parts.append("%s%d" % (label, v2 or v1))
        else:
            parts.append("%s%+d" % (label, v1))
    for c, label in SPECIAL_COLS:
        if str(r.get(c, "")).strip().lower() == "true":
            parts.append("★" + label)
    return " ｜ ".join(parts) if parts else "—"


def clean_bonus(tt):
    """说明文本 -> 行列表。处理转义 \\r\\n 与『套装属性:』前缀"""
    t = (tt or "").replace("\\r\\n", "\n").replace("\\r", "\n").replace("\\n", "\n")
    t = t.replace("\r\n", "\n").replace("\r", "\n")
    out = []
    for ln in t.split("\n"):
        ln = ln.strip()
        if not ln:
            continue
        for pre in ("套装属性:", "套装属性："):
            if ln.startswith(pre):
                ln = ln[len(pre):].strip()
                break
        if ln:
            out.append(ln)
    return out


# ---------------- 读取 ----------------
with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

set_items = collections.defaultdict(list)
for r in rows:
    s = r["ItemSet"].strip()
    if s in ("非套装", "", "None"):
        continue
    set_items[s].append(r)

SETS = {}
for s, lst in set_items.items():
    lvs = sorted(set(to_int(x["ItemRequiredAmount"]) for x in lst if x["ItemRequiredType"] == "Level"))
    # 收集套装说明：同一套装内不同文案可能有多种（分档）
    variants = collections.OrderedDict()
    for x in lst:
        lines = clean_bonus(x.get("ItemToolTip"))
        if lines:
            key = tuple(lines)
            variants[key] = variants.get(key, 0) + 1
    bonus_list = [("　".join(k), v) for k, v in sorted(variants.items(), key=lambda z: -z[1])]
    eq = [x for x in lst if x["ItemType"].strip() in EQUIP_TYPES]
    mats = [x for x in lst if x["ItemType"].strip() not in EQUIP_TYPES]
    SETS[s] = {
        "name": s, "raw": lst, "lv": min(lvs) if lvs else 0, "lvs": lvs,
        "bonus_list": bonus_list, "eq": eq, "mats": mats,
        "classes": sorted(set(x["ItemRequiredClass"] for x in eq)),
    }


def bonus_flat(d, sep="　｜　", lim=5):
    """单行展示（用于总览/一览表）：默认只取件数最多的一档，其余在明细列出"""
    if not d["bonus_list"]:
        return "（无说明文本）"
    txt = d["bonus_list"][0][0]
    if len(d["bonus_list"]) > 1:
        txt += "（另有 %d 档说明，见明细）" % (len(d["bonus_list"]) - 1)
    return txt

print("套装数:", len(SETS), " 装备件合计:", sum(len(v["eq"]) for v in SETS.values()),
      " 材料件:", sum(len(v["mats"]) for v in SETS.values()))

# 职业视图
CLASS_SETS = {}
for cname, groups in CLASS_GROUPS:
    lst = []
    for s, d in SETS.items():
        pieces = [x for x in d["eq"] if x["ItemRequiredClass"].strip() in groups]
        if not pieces:
            continue
        clsset = set(x["ItemRequiredClass"].strip() for x in pieces)
        if DATA_CLASS[cname] in clsset:
            own = "专属"
        elif clsset & {"战法道", "战士刺客"}:
            own = "职业组"
        else:
            own = "通用"
        lst.append((s, pieces, own))
    lst.sort(key=lambda x: (SETS[x[0]]["lv"], x[0]))
    CLASS_SETS[cname] = lst
    print("  %s: %d 套（专属 %d / 职业组 %d / 通用 %d）" % (
        cname, len(lst),
        sum(1 for x in lst if x[2] == "专属"),
        sum(1 for x in lst if x[2] == "职业组"),
        sum(1 for x in lst if x[2] == "通用")))

# =========================================================
# Word
# =========================================================
doc = Document()
st = doc.styles["Normal"]
st.font.name = "微软雅黑"; st.font.size = Pt(9.5)
st._element.rPr.rFonts.set(qn("w:eastAsia"), "微软雅黑")
for s in doc.sections:
    s.top_margin = Cm(1.8); s.bottom_margin = Cm(1.8)
    s.left_margin = Cm(1.6); s.right_margin = Cm(1.6)


def set_run(run, size=9.5, bold=False, color=None, name="微软雅黑"):
    run.font.size = Pt(size); run.bold = bold; run.font.name = name
    run._element.rPr.rFonts.set(qn("w:eastAsia"), name)
    if color:
        run.font.color.rgb = RGBColor(*color)


def para(text, size=9.5, bold=False, color=None, align=None, sb=0, sa=3):
    p = doc.add_paragraph()
    if align:
        p.alignment = align
    p.paragraph_format.space_before = Pt(sb); p.paragraph_format.space_after = Pt(sa)
    set_run(p.add_run(text), size, bold, color)
    return p


def shade(cell, hexcolor):
    tcPr = cell._tc.get_or_add_tcPr()
    sh = OxmlElement("w:shd"); sh.set(qn("w:val"), "clear"); sh.set(qn("w:fill"), hexcolor)
    tcPr.append(sh)


def make_table(header, data, widths, header_fill="2F5496"):
    t = doc.add_table(rows=1, cols=len(header))
    t.style = "Table Grid"; t.alignment = WD_TABLE_ALIGNMENT.CENTER; t.autofit = False
    lay = OxmlElement("w:tblLayout"); lay.set(qn("w:type"), "fixed"); t._tbl.tblPr.append(lay)
    trPr = t.rows[0]._tr.get_or_add_trPr()
    th = OxmlElement("w:tblHeader"); th.set(qn("w:val"), "true"); trPr.append(th)
    hdr = t.rows[0].cells
    for i, h in enumerate(header):
        hdr[i].text = ""
        p = hdr[i].paragraphs[0]
        p.paragraph_format.space_before = Pt(1); p.paragraph_format.space_after = Pt(1)
        set_run(p.add_run(h), 8.5, True, (255, 255, 255)); shade(hdr[i], header_fill)
    for ri, row in enumerate(data):
        cells = t.add_row().cells
        for i, v in enumerate(row):
            cells[i].text = ""
            p = cells[i].paragraphs[0]
            p.paragraph_format.space_before = Pt(0.5); p.paragraph_format.space_after = Pt(0.5)
            set_run(p.add_run(str(v)), 8.5)
        if ri % 2 == 1:
            for c in cells:
                shade(c, "EDF2F9")
    for r_ in t.rows:
        for i, c in enumerate(r_.cells):
            c.width = Cm(widths[i])
    return t


def piece_name(r):
    n = r["ItemName"].strip()
    g = r["ItemRequiredGender"]
    if g == "男性" and "男" not in n:
        n += "【限男】"
    elif g == "女性" and "女" not in n:
        n += "【限女】"
    return n


# ---- 封面 ----
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
set_run(p.add_run("韩Mir2 服务器套装数据总表"), 20, True, (0x1F, 0x38, 0x64))
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
set_run(p.add_run("Lv1 - Lv70 · 按职业整理"), 12, False, (0x40, 0x40, 0x40))
para("数据来源：20260915Mir2Server / Exports / 2_物品数据.csv（117 版服务端导出，4169 条物品记录）",
     9, color=(0x66, 0x66, 0x66), align=WD_ALIGN_PARAGRAPH.CENTER, sb=6)
para("套装构成取自物品的「套装」字段；套装加成取自物品说明中的「套装属性」文本。整理日期：2026-10-01",
     9, color=(0x66, 0x66, 0x66), align=WD_ALIGN_PARAGRAPH.CENTER, sa=14)

# ---- 一、总览 ----
doc.add_heading("一、总览", level=1)
eq_total = sum(len(v["eq"]) for v in SETS.values())
para("服务端现存正式套装 %d 套，装备件合计 %d 件（另有 %d 件套装内的材料/杂物，不计入下表）。"
     % (len(SETS), eq_total, sum(len(v["mats"]) for v in SETS.values())), 9, sa=6)

para("1. 套装总表（按需求等级升序）", 11, True, (0x1F, 0x38, 0x64), sb=6, sa=4)
data = []
for s, d in sorted(SETS.items(), key=lambda x: (x[1]["lv"], x[0])):
    cls = "、".join(GROUP_LABEL.get(c, c) for c in d["classes"] if c in GROUP_LABEL)
    lvs = "/".join("Lv%d" % x for x in d["lvs"])
    data.append([s, lvs, len(d["eq"]), cls, bonus_flat(d)])
make_table(["套装名称", "需求等级", "件数", "适用职业", "套装属性"],
           data, [2.3, 2.0, 1.1, 3.6, 8.8])

para("2. Lv1-70 套装覆盖情况", 11, True, (0x1F, 0x38, 0x64), sb=12, sa=4)
covered = collections.defaultdict(list)
for s, d in SETS.items():
    if d["eq"]:
        covered[d["lv"]].append(s)
lines = []
for band in [(1, 11), (12, 19), (20, 29), (30, 39), (40, 49), (50, 59), (60, 70)]:
    seg = []
    for lv in range(band[0], band[1] + 1):
        for s in covered.get(lv, []):
            seg.append("Lv%d %s" % (lv, s))
    lines.append(["Lv%d-%d" % band, len(seg), "、".join(seg) if seg else "— 无套装 —"])
make_table(["等级段", "套装数", "可用套装"], lines, [2.4, 1.4, 14.2], header_fill="BF8F00")

para("说明：Lv1-11 与 Lv61-70 在本服务端没有套装；Lv60 有昆仑、青宝、双戒三套（满级档）。",
     8.5, color=(0xA0, 0x30, 0x30), sb=4)
para("说明：套装属性取自物品说明文本，共 %d 套有说明、%d 套各档位文案内容一致。"
     "唯一无说明的是 天龙套装、祈祷套装、贝玉套装 三套，其加成需另在服务端脚本或客户端配置中确认。"
     % (sum(1 for d in SETS.values() if d["bonus_list"]),
        sum(1 for d in SETS.values() if len(d["bonus_list"]) == 1)),
     8.5, color=(0xA0, 0x30, 0x30), sa=2)
para("说明：部分套装的说明按「部位组合」分档（如昆仑套装分项链+戒指 / 项链+手镯+戒指 / 戒指+戒指 三档），"
     "明细处已逐档列出。", 8.5, color=(0xA0, 0x30, 0x30), sa=2)

doc.add_page_break()

# ---- 二~六、按职业 ----
CN = ["二", "三", "四", "五", "六"]
for idx, (cname, groups) in enumerate(CLASS_GROUPS):
    doc.add_heading("%s、%s" % (CN[idx], cname), level=1)
    lst = CLASS_SETS[cname]
    own_n = sum(1 for _, _, o in lst if o != "通用")
    para("可穿套装 %d 套（其中专属/职业组 %d 套，其余为通用套装）　｜　按需求等级升序排列"
         % (len(lst), own_n), 9.5, True, (0x1F, 0x38, 0x64), sa=6)

    # 一览表
    idx_data = []
    for s, pieces, own in lst:
        d = SETS[s]
        lvs = "/".join("Lv%d" % x for x in d["lvs"])
        idx_data.append([s, lvs, len(pieces),
                         own,
                         bonus_flat(d)])
    make_table(["套装名称", "需求等级", "本职业件数", "归属", "套装属性"],
               idx_data, [2.4, 2.0, 2.0, 1.3, 10.1], header_fill="548235")

    # 明细
    for s, pieces, own in lst:
        d = SETS[s]
        lvs = "/".join("Lv%d" % x for x in d["lvs"])
        para("◆ %s　%s　%d 件　[%s]" % (s, lvs, len(pieces), own),
             10.5, True, (0x1F, 0x38, 0x64), sb=10, sa=3)
        rows_ = []
        for x in sorted(pieces, key=lambda z: (SLOT_ORDER.index(z["ItemType"].strip())
                                               if z["ItemType"].strip() in SLOT_ORDER else 99,
                                               to_int(z["ItemRequiredAmount"]), z["ItemName"])):
            rows_.append([x["ItemType"].strip(),
                          "Lv%s" % x["ItemRequiredAmount"] if x["ItemRequiredType"] == "Level" else "属性需求",
                          piece_name(x), x["ItemGrade"].replace("None", "无"),
                          GROUP_LABEL.get(x["ItemRequiredClass"].strip(), x["ItemRequiredClass"]),
                          attr_text(x)])
        make_table(["部位", "等级", "装备名称", "品质", "归属", "核心属性"],
                   rows_, [1.3, 1.1, 4.6, 1.1, 2.2, 7.7])
        if d["bonus_list"]:
            if len(d["bonus_list"]) == 1:
                para("★ 套装属性：" + d["bonus_list"][0][0], 9, True, (0xC0, 0x00, 0x00), sb=2, sa=2)
            else:
                para("★ 套装属性（该套装按部位组合分 %d 档）：" % len(d["bonus_list"]),
                     9, True, (0xC0, 0x00, 0x00), sb=2, sa=1)
                marks = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫"
                for vi, (txt, cnt) in enumerate(d["bonus_list"], 1):
                    para("    %s %s　（%d 件）" % (marks[vi-1] if vi <= len(marks) else "(%d)" % vi, txt, cnt),
                         8.5, sa=1)
        else:
            para("★ 套装属性：物品说明中未提供（需在服务端脚本/客户端配置中确认）", 8.5,
                 color=(0x99, 0x99, 0x99), sb=2, sa=2)
    if idx != len(CLASS_GROUPS) - 1:
        doc.add_page_break()

doc.save(DOCX_OUT)
print("已生成 Word:", DOCX_OUT)

# =========================================================
# Excel
# =========================================================
wb = openpyxl.Workbook()
thin = Side(style="thin", color="B0B0B0")
BORDER = Border(left=thin, right=thin, top=thin, bottom=thin)
HFILL = PatternFill("solid", fgColor="2F5496")
HFONT = Font(name="微软雅黑", size=10, bold=True, color="FFFFFF")
CFONT = Font(name="微软雅黑", size=10)

# 套装总览
ws = wb.active; ws.title = "套装总览"
ws.append(["套装名称", "需求等级", "装备件数", "材料件数", "适用职业", "套装属性"])
for j in range(1, 7):
    c = ws.cell(1, j); c.font = HFONT; c.fill = HFILL; c.alignment = Alignment(horizontal="center")
for s, d in sorted(SETS.items(), key=lambda x: (x[1]["lv"], x[0])):
    cls = "、".join(GROUP_LABEL.get(c, c) for c in d["classes"] if c in GROUP_LABEL)
    ws.append([s, "/".join("Lv%d" % x for x in d["lvs"]), len(d["eq"]), len(d["mats"]), cls,
               bonus_flat(d)])
for row in ws.iter_rows(min_row=2, max_row=ws.max_row, max_col=6):
    for c in row:
        c.font = CFONT; c.border = BORDER
ws.freeze_panes = "A2"; ws.auto_filter.ref = "A1:F%d" % ws.max_row
for col, w in zip("ABCDEF", [14, 14, 10, 10, 34, 60]):
    ws.column_dimensions[col].width = w

# 每职业
for cname, groups in CLASS_GROUPS:
    ws = wb.create_sheet(cname)
    ws.append(["套装名称", "需求等级", "归属", "部位", "装备名称", "品质", "职业", "核心属性", "套装属性"])
    for j in range(1, 10):
        c = ws.cell(1, j); c.font = HFONT; c.fill = HFILL; c.alignment = Alignment(horizontal="center")
    for s, pieces, own in CLASS_SETS[cname]:
        d = SETS[s]
        for x in sorted(pieces, key=lambda z: (SLOT_ORDER.index(z["ItemType"].strip())
                                               if z["ItemType"].strip() in SLOT_ORDER else 99,
                                               to_int(z["ItemRequiredAmount"]), z["ItemName"])):
            ws.append([s, "/".join("Lv%d" % y for y in d["lvs"]), own,
                       x["ItemType"].strip(),
                       x["ItemName"].strip() + ("【限男】" if x["ItemRequiredGender"] == "男性" and "男" not in x["ItemName"] else
                                                "【限女】" if x["ItemRequiredGender"] == "女性" and "女" not in x["ItemName"] else ""),
                       x["ItemGrade"].replace("None", "无"),
                       GROUP_LABEL.get(x["ItemRequiredClass"].strip(), x["ItemRequiredClass"]),
                       attr_text(x),
                       bonus_flat(d)])
    for row in ws.iter_rows(min_row=2, max_row=ws.max_row, max_col=9):
        for c in row:
            c.font = CFONT; c.border = BORDER
    ws.freeze_panes = "A2"; ws.auto_filter.ref = "A1:I%d" % ws.max_row
    for col, w in zip("ABCDEFGHI", [14, 14, 8, 9, 28, 8, 14, 62, 50]):
        ws.column_dimensions[col].width = w

wb.save(XLSX_OUT)
print("已生成 Excel:", XLSX_OUT)
