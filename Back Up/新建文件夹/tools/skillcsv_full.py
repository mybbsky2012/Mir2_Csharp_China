p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Exports\技能数据.csv"
txt = open(p,'rb').read().decode('utf-8-sig')
lines = txt.splitlines()[1:]
print("行序 | 技能名 | 法术")
for i,l in enumerate(lines,1):
    parts = l.split(',')
    if len(parts) >= 2 and parts[0]:
        print(f"{i:3d} | {parts[0]:14s} | {parts[1]}")
