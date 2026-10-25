import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
txt = open(p,'rb').read().decode('euc-kr')
lines = txt.splitlines()
out=[]
for i,l in enumerate(lines,1):
    s=l.strip()
    m=re.match(r'^//?\s*(\d+)\s*:', s)
    if m:
        idx=int(m.group(1))
        if 10300<=idx<=11400:
            out.append((idx,i,l))
    elif s.startswith(';;') or s.startswith('//') or (';' in s and not m):
        out.append((None,i,l))
print("count:", len(out))
for idx,i,l in out:
    print(f"{i:5d}| {l}")
