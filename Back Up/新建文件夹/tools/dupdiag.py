import os, hashlib, collections
C = r"E:\micromir2\Sound"
h2f = collections.defaultdict(list)
for f in os.listdir(C):
    p = os.path.join(C,f)
    if not os.path.isfile(p): continue
    if os.path.splitext(f)[1].lower() not in (".wav",".mp3"): continue
    h = hashlib.md5(open(p,'rb').read()).hexdigest()
    h2f[h].append(f)
# 找 M6x/M7x 的同内容伙伴
targets = [f for f in os.listdir(C) if f.upper().startswith("M") and ("M6" in f or "M7" in f)]
import re
for f in sorted(os.listdir(C)):
    if re.match(r'^M(6[1-9]|7[0-9]|8[0-9])-', f, re.I):
        p=os.path.join(C,f)
        if not os.path.isfile(p): continue
        h=hashlib.md5(open(p,'rb').read()).hexdigest()
        peers=[x for x in h2f[h] if x.lower()!=f.lower()]
        print(f"{f:14s} size={os.path.getsize(p):8d}  peers={peers if peers else '无'}")
