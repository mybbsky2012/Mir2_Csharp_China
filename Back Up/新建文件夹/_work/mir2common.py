# -*- coding: utf-8 -*-
"""加载 DB dump + 现有爆率文件，构建参考模型"""
import json, os, re, math, collections

SERVER = r'E:/BaiduNetdiskDownload/韩Mir2/20260915更新/20260915Mir2Server'
DROPS = os.path.join(SERVER, 'Envir', 'Drops')
WORK = os.path.dirname(os.path.abspath(__file__))
DUMP = os.path.join(WORK, 'dump')

TYPENAME = {0:'杂物',1:'武器',2:'盔甲',4:'头盔',5:'项链',6:'手镯',7:'戒指',8:'护身符',9:'腰带',10:'靴子',
            11:'守护石',12:'照明物',13:'药水',14:'矿石',15:'肉',16:'工艺材料',17:'卷轴',18:'宝玉神珠',19:'坐骑',
            20:'技能书',21:'特殊消耗品',22:'缰绳',23:'铃铛',24:'马鞍',25:'蝴蝶结',26:'面甲',27:'坐骑食物',
            28:'鱼钩',29:'鱼漂',30:'鱼饵',31:'探鱼器',32:'摇轮',33:'鱼',34:'任务物品',35:'觉醒物品',36:'灵物',
            37:'外形物品',38:'装饰',39:'镶嵌宝石',40:'怪物蛋',41:'攻城弹药',42:'封印',
            43:'攻击型绝技',44:'防御型绝技',45:'技能型绝技',46:'绝技材料'}

# 爆率文件章节顺序（沿用服务端既有文件的写法）
SECTION_ORDER = ['武器','盔甲','头盔','项链','手镯','戒指','腰带','靴子','守护石','照明物',
                 '药水','宝石','矿石','工艺材料','肉','技能书','宝玉神珠','觉醒物品','灵物','杂物','特殊消耗品']


def load():
    monsters = json.load(open(os.path.join(DUMP, 'db_monsters.json'), encoding='utf-8'))
    maps = json.load(open(os.path.join(DUMP, 'db_maps.json'), encoding='utf-8'))
    items = json.load(open(os.path.join(DUMP, 'db_items.json'), encoding='utf-8'))
    return monsters, maps, items


def read_drop(name):
    """返回 (lines, 是否存在)"""
    p = os.path.join(DROPS, name + '.txt')
    if not os.path.exists(p):
        return [], False
    txt = open(p, 'rb').read().decode('utf-8-sig')
    return txt.replace('\r\n', '\n').split('\n'), True


LINE_RE = re.compile(r'^(\d+)\s*/\s*(\d+)\s+(\S+)(?:\s+(\S+))?\s*$')


def parse_lines(lines):
    """解析爆率文件 -> (gold, [(item, chance, quest)]), 保留分组顺序"""
    gold = None
    out = []
    for l in lines:
        s = l.strip()
        if not s or s.startswith(';'):
            continue
        m = LINE_RE.match(s)
        if not m:
            continue
        num, den, name, extra = m.group(1), int(m.group(2)), m.group(3), m.group(4)
        if name.lower() == 'gold':
            gold = int(extra) if extra and extra.isdigit() else None
            continue
        out.append((name, den, (extra or '').upper() == 'Q'))
    return gold, out
