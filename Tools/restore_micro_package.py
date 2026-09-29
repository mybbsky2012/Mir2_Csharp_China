# -*- coding: utf-8 -*-
"""
把微端包恢复成「干净的发布包」：移走玩家运行时按需下载进来的本地缓存。

判断依据用**修改时间**：包生成时复制的文件保留的是原始时间戳（几个月甚至几年前），
而运行时下载进来的文件，mtime 就是下载发生的那一刻。
用户的运行日志显示下载发生在 10:23:48 ~ 10:24:12，所以按这个时间窗筛。

安全前提：移走前逐个核对「完整客户端里存在同名同大小的原件」——
这些文件本来就是从服务端（完整客户端）下载来的副本，核对通过才移动，绝不丢数据。
移动目标是同目录层级的 _runtime_cache 文件夹，不是删除。
"""

import os
import shutil
import sys
import time

MICRO = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client_Micro'
FULL = r'D:\BaiduNetdiskDownload\20260915更新\20260915Client'
STASH = os.path.join(MICRO, '_runtime_cache')

# 下载发生的时间窗（来自 MicroClient.log）
WIN_START = time.mktime(time.strptime('2026-09-29 10:20:00', '%Y-%m-%d %H:%M:%S'))
WIN_END = time.mktime(time.strptime('2026-09-29 10:30:00', '%Y-%m-%d %H:%M:%S'))


def main():
    candidates = []
    for root, _dirs, files in os.walk(MICRO):
        if '_runtime_cache' in root:
            continue
        for f in files:
            full = os.path.join(root, f)
            try:
                mtime = os.path.getmtime(full)
            except OSError:
                continue
            if WIN_START <= mtime <= WIN_END:
                candidates.append(full)

    candidates.sort()

    ok = []
    missing = []
    for path in candidates:
        rel = os.path.relpath(path, MICRO)
        origin = os.path.join(FULL, rel)
        if os.path.exists(origin) and os.path.getsize(origin) == os.path.getsize(path):
            ok.append((path, rel))
        else:
            missing.append((path, rel))

    print('时间窗内文件 : %d' % len(candidates))
    print('核对通过     : %d  （完整客户端里有同名同大小的原件）' % len(ok))
    print('核对不通过   : %d' % len(missing))

    if missing:
        print('\n以下文件在完整客户端里找不到对应原件，**不会移动**：')
        for path, rel in missing:
            print('  ' + rel)
        print()

    if not ok:
        print('没有可安全移动的文件。')
        return 0

    total = sum(os.path.getsize(p) for p, _ in ok)
    print('\n准备移走 %d 个文件，合计 %.1f MB：' % (len(ok), total / 1048576.0))
    for path, rel in ok:
        print('  %8.2f MB  %s' % (os.path.getsize(path) / 1048576.0, rel))

    os.makedirs(STASH, exist_ok=True)
    moved = 0
    for path, rel in ok:
        dest = os.path.join(STASH, rel)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.move(path, dest)
        moved += 1

    print('\n已移走 %d 个文件到: %s' % (moved, STASH))
    print('（没有删除任何东西；这些文件在完整客户端里有原件，随时可重新下载）')
    return 0


if __name__ == '__main__':
    sys.exit(main())
