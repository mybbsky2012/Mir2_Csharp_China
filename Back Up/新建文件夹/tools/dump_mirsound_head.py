import os
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
txt = open(p,'rb').read().decode('euc-kr')
lines = txt.splitlines()
print("### 前 160 行 ###")
for i,l in enumerate(lines[:160],1):
    print(f"{i:4d}| {l}")
