import os, hashlib
CLI = r"E:\micromir2\Sound"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
def h(p):
    return hashlib.md5(open(p,'rb').read()).hexdigest() if os.path.exists(p) else None
def probe(label, base, off, rng):
    print(f"--- {label}  (Crystal {rng[0]}~{rng[1]}  期望文件 = M{{Spell+{off}}}) ---")
    for d, tag in ((CLI,"cli"),(SRV,"srv")):
        rows=[]
        for sp in range(rng[0], rng[1]+1):
            for v in range(0,4):
                a = os.path.join(d, f"M{sp}-{v}.wav"); b = os.path.join(d, f"M{sp+off}-{v}.wav")
                if os.path.exists(a) and os.path.exists(b):
                    rows.append(("=" if h(a)==h(b) else "≠", f"M{sp}-{v}", f"M{sp+off}-{v}"))
        if rows:
            eq = [r for r in rows if r[0]=="="]
            print(f"  [{tag}] 同名同号对 {len(rows)} 组，内容相同 {len(eq)} 组")
            for r in rows[:14]: print("      ", r[0], r[1], "<->", r[2])
probe("道士", 61, 40, (61,88))
probe("法师", 31, 20, (31,58))
probe("刺客", 91, 60, (91,107))
probe("弓手", 121, 80, (121,141))
