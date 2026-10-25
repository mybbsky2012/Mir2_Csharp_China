import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
txt = open(p,'rb').read().decode('euc-kr')
lines = txt.splitlines()
prev = []
print("=== 行号 | 索引 11000~11400 区域 + 其上方最近注释 ===")
for i,l in enumerate(lines,1):
    s = l.strip()
    m = re.match(r'^(//)?\s*(\d+)\s*:\s*(.*)$', s)
    if m:
        idx = int(m.group(2))
        if 11000 <= idx <= 11400:
            # 往上找最近注释
            cmt = ""
            for j in range(i-2, max(0,i-6), -1):
                t = lines[j].strip()
                if t.startswith(';;') or t.startswith('//'):
                    cmt = t; break
            print(f"{i:5d} | {idx:5d} | {m.group(3):20s} | {cmt}")
