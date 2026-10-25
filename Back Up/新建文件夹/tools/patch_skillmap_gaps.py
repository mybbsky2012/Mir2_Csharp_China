import os, shutil, datetime, hashlib

BS = chr(92)
CLI = r"E:\micromir2\Sound" + BS + "SoundList.lst"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound" + BS + "SoundList.lst"

# 代码会调用、但原映射目标文件不存在 -> 借用同技能其它变体，避免静音
SUPP = [
    (20471, "M67-0.wav",  "法师 47 +1  (原目标 M67-1 缺)"),
    (20861, "M126-0.wav", "道士 86 +1  (原目标 M126-1 缺)"),
    (21264, "M206-5.wav", "弓手 126 +4 (原目标 M206-4 缺)"),
    (21377, "M217-6.wav", "弓手 137 +7 (原目标 M217-7 缺)"),
    (21381, "M218-0.wav", "弓手 138 +1 (原目标 M218-1 缺)"),
]

stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
for path in (CLI, SRV):
    raw = open(path, "rb").read()
    lines = raw.decode("utf-8-sig", errors="ignore").splitlines()
    keep, skill = [], {}
    for ln in lines:
        ps = ln.replace(" ", "").split(":")
        idx = None
        if len(ps) > 1:
            try:
                idx = int(ps[0])
            except Exception:
                idx = None
        if idx is not None and idx >= 20000:
            skill.setdefault(idx, ps[-1])
        else:
            keep.append(ln)
    before = len(skill)
    for idx, tgt, note in SUPP:
        old = skill.get(idx)
        skill[idx] = tgt
        print(f"   {idx}: {old} -> {tgt}   ({note})")
    while keep and keep[-1].strip() == "":
        keep.pop()
    out = keep + [f"{i}: {skill[i]}" for i in sorted(skill)]
    new = ("\r\n".join(out) + "\r\n").encode("utf-8-sig")
    bak = path + "." + stamp + ".bak"
    shutil.copy2(path, bak)
    open(path, "wb").write(new)
    print(f"{os.path.basename(os.path.dirname(path))}/SoundList.lst: {len(raw)} -> {len(new)} 字节  "
          f"20xxx: {before} -> {len(skill)} 条  备份={os.path.basename(bak)}  md5={hashlib.md5(new).hexdigest()}")
