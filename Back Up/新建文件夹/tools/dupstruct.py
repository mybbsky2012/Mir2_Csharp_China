import os, hashlib, collections, re
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
h2f = collections.defaultdict(list)
for f in os.listdir(SRV):
    p=os.path.join(SRV,f)
    if not os.path.isfile(p): continue
    if os.path.splitext(f)[1].lower() not in (".wav",".mp3"): continue
    h2f[hashlib.md5(open(p,'rb').read()).hexdigest()].append(f)
groups=[sorted(v) for v in h2f.values() if len(v)>1]
groups.sort(key=lambda g:-len(g))
print("重复组数:", len(groups), " 总文件:", sum(len(v) for v in h2f.values()), " 唯一内容:", len(h2f))
# 只关心 M开头的组
print("\n--- 含 M 系列文件的重复组（前 60）---")
n=0
for g in groups:
    ms=[x for x in g if re.match(r'^M\d+-', x, re.I)]
    if not ms: continue
    print(" | ".join(g))
    n+=1
    if n>=60: break
