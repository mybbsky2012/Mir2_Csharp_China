import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
lines = open(p,'rb').read().decode('euc-kr').splitlines()
cur=None; out=[]
for i,l in enumerate(lines,1):
    s=l.strip()
    if s.startswith(';;'): cur=s.lstrip(';').strip(); continue
    m=re.match(r'^(\d+)\s*:\s*(.*)$', s)
    if m: out.append((int(m.group(1)), cur or "", m.group(2)))
KW = ["소환","해골","회복","독","저주","은신","신수","소생","정화","유령","방패","성스","무극","곤마","기공","미혼","독무","혈룡수","음양","번뇌","치유","부활","소환수","정령"]
print("=== 含道士关键词的注释 ===")
seen=set()
for idx,cmt,val in out:
    if not cmt: continue
    if any(k in cmt for k in KW) and cmt not in seen:
        seen.add(cmt)
        print(f"  mid={idx//10:4d}  {cmt:28s} -> {val}")
print()
print("=== 全部注释一览（去重，按首次出现）===")
prev=None
for idx,cmt,val in out:
    if cmt and cmt!=prev:
        print(f"  {idx//10:4d}  {cmt}")
        prev=cmt
