import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
lines = open(p,'rb').read().decode('euc-kr').splitlines()
print("=== 10000~10310 区域（原始行）===")
for i,l in enumerate(lines,1):
    s=l.strip()
    m = re.match(r'^(//)?\s*(\d+)\s*:', s)
    if m and 10000 <= int(m.group(2)) <= 10310:
        print(f"{i:5d}| {l}")
