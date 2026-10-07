# -*- coding: utf-8 -*-
# 模拟 Shared/Functions/IniReader 的 FindValue 行为，验证 [FakePlayer] 段 11 个键都能读到
import re

p = r'E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Configs\Setup.ini'
lines = open(p, encoding='utf-8').read().splitlines()

def find_value(section, key):
    in_section = False
    for line in lines:
        line = line.strip()
        if line.startswith('['):
            if in_section:
                break
            in_section = (line.strip('[]').strip() == section)
            continue
        if not in_section:
            continue
        if '=' not in line:
            continue
        k = line.split('=')[0].strip()
        if k == key:
            return line.split('=', 1)[1].strip()
    return None

keys = ['Enabled', 'Count', 'LevelMin', 'LevelMax', 'Maps', 'Chat', 'RandomLogin',
        'ChatFile', 'EquipFile', 'PK', 'PKChance', 'Follow']

ok = True
for k in keys:
    v = find_value('FakePlayer', k)
    print(f'  {k:14} = {v!r}')
    if v is None:
        ok = False

print()
print('全部可读' if ok else '有键读不到！')

# 老键抽检，确认追加没污染其他段
for sec, k in [('Server', 'Port'), ('Server', 'MapPath'), ('FakePlayer', 'Count')]:
    print(f'  [{sec}] {k} = {find_value(sec, k)!r}')
