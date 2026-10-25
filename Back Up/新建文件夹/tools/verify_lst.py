import os, re
BS = chr(92)
CLI = r"E:\micromir2\Sound" + BS + "SoundList.lst"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound" + BS + "SoundList.lst"
DIRS = [r"E:\micromir2\Sound",
        r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"]

def load(path):
    """严格复刻 C# : lines[i].Replace(" ","").Split(':','\t'); 取最后一段"""
    idx = {}
    for line in open(path, encoding="utf-8-sig", errors="ignore").read().splitlines():
        sp = re.split(r'[:\t]', line.replace(" ", ""))
        if len(sp) <= 1: continue
        try: i = int(sp[0])
        except Exception: continue
        if i not in idx: idx[i] = sp[-1]
    return idx

for path, tag in ((CLI, "客户端"), (SRV, "服务端")):
    d = load(path)
    print(f"=== {tag} ===  条目数={len(d)}")
    for i, fn in ((20650, "400-1.wav"), (20780, "1900-1.wav"), (20800, "1920-0.wav")):
        got = d.get(i, "(未定义)")
        ok = "OK" if got == fn else "!!"
        exists = sum(1 for dd in DIRS if os.path.exists(os.path.join(dd, got)))
        print(f"  [{ok}] {i} -> {got}  (本地文件存在: {exists}/2)")
    # 未定义的道士技能索引仍应走兜底
    for i in (20610, 20640, 20660, 20680, 20690, 20710, 20730, 20740, 20790, 20810, 20820, 20830, 20840, 20860, 20880):
        if i in d: print(f"  [注意] {i} 被定义为 {d[i]!r}")
    print()
