# -*- coding: utf-8 -*-
"""
韩Mir2 装备数据整理：按 职业 -> 部位 -> 等级 聚合，输出 Word + Excel
数据源: 20260915Mir2Server/Exports/2_物品数据.csv
"""
import csv, io, os, collections
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
DOCX_OUT = os.path.join(OUT_DIR, "韩Mir2装备数据总表-按职业等级整理.docx")
XLSX_OUT = os.path.join(OUT_DIR, "韩Mir2装备数据总表-按职业等级整理.xlsx")

# ---------------- 配置 ----------------
# 职业 -> 可穿用的归属组(含专属)
CLASS_GROUPS = [
    ("战士",  ["战士", "战法道", "战士刺客", "全职业"]),
    ("法师",  ["法师", "战法道", "全职业"]),
    ("道士",  ["道士", "战法道", "全职业"]),
    ("刺客",  ["刺客", "战士刺客", "全职业"]),
    ("弓箭",  ["弓箭", "全职业"]),
]
GROUP_LABEL = {
    "战士": "战士专属", "法师": "法师专属", "道士": "道士专属",
    "刺客": "刺客专属", "弓箭": "弓箭专属",
    "战法道": "战法道通用", "战士刺客": "战士刺客通用", "全职业": "全职业通用",
}
# 部位: 主分类 -> CSV 中 ItemType 明细
SLOTS = [
    ("武器", ["武器"]),
    ("盔甲", ["盔甲"]),
    ("头盔", ["头盔"]),
    ("首饰", ["项链", "戒指", "手镯"]),
    ("腰带", ["腰带"]),
    ("靴子", ["靴子"]),
]
SLOT_JEWEL = {"项链", "戒指", "手镯"}
EXTRA_SLOTS = ["护身符", "守护石", "面甲", "坐骑", "铃铛", "蝴蝶结", "照明物", "镶嵌宝石", "宝玉神珠"]

LEVEL_BANDS = [(0, 0, "无需求 (Lv0)"), (1, 9, "Lv1-9"), (10, 19, "Lv10-19"),
               (20, 29, "Lv20-29"), (30, 39, "Lv30-39"), (40, 49, "Lv40-49"),
               (50, 59, "Lv50-59"), (60, 9999, "Lv60+")]
BAND_ATTR = "属性需求（无等级限制）"
BAND_ORDER = [b[2] for b in LEVEL_BANDS] + [BAND_ATTR]

# 属性列 -> 显示名
STAT_COLS = [
    ("StatMinAC", "StatMaxAC", "防御"),
    ("StatMinMAC", "StatMaxMAC", "魔御"),
    ("StatMinDC", "StatMaxDC", "攻击"),
    ("StatMinMC", "StatMaxMC", "魔法"),
    ("StatMinSC", "StatMaxSC", "道术"),
    ("Stat准确", None, "准确"),
    ("Stat敏捷", None, "敏捷"),
    ("StatHP", None, "生命"),
    ("StatMP", None, "法力"),
    ("Stat攻击速度", None, "攻速"),
    ("Stat幸运", None, "幸运"),
    ("Stat负重", None, "负重"),
    ("Stat强度", None, "强度"),
    ("Stat神圣", None, "神圣"),
    ("Stat反弹伤害", None, "反弹"),
    ("Stat冰冻伤害", None, "冰冻伤害"),
    ("Stat毒素伤害", None, "毒素伤害"),
    ("Stat魔法躲避", None, "魔法躲避"),
    ("Stat毒物躲避", None, "毒物躲避"),
    ("Stat生命恢复", None, "生命恢复"),
    ("Stat法力恢复", None, "法力恢复"),
    ("Stat暴击率", None, "暴击率"),
    ("Stat暴击伤害", None, "暴击伤害"),
    ("Stat防御强化", None, "防御强化"),
    ("Stat魔法防御强化", None, "魔御强化"),
    ("Stat攻击强化", None, "攻击强化"),
    ("Stat魔法强化", None, "魔法强化"),
    ("Stat道术强化", None, "道术强化"),
    ("Stat攻速强化", None, "攻速强化"),
    ("Stat生命值强化", None, "生命强化"),
    ("Stat法力值强化", None, "法力强化"),
    ("Stat生命偷取", None, "吸血"),
]
SPECIAL_COLS = [
    ("SpecialParalize", "麻痹"), ("SpecialTeleport", "传送"), ("SpecialClearRing", "净化"),
    ("SpecialProtection", "护身"), ("SpecialRevival", "复活"), ("SpecialMuscle", "力量"),
    ("SpecialFlame", "火焰"), ("SpecialHealing", "治愈"), ("SpecialProbe", "探测"),
    ("SpecialSkill", "技能"), ("SpecialNoDuraLoss", "不损持久"), ("SpecialBlink", "闪现"),
]


def to_int(v):
    try:
        return int(str(v).strip())
    except Exception:
        return 0


def level_of(r):
    t = r.get("ItemRequiredType", "Level")
    if t == "Level":
        return to_int(r.get("ItemRequiredAmount", 0)), None
    label = {"MaxDC": "攻击力", "MaxMC": "魔法力", "MaxSC": "道术力"}.get(t, t)
    return None, "%s≥%s" % (label, r.get("ItemRequiredAmount", "0"))


def band_of(lv):
    if lv is None:
        return BAND_ATTR
    for lo, hi, name in LEVEL_BANDS:
        if lo <= lv <= hi:
            return name
    return LEVEL_BANDS[-1][2]


def attr_text(r):
    parts = []
    for c, c2, label in STAT_COLS:
        v1 = to_int(r.get(c, 0))
        v2 = to_int(r.get(c2, 0)) if c2 else 0
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
    pct = to_int(r.get("Stat暴击率", 0))
    return " ｜ ".join(parts) if parts else "—"


# ---------------- 读取 ----------------
with io.open(SRC, "r", encoding="utf-8-sig", newline="") as f:
    rows = list(csv.DictReader(f))

ITEMS = []
for r in rows:
    t = r.get("ItemType", "").strip()
    slot = None
    subtype = None
    if t in ("武器", "盔甲", "头盔", "腰带", "靴子"):
        slot = t
    elif t in SLOT_JEWEL:
        slot, subtype = "首饰", t
    if not slot:
        continue
    lv, reqtxt = level_of(r)
    name = r.get("ItemName", "").strip()
    gender = r.get("ItemRequiredGender", "性别不限")
    if gender == "男性" and "男" not in name:
        name += "【限男】"
    elif gender == "女性" and "女" not in name:
        name += "【限女】"
    ITEMS.append({
        "name": name,
        "raw_name": r.get("ItemName", "").strip(),
        "slot": slot,
        "subtype": subtype or "",
        "cls": r.get("ItemRequiredClass", "全职业").strip(),
        "grade": r.get("ItemGrade", "").strip(),
        "lv": lv,
        "req": reqtxt,
        "band": band_of(lv),
        "attr": attr_text(r),
        "gender": gender,
        "set": r.get("ItemSet", "").strip(),
        "sort": (lv if lv is not None else 9999),
    })

GRADE_FIX = {"None": "无", "": "无"}
for it in ITEMS:
    it["grade"] = GRADE_FIX.get(it["grade"], it["grade"])

# ---------------- 汇总统计 ----------------
total_slot = collections.Counter(it["slot"] for it in ITEMS)
total_sub = collections.Counter(it["slot"] + ("/" + it["subtype"] if it["subtype"] else "") for it in ITEMS)
total_cls = collections.Counter(it["cls"] for it in ITEMS)
total_grade = collections.Counter(it["grade"] for it in ITEMS)

print("装备类条目总数:", len(ITEMS))
for s, _ in SLOTS:
    print("  %s: %d" % (s, total_slot[s]))

# 每职业可穿清单
CLASS_ITEMS = {}
for cname, groups in CLASS_GROUPS:
    lst = [it for it in ITEMS if it["cls"] in groups]
    CLASS_ITEMS[cname] = lst
    print("  %s 可穿: %d" % (cname, len(lst)))

# =========================================================
#  Word
# =========================================================
doc = Document()
# 全局中文字体
st = doc.styles["Normal"]
st.font.name = "微软雅黑"
st.font.size = Pt(9.5)
st._element.rPr.rFonts.set(qn("w:eastAsia"), "微软雅黑")

for i, s in enumerate(doc.sections):
    s.top_margin = Cm(1.8); s.bottom_margin = Cm(1.8)
    s.left_margin = Cm(1.6); s.right_margin = Cm(1.6)


def set_run(run, size=9.5, bold=False, color=None, name="微软雅黑"):
    run.font.size = Pt(size)
    run.bold = bold
    run.font.name = name
    run._element.rPr.rFonts.set(qn("w:eastAsia"), name)
    if color:
        run.font.color.rgb = RGBColor(*color)


def para(text, size=9.5, bold=False, color=None, align=None, space_before=0, space_after=3):
    p = doc.add_paragraph()
    if align:
        p.alignment = align
    p.paragraph_format.space_before = Pt(space_before)
    p.paragraph_format.space_after = Pt(space_after)
    set_run(p.add_run(text), size, bold, color)
    return p


def shade(cell, hexcolor):
    tcPr = cell._tc.get_or_add_tcPr()
    sh = OxmlElement("w:shd")
    sh.set(qn("w:val"), "clear"); sh.set(qn("w:fill"), hexcolor)
    tcPr.append(sh)


def make_table(header, data, widths, header_fill="2F5496"):
    t = doc.add_table(rows=1, cols=len(header))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    # 固定列宽布局
    tblPr = t._tbl.tblPr
    layout = OxmlElement("w:tblLayout")
    layout.set(qn("w:type"), "fixed")
    tblPr.append(layout)
    hdr = t.rows[0].cells
    # 表头跨页重复
    trPr = t.rows[0]._tr.get_or_add_trPr()
    th = OxmlElement("w:tblHeader")
    th.set(qn("w:val"), "true")
    trPr.append(th)
    for i, h in enumerate(header):
        hdr[i].text = ""
        p = hdr[i].paragraphs[0]
        p.paragraph_format.space_before = Pt(1); p.paragraph_format.space_after = Pt(1)
        set_run(p.add_run(h), 8.5, True, (255, 255, 255))
        shade(hdr[i], header_fill)
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


# ---- 封面/标题 ----
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
set_run(p.add_run("韩Mir2 服务器装备数据总表"), 20, True, (0x1F, 0x38, 0x64))
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
set_run(p.add_run("按 职业 · 部位 · 等级 整理"), 12, False, (0x40, 0x40, 0x40))
para("数据来源：20260915Mir2Server / Exports / 2_物品数据.csv（由 Server.MirDB 导出，共 4169 条物品记录，其中可穿戴装备 %d 件）"
     % len(ITEMS), 9, color=(0x66, 0x66, 0x66), align=WD_ALIGN_PARAGRAPH.CENTER, space_before=6)
para("整理日期：2026-10-01", 9, color=(0x66, 0x66, 0x66), align=WD_ALIGN_PARAGRAPH.CENTER, space_after=14)

# ---- 一、总览 ----
doc.add_heading("一、总览", level=1)
para("1. 部位体系", 11, True, (0x1F, 0x38, 0x64), space_before=6)
para("本表按 6 大部位归类（首饰 = 项链 + 戒指 + 手镯）：", 9, space_after=4)
make_table(["部位", "包含的装备类型", "件数"],
           [[s, "、".join(types), total_slot[s]] for s, types in SLOTS],
           [2.6, 6.0, 2.0])

para("2. 职业体系", 11, True, (0x1F, 0x38, 0x64), space_before=10)
para("装备的「需求职业」共 8 种取值，其中 3 种为跨职业通用组：", 9, space_after=4)
make_table(["需求职业取值", "含义", "件数"],
           [["战士 / 法师 / 道士 / 刺客 / 弓箭", "单职业专属", "%d" % sum(total_cls[c] for c in ["战士","法师","道士","刺客","弓箭"])],
            ["战法道", "战士、法师、道士 三职业通用", total_cls["战法道"]],
            ["战士刺客", "战士、刺客 通用", total_cls["战士刺客"]],
            ["全职业", "任意职业通用", total_cls["全职业"]]],
           [4.2, 5.4, 1.8],
           header_fill="548235")
para("说明：本表中每个职业章节均列出「该职业专属 + 其可用的通用组」的全部装备，因此通用装备会在多个职业章节中重复出现，"
     "以保证单个职业章节即可完整查阅。", 8.5, color=(0xA0, 0x30, 0x30), space_before=4)

para("3. 各职业可穿装备统计", 11, True, (0x1F, 0x38, 0x64), space_before=10)
hdr = ["职业", "可穿装备合计"] + [s for s, _ in SLOTS]
data = []
for cname, groups in CLASS_GROUPS:
    lst = CLASS_ITEMS[cname]
    cnt = collections.Counter(it["slot"] for it in lst)
    data.append([cname, len(lst)] + [cnt[s] for s, _ in SLOTS])
make_table(hdr, data, [2.0, 2.6, 1.9, 1.9, 1.9, 1.9, 1.9, 1.9], header_fill="BF8F00")
para("备注：弓箭职业不含「战法道」通用组；刺客不含「战法道」但可用「战士刺客」通用组。", 8.5,
     color=(0x66, 0x66, 0x66), space_before=3)

doc.add_page_break()

# ---- 二、按职业分章 ----
CN = ["二", "三", "四", "五", "六"]
for idx, (cname, groups) in enumerate(CLASS_GROUPS):
    doc.add_heading("%s、%s" % (CN[idx], cname), level=1)
    lst = CLASS_ITEMS[cname]
    gdesc = "、".join(GROUP_LABEL[g] for g in groups)
    cnt = collections.Counter(it["slot"] for it in lst)
    para("可穿装备合计 %d 件　｜　归属组：%s" % (len(lst), gdesc), 9.5, True, (0x1F, 0x38, 0x64), space_after=6)

    for sname, _ in SLOTS:
        sub = [it for it in lst if it["slot"] == sname]
        doc.add_heading("%s.%s  %s（%d 件）" % (idx + 1, SLOTS.index((sname, _)) + 1, sname, len(sub)), level=2)
        if not sub:
            para("无。", 9, color=(0x99, 0x99, 0x99))
            continue

        # 首饰拆分子类
        if sname == "首饰":
            groups_sub = [(t, [it for it in sub if it["subtype"] == t]) for t in ["项链", "戒指", "手镯"]]
        else:
            groups_sub = [(None, sub)]

        for subname, slist in groups_sub:
            if subname:
                para("◆ %s（%d 件）" % (subname, len(slist)), 10, True, (0x54, 0x82, 0x35), space_before=6, space_after=3)
            if not slist:
                para("无。", 9, color=(0x99, 0x99, 0x99))
                continue
            # 按等级段分组
            for bandname in BAND_ORDER:
                bl = [it for it in slist if it["band"] == bandname]
                if not bl:
                    continue
                bl.sort(key=lambda x: (x["sort"], x["name"]))
                para("▍%s　（%d 件）" % (bandname, len(bl)), 9, True, (0x40, 0x40, 0x40),
                     space_before=5, space_after=2)
                data = [[it["req"] if it["req"] else ("Lv%d" % it["lv"] if it["lv"] else "无"),
                         it["name"], it["grade"],
                         GROUP_LABEL.get(it["cls"], it["cls"]),
                         it["attr"]] for it in bl]
                make_table(["需求", "装备名称", "品质", "归属", "核心属性"],
                           data, [1.4, 4.2, 1.2, 2.2, 8.6])
    if idx != len(CLASS_GROUPS) - 1:
        doc.add_page_break()

# ---- 附录 ----
doc.add_page_break()
doc.add_heading("附录、其他可装备类目", level=1)
para("以下类目不属于上述 6 大部位，但同样属于可穿戴/装备类物品，按等级需求给出分布，供参考。", 9,
     color=(0x66, 0x66, 0x66), space_after=6)
extra = collections.defaultdict(list)
for r in rows:
    t = r.get("ItemType", "").strip()
    if t in EXTRA_SLOTS:
        lv, reqtxt = level_of(r)
        extra[t].append((lv if lv is not None else -1, r.get("ItemName", "").strip(),
                         r.get("ItemRequiredClass", "").strip(), band_of(lv)))
hdr = ["类目", "件数", "等级需求分布"]
data = []
for t in EXTRA_SLOTS:
    lst = extra.get(t, [])
    if not lst:
        continue
    bc = collections.Counter(x[3] for x in lst)
    dist = "；".join("%s %d 件" % (b, bc[b]) for b in BAND_ORDER if b in bc)
    data.append([t, len(lst), dist])
make_table(hdr, data, [2.2, 1.6, 13.2], header_fill="7030A0")
para("说明：护身符多为全职业 Lv0-1 入门类；守护石为副手石，共 77 件（全职业 72 / 战士 5，Lv0-60）；"
     "坐骑、铃铛、蝴蝶结、面甲等为外观/坐骑类。", 8.5, color=(0x66, 0x66, 0x66), space_before=6)

doc.save(DOCX_OUT)
print("已生成 Word:", DOCX_OUT)

# =========================================================
#  Excel
# =========================================================
wb = openpyxl.Workbook()
thin = Side(style="thin", color="B0B0B0")
BORDER = Border(left=thin, right=thin, top=thin, bottom=thin)
HFILL = PatternFill("solid", fgColor="2F5496")
HFONT = Font(name="微软雅黑", size=10, bold=True, color="FFFFFF")
CFONT = Font(name="微软雅黑", size=10)

# 总览 sheet
ws = wb.active
ws.title = "总览"
ws["A1"] = "韩Mir2 服务器装备数据总表（按职业 / 部位 / 等级）"
ws["A1"].font = Font(name="微软雅黑", size=14, bold=True, color="1F3864")
ws["A2"] = "数据来源：Exports/2_物品数据.csv　｜　装备类条目 %d 件　｜　整理日期 2026-10-01" % len(ITEMS)
ws["A2"].font = Font(name="微软雅黑", size=9, color="666666")
r0 = 4
ws.cell(r0, 1, "职业 \\ 部位").font = HFONT
ws.cell(r0, 1).fill = HFILL
for j, (s, _) in enumerate(SLOTS):
    c = ws.cell(r0, 2 + j, s); c.font = HFONT; c.fill = HFILL; c.alignment = Alignment(horizontal="center")
c = ws.cell(r0, 2 + len(SLOTS), "合计"); c.font = HFONT; c.fill = HFILL; c.alignment = Alignment(horizontal="center")
for i, (cname, groups) in enumerate(CLASS_GROUPS):
    lst = CLASS_ITEMS[cname]
    cnt = collections.Counter(it["slot"] for it in lst)
    ws.cell(r0 + 1 + i, 1, cname).font = CFONT
    for j, (s, _) in enumerate(SLOTS):
        ws.cell(r0 + 1 + i, 2 + j, cnt[s]).font = CFONT
    ws.cell(r0 + 1 + i, 2 + len(SLOTS), len(lst)).font = Font(name="微软雅黑", size=10, bold=True)
ws.column_dimensions["A"].width = 14
for j in range(len(SLOTS) + 1):
    ws.column_dimensions[get_column_letter(2 + j)].width = 10

# 每职业一个 sheet
SHEET_HDR = ["部位", "子类", "等级段", "需求等级", "装备名称", "品质", "归属", "核心属性"]
for cname, groups in CLASS_GROUPS:
    ws = wb.create_sheet(cname)
    ws.append(SHEET_HDR)
    for j in range(1, len(SHEET_HDR) + 1):
        c = ws.cell(1, j); c.font = HFONT; c.fill = HFILL
        c.alignment = Alignment(horizontal="center", vertical="center")
    lst = sorted(CLASS_ITEMS[cname],
                 key=lambda x: (SLOTS.index(next(s for s in SLOTS if s[0] == x["slot"])),
                                x["subtype"], x["sort"], x["name"]))
    for it in lst:
        ws.append([it["slot"], it["subtype"], it["band"],
                   it["req"] if it["req"] else (it["lv"] if it["lv"] is not None else 0),
                   it["name"], it["grade"], GROUP_LABEL.get(it["cls"], it["cls"]), it["attr"]])
    for row in ws.iter_rows(min_row=2, max_row=ws.max_row, max_col=len(SHEET_HDR)):
        for c in row:
            c.font = CFONT; c.border = BORDER
            c.alignment = Alignment(vertical="center")
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = "A1:H%d" % ws.max_row
    for col, w in zip("ABCDEFGH", [9, 9, 11, 10, 26, 9, 15, 70]):
        ws.column_dimensions[col].width = w

wb.save(XLSX_OUT)
print("已生成 Excel:", XLSX_OUT)
