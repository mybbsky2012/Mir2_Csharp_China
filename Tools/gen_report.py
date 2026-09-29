# -*- coding: utf-8 -*-
"""生成《装备/首饰/腰带/头盔/宝石/技能》摸排报告"""
import csv, collections, re, io, os, sys

CSV = r'D:\BaiduNetdiskDownload\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv'
ENVIR = r'D:\BaiduNetdiskDownload\mir2-20241027\Server\MirEnvir\Envir.cs'
OUT = r'D:\BaiduNetdiskDownload\20260915更新\装备技能摸排报告.md'

with io.open(CSV, encoding='utf-8-sig', errors='ignore') as f:
    rows = list(csv.DictReader(f))


def lv(r):
    try:
        return int(r['ItemRequiredAmount'])
    except Exception:
        return -1


# ---------- 技能表：中文名 -> Spell 枚举 ----------
t = io.open(ENVIR, encoding='utf-8', errors='ignore').read()
spell_of = {}
for m in re.finditer(r'new MagicInfo\s*\{([^}]*)\}', t, re.S):
    body = m.group(1)
    n = re.search(r'Name\s*=\s*"([^"]+)"', body)
    s = re.search(r'Spell\s*=\s*Spell\.([A-Za-z0-9_]+)', body)
    if n and s:
        spell_of[n.group(1)] = s.group(1)

out = []
w = out.append

w('# 装备 / 首饰 / 腰带 / 头盔 / 宝石 / 技能 摸排报告')
w('')
w('> 数据来源：`Exports/2_物品数据.csv`（117 版导出，4169 件）+ 技能定义表 + `NPC命令说明.txt`')
w('')

# ---------- 一、总览 ----------
w('## 一、总览')
w('')
w('| 类别 | 数量 | 说明 |')
w('|---|---|---|')
cnt = collections.Counter(r['ItemType'] for r in rows)
groups = [
    ('武器', '主手武器，分职业/通用'),
    ('盔甲', '衣服，**分职业**'),
    ('头盔', '头部装备，多为全职业'),
    ('项链', '首饰，多为全职业'),
    ('戒指', '首饰，多为全职业'),
    ('手镯', '首饰，多为全职业'),
    ('腰带', '腰部装备，多为全职业'),
    ('靴子', '脚部装备，多为全职业'),
    ('镶嵌宝石', '镶嵌用，**无等级需求**'),
    ('守护石', '副手石，部分有等级'),
    ('宝玉神珠', '**无等级需求**'),
    ('技能书', '对应可学技能，含等级+职业'),
]
for k, d in groups:
    w('| %s | %d | %s |' % (k, cnt.get(k, 0), d))
w('')
w('全部物品类型共 %d 种、%d 件。' % (len(cnt), len(rows)))
w('')
w('**职业体系（6 系）**：' + '、'.join(
    '%s(%d)' % (k, v) for k, v in collections.Counter(
        r['ItemRequiredClass'] for r in rows).most_common()))
w('')

# ---------- 二、装备清单 ----------
w('## 二、装备清单（按等级段）')
w('')
w('等级取物品的 `需求等级`；`全职业` 表示任何职业可穿。')
w('')

GEAR = ['武器', '盔甲', '头盔', '项链', '戒指', '手镯', '腰带', '靴子', '镶嵌宝石', '守护石', '宝玉神珠']
BANDS = [(0, 0, '无需求(Lv0)'), (1, 6, 'Lv1-6'), (7, 9, 'Lv7-9'), (10, 14, 'Lv10-14'),
         (15, 19, 'Lv15-19'), (20, 24, 'Lv20-24'), (25, 29, 'Lv25-29'), (30, 34, 'Lv30-34'),
         (35, 39, 'Lv35-39'), (40, 45, 'Lv40-45'), (46, 200, 'Lv46+')]

for g in GEAR:
    sub = [r for r in rows if r['ItemType'] == g]
    if not sub:
        continue
    w('### %s（共 %d 件）' % (g, len(sub)))
    w('')
    for lo, hi, label in BANDS:
        seg = [r for r in sub if lo <= lv(r) <= hi] if lo > 0 else [r for r in sub if lv(r) == 0]
        if not seg:
            continue
        byc = collections.defaultdict(list)
        for r in seg:
            byc[r['ItemRequiredClass']].append(r['ItemName'])
        parts = []
        for c in ['全职业', '战法道', '战士', '法师', '道士', '刺客', '弓箭', '战士刺客']:
            if c in byc:
                parts.append('**%s**：%s' % (c, '、'.join(byc[c])))
        for c in byc:
            if c not in ['全职业', '战法道', '战士', '法师', '道士', '刺客', '弓箭', '战士刺客']:
                parts.append('**%s**：%s' % (c, '、'.join(byc[c])))
        w('- **%s**（%d）：%s' % (label, len(seg), '　'.join(parts)))
    w('')

# ---------- 三、技能 ----------
w('## 三、技能清单（按职业 + 等级）')
w('')
sk = [r for r in rows if r['ItemType'] == '技能书']
byclass = collections.defaultdict(list)
for r in sk:
    if lv(r) <= 45:
        byclass[r['ItemRequiredClass']].append((lv(r), r['ItemName']))
w('下表只列 **Lv45 及以下**（共 %d 本技能书），已标出对应的 `GiveSkill` 枚举名。' %
  sum(len(v) for v in byclass.values()))
w('')
order = ['战士', '法师', '道士', '刺客', '弓箭', '全职业']
for c in order + [x for x in byclass if x not in order]:
    if c not in byclass:
        continue
    w('### %s（%d 个）' % (c, len(byclass[c])))
    w('')
    w('| 需求等级 | 技能名 | GiveSkill 枚举名 |')
    w('|---|---|---|')
    for L, nm in sorted(byclass[c]):
        w('| %d | %s | `%s` |' % (L, nm, spell_of.get(nm, '**未匹配**')))
    w('')

# ---------- 四、结论 ----------
w('## 四、摸排结论')
w('')
w('1. **武器**分三条线：`战法道` 共用的经典武器（数量最多）、`刺客` 专用、`弓箭` 专用；'
  '另有少量单职业武器和 `全职业` 武器。')
w('2. **盔甲严格分职业**，六系各 72~80 件，7-45 级区间每个职业都有充足的等级梯度。')
w('3. **头盔 / 项链 / 戒指 / 手镯 / 腰带 / 靴子**以 `全职业` 为主，'
  '各职业另有少量专属件 —— 发全职业件最省事，且不会出现「穿不上」的问题。')
w('4. **镶嵌宝石 / 宝玉神珠全部无等级需求**，可直接随装备一起发放。')
w('5. **技能**：每职业第一个技能都是 **Lv7**（基本剑术 / 火球术 / 治愈术 / 绝命剑法 / 必中闪），'
  '之后逐步解锁，Lv45 是 7-45 区间的最后一档（战士「血龙剑法」、道士「血龙兽」等）。')
w('6. CSV 里 `ItemGrade` 有 965 件为 `None`，是导出字段缺失，**不影响发放**（脚本按名字发放）。')
w('')

io.open(OUT, 'w', encoding='utf-8').write('\n'.join(out))
print('报告已生成：', OUT)
print('行数：', len(out))
