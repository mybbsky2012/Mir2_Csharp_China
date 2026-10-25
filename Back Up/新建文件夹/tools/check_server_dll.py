# -*- coding: utf-8 -*-
import os, hashlib

def md5(fn):
    try:
        h = hashlib.md5()
        with open(fn,'rb') as f:
            for b in iter(lambda: f.read(1<<20), b''): h.update(b)
        return h.hexdigest()[:16]
    except Exception as e:
        return "ERR"

def cnt(fn, s):
    try:
        with open(fn,'rb') as f: d = f.read()
    except Exception: return "NOFILE"
    return d.count(s.encode('utf-16-le'))

# 服务端新技能专属字符串
probes = ["必须装备【气功石】才能施展落雷杀", "需要先施放「落雷杀-秘笈」", "吸魔炎风", "影华连击", "月华乱舞-秘籍", "龙血兽", "万效符", "落雷杀"]
files = [
    r"D:/mir2-20241027/Build/Server/Release/Server.Library.dll",
    r"D:/mir2-20241027/Build/Server/Release/Server.dll",
    r"D:/mir2-20241027/Mir2Server/Server.Library.dll",
    r"D:/mir2-20241027/Mir2Server/Server.dll",
    r"D:/mir2-20241027/Build/Server/Release/Shared.dll",
    r"D:/mir2-20241027/Mir2Server/Shared.dll",
]
import datetime
for fn in files:
    if not os.path.exists(fn):
        print("MISSING:", fn); continue
    mt = datetime.datetime.fromtimestamp(os.path.getmtime(fn)).strftime("%Y-%m-%d %H:%M:%S")
    size = os.path.getsize(fn)
    print("%-62s %s  %9d  %s" % (fn, mt, size, md5(fn)))
    for p in probes:
        c = cnt(fn, p)
        if c: print("        HIT %-34s x%s" % (p, c))
