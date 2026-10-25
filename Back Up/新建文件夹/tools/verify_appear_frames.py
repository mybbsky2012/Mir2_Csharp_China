# -*- coding: utf-8 -*-
"""严格复刻客户端 MLibrary.Initialize() 的 .Lib 解析，验证召唤物图库里是否真的含动作 27（召唤初现）。"""
import struct, os

BASE = r"E:\micromir2\Data\Monster"
TARGETS = [(36, "骷髅 BoneFamiliar"), (170, "神兽 Shinsu"), (172, "月灵 HolyDeva")]
NAMES = {27: "召唤初现"}


def dump(path, label):
    D = open(path, "rb").read()
    ver, cnt = struct.unpack_from("<ii", D, 0)
    print("\n%s  %s   version=%d  images=%d" % (label, os.path.basename(path), ver, cnt))
    if ver < 3:
        print("   无内嵌帧段（version < 3）")
        return
    fs, = struct.unpack_from("<i", D, 8)
    if fs <= 0 or fs >= len(D):
        print("   无内嵌帧段（frameSeek=%d）" % fs)
        return
    fc, = struct.unpack_from("<i", D, fs)
    print("   帧段数 = %d" % fc)
    p = fs + 4
    hit = False
    for i in range(fc):
        act = D[p]; p += 1
        st, ct, sk, iv, es, ec, esk, eiv = struct.unpack_from("<8i", D, p); p += 32
        p += 2  # Reverse, Blend
        if act == 27:
            hit = True
            print("   action=27 start=%-5d count=%-4d skip=%-4d interval=%-5d  ★★★ 召唤初现" % (st, ct, sk, iv))
        else:
            print("   action=%-4d start=%-5d count=%-4d skip=%-4d interval=%-5d" % (act, st, ct, sk, iv))
    print("   => 含『召唤初现』: %s" % ("是" if hit else "否"))


for n, lab in TARGETS:
    p = os.path.join(BASE, "%03d.Lib" % n)
    if os.path.exists(p):
        dump(p, lab)
    else:
        print("\n%s  %s  【文件不存在】" % (lab, p))
