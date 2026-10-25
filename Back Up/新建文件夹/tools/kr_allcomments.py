import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
lines = open(p,'rb').read().decode('euc-kr').splitlines()
cur = None
out = []
for i,l in enumerate(lines,1):
    s = l.strip()
    if s.startswith(';;'):
        cur = s.lstrip(';').strip(); continue
    if s.startswith('//'):
        cur = s[2:].strip(); continue
    m = re.match(r'^(\d+)\s*:\s*(.*)$', s)
    if m:
        out.append((int(m.group(1)), cur or "", m.group(2), i))
        if cur: pass
# 只保留 10000 以上（技能段）
sk = [o for o in out if o[0] >= 10000]
print("技能段条目总数:", len(sk))
prev_c = None
for idx, cmt, val, ln in sk:
    if cmt and cmt != prev_c:
        print(f"\n;; {cmt}   (mid={idx//10})")
        prev_c = cmt
    print(f"   {idx:5d} -> {val}")
