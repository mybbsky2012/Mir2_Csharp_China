# -*- coding: utf-8 -*-
"""
微端客户端包验收。

检查两件事：
  1. 包里该有的东西是不是都在（程序文件、标记文件、干净配置、核心图库）；
  2. 对每个图库目录，验证「微端包本地几乎为空」时，
     客户端能否靠服务端清单还原出完整客户端应有的数量 —— 这正是微端能跑起来的前提。
"""
import os
import sys

FULL = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client'
MICRO = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client_Micro'

LIBS = [
    ('CArmour', 'Data/CArmour', ''),
    ('CHair', 'Data/CHair', ''),
    ('CWeapon', 'Data/CWeapon', ''),
    ('CWeaponEffect', 'Data/CWeaponEffect', ''),
    ('CHumEffect', 'Data/CHumEffect', ''),
    ('AArmour', 'Data/AArmour', ''),
    ('AHair', 'Data/AHair', ''),
    ('AWeapon_L', 'Data/AWeapon', ' L'),
    ('AWeapon_R', 'Data/AWeapon', ' R'),
    ('AWeaponEffect_L', 'Data/AWeaponEffect', ' L'),
    ('AWeaponEffect_R', 'Data/AWeaponEffect', ' R'),
    ('AHumEffect', 'Data/AHumEffect', ''),
    ('ARArmour', 'Data/ARArmour', ''),
    ('ARHair', 'Data/ARHair', ''),
    ('ARWeapon', 'Data/ARWeapon', ''),
    ('ARWeapon_S', 'Data/ARWeapon', ' S'),
    ('ARWeaponEffect', 'Data/ARWeaponEffect', ''),
    ('ARWeaponEffect_S', 'Data/ARWeaponEffect', ' S'),
    ('ARHumEffect', 'Data/ARHumEffect', ''),
    ('Monster', 'Data/Monster', ''),
    ('Gate', 'Data/Gate', ''),
    ('Flag', 'Data/Flag', ''),
    ('Siege', 'Data/Siege', ''),
    ('NPC', 'Data/NPC', ''),
    ('Mount', 'Data/Mount', ''),
    ('Fishing', 'Data/Fishing', ''),
    ('Pet', 'Data/Pet', ''),
    ('Transform', 'Data/Transform', ''),
    ('TransformRide2', 'Data/TransformRide2', ''),
    ('TransformEffect', 'Data/TransformEffect', ''),
    ('TransformWeaponEffect', 'Data/TransformWeaponEffect', ''),
]

pass_n = 0
fail_n = 0


def check(name, ok, detail=''):
    global pass_n, fail_n
    if ok:
        pass_n += 1
        print('  [通过] %s' % name)
    else:
        fail_n += 1
        print('  [失败] %s  %s' % (name, detail))


def build_index(root):
    index = {}
    for dp, dn, fn in os.walk(root):
        rel = os.path.relpath(dp, root).replace('\\', '/')
        if rel == '.':
            rel = ''
        if fn:
            index[rel.lower()] = fn
    return index


def trailing_number(name):
    if not name:
        return -1
    end = len(name) - 1
    while end >= 0 and not name[end].isdigit():
        end -= 1
    if end < 0:
        return -1
    start = end
    while start >= 0 and name[start].isdigit():
        start -= 1
    try:
        return int(name[start + 1:end + 1])
    except ValueError:
        return -1


def count_from_index(index, dir_rel, suffix):
    key = dir_rel.strip('/').lower()
    files = index.get(key)
    if not files:
        return 0
    tail = (suffix or '').lower() + '.lib'
    mx = -1
    for n in files:
        if not n.lower().endswith(tail):
            continue
        v = trailing_number(n)
        if v > mx:
            mx = v
    return mx + 1


def local_count(root, dir_rel, suffix):
    d = os.path.join(root, dir_rel.replace('/', os.sep))
    if not os.path.isdir(d):
        return 1
    files = [f for f in os.listdir(d)
             if f.lower().endswith('.lib') and f.lower().endswith((suffix or '').lower() + '.lib')]
    if not files:
        return 1
    mx = -1
    for f in files:
        m = __import__('re').match(r'(\d+)', f)
        if m and int(m.group(1)) > mx:
            mx = int(m.group(1))
    return mx + 1


def main():
    global pass_n, fail_n

    if not os.path.isdir(MICRO):
        print('微端包不存在: ' + MICRO)
        return 1

    print('=== 1. 包内容检查 ===')
    check('Client.exe', os.path.exists(os.path.join(MICRO, 'Client.exe')))
    check('Client.dll', os.path.exists(os.path.join(MICRO, 'Client.dll')))
    check('Shared.dll', os.path.exists(os.path.join(MICRO, 'Shared.dll')))
    check('DirectX 运行库', os.path.isdir(os.path.join(MICRO, 'DirectX')))
    check('microclient.flag 标记', os.path.exists(os.path.join(MICRO, 'microclient.flag')))
    check('Mir2Config.ini', os.path.exists(os.path.join(MICRO, 'Mir2Config.ini')))

    cfg = ''
    p = os.path.join(MICRO, 'Mir2Config.ini')
    if os.path.exists(p):
        cfg = open(p, encoding='utf-8', errors='ignore').read()
    check('配置启用了微端', 'Enabled=True' in cfg)
    check('配置未带出账号密码', 'AccountID' not in cfg and 'Password' not in cfg)

    data = os.path.join(MICRO, 'Data')
    core = [f for f in os.listdir(data) if f.lower().endswith('.lib')] if os.path.isdir(data) else []
    check('核心界面图库已预置（>20 个）', len(core) > 20, '实际=%d' % len(core))

    print()
    print('=== 2. 图库数量：微端包本地 vs 完整客户端 ===')
    full_index = build_index(FULL)
    bad = 0
    for name, dir_rel, suffix in LIBS:
        full = local_count(FULL, dir_rel, suffix)
        micro_local = local_count(MICRO, dir_rel, suffix)
        from_index = count_from_index(full_index, dir_rel, suffix)
        final = max(micro_local, from_index)

        if final != full:
            bad += 1
            print('  [不一致] %-24s 微端本地=%d 清单=%d 最终=%d 应为=%d'
                  % (name, micro_local, from_index, final, full))
        else:
            print('  [通过]   %-24s 本地=%-4d 清单补全后=%d' % (name, micro_local, final))

    if bad:
        fail_n += bad
    else:
        pass_n += 1

    print()
    print('通过 %d 项，失败 %d 项' % (pass_n, fail_n))
    return 0 if fail_n == 0 else 2


if __name__ == '__main__':
    sys.exit(main())
