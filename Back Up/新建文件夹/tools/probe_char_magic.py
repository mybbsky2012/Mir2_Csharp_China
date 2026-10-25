# -*- coding: utf-8 -*-
import os, struct

DB = r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.MirDB"
D = open(DB,'rb').read()
print("DB size:", len(D))

names = ["道爷爷", "一刀剁了你", "大大哒哒", "法海啊"]
for nm in names:
    b = nm.encode('gbk')
    print("="*70)
    print("角色:", nm, " GBK出现次数:", D.count(b))
    # .NET BinaryWriter 写字符串：7bit 编码长度 + UTF8
    ub = nm.encode('utf-8')
    pats = []
    # 长度 <128 时 7bit 编码就是 1 字节
    if len(ub) < 128:
        pats.append(bytes([len(ub)]) + ub)
    idxs = []
    for p in pats:
        st = 0
        while True:
            i = D.find(p, st)
            if i < 0: break
            idxs.append((i, len(p)))
            st = i + 1
    print("  .NET字符串出现位置:", [i for i,_ in idxs][:6])
    for i, ln in idxs[:3]:
        seg = D[i+ln : i+ln+120]
        print("   @%d 后续120字节: %s" % (i, seg.hex()))
        # 打印可读
        print("        ascii:", ''.join(chr(c) if 32 <= c < 127 else '.' for c in seg))
