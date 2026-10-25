import os, shutil, datetime, hashlib
BS = chr(92)
CLI = r"E:\micromir2\Sound" + BS + "SoundList.lst"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound" + BS + "SoundList.lst"
ADD = [
    (20650, "400-1.wav"),    # 召唤骷髅  -> 骷髅本体音
    (20780, "1900-1.wav"),   # 召唤神兽  -> 神兽本体音
    (20800, "1920-0.wav"),   # 召唤圣兽  -> 月灵/圣兽本体音
]
stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
for path in (CLI, SRV):
    raw = open(path, "rb").read()
    # 先去掉已有的同索引行（幂等）
    lines = raw.decode("utf-8-sig", errors="ignore").splitlines()
    keep = []
    added_idx = set(i for i, _ in ADD)
    for ln in lines:
        ps = ln.replace(" ", "").split(":")
        try:
            if len(ps) > 1 and int(ps[0]) in added_idx: continue
        except Exception: pass
        keep.append(ln)
    while keep and keep[-1].strip() == "": keep.pop()
    for idx, fn in ADD:
        keep.append(f"{idx}: {fn}")
    new = ("\r\n".join(keep) + "\r\n").encode("utf-8-sig")
    bak = path + "." + stamp + ".bak"
    shutil.copy2(path, bak)
    open(path, "wb").write(new)
    print(f"{os.path.basename(path)}: {len(raw)} -> {len(new)} 字节  备份={os.path.basename(bak)}")
    print("   md5:", hashlib.md5(new).hexdigest())
print()
for path, tag in ((CLI, "客户端"), (SRV, "服务端")):
    txt = open(path, encoding="utf-8-sig", errors="ignore").read()
    print(f"--- {tag} 尾部 ---")
    print(BS.join(txt.strip().splitlines()[-3:]) if False else "\n".join(txt.strip().splitlines()[-3:]))
