import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
txt = open(p,'rb').read().decode('euc-kr')
lines = txt.splitlines()
print("=== 索引 10000~11400 的显式条目（含被注释的）===")
for i,l in enumerate(lines,1):
    s=l.strip()
    m=re.match(r'^(//)?\s*(\d+)\s*:\s*(.*)$', s)
    if m:
        idx=int(m.group(2))
        if 10000<=idx<=11400:
            mark = "X" if m.group(1) else " "
            print(f"[{mark}] {idx:5d} | {m.group(3)}")
