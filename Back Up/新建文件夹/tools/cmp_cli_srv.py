import os, hashlib
C = r"E:\micromir2\Sound"
S = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
def md5(p):
    return hashlib.md5(open(p,'rb').read()).hexdigest()
diff=[]; same=0; only_c=[]
for f in sorted(os.listdir(C)):
    pc=os.path.join(C,f)
    if not os.path.isfile(pc): continue
    ps=os.path.join(S,f)
    if not os.path.exists(ps):
        only_c.append(f); continue
    if md5(pc)==md5(ps): same+=1
    else: diff.append((f, os.path.getsize(pc), os.path.getsize(ps)))
print("客户端文件数:", len([x for x in os.listdir(C) if os.path.isfile(os.path.join(C,x))]))
print("同名且内容一致:", same)
print("同名但内容不同:", len(diff))
for f,a,b in diff[:40]: print(f"   差异 {f}  cli={a} srv={b}")
print("客户端独有(服务端没有):", len(only_c))
print("   ", only_c[:40])
