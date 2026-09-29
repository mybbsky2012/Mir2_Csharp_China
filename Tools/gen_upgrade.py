# -*- coding: utf-8 -*-
"""
生成 7~45 级「升级发放装备 + 技能」NPC 脚本。

数据源（全部来自当前运行库，保证名字真实存在）：
    Tools/magic_dump.csv   技能表（DbDump 从 Server.MirDB 导出）
    Tools/item_dump.csv    物品表（同上）
    Exports/2_物品数据.csv  仅用于取「技能 -> 职业」归属

规则（经用户确认）：
    * 每 5 级一档：7/10/15/20/25/30/35/40/45
    * 分职业：武器、衣服按职业；首饰/头盔/腰带/靴子用「全职业」件
    * 每档发全套九件：武器 + 衣服 + 头盔 + 项链 + 戒指x2 + 手镯x2 + 腰带 + 靴子
    * 技能：7 级时一次性学会 45 级及以内的全部技能（按职业）
"""
import csv, io, re, os, collections

TOOLS = r'D:\BaiduNetdiskDownload\mir2-20241027\Tools'
SRV = r'D:\BaiduNetdiskDownload\20260915更新\20260915Mir2Server'
CSV_ITEM = os.path.join(SRV, r'Exports\2_物品数据.csv')
MAGIC_DUMP = os.path.join(TOOLS, 'magic_dump.csv')
ITEM_DUMP = os.path.join(TOOLS, 'item_dump.csv')
OUT = os.path.join(SRV, r'Envir\SystemScripts\00Default\等级触发\升级装备奖励.txt')

BANDS = [7, 10, 15, 20, 25, 30, 35, 40, 45]
CLASSES = ['战士', '法师', '道士', '刺客', '弓箭']
GENDERS = ['男性', '女性']

# 117 版技能名 -> 110 版技能名（用户运行库是 110 版命名）
ALIAS = {'烦恼': '烦脑', '血龙兽': '血龙水', '万斤闪': '万金闪'}

# 非战斗武器，不发放
WEAPON_BAD = ('钓竿', '鱼竿', '鹤嘴锄', '锄', '镐', '钓', '探鱼')
ARMOR_BAD = ('内裤', '内衣')

SLOTS = [('武器', 1, False), ('盔甲', 1, True), ('头盔', 1, False), ('项链', 1, False),
         ('戒指', 2, False), ('手镯', 2, False), ('腰带', 1, False), ('靴子', 1, False)]


def rd(p):
    with io.open(p, encoding='utf-8-sig', errors='ignore') as f:
        return list(csv.DictReader(f))


# ---------- 技能：名字 -> 职业 ----------
skill_class = {}
for r in rd(CSV_ITEM):
    if r['ItemType'] != '技能书':
        continue
    skill_class[ALIAS.get(r['ItemName'], r['ItemName'])] = r['ItemRequiredClass']

magics = rd(MAGIC_DUMP)
skill_by_class = collections.defaultdict(list)
unmatched_skill = []
for m in magics:
    try:
        lv1 = int(m['Level1'] or 0)
    except ValueError:
        lv1 = 0
    if lv1 <= 0 or lv1 > 45:
        continue
    nm, sp = m['Name'], m['SpellName']
    c = skill_class.get(nm)
    if c is None:
        unmatched_skill.append((nm, sp, lv1))
        continue
    targets = CLASSES if c == '全职业' else (['战士', '法师', '道士'] if c == '战法道' else [c])
    if targets[0] not in CLASSES:
        unmatched_skill.append((nm, sp, lv1))
        continue
    for cc in targets:
        skill_by_class[cc].append((lv1, nm, sp))
for v in skill_by_class.values():
    v.sort()

# ---------- 装备 ----------
items = []
for r in rd(ITEM_DUMP):
    if (r.get('RequiredType') or 'Level') != 'Level':
        continue
    try:
        amt = int(r['RequiredAmount'] or 0)
    except ValueError:
        amt = 0
    items.append({'name': r['Name'], 'type': r['Type'], 'lv': amt,
                  'cls': r['RequiredClass'], 'gender': r['RequiredGender']})


def class_ok(rc, cls):
    if rc == '全职业' or rc == cls:
        return True
    if rc == '战法道' and cls in ('战士', '法师', '道士'):
        return True
    if rc == '战士刺客' and cls in ('战士', '刺客'):
        return True
    return False


def is_variant(nm):
    if '秘籍' in nm:
        return True
    return bool(re.search(r'\d$', nm) or re.search(r'\d{3,}', nm))


def pick(kind, band, cls, prev, gender=None):
    pool = []
    for it in items:
        if it['type'] != kind:
            continue
        L = it['lv']
        if L <= prev or L > band:
            continue
        if not class_ok(it['cls'], cls):
            continue
        if gender and it['gender'] not in ('性别不限', gender):
            continue
        nm = it['name']
        if is_variant(nm):
            continue
        if kind == '武器' and any(b in nm for b in WEAPON_BAD):
            continue
        if kind == '盔甲' and any(b in nm for b in ARMOR_BAD):
            continue
        pool.append((L, nm))
    if not pool:
        return None
    pool.sort(key=lambda x: (-x[0], len(x[1]), x[1]))
    return pool[0]


result = {}
missing_item = []
for bi, band in enumerate(BANDS):
    prev = BANDS[bi - 1] if bi > 0 else 0
    for cls in CLASSES:
        for g in GENDERS:
            got = []
            for kind, cnt, by_gender in SLOTS:
                p = pick(kind, band, cls, prev, g if by_gender else None)
                got.append((kind, p[1] if p else None, cnt))
                if p is None:
                    missing_item.append((band, cls, kind))
            result[(band, cls, g)] = got

# ---------- 生成脚本 ----------
L = []
A = L.append
A('; ============================================================')
A(';  7~45 级升级奖励：装备 + 技能')
A(';')
A(';  本文件由 Tools/gen_upgrade.py 自动生成，请勿手工修改。')
A(';  挂载方式：LevelUp.txt 的 [@_LevelUp] 页通过 #INCLUDE 引入本文件。')
A(';')
A(';  规则：')
A(';    * 每 5 级一档：7/10/15/20/25/30/35/40/45')
A(';    * 每档发全套九件：武器+衣服+头盔+项链+戒指x2+手镯x2+腰带+靴子')
A(';    * 武器装备、衣服按职业发放；其余用全职业通用件')
A(';    * 7 级时一次性学会 45 级及以内的全部技能')
A('; ============================================================')
A('')
A('[@Main]')
A('{')

# --- 技能 ---
A('; ---------- 7 级：一次性学会 45 级以内全部技能 ----------')
for cls in CLASSES:
    sk = skill_by_class.get(cls, [])
    if not sk:
        continue
    A('#IF')
    A('LEVEL == 7')
    A('CHECKCLASS %s' % cls)
    A('#ACT')
    for lv1, nm, sp in sk:
        A('GIVESKILL %s 0' % sp)
    A('LOCALMESSAGE "【升级奖励】%s 已学会 45 级以内的全部技能（共 %d 个）。" Hint' % (cls, len(sk)))
    A('')

# --- 装备 ---
A('; ---------- 装备发放 ----------')
for band in BANDS:
    for cls in CLASSES:
        for g in GENDERS:
            got = [(k, n, c) for k, n, c in result[(band, cls, g)] if n]
            if not got:
                continue
            A('#IF')
            A('LEVEL == %d' % band)
            A('CHECKCLASS %s' % cls)
            A('CHECKGENDER %s' % g)
            A('#ACT')
            for kind, nm, cnt in got:
                A('GIVEITEM %s%s' % (nm, '' if cnt == 1 else ' %d' % cnt))
            A('LOCALMESSAGE "【升级奖励】%d 级 %s 装备已发放，请查看背包。" Hint' % (band, cls))
            A('')

A('}')

os.makedirs(os.path.dirname(OUT), exist_ok=True)
io.open(OUT, 'w', encoding='utf-8', newline='\r\n').write('\n'.join(L))

# ---------- 发放清单 ----------
LIST_OUT = os.path.join(os.path.dirname(SRV), '升级奖励发放清单.md')
D = []
B = D.append
B('# 7~45 级升级奖励发放清单')
B('')
B('> 由 `Tools/gen_upgrade.py` 生成，内容与 `Envir/SystemScripts/00Default/等级触发/升级装备奖励.txt` 一致。<br>')
B('> 「男/女」表示该件按角色性别自动发放对应版本；「x2」表示发两个（戒指、手镯）。')
B('')
B('## 一、技能（7 级一次性学会，45 级及以内）')
B('')
B('| 职业 | 数量 | 技能 |')
B('|---|---|---|')
for c in CLASSES:
    sk = skill_by_class.get(c, [])
    B('| %s | %d | %s |' % (c, len(sk), '、'.join('%s(%d)' % (n, l) for l, n, s in sk)))
B('')
B('## 二、装备（每 5 级一档，全套九件）')
B('')
for c in CLASSES:
    B('### %s' % c)
    B('')
    B('| 等级 | 武器 | 衣服 | 头盔 | 项链 | 戒指 | 手镯 | 腰带 | 靴子 |')
    B('|---|---|---|---|---|---|---|---|---|')
    for band in BANDS:
        cells = {}
        for g in GENDERS:
            for k, n, cnt in result[(band, c, g)]:
                cells.setdefault(k, {})[g] = n
        def cell(k):
            v = cells.get(k)
            if not v:
                return '—'
            names = []
            for g in GENDERS:
                if v.get(g) and v[g] not in names:
                    names.append(v[g])
            txt = ' / '.join(names)
            # 男/女同款名去掉性别后缀合并显示
            a = v.get('男性'); b = v.get('女性')
            if a and b and a != b and a.replace('(男)', '') == b.replace('(女)', ''):
                txt = a.replace('(男)', '') + '(男/女)'
            if k in ('戒指', '手镯'):
                txt += ' x2'
            return txt
        B('| %d | %s | %s | %s | %s | %s | %s | %s | %s |' % (
            band, cell('武器'), cell('盔甲'), cell('头盔'), cell('项链'),
            cell('戒指'), cell('手镯'), cell('腰带'), cell('靴子')))
    B('')

io.open(LIST_OUT, 'w', encoding='utf-8').write('\n'.join(D))
print('清单已生成:', LIST_OUT)

print('脚本已生成:', OUT)

print('总行数:', len(L))
print()
print('=== 技能统计 ===')
for c in CLASSES:
    print('  %-3s %d 个: %s' % (c, len(skill_by_class.get(c, [])),
                                '、'.join('%s(%s)' % (n, s) for _, n, s in skill_by_class.get(c, []))))
if unmatched_skill:
    print('  !! 未归属职业的技能:', unmatched_skill)
else:
    print('  未归属职业的技能: 无')
print()
print('=== 装备缺口（该档位区间确实没有对应装备，属正常）===')
agg = collections.Counter((b, k) for b, c, k in missing_item)
for (b, k), n in sorted(agg.items()):
    print('  Lv%-3d 缺 %-4s x%d 个职业' % (b, k, n))
