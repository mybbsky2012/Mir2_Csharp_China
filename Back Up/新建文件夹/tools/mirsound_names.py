import re, json
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
txt = open(p,'rb').read().decode('euc-kr')
lines = txt.splitlines()
pending = None
blocks = []  # (start_idx, name)
for l in lines:
    s = l.strip()
    if s.startswith(';;'):
        pending = s.lstrip(';').strip()
        continue
    m = re.match(r'^(//)?\s*(\d+)\s*:\s*(.*)$', s)
    if m and pending:
        blocks.append((int(m.group(2)), pending, m.group(3)))
        pending = None
# 建 idx -> name (取该 idx 之前最近的注释)
name_of = {}
for start, name, val in blocks:
    name_of[start] = (name, val)
# 输出我们关心的区间
keys = sorted(name_of)
def show(lo, hi):
    for k in keys:
        if lo <= k <= hi:
            print(f"{k:6d}  {name_of[k][0]:24s} -> {name_of[k][1]}")
print("=== 0~500 ===");  show(0, 500)
print("=== 1800~1960 ==="); show(1800, 1960)
print("=== 8080~8120 ==="); show(8080, 8120)
