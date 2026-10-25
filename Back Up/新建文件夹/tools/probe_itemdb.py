# -*- coding: utf-8 -*-
"""在服务端物品数据库里定位护身符类物品，读出 Shape（0=符 1/2=毒粉）"""
import struct

PATH = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Server.MirDB"

d = open(PATH, "rb").read()
print("db size", len(d))


def find_all(b):
    out = []
    i = d.find(b)
    while i >= 0:
        out.append(i)
        i = d.find(b, i + 1)
    return out


for name in ["黄色毒粉", "灰色毒粉", "护身符"]:
    for enc in ("utf-8", "gbk"):
        b = name.encode(enc)
        pos = find_all(b)
        if not pos:
            continue
        print("---- %s (%s) hits=%d" % (name, enc, len(pos)))
        for i in pos[:8]:
            start = i + len(b)
            raw = d[start:start + 12]
            print("   at %d  next12=%s (hex %s)" % (i, list(raw), raw.hex()))
