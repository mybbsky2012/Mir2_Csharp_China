# -*- coding: utf-8 -*-
import os

for DB in [r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.MirADB",
           r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.MirDB"]:
    if not os.path.exists(DB):
        print("MISSING", DB); continue
    D = open(DB,'rb').read()
    print("="*74)
    print(DB, len(D), "bytes")
    for enc in ('utf-8','gbk'):
        for nm in ["道爷爷","一刀剁了你","大大哒哒","法海啊","月忘忧"]:
            try: b = nm.encode(enc)
            except Exception: continue
            c = D.count(b)
            if c:
                i = D.find(b)
                print("   [%s] %-10s x%d  @%d" % (enc, nm, c, i))
    # 看看文件头
    print("   head64:", D[:64].hex())
