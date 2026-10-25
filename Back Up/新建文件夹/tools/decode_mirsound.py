import os
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound\mirsound.lst"
if not os.path.exists(p):
    # fallback: search
    import glob
    for c in glob.glob(r"E:\BaiduNetdiskDownload\韩Mir2\**\mirsound.lst", recursive=True):
        p = c; break
print("FILE:", p, os.path.getsize(p) if os.path.exists(p) else "N/A")
raw = open(p, 'rb').read()
for enc in ("euc-kr","cp949","utf-8"):
    try:
        txt = raw.decode(enc)
        print("DECODED as", enc)
        break
    except Exception as e:
        print(enc, "fail")
lines = txt.splitlines()
print("total lines:", len(lines))
print("="*60)
# print lines containing non-ascii or ';;'
n=0
for i,l in enumerate(lines,1):
    if any(ord(c)>127 for c in l) or ';;' in l or '//' in l:
        print(i, l)
        n+=1
        if n>120: 
            print("...truncated")
            break
