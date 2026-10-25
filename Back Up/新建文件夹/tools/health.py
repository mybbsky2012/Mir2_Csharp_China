import os, hashlib
CLI = r"E:\micromir2\Sound"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
def h(d,f):
    p=os.path.join(d,f)
    return hashlib.md5(open(p,'rb').read()).hexdigest() if os.path.exists(p) else None
CLASSES = [
 ("战士", 1, 22, 0),
 ("法师", 31, 58, 20),
 ("道士", 61, 88, 40),
 ("刺客", 91, 107, 60),
 ("弓手", 121, 141, 80),
]
for d,tag in ((SRV,"服务端"),(CLI,"客户端")):
    print(f"########## {tag} ##########")
    for name, lo, hi, off in CLASSES:
        ok=bad=miss=0; badlist=[]
        for sp in range(lo,hi+1):
            hit=False
            for v in range(0,10):
                a=h(d,f"M{sp}-{v}.wav"); b=h(d,f"M{sp+off}-{v}.wav")
                if a is None: continue
                hit=True
                if b is None: miss+=1
                elif a==b: ok+=1
                else:
                    bad+=1; badlist.append(f"M{sp}-{v}≠M{sp+off}-{v}")
            if not hit: pass
        print(f"  {name}({lo}-{hi}, offset +{off}):  本号槽位 已正确={ok}  内容不符={bad}  目标缺失={miss}")
        if badlist: print("      不符示例:", ", ".join(badlist[:8]))
    print()
