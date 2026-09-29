# -*- coding: utf-8 -*-
"""
制作微端客户端包。

原理：客户端只保留「进游戏必须的核心」——程序文件、DirectX 运行库、界面图库、
      着色器、音效索引表，其余资源（角色/怪物/NPC 图库、音效、地图，合计约 9.3GB）
      由客户端在**真正用到的那一刻**才向服务端下载。

      注意：启动阶段的批量图库登记会显式抑制下载（ResourceDownloader.BeginBulkLoad），
      所以微端客户端启动速度和完整客户端几乎一样，不会出现"先等十分钟下完再进游戏"。

用法：
    python make_micro_client.py [源客户端目录] [输出目录] [资源服务地址]

默认值：
    源 = D:\\BaiduNetdiskDownload\\20260915更新\\20260915Client
    出 = D:\\BaiduNetdiskDownload\\20260915更新\\20260915Client_Micro
    地址 = http://127.0.0.1:5679/
"""
import os
import shutil
import sys

DEFAULT_SRC = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client'
DEFAULT_DST = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client_Micro'
DEFAULT_HOST = 'http://127.0.0.1:5679/'

# 根目录下的程序文件后缀
PROG_EXT = ('.dll', '.exe', '.json', '.config', '.xml', '.ico', '.pdb')

# 整体复制的目录
COPY_DIRS = ['DirectX', 'Localization']

# 根目录下单独复制的文件
COPY_FILES = ['KeyBinds.ini', 'Language.ini', 'MIR2.ICO']

# 绝不复制的内容
SKIP_NAMES = {'Back Up', 'Screenshots', 'Data'}


def human(n):
    for unit in ('B', 'KB', 'MB', 'GB'):
        if n < 1024 or unit == 'GB':
            return '%.1f %s' % (n, unit)
        n /= 1024.0


def dir_size(path):
    total = 0
    for dp, dn, fn in os.walk(path):
        for f in fn:
            try:
                total += os.path.getsize(os.path.join(dp, f))
            except OSError:
                pass
    return total


def copy_file(src, dst):
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copy2(src, dst)


def main():
    # 支持 --host 具名参数，也保留原来的位置参数写法：
    #   python make_micro_client.py
    #   python make_micro_client.py <源客户端> <输出目录> <资源服务地址>
    #   python make_micro_client.py --host http://你的公网IP:5679/
    argv = list(sys.argv[1:])
    host_opt = None
    if '--host' in argv:
        i = argv.index('--host')
        if i + 1 >= len(argv):
            print('--host 后面要跟资源服务地址，例如 --host http://1.2.3.4:5679/')
            return 1
        host_opt = argv[i + 1]
        del argv[i:i + 2]

    src = argv[0] if len(argv) > 0 else DEFAULT_SRC
    dst = argv[1] if len(argv) > 1 else DEFAULT_DST
    host = host_opt or (argv[2] if len(argv) > 2 else DEFAULT_HOST)

    # 地址末尾的斜杠必须补上，否则拼出来的 URL 会少一层
    if not host.endswith('/'):
        host += '/'
    if not (host.startswith('http://') or host.startswith('https://')):
        host = 'http://' + host

    if not os.path.isdir(src):
        print('源客户端目录不存在: ' + src)
        return 1

    if os.path.isdir(dst):
        print('输出目录已存在，先删除: ' + dst)
        shutil.rmtree(dst)
    os.makedirs(dst)

    copied = 0
    copied_bytes = 0

    def record(size):
        nonlocal copied, copied_bytes
        copied += 1
        copied_bytes += size

    # 1) 程序文件
    for f in os.listdir(src):
        p = os.path.join(src, f)
        if not os.path.isfile(p):
            continue
        if f.lower().endswith(PROG_EXT):
            copy_file(p, os.path.join(dst, f))
            record(os.path.getsize(p))

    # 2) 运行库与本地化目录
    for d in COPY_DIRS:
        s = os.path.join(src, d)
        if os.path.isdir(s):
            shutil.copytree(s, os.path.join(dst, d))
            n = sum(len(fn) for _, _, fn in os.walk(s))
            copied += n
            copied_bytes += dir_size(s)
            print('  复制目录 %-14s %d 个文件' % (d, n))

    # 3) 根目录配置文件
    for f in COPY_FILES:
        s = os.path.join(src, f)
        if os.path.isfile(s):
            copy_file(s, os.path.join(dst, f))
            record(os.path.getsize(s))

    # 4) Data 顶层图库（界面、物品图标等，进游戏就要用）
    data_src = os.path.join(src, 'Data')
    data_dst = os.path.join(dst, 'Data')
    if os.path.isdir(data_src):
        os.makedirs(data_dst, exist_ok=True)

        for f in os.listdir(data_src):
            p = os.path.join(data_src, f)
            if os.path.isfile(p) and f.lower().endswith('.lib'):
                copy_file(p, os.path.join(data_dst, f))
                record(os.path.getsize(p))

        # 着色器很小，但是"缺了就没法渲染"，预置上避免启动时等待
        shaders = os.path.join(data_src, 'Shaders')
        if os.path.isdir(shaders):
            shutil.copytree(shaders, os.path.join(data_dst, 'Shaders'))
            copied += sum(len(fn) for _, _, fn in os.walk(shaders))
            copied_bytes += dir_size(shaders)

    # 4b) 音效索引表：只有几十 KB，但少了它所有音效都放不出来，预置省一次启动等待
    sound_src = os.path.join(src, 'Sound')
    if os.path.isdir(sound_src):
        os.makedirs(os.path.join(dst, 'Sound'), exist_ok=True)
        lst = os.path.join(sound_src, 'SoundList.lst')
        if os.path.isfile(lst):
            copy_file(lst, os.path.join(dst, 'Sound', 'SoundList.lst'))
            record(os.path.getsize(lst))

    # 5) 生成微端标记（客户端看到它就自动启用按需下载，玩家无需改配置）
    open(os.path.join(dst, 'microclient.flag'), 'w').close()

    # 6) 生成干净的 Mir2Config.ini（不带出原客户端的账号密码）
    config = (
        '[Graphics]\r\n'
        'FullScreen=False\r\n'
        'Borderless=False\r\n'
        'MouseClip=False\r\n'
        'AlwaysOnTop=True\r\n'
        'FPSCap=True\r\n'
        'Resolution=1024\r\n'
        'DebugMode=False\r\n'
        'UseMouseCursors=True\r\n'
        '\r\n'
        '[Network]\r\n'
        'UseConfig=True\r\n'
        'IPAddress=127.0.0.1\r\n'
        'Port=7000\r\n'
        '\r\n'
        '[MicroClient]\r\n'
        'Enabled=True\r\n'
        'Host=%s\r\n'
        'Timeout=8000\r\n'
        'Log=True\r\n'
        '#资源严格按照"用到才下"补齐，绝不在启动时批量预下载\r\n'
        'Concurrency=2\r\n'
        'RateLimit=0\r\n'
        '#是否在聊天框显示微端下载提示，False=不打扰玩家（默认），True=排障时打开\r\n'
        'Hint=False\r\n'
        % host
    )
    with open(os.path.join(dst, 'Mir2Config.ini'), 'wb') as fh:
        fh.write(config.encode('utf-8'))

    # 7) 预先建好几张按需目录，避免首次运行时报找不到目录
    for d in ['Monster', 'NPC', 'Map']:
        os.makedirs(os.path.join(data_dst, d), exist_ok=True)
    os.makedirs(os.path.join(dst, 'Map'), exist_ok=True)
    os.makedirs(os.path.join(dst, 'Sound'), exist_ok=True)

    print()
    print('微端客户端已生成: ' + dst)
    print('  文件数: %d' % copied)
    print('  体积  : %s' % human(copied_bytes))
    print('  资源服务: %s' % host)
    print()
    print('提示：')
    print('  1. 该目录已含 microclient.flag，客户端会自动开启按需下载；')
    print('  2. 服务端 Configs\\Setup.ini 需保证 EnableResourceService=True；')
    print('  3. 上面的 Host 若给外网玩家用，要换成服务端可被访问的地址。')
    return 0


if __name__ == '__main__':
    sys.exit(main())
