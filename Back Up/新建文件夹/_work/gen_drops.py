# -*- coding: utf-8 -*-
"""
为「地图上有刷怪、但爆率文件里没有任何物品掉落」的怪物补齐爆率。
设计依据：
  1) 服务端 DB（Server.MirDB v110）里的怪物属性：等级 / HP / 经验 / AI / 刷怪地图
  2) 服务端 DB 里的物品表（4238 件）——保证写出的物品名 100% 在库内
  3) 现有 139 个已有真实爆率表的怪物作为「同级同类参照」，保证经济强度一致
"""
import json, os, re, math, random, collections, sys

SERVER = r'E:/BaiduNetdiskDownload/韩Mir2/20260915更新/20260915Mir2Server'
DROPS = os.path.join(SERVER, 'Envir', 'Drops')
WORK = os.path.dirname(os.path.abspath(__file__))
DUMP = os.path.join(WORK, 'dump')
STAGE = os.path.join(WORK, 'stage')

TYPENAME = {0:'杂物',1:'武器',2:'盔甲',4:'头盔',5:'项链',6:'手镯',7:'戒指',8:'护身符',9:'腰带',10:'靴子',
            11:'守护石',12:'照明物',13:'药水',14:'矿石',15:'肉',16:'工艺材料',17:'卷轴',18:'宝玉神珠',19:'坐骑',
            20:'技能书',21:'特殊消耗品',22:'缰绳',23:'铃铛',24:'马鞍',25:'蝴蝶结',26:'面甲',27:'坐骑食物',
            28:'鱼钩',29:'鱼漂',30:'鱼饵',31:'探鱼器',32:'摇轮',33:'鱼',34:'任务物品',35:'觉醒物品',36:'灵物',
            37:'外形物品',38:'装饰',39:'镶嵌宝石',40:'怪物蛋',41:'攻城弹药',42:'封印',43:'攻击型绝技',
            44:'防御型绝技',45:'技能型绝技',46:'绝技材料'}

EQUIP_TYPES = [1, 2, 4, 5, 6, 7, 9, 10, 11]
SECTIONS = ['武器','盔甲','头盔','项链','手镯','戒指','腰带','靴子','守护石','照明物',
            '药水','宝石','矿石','工艺材料','肉','技能书','宝玉神珠','觉醒物品','灵物','杂物','特殊消耗品']

monsters = json.load(open(os.path.join(DUMP, 'db_monsters.json'), encoding='utf-8'))
maps = json.load(open(os.path.join(DUMP, 'db_maps.json'), encoding='utf-8'))
items = json.load(open(os.path.join(DUMP, 'db_items.json'), encoding='utf-8'))
ITEMMAP = {it['Name']: it for it in items}
MON = {m['Index']: m for m in monsters}

SPAWN = collections.defaultdict(lambda: {'maps': collections.Counter(), 'points': 0, 'count': 0})
for mp in maps:
    for rp in mp['Respawns']:
        s = SPAWN[rp['MonsterIndex']]
        s['maps'][mp['Title']] += rp['Count']
        s['points'] += 1
        s['count'] += rp['Count']

MONIDX = {}
for m in monsters:
    MONIDX.setdefault(m['Name'], m['Index'])


def hp_of(m):
    return int(m.get('Stats', {}).get('12', 0) or 0)


def read_drop(name):
    p = os.path.join(DROPS, name + '.txt')
    if not os.path.exists(p):
        return None
    return open(p, 'rb').read().decode('utf-8-sig').replace('\r\n', '\n')


LINE_RE = re.compile(r'^(\d+)\s*/\s*(\d+)\s+(\S+)(?:\s+(\S+))?\s*$')


def parse_drop(txt):
    gold, grow, rows = None, None, []
    for l in (txt or '').split('\n'):
        s = l.strip()
        if not s or s.startswith(';'):
            continue
        m = LINE_RE.match(s)
        if not m:
            continue
        den, name, extra = int(m.group(2)), m.group(3), m.group(4)
        if name.lower() == 'gold':
            gold = int(extra) if extra and extra.isdigit() else None
            grow = s
            continue
        rows.append((name, den, (extra or '').upper() == 'Q'))
    return gold, rows, grow


# ---------------------------------------------------------------- 参照表
REFS = []
for i in sorted(SPAWN):
    m = MON[i]
    txt = read_drop(m['Name'])
    if txt is None:
        continue
    g, rows, _ = parse_drop(txt)
    if len(rows) < 12:
        continue
    if not any(ITEMMAP.get(r[0], {}).get('Type') in EQUIP_TYPES for r in rows):
        continue
    REFS.append((i, m['Name'], m, g, rows))
REF_BY_NAME = {r[1]: r for r in REFS}


def is_boss(m):
    """以现有语料为标尺：HP>=7000 或 经验>=8000 即属「BOSS 一档」"""
    return hp_of(m) >= 7000 or m['Experience'] >= 8000


def is_top_boss(m):
    return hp_of(m) >= 30000 or m['Experience'] >= 20000


def power(m):
    return 0.6 * math.log2(hp_of(m) + 300) + 0.4 * math.log2(m['Experience'] + 1500)


def dist(a, b):
    d = 1.5 * abs(math.log2((hp_of(a) + 300) / (hp_of(b) + 300)))
    d += 1.1 * abs(math.log2((a['Experience'] + 1500) / (b['Experience'] + 1500)))
    d += 0.05 * abs(a['Level'] - b['Level'])
    if is_boss(a) != is_boss(b):
        d += 2.2
    if set(SPAWN[a['Index']]['maps']) & set(SPAWN[b['Index']]['maps']):
        d -= 2.6
    return max(d, 0.01)


def pick_refs(m, k=4):
    return sorted(((dist(m, r[2]), r) for r in REFS), key=lambda x: x[0])[:k]


def wmedian(pairs):
    pairs = sorted(pairs)
    tot = sum(w for _, w in pairs)
    acc = 0.0
    for v, w in pairs:
        acc += w
        if acc >= tot / 2:
            return v
    return pairs[-1][0]


def build_pool(m, k=4):
    refs = pick_refs(m, k)
    per_item = collections.defaultdict(list)
    for d, r in refs:
        w = 1.0 / (1.0 + d)
        for name, den, q in r[4]:
            if q or name not in ITEMMAP:
                continue
            per_item[name].append((den, w))
    pool = collections.defaultdict(dict)
    for name, pairs in per_item.items():
        t = ITEMMAP[name]['Type']
        sec = '宝石' if t == 39 else TYPENAME.get(t)
        if sec not in SECTIONS:
            continue
        pool[sec][name] = wmedian(pairs)
    return pool, refs


def nice(n):
    if n <= 10:
        return n
    if n <= 100:
        return int(round(n / 5.0) * 5)
    if n <= 1000:
        return int(round(n / 10.0) * 10)
    if n <= 10000:
        return int(round(n / 100.0) * 100)
    return int(round(n / 500.0) * 500)


_REF_DENS = sorted(SPAWN[r[0]]['count'] for r in REFS)
REF_DENS_MED = _REF_DENS[len(_REF_DENS) // 2]
POTION_FACTOR = {'topboss': 1.0, 'boss': 1.5, 'normal': 3.0}
POTION_FLOOR = {'topboss': 2, 'boss': 3, 'normal': 5}


def density_factor(m):
    """刷怪越密 → 单只越应「抠门」"""
    d = SPAWN[m['Index']]['count'] or 1
    return min(2.5, max(0.7, (d / max(REF_DENS_MED, 1)) ** 0.5))


def gold_density(m):
    d = SPAWN[m['Index']]['count'] or 1
    return min(1.3, max(0.4, (max(REF_DENS_MED, 1) / d) ** 0.4))


MAX_KEEP = {'武器': 8, '盔甲': 8, '头盔': 5, '项链': 8, '手镯': 8, '戒指': 8,
            '腰带': 5, '靴子': 5, '守护石': 4, '照明物': 3, '药水': 12, '宝石': 6,
            '矿石': 4, '工艺材料': 7, '肉': 2, '技能书': 4, '宝玉神珠': 6,
            '觉醒物品': 4, '灵物': 4, '杂物': 6, '特殊消耗品': 4}


def compose(m, pool, refs, rng, tier):
    boss = tier != 'normal'
    dens = density_factor(m)
    out = collections.OrderedDict()
    for sec in SECTIONS:
        cand = pool.get(sec)
        if not cand:
            continue
        lst = sorted(cand.items(), key=lambda kv: (kv[1], kv[0]))
        # 普通怪不承接「传说级」掉率（分母过大 = 原属 BOSS 的表）
        if not boss:
            lst = [x for x in lst if x[1] <= 20000]
        lst = lst[:MAX_KEEP.get(sec, 5)]
        picked = []
        for name, den in lst:
            if picked and rng.random() > 0.82:
                continue
            den = den * rng.uniform(0.8, 1.35) * dens
            if sec == '药水':
                den = max(POTION_FLOOR[tier], den * POTION_FACTOR[tier])
            picked.append((name, max(2, nice(int(den)))))
        if not picked and lst:
            picked.append((lst[0][0], max(2, nice(int(lst[0][1] * dens)))))
        if picked:
            out[sec] = picked
    gp = [(r[3], 1.0 / (1.0 + d)) for d, r in refs if r[3]]
    gold = nice(int(wmedian(gp))) if gp else 100
    gold = max(50, nice(int(gold * gold_density(m))))
    return gold, out


# ---------------------------------------------------------------- 特殊模板
def special_pool(kind):
    if kind == 'wood':
        return collections.OrderedDict([('工艺材料', [('木材', 10), ('木材', 20), ('蘑菇', 30),
                                                ('乌木果实', 4), ('坚固的绳子', 60)])])
    if kind == 'herb':
        return collections.OrderedDict([('工艺材料', [('食人花叶子', 6), ('蘑菇', 20), ('蜘蛛网', 25)])])
    if kind == 'flower':
        return collections.OrderedDict([('工艺材料', [('食人花树叶', 2), ('食人花叶子', 4),
                                                ('食人花果实', 3), ('食人花茎', 4)])])
    if kind == 'box':
        return collections.OrderedDict([
            ('药水', [('体力恢复药(特)', 3), ('魔力恢复药(特)', 3), ('强效太阳水', 5)]),
            ('宝玉神珠', [('强化宝玉', 40), ('仙界宝玉', 50), ('魔性宝玉', 50)]),
            ('技能书', [('护身气幕', 300), ('天霜冰环', 400)]),
            ('杂物', [('红色宝箱', 200), ('未知宝箱', 300)]),
        ])
    if kind == 'egg':
        return collections.OrderedDict([
            ('灵物', [('娱美蛋', 20), ('龙蛋', 60), ('火娃', 120)]),
            ('工艺材料', [('虫壳', 10), ('蛛牙', 20)]),
        ])
    return collections.OrderedDict()


TIER_CN = {'normal': '普通怪档', 'boss': '精英/BOSS档', 'topboss': '顶级BOSS档',
           'resource': '资源实体', 'container': '宝箱/容器'}

TIER_CN = {'normal': '普通怪档', 'boss': '精英/BOSS档', 'topboss': '顶级BOSS档',
           'resource': '资源实体', 'container': '宝箱/容器'}

SKIP_NAMES = {
    '卫士', '卫士0', '卫士1', '带刀侍卫', '带刀侍卫0', '巡城侍卫', '弓箭守卫', '练功师',
    '寒冰之门', '通道门1', '通道门2', '燃烧昆仑城门', 'WarpGate',
    '围栏', '战锣', '狐月陷阱',
    '破龙神像', '天龙神像', '魔龙神像', '归龙神像', '火龙神像', '四龙神像',
}
CONTAINERS = {'木盒': 'box', '被诅咒的银箱': 'box', '异秘蛋': 'egg', '异秘蛋1': 'egg'}
RESOURCES = {
    '灌木': 'wood', '如何树': 'wood', '变异棘蔓草': 'wood', '杉精魄': 'wood', '杉精魄0': 'wood',
    '变异食虫草': 'herb', '毒妖花1': 'herb', '毒妖花2': 'herb',
    '大食人花': 'flower', '大食人花0': 'flower',
}


def filter_valid(secs):
    out = collections.OrderedDict()
    for sec, lst in secs.items():
        keep = [(n, d) for n, d in lst if n in ITEMMAP]
        if keep:
            out[sec] = keep
    return out


# ---------------------------------------------------------------- 生成
def collect_targets():
    """上刷 + 爆率文件里一件物品都没有（含只有金币的）"""
    out = []
    for i in sorted(SPAWN):
        m = MON[i]
        txt = read_drop(m['Name'])
        if txt is None:
            out.append((m, None, None, True))    # 连文件都没有
            continue
        g, rows, grow = parse_drop(txt)
        if rows:
            continue
        out.append((m, g, grow, False))
    return out


def build(m, rng, existing_gold):
    n = m['Name']
    if n in SKIP_NAMES:
        return None, 'skip'
    if n in RESOURCES:
        return (existing_gold or 0, filter_valid(special_pool(RESOURCES[n]))), 'resource'
    if n in CONTAINERS:
        return (existing_gold or 0, filter_valid(special_pool(CONTAINERS[n]))), 'container'

    tier = 'topboss' if is_top_boss(m) else ('boss' if is_boss(m) else 'normal')
    pool, refs = build_pool(m)
    gold, secs = compose(m, pool, refs, rng, tier)

    if tier == 'topboss':
        top = sorted(REFS, key=lambda r: -power(r[2]))[:6]
        highs = collections.defaultdict(dict)
        for r in top:
            for name, den, q in r[4]:
                if q or name not in ITEMMAP or den < 800:
                    continue
                t = ITEMMAP[name]['Type']
                sec = '宝石' if t == 39 else TYPENAME.get(t)
                if sec in ('武器', '盔甲', '头盔', '项链', '手镯', '戒指', '技能书', '宝玉神珠', '觉醒物品'):
                    highs[sec][name] = max(highs[sec].get(name, 0), den)
        for sec, d in highs.items():
            cur = dict(secs.get(sec, []))
            for name, den in d.items():
                if name in cur or rng.random() > 0.5:
                    continue
                cur[name] = nice(int(den * rng.uniform(2.0, 3.0)))
            if cur:
                secs[sec] = sorted(cur.items(), key=lambda kv: (kv[1], kv[0]))
        gold = nice(int(gold * 1.6))

    if existing_gold:
        gold = existing_gold                        # 尊重原文件里已写好的金币
    return (gold, secs), tier


def render(m, data, gold_line=None, tier=''):
    gold, secs = data
    maps = list(SPAWN[m['Index']]['maps'])[:5]
    L = [';%s  等级%s  生命%s  经验%s' % (m['Name'], m['Level'], hp_of(m), m['Experience']),
         ';刷怪地图：%s（共%s处刷点/%s只）' % ('、'.join(maps), SPAWN[m['Index']]['points'],
                                          SPAWN[m['Index']]['count']),
         ';档位：%s ｜ 依据 Server.MirDB 怪物属性与现有同级爆率体系生成' % TIER_CN.get(tier, tier),
         '']
    L.append(';金币')
    if gold_line:
        L.append(gold_line)
    elif gold:
        L.append('1/10 Gold %d' % gold)
    L.append('')
    for sec in SECTIONS:
        if sec not in secs:
            continue
        L.append(';%s' % sec)
        for name, den in secs[sec]:
            L.append('1/%d %s' % (den, name))
        L.append('')
    while L and L[-1] == '':
        L.pop()
    return '\r\n'.join(L) + '\r\n'


def run():
    targets = collect_targets()
    results = []
    for m, gold, grow, nofile in targets:
        rng = random.Random(m['Index'] * 7919 + 13)
        data, kind = build(m, rng, gold)
        results.append(dict(index=m['Index'], name=m['Name'], level=m['Level'], hp=hp_of(m),
                            exp=m['Experience'], kind=kind, nofile=nofile,
                            maps=list(SPAWN[m['Index']]['maps'])[:6],
                            points=SPAWN[m['Index']]['points'], count=SPAWN[m['Index']]['count'],
                            text=render(m, data, grow, kind) if data else None,
                            items=sum(len(v) for v in data[1].values()) if data else 0,
                            gold=data[0] if data else 0))
    return results


if __name__ == '__main__':
    res = run()
    print('待补怪物数：', len(res))
    print('分类：', dict(collections.Counter(r['kind'] for r in res)))
    os.makedirs(STAGE, exist_ok=True)
    n = 0
    for r in res:
        if r['text']:
            open(os.path.join(STAGE, r['name'] + '.txt'), 'wb').write(r['text'].encode('utf-8'))
            n += 1
    json.dump([{k: v for k, v in r.items() if k != 'text'} for r in res],
              open(os.path.join(WORK, 'gen_report.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('已写暂存文件：', n)
