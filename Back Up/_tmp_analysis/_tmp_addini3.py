# -*- coding: utf-8 -*-
p = r'E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Configs\Setup.ini'

data = open(p, 'rb').read()

if b'SpawnFile=' in data:
    print('已存在，跳过')
else:
    lines = [
        '',
        '; 假人登录点（内联写法，多个点用分号隔开，格式 地图序号:X,Y）',
        '; 例如  0:330,330; 3:300,300  —— 一般不用写这里，直接编辑下面的登录点文件更方便',
        'Spawns=',
        '',
        '; 登录点文件（相对 Configs 目录）。文件里每行一个「地图 X Y」，',
        '; 假人上线时就站在这些点上；留空或文件里没内容 = 在活动地图上随机登录',
        'SpawnFile=FakePlayerSpawns.txt',
    ]

    add = ''.join(x + '\r\n' for x in lines).encode('utf-8')

    marker = b'Follow=True\r\n'
    i = data.find(marker)
    assert i > 0, '找不到 Follow 行'

    j = i + len(marker)
    data = data[:j] + add + data[j:]
    open(p, 'wb').write(data)
    print('已写入')
