import os, hashlib, collections, re
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
CLI = r"E:\micromir2\Sound"
cli = set(os.listdir(CLI))
h2f = collections.defaultdict(list)
for f in os.listdir(SRV):
    p=os.path.join(SRV,f)
    if not os.path.isfile(p): continue
    if os.path.splitext(f)[1].lower() not in (".wav",".mp3"): continue
    h2f[hashlib.md5(open(p,'rb').read()).hexdigest()].append(f)
uniq = []
for h,v in h2f.items():
    if len(v)==1 and re.match(r'^M\d+-', v[0]):
        uniq.append(v[0])
def key(f):
    m=re.match(r'^M(\d+)-(\d+)$', f[:-4], re.I)
    return (int(m.group(1)), int(m.group(2)))
uniq.sort(key=key)
print("M 系列中「内容全局唯一」的音效数:", len(uniq))
print("其中客户端本地已有的:", sum(1 for f in uniq if f in cli))
for f in uniq:
    print(("  [本地]" if f in cli else "  [需下载]"), f, os.path.getsize(os.path.join(SRV,f)))
