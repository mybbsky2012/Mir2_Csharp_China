import csv, io
p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Exports\技能数据.csv"
raw = open(p, 'rb').read()
for enc in ("utf-8-sig","gbk","euc-kr"):
    try:
        txt = raw.decode(enc); print("ENCODING:", enc); break
    except Exception: pass
lines = txt.splitlines()
print("行数:", len(lines))
print("表头:", lines[0])
print("-"*80)
for l in lines[:5]:
    print(l[:200])
