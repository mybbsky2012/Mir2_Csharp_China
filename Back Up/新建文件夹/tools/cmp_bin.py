# -*- coding: utf-8 -*-
import hashlib, os

def md5(fn):
    try:
        h = hashlib.md5()
        with open(fn,'rb') as f:
            for b in iter(lambda: f.read(1<<20), b''): h.update(b)
        return h.hexdigest()
    except Exception as e: return "ERR:%s"%e

pairs = [
 ("Client.dll", r"E:/micromir2/Client.dll", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Client.dll"),
 ("Client.exe", r"E:/micromir2/Client.exe", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Client.exe"),
 ("Shared.dll", r"E:/micromir2/Shared.dll", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Shared.dll"),
 ("Data/Magic_32bit.Lib", r"E:/micromir2/Data/Magic_32bit.Lib", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Data/Magic_32bit.Lib"),
 ("Data/Magic.Lib", r"E:/micromir2/Data/Magic.Lib", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Data/Magic.Lib"),
 ("Data/Magic2.Lib", r"E:/micromir2/Data/Magic2.Lib", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Data/Magic2.Lib"),
 ("Data/Magic3.Lib", r"E:/micromir2/Data/Magic3.Lib", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Data/Magic3.Lib"),
 ("Shared(server)", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Shared.dll", r"D:/mir2-20241027/Build/Server/Release/Shared.dll"),
 ("Server.Library(run)", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.Library.dll", r"D:/mir2-20241027/Build/Server/Release/Server.Library.dll"),
 ("Server(run)", r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.dll", r"D:/mir2-20241027/Build/Server/Release/Server.dll"),
]
for name, a, b in pairs:
    ma, mb = md5(a), md5(b)
    same = (ma == mb)
    print("%-24s %s" % (name, "SAME" if same else "*** DIFFERENT ***"))
    print("    A %s  %s" % (ma[:16], a))
    print("    B %s  %s" % (mb[:16], b))
