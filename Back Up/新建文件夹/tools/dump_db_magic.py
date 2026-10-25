# -*- coding: utf-8 -*-
import struct, sys, os

def load(fn):
    with open(fn,'rb') as f: return f.read()

def text(d, p):
    ln = d[p]; return d[p+1:p+1+ln].decode('gbk','replace')

for db in [r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.MirDB",
           r"D:/mir2-20241027/Mir2Server/Server.MirDB"]:
    if not os.path.exists(db):
        print("SKIP(missing):", db); continue
    D = load(db)
    print("="*80)
    print(db, len(D), "bytes")
    key = "MagicInfoList".encode('gbk')
    i = D.find(key)
    print("MagicInfoList marker at:", i)
