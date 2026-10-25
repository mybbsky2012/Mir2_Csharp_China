import os, re, shutil, datetime, hashlib, json
BS = chr(92)
CLI = r"E:\micromir2\Sound" + BS + "SoundList.lst"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound" + BS + "SoundList.lst"
SDIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
CDIR = r"E:\micromir2\Sound"
CLASSES = [("法师",31,58,20),("道士",61,88,40),("刺客",91,107,60),("弓手",121,141,80)]
srv = set(os.listdir(SDIR)); cli = set(os.listdir(CDIR))

entries = []
gaps = []
for name, lo, hi, off in CLASSES:
    for sp in range(lo, hi+1):
        avail = [v for v in range(0,10) if f"M{sp+off}-{v}.wav" in srv or f"M{sp+off}-{v}.wav" in cli]
        if not avail:
            gaps.append(f"{name} {sp}: 无任何目标文件"); continue
        for v in range(0,10):
            if v in avail: tgt = f"M{sp+off}-{v}.wav"; sub = False
            elif v == 0:   tgt = f"M{sp+off}-{avail[0]}.wav"; sub = True      # +0 缺 → 借用同技能最小变体
            else: continue
            entries.append((20000 + sp*10 + v, tgt, name, sp, v, sub))
entries.sort()
print("生成条目:", len(entries), "  变体替身:", sum(1 for e in entries if e[5]))
for g in gaps: print("  缺口:", g)

stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
for path in (CLI, SRV):
    raw = open(path, "rb").read()
    lines = raw.decode("utf-8-sig", errors="ignore").splitlines()
    keep = []
    removed = 0
    for ln in lines:
        ps = ln.replace(" ", "").split(":")
        ok = False
        if len(ps) > 1:
            try:
                if int(ps[0]) >= 20000: ok = True
            except Exception: pass
        if ok: removed += 1; continue
        keep.append(ln)
    while keep and keep[-1].strip() == "": keep.pop()
    for idx, tgt, cls, sp, v, sub in entries:
        keep.append(f"{idx}: {tgt}")
    new = ("\r\n".join(keep) + "\r\n").encode("utf-8-sig")
    bak = path + "." + stamp + ".bak"
    shutil.copy2(path, bak)
    open(path, "wb").write(new)
    print(f"{os.path.basename(os.path.dirname(path))}/SoundList.lst: {len(raw)} -> {len(new)} 字节  替换掉旧 20xxx {removed} 条  备份={os.path.basename(bak)}  md5={hashlib.md5(new).hexdigest()}")
