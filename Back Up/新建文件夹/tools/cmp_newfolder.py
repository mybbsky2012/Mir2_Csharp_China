import os, re, hashlib, collections
NEW = r"D:\Program Files\desktop\新建文件夹"
CLI = r"E:\micromir2\Sound"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
def md5(p): return hashlib.md5(open(p,'rb').read()).hexdigest()
def key(f):
    m = re.match(r'^M?(\d+)-(.+)\.wav$', f, re.I)
    return (int(m.group(1)), m.group(2)) if m else (9999,"")
cli_h = {}
for f in os.listdir(CLI):
    p=os.path.join(CLI,f)
    if os.path.isfile(p) and f.lower().endswith('.wav'): cli_h[f.lower()] = md5(p)
srv_h = {}
for f in os.listdir(SRV):
    p=os.path.join(SRV,f)
    if os.path.isfile(p) and f.lower().endswith('.wav'): srv_h[f.lower()] = md5(p)
# 反向索引：内容 -> 文件
inv = collections.defaultdict(list)
for f,h in cli_h.items(): inv[h].append(("cli",f))
for f,h in srv_h.items(): inv[h].append(("srv",f))

same=diff=onlynew=0
print(f"{'新建文件':14s} {'大小':>8s}  {'客户端同名':10s} {'服务端同名':10s}  说明")
for f in sorted(os.listdir(NEW), key=key):
    p = os.path.join(NEW, f); h = md5(p); sz = os.path.getsize(p)
    c = cli_h.get(f.lower()); s = srv_h.get(f.lower())
    tag=[]
    if c is None and s is None:
        onlynew += 1; tag.append("★两端都没有")
    else:
        if c is not None and c != h: tag.append("客户端内容不同")
        if s is not None and s != h: tag.append("服务端内容不同")
        if not tag: tag.append("两端一致"); same += 1
    if tag and "两端一致" not in tag: diff += 1
    peers = [(k,f2) for k,f2 in inv[h] if f2 != f.lower()]
    pstr = ("="+",".join(f"{k}:{f2}" for k,f2 in peers[:3])) if peers else ""
    print(f"{f:14s} {sz:8d}  {'有' if c else '—':10s} {'有' if s else '—':10s}  {'; '.join(tag)} {pstr}")
print(f"\n汇总: 一致={same}  有差异/新增={diff}  两端都没有={onlynew}  总={len(os.listdir(NEW))}")
