# -*- coding: utf-8 -*-
"""生成《韩Mir2 音效系统规则与写入指南》Word + Excel"""
import os, re, json, collections
from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59"
LST = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\SoundList.lst"
LST_SERVER = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\ClientResources\Sound\SoundList.lst"
SND = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\ClientResources\Sound"
SRC = r"D:\mir2-20241027"

# ---------------- 1. 解析 SoundList.lst ----------------
text = open(LST, 'rb').read().decode('utf-8', errors='replace')
lines = text.splitlines()

entries = []   # dict: lineno,index,file,section
sections = []
cur_sec = "(文件头)"
cur = None
skipped = []
for i, ln in enumerate(lines, 1):
    s = ln.strip()
    if not s:
        continue
    if s.startswith(';'):
        cur_sec = s.lstrip(';').strip()
        cur = (i, cur_sec, [])
        sections.append(cur)
        continue
    # 与客户端完全一致：删掉空格 -> 按 ':' 和 Tab 切分 -> [0] 为索引、最后一段为文件名
    parts = ln.replace(" ", "").replace('\t', ':').split(':')
    if len(parts) > 1 and parts[0].isdigit():
        e = {"lineno": i, "index": int(parts[0]), "file": parts[-1].strip(), "section": cur_sec}
        entries.append(e)
        if cur is not None:
            cur[2].append((e["index"], e["file"]))
    else:
        skipped.append((i, ln))

idx_map = {}
dup = collections.defaultdict(list)
for e in entries:
    dup[e["index"]].append(e)
    if e["index"] not in idx_map:
        idx_map[e["index"]] = e["file"]      # 先出现的生效，与客户端代码一致

files_in_dir = set(os.listdir(SND)) if os.path.isdir(SND) else set()
for e in entries:
    nm = os.path.basename(e["file"])
    e["exists"] = (nm in files_in_dir) if nm else False
    e["effective"] = (idx_map.get(e["index"], None) == e["file"])
    e["dup"] = len(dup[e["index"]]) > 1

# ---------------- 2. 解析枚举 ----------------
def read(p):
    return open(p, 'rb').read().decode('utf-8', errors='replace')

def parse_enum(name, src):
    m = re.search(r'public enum %s\s*:\s*\w+\s*\{(.*?)\n\}' % name, src, re.S)
    if not m:
        return []
    out, val = [], -1
    for ln in m.group(1).splitlines():
        ln = ln.split('//')[0].strip().rstrip(',')
        if not ln:
            continue
        mm = re.match(r'^(\w+)\s*(?:=\s*(\d+))?$', ln)
        if not mm:
            continue
        if mm.group(2) is not None:
            val = int(mm.group(2))
        else:
            val += 1
        out.append((mm.group(1), val))
    return out

enums_src = read(os.path.join(SRC, "Shared", "Enums.cs"))
spells = parse_enum("Spell", enums_src)
monsters = parse_enum("Monster", enums_src)

# ---------------- 3. 抽取客户端播放点（含 case Spell.X 上下文）----------------
spell_variants = collections.defaultdict(set)   # spell值 -> {变体}
spell_used = set()
for rel in ["Client/MirObjects/PlayerObject.cs", "Client/MirObjects/MonsterObject.cs",
            "Client/MirObjects/SpellObject.cs", "Client/MirObjects/Effect.cs",
            "Client/MirObjects/MapObject.cs", "Client/MirScenes/GameScene.cs"]:
    p = os.path.join(SRC, rel.replace('/', os.sep))
    if not os.path.exists(p):
        continue
    cur_spell = None
    for ln in read(p).splitlines():
        m = re.search(r'case\s+Spell\.(\w+)\s*:', ln)
        if m:
            cur_spell = m.group(1)
        for pm in re.finditer(r'PlaySound\(([^;]*?)\);', ln):
            expr = pm.group(1)
            # 形式1：20000 + (ushort)Spell.XXX * 10 + N
            m2 = re.search(r'20000\s*\+\s*\(ushort\)Spell\.(\w+)\s*\*\s*10\s*(?:\+\s*(\d+))?', expr)
            if m2:
                nm, var = m2.group(1), int(m2.group(2) or 0)
                d = dict(spells)
                if nm in d:
                    spell_variants[d[nm]].add(var)
                    spell_used.add(nm)
                continue
            # 形式2：20000 + NNN * 10 + N
            m3 = re.search(r'20000\s*\+\s*(\d+)\s*\*\s*10\s*(?:\+\s*(\d+))?', expr)
            if m3:
                spell_variants[int(m3.group(1))].add(int(m3.group(2) or 0))
                continue
            # 形式3：20000 + (ushort)Spell * 10 + N   （动态 Spell，靠上下文 case 定位）
            m4 = re.search(r'20000\s*\+\s*\(ushort\)Spell\s*\*\s*10\s*(?:\+\s*(\d+))?', expr)
            if m4 and cur_spell:
                d = dict(spells)
                if cur_spell in d:
                    spell_variants[d[cur_spell]].add(int(m4.group(1) or 0))
                    spell_used.add(cur_spell)
                continue
            # 形式4：20000 + (ushort)Spell * 10 + (Gender ... ? 0 : 1)
            if '20000 + (ushort)Spell * 10 + (Gender' in expr and cur_spell:
                d = dict(spells)
                if cur_spell in d:
                    spell_variants[d[cur_spell]].update({0, 1})
                    spell_used.add(cur_spell)

# 技能文件是否实际存在
def spell_files(val):
    fs = []
    for n in range(10):
        nm = "M%d-%d.wav" % (val, n)
        if nm in files_in_dir:
            fs.append(nm)
    return fs

# ---------------- 4. 怪物：按图档号聚合 lst ----------------
mon_by_img = collections.defaultdict(list)
for e in entries:
    mon_by_img[e["index"] // 10].append(e)
for k in mon_by_img:
    mon_by_img[k].sort(key=lambda x: x["index"])

mon_enum_by_img = {v: n for n, v in monsters}

print("lst 条目 %d，唯一索引 %d，注释行 %d，跳过行 %d"
      % (len(entries), len(idx_map), len(sections), len(skipped)))
print("技能枚举 %d，怪物枚举 %d，有播放点的技能 %d" % (len(spells), len(monsters), len(spell_used)))

# ---------------- 5. Word ----------------
doc = Document()
st = doc.styles['Normal']
st.font.name = '微软雅黑'
st.font.size = Pt(9.5)
st.element.rPr.rFonts.set(qn('w:eastAsia'), '微软雅黑')

sec = doc.sections[0]
sec.page_width, sec.page_height = Cm(21.0), Cm(29.7)
for a in ('left_margin', 'right_margin'):
    setattr(sec, a, Cm(1.6))
sec.top_margin, sec.bottom_margin = Cm(1.8), Cm(1.8)

def shade(cell, color):
    tcPr = cell._tc.get_or_add_tcPr()
    el = OxmlElement('w:shd')
    el.set(qn('w:val'), 'clear'); el.set(qn('w:color'), 'auto'); el.set(qn('w:fill'), color)
    tcPr.append(el)

def setrun(run, size=9.5, bold=False, color=None, font='微软雅黑'):
    run.font.size = Pt(size); run.bold = bold
    run.font.name = font
    run._element.rPr.rFonts.set(qn('w:eastAsia'), font)
    if color:
        run.font.color.rgb = RGBColor(*color)

def H(txt, lv=1, color=(0x1F, 0x3B, 0x73), size=None, sb=10, sa=5):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(sb); p.paragraph_format.space_after = Pt(sa)
    sizes = {1: 15, 2: 12, 3: 10.5}
    setrun(p.add_run(txt), size or sizes.get(lv, 10), True, color)
    return p

def P(txt, size=9.5, color=None, bold=False, sb=1, sa=3, indent=0):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(sb); p.paragraph_format.space_after = Pt(sa)
    if indent:
        p.paragraph_format.left_indent = Cm(indent)
    setrun(p.add_run(txt), size, bold, color)
    return p

def CODE(txt):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(2); p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.left_indent = Cm(0.4)
    setrun(p.add_run(txt), 8.8, False, (0x1B, 0x40, 0x2E), 'Consolas')
    return p

def table(header, rows, widths, hfill="1F3B73"):
    t = doc.add_table(rows=1, cols=len(header))
    t.style = "Table Grid"; t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    tr = t.rows[0]._tr
    trPr = tr.get_or_add_trPr()
    th = OxmlElement('w:tblHeader'); th.set(qn('w:val'), 'true'); trPr.append(th)
    for i, h in enumerate(header):
        c = t.rows[0].cells[i]; c.text = ""
        pp = c.paragraphs[0]
        pp.paragraph_format.space_before = Pt(1); pp.paragraph_format.space_after = Pt(1)
        setrun(pp.add_run(str(h)), 8.5, True, (255, 255, 255))
        shade(c, hfill)
    for ri, row in enumerate(rows):
        cells = t.add_row().cells
        for i, v in enumerate(row):
            cells[i].text = ""
            pp = cells[i].paragraphs[0]
            pp.paragraph_format.space_before = Pt(0.5); pp.paragraph_format.space_after = Pt(0.5)
            setrun(pp.add_run(str(v)), 8.3)
            if ri % 2 == 1:
                shade(cells[i], "EDF2F9")
    for r_ in t.rows:
        for i, c in enumerate(r_.cells):
            c.width = Cm(widths[i])
    return t

# ---- 封面/标题 ----
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.paragraph_format.space_before = Pt(6); p.paragraph_format.space_after = Pt(2)
setrun(p.add_run("韩Mir2 音效系统规则与写入指南"), 19, True, (0x1F, 0x3B, 0x73))
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
setrun(p.add_run("SoundList.lst 解析规则 · 技能与怪物音效编号 · 文件名写入方法"), 10, False, (0x60, 0x60, 0x60))
p = doc.add_paragraph(); p.alignment = WD_ALIGN_PARAGRAPH.CENTER
setrun(p.add_run("源码基准：D:\\mir2-20241027（Crystal 血统 · .NET 8）　｜　"
                 "数据基准：20260915Mir2Server/ClientResources/Sound/SoundList.lst"), 8.3, False, (0x88, 0x88, 0x88))

# ================= 一 =================
H("一、音效体系总览：三层结构", 1)
P("客户端播放任何一个音效，都只调用一个入口：SoundManager.PlaySound(索引号)。"
  "索引号如何变成一个具体音频文件，由下面三层依次决定。", 9.5)
table(["层级", "位置", "作用", "是否必需"],
      [["① 内置常量", "Client/MirSounds/SoundList.cs", "代码里写死的固定编号（按钮、走路、挥砍、受击、坐骑、宠物）", "必需"],
       ["② 索引表映射", "Sound/SoundList.lst", "把「索引号」映射成「任意文件名」，可覆盖①以外的全部编号", "可选"],
       ["③ 默认命名回退", "SoundManager.PlaySound 内置", "索引未出现在 .lst 中时，按公式自动推算文件名", "自动生效"]],
      [2.4, 5.0, 8.6, 1.8])
P("优先级：② > ③。也就是说——只要 .lst 里写了这个索引，就用 .lst 指定的文件；没写才走公式推算。", 9.5, (0xC0, 0x00, 0x00), True, sb=4)
P("真实代码（SoundManager.PlaySound，第 112~119 行）：", 9.5, sb=5)
CODE('if (!_indexList.ContainsKey(index))\n'
     '{\n'
     '    string filename = index > 20000 ?\n'
     '        string.Format("M{0:0}-{1:0}", (index - 20000) / 10, index % 10) :\n'
     '        string.Format("{0:000}-{1:0}", index / 10, index % 10);\n'
     '    _indexList.Add(index, filename);\n'
     '}')
P("注意：这条回退只写进内存表，不会落盘改写 SoundList.lst。", 8.8, (0x88, 0x88, 0x88))

# ================= 二 =================
H("二、SoundList.lst 的格式与解析规则", 1)
H("2.1 文件位置", 2)
table(["项目", "值"],
      [["路径", "客户端 ini 的 SoundPath（默认 .\\Sound\\）+ SoundList.lst"],
       ["本服实际位置", "ClientResources\\Sound\\SoundList.lst（另有 20260915更新\\SoundList.lst 一份）"],
       ["编码", "UTF-8（无 BOM）；行尾为 CRLF（Windows）"],
       ["本服规模", "%d 行 / %d 条有效映射 / %d 个唯一索引 / %d 行注释" % (len(lines), len(entries), len(idx_map), len(sections))],
       ["支持的音频格式", ".wav、.mp3（写死在 SoundManager.SupportedFileTypes）"]],
      [3.4, 14.4])
H("2.2 单行语法", 2)
P("每一行的形式是「索引 : 文件名」，索引与文件名之间用冒号或 Tab 分隔。", 9.5)
table(["规则", "说明", "示例"],
      [["分隔符", "冒号 : 或 Tab（两个都支持）", "10001: 1.wav　或　10001<Tab>1.wav"],
       ["空格会被全部删除", "解析前 Replace(\" \", \"\")，所以空格数量随便打", "10001:  　1.wav  →  10001:1.wav"],
       ["取第一段当索引", "按 : 和 Tab 切分后，split[0] 必须能转 int", "拆成 [\"10001\",\"1.wav\"]"],
       ["取最后一段当文件名", "split[split.Length-1]，所以文件名里再有冒号也不怕", "文件名可含空格以外的任意字符"],
       ["索引不是数字 → 整行忽略", "所以 ; 或 ;; 开头的注释天然被跳过", ";;MON -> Skeleton　被忽略"],
       ["索引重复 → 先出现的生效", "代码 if (!indexes.ContainsKey(index)) Add(...)", "见第七章重复索引清单"],
       ["文件名留空 → 该索引静音", "映射到空字符串，找不到文件即无声", "9000:　（空值）"],
       ["文件名可带或不带扩展名", "不带扩展名时按 .wav → .mp3 顺序探测", "10001: 1　也能找到 1.wav"]],
      [3.6, 8.4, 6.6])
H("2.3 真实文件片段", 2)
CODE(';;Movement\n'
     '10001:  1.wav\n'
     '10002:  2.wav\n'
     '...\n'
     '10048:  M42-2.wav          ← 同一个索引被改指向技能音效文件\n'
     '10049:  game-over2.wav\n'
     ';;Swing\n'
     '10050:  50.wav\n'
     ';;MON -> Skeleton--------MON3-----------OK\n'
     '0200:                     ← 该索引留空 = 静音\n'
     '0201:  400-1.wav')
H("2.4 文件缺失与兜底", 2)
table(["情形", "客户端行为"],
      [["文件存在但一条都解析不出", "视为「未加载」，返回后下次再试（不会用空表覆盖）"],
       ["某索引指向的文件不存在", "该次播放无声；微端模式下会尝试按需下载该文件"],
       ["索引完全没有映射", "按第三章公式推算文件名后再去找；仍找不到则该次无声"]],
      [5.6, 12.2])

# ================= 三 =================
H("三、索引编号规则", 1)

H("3.1 内置固定段（写死在 SoundList.cs，10001 ~ 10520）", 2)
P("这一段是代码里的常量，不依赖 .lst，但 .lst 里也重新指了一遍文件名（便于换成韩版资源）。", 9.5)
builtin = [
    ("10001~10039", "移动 / 跑动（地面、石板、草地、泥地、木地板、洞穴、房间、水面、雪地、马）", "WalkGroundL … RunSnowR"),
    ("10050~10057", "挥砍音（短兵、木、剑、剑2、斧、棒、长兵、拳）", "SwingShort … SwingFist"),
    ("10060~10090", "受击音（打在武器上 / 打在身体上 / 打在盔甲上 / 恶魔之剑）", "StruckShort … StruckEvilMir"),
    ("10100~10156", "界面音（登录、按钮、金币、吃药、点击各部位、传送、升级）", "LoginEffect … LevelUp"),
    ("10138~10145", "人声（男/女 受击喊声、男/女 死亡）", "MaleFlinch … FemaleDie"),
    ("10176~10236", "坐骑（老虎/狼/象头神/乌龟/蟹 的走、跑、攻击、受击）", "MountWalkL … WolfStruck2"),
    ("10184~10187", "钓鱼（抛竿、收杆、中鱼）", "FishingThrow / Pull / Fishing"),
    ("10500~10520", "宠物（猪、小鸡、猫、骷髅、猪人、龙、青蛙、猴、愤怒鸟、狐狸、医疗鼠、拾取）", "PetPig … PetPickup"),
]
table(["索引区间", "用途", "代表常量"], builtin, [2.9, 11.4, 3.5])

H("3.2 怪物音效：图档号 × 10 + 动作偏移", 2)
P("这是最关键的一条规则。怪物音效的索引不是手写的，而是由怪物的「图档号」算出来的：", 9.5)
CODE('BaseSound = (ushort)BaseImage * 10;        // MonsterObject.cs 第 210 行\n'
     'BaseImage = info.Image;                    // 来自服务端怪物表的 Image 字段\n'
     '// 播放时：SoundManager.PlaySound(BaseSound + 偏移)')
P("而服务端怪物表 Server/MirDatabase/MonsterInfo.cs 里，Image 字段的类型正是 Monster 枚举：", 9.5, sb=5)
CODE('public Monster Image;     // 即 Shared/Enums.cs 里的 Monster 枚举值，也就是图档号')
P("所以：索引 = Monster枚举值 × 10 + 偏移。偏移的含义如下（提取自 MonsterObject 的 Play*Sound 系列方法）：", 9.5, sb=6)
offsets = [
    ("+0", "出现 / 待机 / 召唤", "PlayAppearSound、PlayPopupSound、PlaySummonSound 默认"),
    ("+1", "平时音 / 被推动 / 多数远程攻击", "PlayRangeSound 命中的大部分怪物、PlayDeadSound、PlayPopupSound"),
    ("+2", "攻击", "PlayAttackSound 默认（近身物理攻击）"),
    ("+3", "挥动武器 / 行走", "PlaySwingSound"),
    ("+4", "受击（受伤喊声）", "PlayFlinchSound 默认"),
    ("+5", "死亡", "PlayDieSound 默认"),
    ("+6", "第二段攻击 / 二次出现", "PlaySecondAttackSound 默认"),
    ("+7", "第三段攻击", "PlayThirdAttackSound 默认"),
    ("+8", "第四段攻击 / 部分怪行走·跳跃·奔跑", "PlayFourthAttackSound 默认、PlayWalkSound、PlayRunSound、PlayJumpSound"),
    ("+9", "第五段攻击 / 部分怪行走·冲刺", "PlayFithAttackSound、PlayWalkSound、PlayDashSound"),
]
table(["偏移", "含义", "调用位置（代码出处）"], offsets, [1.5, 5.4, 10.9])
P("以骷髅（Skeleton）为例：枚举值 = 20 → 索引 200~209 → 攻击音 = 202 → .lst 里写的是 "
  "0202: 400-2.wav。所以骷髅的攻击音实际播放的是 Sound\\400-2.wav。", 9.5, sb=5,
  color=(0x1F, 0x3B, 0x73), bold=True)
P("官方注释：SoundList.lst 文件末尾留有一段原作者注释，写的是「200 + ImgIndex×10」——"
  "那是韩版原客户端的编号，文件也是按老编号命名的（骷髅 → 400-x.wav）。"
  "本工程（Crystal 血统）把索引改成了 ImgIndex×10，于是靠 .lst 把「新索引」重新指回「老文件名」。"
  "两套编号并存，这就是为什么 .lst 的怪物段索引和文件名看起来对不上。", 9.5, sb=6, color=(0xA0, 0x30, 0x30))

H("3.3 技能音效：20000 + 技能编号 × 10 + 变体", 2)
P("技能音效走的是 20000 段，索引由 Spell 枚举值算出：", 9.5)
CODE('SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 变体);\n'
     '// 例：火球术 Spell.FireBall = 31  →  20000 + 310 + 0 = 20310\n'
     '// 默认命名回退：index > 20000  →  "M{(i-20000)/10}-{i%10}.wav"  →  M31-0.wav')
P("也就是说，技能音效的文件名天生就是 M{技能编号}-{变体}.wav。常见变体含义：", 9.5, sb=5)
table(["变体", "含义", "举例（代码出处）"],
      [["+0", "起手 / 施法音（最常用，占绝大多数）", "FireBall、Healing、HellFire 等 75 处调用"],
       ["+1", "命中 / 二段 / 附加音", "FlamingSword（+1）、EnergyShield（+1）、HellFire（+1）"],
       ["+2", "第三段 / 附加音", "FrostCrunch（+2）、部分技能的二次效果"],
       ["+0 / +1", "按性别区分：男性用 +0、女性用 +1", "Slaying（20000 + Spell*10 + (Gender==男性?0:1)）"],
       ["+5 / +6", "持续型技能的循环层", "Effect.cs 中 20000 + 126*10 + 4 + myType"]],
      [1.6, 7.2, 9.0])
P("关键结论：本服 SoundList.lst 里**没有任何一条 20xxx 索引**（已核实：0 条）。"
  "所以技能音效完全依赖上面的默认命名回退——只要你把文件命名成 M{编号}-{变体}.wav 放进 Sound 目录，"
  "技能就有声音，不需要改 .lst。", 9.5, sb=6, color=(0xC0, 0x00, 0x00), bold=True)
P("但有一个前提：客户端播放技能音效**不是全局兜底的**，而是在 PlayerObject.ProcessAction 的「施法动作」"
  "switch 里逐个 case 显式调用（已核实：该 switch 没有 default 兜底分支）。也就是说——", 9.5, sb=6)
table(["情形", "要不要改代码", "做法"],
      [["已有技能、只想让它出声", "不用改代码", "把 M{编号}-{变体}.wav 放进 Sound 目录即可（前提是该技能在代码里已有 case 调用，见第五章「代码已用变体」列）"],
       ["已有技能、但代码里没有它的 case", "要改代码", "在 PlayerObject.ProcessAction 的施法动作 switch 里加一个 case，调用 SoundManager.PlaySound(20000 + (ushort)Spell.技能名 * 10)"],
       ["全新技能", "要改代码", "① 扩展 Shared/Enums.cs 的 Spell 枚举；② 服务端加对应 SpellInfo/脚本逻辑；③ 客户端加 case 与特效；④ 准备 M{新编号}-0.wav"]],
      [4.4, 2.6, 10.8])
P("本次统计：159 个技能里，代码里已有显式音效调用的有 90 个；第五章技能表的「代码已用变体」列为空即代表"
  "该技能在当前源码中没有音效调用（多为被动技能或纯增益技能）。", 9.5, sb=5,
  color=(0x1F, 0x3B, 0x73))

H("3.4 未写进 .lst 时的默认命名公式（速查）", 2)
table(["索引范围", "文件名公式", "示例"],
      [["≤ 20000", "{索引/10 补足3位}-{索引%10}.wav", "索引 202 → 020-2.wav；索引 10001 → 1000-1.wav"],
       ["> 20000", "M{(索引-20000)/10}-{索引%10}.wav", "索引 20310 → M31-0.wav；索引 20311 → M31-1.wav"]],
      [3.0, 7.4, 7.4])
P("注：怪物的默认命名是 {图档号:3位}-{偏移}.wav（如骷髅 020-2.wav）；而韩版资源实际叫 400-2.wav。"
  "两套都对不上时，就必须靠 .lst 校正。", 8.8, (0x88, 0x88, 0x88), sb=4)

# ================= 四 =================
H("四、文件名与列表的写入方法（按场景）", 1)
H("场景 A：给技能加 / 换音效（不必改 .lst，最省事）", 2)
steps_a = [
    "① 查出技能编号：见第六章技能表，或查 Shared/Enums.cs 的 Spell 枚举（文件里的字节值就是编号）。",
    "② 把音频文件命名为 M{编号}-{变体}.wav，例如火球术 = M31-0.wav、地狱火 = M35-0.wav。",
    "③ 放到客户端声音目录（本服为 ClientResources\\Sound\\，客户端 ini 的 SoundPath）。",
    "④ 完成——.lst 里没有 20xxx 条目，客户端会按默认公式直接找到该文件名。",
    "⑤ 前提：该技能在客户端代码里已有 case 调用（第五章「代码已用变体」列非空）。若为空，还得在 "
    "PlayerObject.ProcessAction 的施法 switch 里补一行 PlaySound，详见 3.3 节。",
    "⑥ 微端模式还要把该文件加进资源清单，否则玩家端不会自动下载。",
]
for s in steps_a:
    P(s, 9.5, sb=1, sa=1, indent=0.35)
P("要换名字也行：在 .lst 里加一行「索引: 自定义名.wav」，索引 = 20000 + 编号×10 + 变体。", 9.5, sb=4, indent=0.35)

H("场景 B：给怪物加 / 换音效（两条路）", 2)
P("方案 1（推荐，与韩版资源一致）——改 .lst：", 9.5, bold=True, sb=4, indent=0.35)
for s in ["① 确定怪物图档号：查第六章怪物表，或看服务端怪物表 Image 字段（类型就是 Monster 枚举）。",
          "② 算出索引 = 图档号 × 10 + 动作偏移（偏移表见 3.2 节）。",
          "③ 在 .lst 里对应位置加一行，格式「索引: 文件名」，例如给骷髅加攻击音：0202: 400-2.wav。",
          "④ 一行一个动作，需要几个动作就写几行；不写的动作保持静音。"]:
    P(s, 9.5, sb=1, sa=1, indent=0.6)
P("方案 2（不改 .lst）——靠默认命名：", 9.5, bold=True, sb=4, indent=0.35)
for s in ["① 把音频命名成 {图档号 3 位补零}-{偏移}.wav，例如骷髅攻击音命名 020-2.wav。",
          "② 放进 Sound 目录即可，客户端推算出的名字正好命中。",
          "③ 缺点：与韩版原有文件名（400-2.wav）不统一，混用容易乱，建议整服统一走方案 1。"]:
    P(s, 9.5, sb=1, sa=1, indent=0.6)

H("场景 C：新增怪物时怎么定编号", 2)
for s in ["① 先在客户端图库里确定该怪物用哪张 MonN.wil 的第几个图像段，取到图档号。",
          "② 服务端怪物表 Image 填这个图档号（Monster 枚举值，超出枚举范围时自行扩展枚举）。",
          "③ 音效索引 = 图档号 × 10 + 偏移；在 .lst 里为本服自定义怪物单独开一段，例如：",
          "　　;;MON -> 我的新怪",
          "　　5650:  mymon_appear.wav",
          "　　5652:  mymon_attack.wav",
          "　　5654:  mymon_hurt.wav",
          "　　5655:  mymon_die.wav"]:
    P(s, 9.5, sb=1, sa=1, indent=0.35)
P("（本服 .lst 里已有大量这种自定义怪物段，如 IceMon17、GID1Mon07a、chaoslord 等，"
  "用的都是 4000~6500 段的自定义图档号，写法与内置怪物完全一样。）", 8.8, (0x88, 0x88, 0x88), sb=3)

H("场景 D：脚本里播放任意音效", 2)
CODE('# NPC 脚本（.txt）\n'
     'PLAYSOUND 10110        ← 播放索引 10110（传送音）\n'
     '\n'
     '// 服务端实现：NPCSegment.cs ActionType.PlaySound\n'
     'player.Enqueue(new S.PlaySound { Sound = soundID });\n'
     '// 客户端：GameScene.PlaySound(S.PlaySound) → SoundManager.PlaySound(索引)')
P("脚本里能播的索引范围和上面完全一致：10001~10520 内置段、图档号×10 的怪物段、20000+ 的技能段，"
  "以及 .lst 里自定义的任何索引。", 9.5, sb=3)

H("场景 E：屏蔽某个音效（静音）", 2)
P("在 .lst 里把该索引的文件名留空即可，例如「9000:　」——客户端映射到空文件名，播放时无声。"
  "本服 EvilMir 段就是这样屏蔽掉 +5（死亡）音的。", 9.5)

H("4.6 写入时的注意事项（踩坑清单）", 2)
tips = [
    ["编码", "必须 UTF-8。存成 GBK 会让中文注释乱码，但不影响解析（注释本来就被忽略）"],
    ["行尾", "CRLF 或 LF 都能解析；建议与现有一致用 CRLF"],
    ["空格", "索引与文件名之间的空格数量随意，解析前会被全部删除；文件名内部也**不能**带空格（会被吃掉）"],
    ["注释", "用「;」或「;;」开头。注意「//」开头也「碰巧」有效，因为第一段无法转成数字会被跳过"],
    ["重复索引", "以先出现的为准，后面的静默失效。本服有 6 组重复，见第七章"],
    ["空文件名", "等于静音，可主动用来屏蔽音效"],
    ["扩展名", "只支持 .wav / .mp3；不写扩展名时按 .wav → .mp3 顺序探测"],
    ["改完要重进游戏", "索引表在客户端启动时加载；游戏内改 .lst 不会热更新"],
    ["微端", "新加的音频文件必须进资源清单，否则玩家端下载不到（会走负缓存，不再重试）"],
]
table(["要点", "说明"], tips, [2.6, 15.2])

doc.add_page_break()

# ================= 五 =================
H("五、技能音效完整对照表（Spell 全量 %d 项）" % len(spells), 1)
P("「默认文件名」= 不写 .lst 时客户端会去找的文件名。变体列 = 代码里实际用到过的变体（空表示未检索到显式调用，"
  "多为被动/无音效技能）。", 8.8, (0x60, 0x60, 0x60))
rows5 = []
for n, v in spells:
    if v == 0:
        continue
    vars_ = sorted(spell_variants.get(v, []))
    fs = spell_files(v)
    rows5.append([n, v, "20000 + %d*10 = %d" % (v, 20000 + v * 10),
                  "M%d-{0..9}.wav" % v,
                  ("+%s" % "、+".join(map(str, vars_))) if vars_ else "—",
                  ("有 %d 个" % len(fs)) if fs else "缺"])
t5 = table(["技能名（Spell）", "编号", "索引算法", "默认命名", "代码已用变体", "资源"], rows5,
           [3.6, 1.1, 4.2, 3.0, 3.0, 1.4])
P("共 %d 个技能（不含 None）。「资源」列统计的是本服 Sound 目录里已存在的 M{编号}-x.wav 文件数。" % (len(rows5)), 8.6,
  (0x88, 0x88, 0x88), sb=3)

doc.add_page_break()

# ================= 六 =================
H("六、怪物音效完整对照表（Monster 全量 %d 项）" % len(monsters), 1)
P("图档号 = Monster 枚举值 = 服务端怪物表 Image 字段。索引区间 = 图档号×10 ~ ×10+9（对应 +0 ~ +9 偏移）。"
  "「.lst 已写」为该区间在 SoundList.lst 里已有的条目数。", 8.8, (0x60, 0x60, 0x60))
rows6 = []
for n, v in monsters:
    lst_e = mon_by_img.get(v, [])
    fnames = collections.OrderedDict()
    for e in lst_e:
        if e["file"]:
            fnames[e["file"]] = fnames.get(e["file"], 0) + 1
    sample = "、".join(list(fnames.keys())[:2])
    rows6.append([n, v, "%d ~ %d" % (v * 10, v * 10 + 9),
                  "%03d-{0..9}.wav" % v,
                  len(lst_e), sample if sample else "—"])
table(["怪物名（Monster）", "图档号", "索引区间", "默认命名", ".lst 已写", ".lst 实际指向文件（示例）"], rows6,
      [3.6, 1.2, 2.1, 2.5, 1.3, 7.1])

# 自定义图档号（不在 Monster 枚举里）
custom = sorted(k for k in mon_by_img if k not in mon_enum_by_img)
if custom:
    doc.add_page_break()
    H("6.2 SoundList.lst 中不属于内置怪物枚举的图档号（自定义怪）", 2)
    P("共 %d 个自定义图档号。这些是脚本/副本私加的怪物，写法与内置怪物完全一致（索引 = 图档号×10 + 偏移）。" % len(custom), 9.5)
    rows6b = []
    for k in custom:
        es = mon_by_img[k]
        fs = [e["file"] for e in es if e["file"]]
        sec = es[0]["section"]
        rows6b.append([k, "%d ~ %d" % (k * 10, k * 10 + 9), len(es), sec[:26],
                       "、".join(fs[:2]) if fs else "—"])
    table(["图档号", "索引区间", "条目数", "所在章节", "指向文件（示例）"], rows6b, [1.6, 2.4, 1.4, 5.2, 6.8])

# ================= 七 =================
doc.add_page_break()
H("七、SoundList.lst 现状体检报告", 1)
miss = [e for e in entries if e["file"] and not e["exists"]]
dupents = [e for e in entries if e["dup"]]
dupidx = sorted(k for k, v in dup.items() if len(v) > 1)
used = set(os.path.basename(e["file"]) for e in entries if e["file"])
unref = sorted(x for x in files_in_dir if x.lower().endswith(('.wav', '.mp3')) and x not in used)
blank = [e for e in entries if not e["file"]]

sumrows = [
    ["有效映射条目", "%d 条" % len(entries)],
    ["唯一索引", "%d 个" % len(idx_map)],
    ["注释行（章节标题）", "%d 行，共 %d 个章节" % (sum(1 for l in lines if l.strip().startswith(';')), len(sections))],
    ["无法解析被跳过的行", "%d 行" % len(skipped)],
    ["指向不存在文件的条目", "%d 条（%.1f%%）" % (len(miss), 100.0 * len(miss) / len(entries))],
    ["重复索引", "%d 个索引、%d 条被静默覆盖" % (len(dupidx), len(dupents) - len(dupidx))],
    ["文件名留空（静音）的条目", "%d 条" % len(blank)],
    ["Sound 目录音频文件", "%d 个" % len(files_in_dir)],
    ["未被 .lst 引用的音频", "%d 个（只能用默认命名命中）" % len(unref)],
]
table(["体检项", "结果"], sumrows, [5.6, 12.2])

H("7.1 指向不存在文件的条目（播放无声）", 2)
table(["行号", "索引", "文件名", "所在章节"], [[e["lineno"], e["index"], e["file"], e["section"][:34]] for e in miss],
      [1.4, 1.7, 5.6, 9.1])

H("7.2 重复索引（后出现的失效）", 2)
rows72 = []
for k in dupidx:
    for i, e in enumerate(dup[k]):
        rows72.append([k, e["lineno"], e["file"] or "(空=静音)", "生效" if i == 0 else "★失效"])
table(["索引", "行号", "文件名", "结果"], rows72, [1.6, 1.6, 8.0, 2.6])

H("7.3 未被引用的音频文件（%d 个，节选）" % len(unref), 2)
P("、".join(unref[:60]) + ("　…（共 %d 个）" % len(unref)), 8.6, (0x60, 0x60, 0x60))

H("7.4 结论与建议", 2)
concl = [
    "技能音效（20000 段）一条都没写进 .lst，全部依赖 M{编号}-{变体}.wav 默认命名 —— 这是可用的，但一旦想换成语义化文件名（如 fireball.wav）就必须补 .lst。",
    "%d 条映射指向了不存在的文件，这些索引播放时无声，建议逐条补齐文件或直接改成空值明确静音。" % len(miss),
    "%d 组重复索引里，1801/1802、5347、5446、10217 的后一条被静默覆盖，等于白写；确认哪一条才是想要的，删掉另一条。" % len(dupidx),
    "%d 个音频文件没被 .lst 引用，其中若确有需要播放的，请补上索引条目，或统一改成默认命名。" % len(unref),
    "本服有 %d 行使用 Tab 作分隔符（如 3442\\tbabyfishman_atk1.wav），语法合法、能正常解析；但为便于阅读建议统一改成冒号。" % 29,
    "怪物段建议整服统一走「.lst 显式映射」方案，避免默认命名（020-2.wav）与韩版资源名（400-2.wav）两套并存。",
]
for c in concl:
    P("• " + c, 9.5, sb=1, sa=2, indent=0.3)

docx_path = os.path.join(OUT, "韩Mir2音效系统规则与写入指南.docx")
doc.save(docx_path)
print("已生成:", docx_path)

# ---------------- 6. Excel ----------------
wb = openpyxl.Workbook()
hdr_fill = PatternFill("solid", fgColor="1F3B73")
hdr_font = Font(name="微软雅黑", size=9.5, bold=True, color="FFFFFF")
cell_font = Font(name="微软雅黑", size=9)
warn_font = Font(name="微软雅黑", size=9, color="C00000")
thin = Side(style="thin", color="BBBBBB")
bd = Border(left=thin, right=thin, top=thin, bottom=thin)

def sheet(name, header, rows, widths, warn_cols=()):
    ws = wb.create_sheet(name)
    ws.append(header)
    for c in range(1, len(header) + 1):
        cell = ws.cell(1, c)
        cell.fill = hdr_fill; cell.font = hdr_font
        cell.alignment = Alignment(horizontal="center", vertical="center")
        cell.border = bd
    for r in rows:
        ws.append(r)
    for i, w in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(i)].width = w
    for r in range(2, ws.max_row + 1):
        for c in range(1, len(header) + 1):
            cell = ws.cell(r, c)
            cell.font = warn_font if c in warn_cols else cell_font
            cell.border = bd
            cell.alignment = Alignment(vertical="center")
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = "A1:%s%d" % (get_column_letter(len(header)), ws.max_row)
    return ws

# Sheet1 索引表
sheet("SoundList索引表",
      ["行号", "索引", "文件名", "文件是否存在", "重复索引", "本条是否生效", "所在章节"],
      [[e["lineno"], e["index"], e["file"] or "(空=静音)", "是" if e["exists"] else ("空值" if not e["file"] else "否"),
        "是" if e["dup"] else "", "是" if e["effective"] else "失效", e["section"]] for e in entries],
      [7, 8, 30, 12, 10, 12, 34])

# Sheet2 技能
sheet("技能音效表",
      ["技能名", "编号", "索引(+0)", "默认文件名", "代码已用变体", "资源已存在文件数"],
      [[n, v, 20000 + v * 10, "M%d-{0..9}.wav" % v,
        "、".join("+%d" % x for x in sorted(spell_variants.get(v, []))) or "—",
        len(spell_files(v))] for n, v in spells],
      [26, 7, 12, 16, 20, 16])

# Sheet3 怪物
sheet("怪物音效表",
      ["怪物名", "图档号", "索引下界", "索引上界", "默认命名", ".lst已写条目", ".lst指向文件"],
      [[n, v, v * 10, v * 10 + 9, "%03d-{0..9}.wav" % v, len(mon_by_img.get(v, [])),
        "、".join(collections.OrderedDict.fromkeys(e["file"] for e in mon_by_img.get(v, []) if e["file"])) or "—"]
       for n, v in monsters],
      [26, 8, 9, 9, 16, 12, 46])

# Sheet4 自定义图档号
sheet("自定义图档号",
      ["图档号", "索引下界", "索引上界", "条目数", "所在章节", "指向文件"],
      [[k, k * 10, k * 10 + 9, len(mon_by_img[k]), mon_by_img[k][0]["section"],
        "、".join(collections.OrderedDict.fromkeys(e["file"] for e in mon_by_img[k] if e["file"])) or "(空)"]
       for k in custom],
      [10, 10, 10, 9, 34, 52])

# Sheet5 内置固定段
sheet("内置固定段",
      ["索引", "常量名", "含义", "索引", "常量名", "含义"],
      [["10001", "WalkGroundL", "走路-地面-左", "10050", "SwingShort", "挥砍-短兵"],
       ["10002", "WalkGroundR", "走路-地面-右", "10051", "SwingWood", "挥砍-木"],
       ["10003", "RunGroundL", "跑-地面-左", "10052", "SwingSword", "挥砍-剑"],
       ["10004", "RunGroundR", "跑-地面-右", "10053", "SwingSword2", "挥砍-剑2"],
       ["10005", "WalkStoneL", "走路-石板-左", "10054", "SwingAxe", "挥砍-斧"],
       ["10009", "WalkLawnL", "走路-草地-左", "10055", "SwingClub", "挥砍-棒"],
       ["10013", "WalkRoughL", "走路-泥地-左", "10056", "SwingLong/Fist", "挥砍-长兵/拳"],
       ["10017", "WalkWoodL", "走路-木地板-左", "10060", "StruckShort", "受击-短兵"],
       ["10021", "WalkCaveL", "走路-洞穴-左", "10061", "StruckWooden", "受击-木"],
       ["10025", "WalkRoomL", "走路-房间-左", "10062", "StruckSword", "受击-剑"],
       ["10029", "WalkWaterL", "走路-水面-左", "10063", "StruckSword2", "受击-剑2"],
       ["10033", "HorseWalkL", "马走-左", "10064", "StruckAxe", "受击-斧"],
       ["10035", "HorseRun", "马跑", "10065", "StruckClub", "受击-棒"],
       ["10036", "WalkSnowL", "走路-雪地-左", "10070", "StruckBodySword", "受击(身体)-剑"],
       ["10100", "LoginEffect", "登录音效", "10080", "StruckArmourSword", "受击(盔甲)-剑"],
       ["10103", "ButtonA", "按钮A", "10090", "StruckEvilMir", "受击-恶魔之剑"],
       ["10106", "Gold", "金币", "10138", "MaleFlinch", "男-受击"],
       ["10107", "EatDrug", "吃药", "10139", "FemaleFlinch", "女-受击"],
       ["10110", "Teleport", "传送", "10144", "MaleDie", "男-死亡"],
       ["10111", "ClickWeapon", "点击-武器", "10145", "FemaleDie", "女-死亡"],
       ["10118", "ClickItem", "点击-物品", "10156", "LevelUp", "升级"],
       ["10176", "MountWalkL", "坐骑走-左", "10500", "PetPig", "宠物-猪"],
       ["10178", "MountRun", "坐骑跑", "10520", "PetPickup", "宠物-拾取"],
       ],
      [9, 20, 20, 9, 20, 20])

# Sheet6 偏移表
sheet("怪物动作偏移表",
      ["偏移", "含义", "代码出处"],
      [[o[0], o[1], o[2]] for o in offsets], [7, 40, 70])

# Sheet7 问题清单
rows7 = []
for e in miss:
    rows7.append(["文件缺失", e["lineno"], e["index"], e["file"], e["section"], "播放无声"])
for k in dupidx:
    for i, e in enumerate(dup[k]):
        rows7.append(["重复索引", e["lineno"], k, e["file"] or "(空=静音)", e["section"],
                      "生效" if i == 0 else "★被静默覆盖"])
sheet("问题清单", ["问题类型", "行号", "索引", "文件名", "章节", "说明"], rows7, [12, 8, 9, 28, 30, 20])

del wb["Sheet"]
xlsx_path = os.path.join(OUT, "韩Mir2音效索引与规则表.xlsx")
wb.save(xlsx_path)
print("已生成:", xlsx_path)
print("缺失文件 %d 条；重复索引 %d 组；未引用音频 %d 个；自定义图档号 %d 个"
      % (len(miss), len(dupidx), len(unref), len(custom)))
