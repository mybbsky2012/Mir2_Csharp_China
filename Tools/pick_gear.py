# -*- coding: utf-8 -*-
"""按「7-45 级、每 5 级一档、分职业、全套九件」挑选装备，输出预览与脚本"""
import csv, io, re, collections, sys

CSV = r'D:\BaiduNetdiskDownload\20260915更新\20260915Mir2Server\Exports\2_物品数据.csv'
BANDS = [7, 10, 15, 20, 25, 30, 35, 40, 45]
CLASSES = ['战士', '法师', '道士', '刺客', '弓箭']
GENDERS = ['男性', '女性']

with io.open(CSV, encoding='utf-8-sig', errors='ignore') as f:
    rows = list(csv.DictReader(f))


def lv(r):
    try:
        return int(r['ItemRequiredAmount'])
    except Exception:
        return -1


def class_ok(rc, cls):
    if rc == '全职业' or rc == cls:
        return True
    if rc == '战法道' and cls in ('战士', '法师', '道士'):
        return True
    if rc == '战士刺客' and cls in ('战士', '刺客'):
        return True
    return False


BAD = re.compile(r'(\d{2,}[-）)]|\(\w*\)\d|\d$|-秘籍)')

# 非战斗武器 / 不宜发放的装备
WEAPON_BAD = ('钓竿', '鱼竿', '鹤嘴锄', '锄', '镐', '钓', '探鱼')
ARMOR_BAD = ('内裤', '内衣')


def is_variant(name):
    """排除系列编号变体 / 秘籍"""
    if '秘籍' in name:
        return True
    if re.search(r'\d$', name):
        return True
    if re.search(r'\d{3,}', name):
        return True
    return False


def pick(kind, band, cls, prev, gender=None):
    """挑一件等级落在 (prev, band] 的装备；没有就退到 <=band 里等级最高的"""
    pool = []
    for r in rows:
        if r['ItemType'] != kind:
            continue
        L = lv(r)
        if L <= 0 or L > band:
            continue
        if not class_ok(r['ItemRequiredClass'], cls):
            continue
        if gender and r['ItemRequiredGender'] not in ('性别不限', gender):
            continue
        nm = r['ItemName']
        if is_variant(nm):
            continue
        if kind == '武器' and any(b in nm for b in WEAPON_BAD):
            continue
        if kind == '盔甲' and any(b in nm for b in ARMOR_BAD):
            continue
        pool.append((L, nm))
    if not pool:
        return None
    fresh = [x for x in pool if x[0] > prev]
    if not fresh:
        return None  # 本档位区间没有新装备，就不发，避免重复塞旧装备
    pool = fresh
    pool.sort(key=lambda x: (-x[0], len(x[1]), x[1]))
    return pool[0]


SLOTS = [('武器', 1, None), ('盔甲', 1, 'G'), ('头盔', 1, None), ('项链', 1, None),
         ('戒指', 2, None), ('手镯', 2, None), ('腰带', 1, None), ('靴子', 1, None)]

result = {}
for bi, band in enumerate(BANDS):
    prev = BANDS[bi - 1] if bi > 0 else 0
    for cls in CLASSES:
        for g in GENDERS:
            got = []
            prev_names = {k: n for k, n, c in result.get((prev, cls, g), [])} if bi > 0 else {}
            for kind, cnt, flag in SLOTS:
                gd = g if flag == 'G' else None
                p = pick(kind, band, cls, prev, gd)
                if p:
                    if prev_names.get(kind) == p[1]:
                        continue  # 与上一档完全同名，跳过，避免重复发
                    got.append((kind, p[1], cnt))
            result[(band, cls, g)] = got

# ---------- 预览 ----------
print('=' * 100)
for band in BANDS:
    print('### Lv%d' % band)
    for cls in CLASSES:
        g = result[(band, cls, '男性')]
        names = []
        for kind, nm, cnt in g:
            names.append('%s' % nm if cnt == 1 else '%s x%d' % (nm, cnt))
        print('  %-3s %s' % (cls, ' / '.join(names)))
        # 女性只差衣服，单独提示
        gf = result[(band, cls, '女性')]
        if gf:
            d = [n for k, n, c in gf if k == '盔甲']
            if d:
                print('      (女性衣服: %s)' % d[0])
    print()

# 缺口检查
print('=' * 100)
print('=== 缺口检查（某档位没发到的部位）===')
for band in BANDS:
    for cls in CLASSES:
        got = {k for k, n, c in result[(band, cls, '男性')]}
        miss = [k for k, c, f in SLOTS if k not in got]
        if miss:
            print('Lv%-3d %s : 缺 %s' % (band, cls, '、'.join(miss)))
print('缺口检查完毕')
