# -*- coding: utf-8 -*-
"""
微端一致性验证（离线模拟，不启动服务端）。

验证三件事：
  1. 服务端清单里的路径格式，与客户端 ToRelativePath 产出的格式完全一致；
  2. 客户端从清单推算的图库数量（GetExpectedLibraryCount），
     与本地有完整资源时扫描目录算出的数量一致 —— 两边不一致就会索引越界或缺资源；
  3. 本地资源残缺（模拟微端）时，清单驱动仍能算出正确数量。
"""
import os
import re
import sys

CLIENT_ROOT = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client'

# 取自 MLibrary.cs 的 static 构造函数：变量名 -> (目录相对路径, 编号位数, 后缀)
# 目录相对路径按 Settings 里的 @".\Data\XXX\" 换算
LIBS = [
    ('CArmour', 'Data/CArmour', '00', ''),
    ('CHair', 'Data/CHair', '00', ''),
    ('CWeapon', 'Data/CWeapon', '00', ''),
    ('CWeaponEffect', 'Data/CWeaponEffect', '00', ''),
    ('CHumEffect', 'Data/CHumEffect', '00', ''),
    ('AArmour', 'Data/AArmour', '00', ''),
    ('AHair', 'Data/AHair', '00', ''),
    ('AWeapon_L', 'Data/AWeapon', '00', ' L'),
    ('AWeapon_R', 'Data/AWeapon', '00', ' R'),
    ('AWeaponEffect_L', 'Data/AWeaponEffect', '00', ' L'),
    ('AWeaponEffect_R', 'Data/AWeaponEffect', '00', ' R'),
    ('AHumEffect', 'Data/AHumEffect', '00', ''),
    ('ARArmour', 'Data/ARArmour', '00', ''),
    ('ARHair', 'Data/ARHair', '00', ''),
    ('ARWeapon', 'Data/ARWeapon', '00', ''),
    ('ARWeapon_S', 'Data/ARWeapon', '00', ' S'),
    ('ARWeaponEffect', 'Data/ARWeaponEffect', '00', ''),
    ('ARWeaponEffect_S', 'Data/ARWeaponEffect', '00', ' S'),
    ('ARHumEffect', 'Data/ARHumEffect', '00', ''),
    ('Monster', 'Data/Monster', '000', ''),
    ('Gate', 'Data/Gate', '00', ''),
    ('Flag', 'Data/Flag', '00', ''),
    ('Siege', 'Data/Siege', '00', ''),
    ('NPC', 'Data/NPC', '00', ''),
    ('Mount', 'Data/Mount', '00', ''),
    ('Fishing', 'Data/Fishing', '00', ''),
    ('Pet', 'Data/Pet', '00', ''),
    ('Transform', 'Data/Transform', '00', ''),
    ('TransformRide2', 'Data/TransformRide2', '00', ''),
    ('TransformEffect', 'Data/TransformEffect', '00', ''),
    ('TransformWeaponEffect', 'Data/TransformWeaponEffect', '00', ''),
]


def build_server_index(root):
    """模拟服务端 BuildResourceIndex：输出 相对路径 -> 文件名列表。"""
    index = {}
    for dp, dn, fn in os.walk(root):
        rel = os.path.relpath(dp, root).replace('\\', '/')
        if rel == '.':
            rel = ''
        if fn:
            index[rel.lower()] = fn
    return index


def trailing_number(name):
    """与 C# ResourceDownloader.TrailingNumber 等价。"""
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


def client_count_from_index(index, dir_rel, suffix):
    """与 C# ResourceDownloader.GetExpectedLibraryCount 等价。"""
    key = dir_rel.strip('/').lower()
    files = index.get(key)
    if not files:
        return 0
    tail = (suffix or '') + '.lib'
    mx = -1
    for n in files:
        if not n.lower().endswith(tail.lower()):
            continue
        v = trailing_number(n)
        if v > mx:
            mx = v
    return mx + 1


def original_local_count(root, dir_rel, suffix):
    """改造前 InitLibrary 的算法：扫描本地目录 + 文件名里第一个数字序列。"""
    d = os.path.join(root, dir_rel.replace('/', os.sep))
    if not os.path.isdir(d):
        return 1
    files = [f for f in os.listdir(d)
             if f.lower().endswith('.lib') and f.lower().endswith((suffix or '').lower() + '.lib')]
    if not files:
        return 1
    mx = -1
    for f in files:
        m = re.match(r'(\d+)', f)
        if m:
            v = int(m.group(1))
            if v > mx:
                mx = v
    return mx + 1


def main():
    if not os.path.isdir(CLIENT_ROOT):
        print('客户端目录不存在:', CLIENT_ROOT)
        return 1

    index = build_server_index(CLIENT_ROOT)
    total_files = sum(len(v) for v in index.values())
    print('服务端清单模拟：%d 个目录、%d 个文件\n' % (len(index), total_files))

    bad = 0
    checked = 0
    for name, dir_rel, digits, suffix in LIBS:
        local = original_local_count(CLIENT_ROOT, dir_rel, suffix)
        remote = client_count_from_index(index, dir_rel, suffix)

        checked += 1
        if local != remote:
            bad += 1
            print('  [不一致] %-24s 本地=%d 清单=%d' % (name, local, remote))
        else:
            print('  [一致]   %-24s %d' % (name, local))

    print('\n检查 %d 项，不一致 %d 项' % (checked, bad))

    # 模拟真正的微端场景：**清单是完整的**（来自服务端），只是本地文件残缺。
    # 客户端实际取 max(本地扫描数量, 清单推算数量)，这里验证它能否还原出完整数量。
    print('\n微端场景模拟（本地只剩 3 个文件、服务端清单完整）：')
    ok = True
    for name, dir_rel, digits, suffix in LIBS:
        full = original_local_count(CLIENT_ROOT, dir_rel, suffix)

        local_partial = min(3, full)                       # 本地残缺时的扫描结果
        expected = client_count_from_index(index, dir_rel, suffix)   # 服务端清单推算
        final = max(local_partial, expected)               # 对应 C# 里的 Math.Max

        if final != full:
            ok = False
            print('  [异常] %-24s 残缺推算=%d 完整应为=%d' % (name, final, full))

    print('  结果:', '本地残缺时可由清单还原完整数量' if ok else '存在异常')
    if not ok:
        bad += 1

    return 0 if bad == 0 else 2


if __name__ == '__main__':
    sys.exit(main())
