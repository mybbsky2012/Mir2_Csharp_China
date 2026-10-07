# -*- coding: utf-8 -*-
p = r'E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Configs\Setup.ini'

data = open(p, 'rb').read()

if b'PKChance=' in data:
    print('已存在，跳过')
else:
    lines = [
        '',
        '; 假人之间会不会在野外随机 PK（互相切磋）。关掉就只有打怪了',
        'PK=True',
        '; 每轮判定「要不要挑个人打」的概率（%）',
        'PKChance=6',
        '',
        '; 允许假人接受组队邀请，并跟随队长一起换图打怪（队长阵亡时它会自行下线重登）',
        'Follow=True',
    ]

    add = ''.join(x + '\r\n' for x in lines).encode('utf-8')

    marker = b'EquipFile=FakePlayerEquip.txt\r\n'
    i = data.find(marker)
    assert i > 0, '找不到 EquipFile 行'

    j = i + len(marker)
    data = data[:j] + add + data[j:]
    open(p, 'wb').write(data)
    print('已写入')
