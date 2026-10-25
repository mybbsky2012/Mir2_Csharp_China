import re
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
lines = open(p,'rb').read().decode('euc-kr').splitlines()
print("=== 原始行 3495~3540 (MagicID 85~100) ===")
for i in range(3495,3541):
    print(f"{i:5d}| {lines[i-1]}")
