p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
lines = open(p,'rb').read().decode('euc-kr').splitlines()
for lo,hi,tag in ((3436,3476,"道士段 M50~M71"),(3528,3556,"M100~M102")):
    print(f"===== {tag}  原始行 {lo}~{hi} =====")
    for i in range(lo,hi+1):
        if i-1 < len(lines): print(f"{i:5d}| {lines[i-1]}")
    print()
