# -*- coding: utf-8 -*-
"""校验暂存爆率文件，并部署到服务端（部署前自动备份原文件）"""
import os, re, json, shutil, sys, datetime, collections

SERVER = r'E:/BaiduNetdiskDownload/韩Mir2/20260915更新/20260915Mir2Server'
DROPS = os.path.join(SERVER, 'Envir', 'Drops')
WORK = os.path.dirname(os.path.abspath(__file__))
STAGE = os.path.join(WORK, 'stage')

items = json.load(open(os.path.join(WORK, 'dump', 'db_items.json'), encoding='utf-8'))
NAMES = set(i['Name'] for i in items)
EQUIP = set([1, 2, 4, 5, 6, 7, 9, 10, 11])

LINE = re.compile(r'^(\d+)/(\d+) (\S+)(?: (\S+))?$')
KNOWN_SECTIONS = {'金币','武器','盔甲','头盔','项链','手镯','戒指','腰带','靴子','守护石','照明物',
                  '药水','宝石','矿石','工艺材料','肉','技能书','宝玉神珠','觉醒物品','灵物','杂物','特殊消耗品'}
NAMELIST = [i['Name'] for i in items]
IDX = {n: k for k, n in enumerate(NAMELIST)}


def validate():
    problems = []
    stats = []
    for f in sorted(os.listdir(STAGE)):
        path = os.path.join(STAGE, f)
        raw = open(path, 'rb').read()
        try:
            txt = raw.decode('utf-8')
        except UnicodeDecodeError as e:
            problems.append((f, '非UTF8: %s' % e))
            continue
        name = f[:-4]
        if b'\r\n' not in raw:
            problems.append((f, '缺少CRLF'))
        sec, secs = None, collections.Counter()
        nitems, nlines = 0, 0
        has_equip = False
        empty_sec = False
        for l in txt.replace('\r\n', '\n').split('\n'):
            s = l.strip()
            if not s:
                if sec in KNOWN_SECTIONS and sec != '金币' and secs[sec] == 0:
                    empty_sec = True
                continue
            if s.startswith(';'):
                cand = s[1:].strip()
                sec = cand if cand in KNOWN_SECTIONS else None
                if sec:
                    secs.setdefault(sec, 0)
                continue
            m = LINE.match(s)
            if not m:
                problems.append((f, '格式异常行: %r' % s))
                continue
            nm = m.group(3)
            if nm.lower() == 'gold':
                if sec == '金币':
                    secs['金币'] += 1
                continue
            nlines += 1
            if nm not in NAMES:
                problems.append((f, '物品不在库: %s' % nm))
                continue
            if items[IDX[nm]]['Type'] in EQUIP:
                has_equip = True
            nitems += 1
            if sec:
                secs[sec] += 1
        if nitems == 0:
            problems.append((f, '没有任何物品'))
        if empty_sec:
            problems.append((f, '存在空章节'))
        stats.append((name, nitems, has_equip))
    return problems, stats


def deploy():
    ts = datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
    bdir = os.path.join(SERVER, 'Back Up', 'auto-%s-爆率补齐前' % ts)
    os.makedirs(bdir, exist_ok=True)
    names = [f for f in os.listdir(STAGE) if f.endswith('.txt')]
    for f in names:
        src = os.path.join(DROPS, f)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(bdir, f))
    for f in names:
        shutil.copy2(os.path.join(STAGE, f), os.path.join(DROPS, f))
    return bdir, len(names)


if __name__ == '__main__':
    probs, stats = validate()
    print('校验文件数：', len(stats))
    print('无装备类物品的文件：', [s[0] for s in stats if not s[1]])
    print('物品行数：最少%d 最多%d 平均%.1f' % (min(s[1] for s in stats), max(s[1] for s in stats),
                                         sum(s[1] for s in stats) / len(stats)))
    if probs:
        print('！！问题 %d 条：' % len(probs))
        for f, p in probs[:40]:
            print('   ', f, p)
    else:
        print('校验通过：无问题')
    if '--apply' in sys.argv and not probs:
        bdir, n = deploy()
        print('已部署 %d 个文件，原文件备份于：%s' % (n, bdir))
