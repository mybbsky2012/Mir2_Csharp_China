import os, hashlib
NEW = r"D:\Program Files\desktop\新建文件夹"
CLI = r"E:\micromir2\Sound"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
def info(d, f):
    p = os.path.join(d, f)
    if not os.path.exists(p): return None
    return (os.path.getsize(p), hashlib.md5(open(p,'rb').read()).hexdigest()[:8])
print(f"{'文件':12s} {'新建(大小/md5)':>22s} {'客户端(大小/md5)':>22s} {'服务端(大小/md5)':>22s}")
for sp in [61,62,63,64,65,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86,87,88]:
    for v in range(0,3):
        f = f"M{sp}-{v}.wav"
        a = info(NEW, f); c = info(CLI, f); s = info(SRV, f)
        if not (a or c or s): continue
        fmt = lambda t: (f"{t[0]:8d} {t[1]}" if t else "     —   ")
        mark = ""
        if a and c and a[1]==c[1]: mark=" [同cli]"
        elif a and s and a[1]==s[1]: mark=" [同srv]"
        print(f"{f:12s} {fmt(a):>22s} {fmt(c):>22s} {fmt(s):>22s}{mark}")
