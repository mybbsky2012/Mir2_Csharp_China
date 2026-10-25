# -*- coding: utf-8 -*-
"""修复 Server.MirDB 里 MagicInfo 的 Icon 值。
公式：客户端画 MagIcon/MagIcon2 第 Icon*2 帧；官网图标 id=N 对应帧 (N-1)*2，
所以 Icon 必须等于 官网id-1。当前运行库 90 条里 89 条是错的（两类坏值：
一类存成了帧号(=2*(id-1))，一类是乱值）。
用法：先停服务端！python patch_mirdb_magic_icons.py  会自动备份。
定位方式：技能名(长度前缀字符串) + Spell字节 + Level1/2/3 与 Envir.cs 完全一致 → 唯一命中才改。
"""
import struct, json, re, shutil, sys, os

DB = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Server.MirDB"

spell_enum = json.load(open("tools/mir2db/spell_enum.json", encoding="utf-8"))
cdn = {}
for lid in [49,50,51,52,53]:
    d = json.load(open(f"tools/mir2db/sg_{lid}.json", encoding="utf-8"))
    for r in d["data"]:
        try: cdn[r["name_cn"]] = int(r["id"])
        except: pass
# 别名：服务端名 -> 官网名
ALIAS = {"嗜血术":"噬血术", "万金闪":"万斤闪"}

src = open(r"D:\mir2-20241027\Server\MirEnvir\Envir.cs", encoding="utf-8").read()
env = {}
for m in re.finditer(r'MagicInfoList\.Add\(new MagicInfo\s*\{(.{0,900}?)\}\);', src, re.S):
    b = m.group(1)
    nm = re.search(r'Name\s*=\s*"([^"]+)"', b)
    ic = re.search(r'\bIcon\s*=\s*(\d+)', b)
    sp = re.search(r'Spell\s*=\s*Spell\.(\w+)', b)
    l1 = re.search(r'Level1\s*=\s*(\d+)', b)
    if nm and sp:
        env[nm.group(1)] = dict(spell=sp.group(1), envir_icon=int(ic.group(1)) if ic else -1,
                                l1=int(l1.group(1)) if l1 else None)

def find_occ(name):
    b = name.encode("utf-8"); n = len(b)
    pat = bytes([n]) + b if n < 128 else bytes([(n & 0x7F) | 0x80, (n >> 7) & 0x7F]) + b
    out, start = [], 0
    while True:
        i = D.find(pat, start)
        if i < 0: break
        out.append(i); start = i + 1
    return out

def skip_prefix(pos):
    n = 0; shift = 0; p = pos
    while True:
        if p >= len(D): return None
        c = D[p]; p += 1
        n |= (c & 0x7F) << shift
        if not (c & 0x80): break
        shift += 7
        if shift > 14: return None
    return p + n

D = bytearray(open(DB, "rb").read())
patched, skipped_noofficial, skipped_notfound, ambiguous = [], [], [], []

for name, info in sorted(env.items(), key=lambda x: x[0]):
    want = spell_enum.get(info["spell"])
    if want is None: continue
    off_name = cdn.get(name) or cdn.get(ALIAS.get(name, ""))
    if off_name is None:
        skipped_noofficial.append(name); continue
    target_icon = off_name - 1
    hits = []
    for o in find_occ(name):
        p = skip_prefix(o)
        if p is None or p + 7 > len(D): continue
        if D[p] != want: continue
        l1 = D[p+4]
        if info["l1"] is not None and l1 != info["l1"]: continue
        hits.append(p)
    if not hits:
        skipped_notfound.append(name); continue
    if len(hits) > 1:
        # 多命中时只改 Icon 字节相同的（同一记录重复出现的情况极少）
        icons = {D[h+3] for h in hits}
        if len(icons) > 1:
            ambiguous.append((name, len(hits))); continue
    for p in hits:
        old = D[p+3]
        if old != target_icon:
            D[p+3] = target_icon
            patched.append((name, old, target_icon, off_name))

print(f"待修正: {len(patched)} 条  无官网对照跳过: {len(skipped_noofficial)}  DB未找到: {len(skipped_notfound)}  多义跳过: {len(ambiguous)}")
for n,o,t,cid in patched:
    print(f"  {n:<14} Icon {o} -> {t}   (官网id{cid}, 帧{(cid-1)*2})")
if skipped_noofficial: print("无官网对照:", ", ".join(skipped_noofficial))
if skipped_notfound:   print("DB未找到:", ", ".join(skipped_notfound))
if ambiguous:          print("多义:", ambiguous)

if "--apply" not in sys.argv:
    print("\n[预览模式] 加 --apply 才写入。")
    sys.exit(0)

bak = DB + "." + __import__("time").strftime("%Y%m%d_%H%M") + ".bak"
shutil.copy2(DB, bak)
open(DB, "wb").write(D)
print(f"\n已写入 {DB}\n备份: {bak}")
