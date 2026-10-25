# -*- coding: utf-8 -*-
"""严格版：只认 Spell 枚举值也对得上的 MagicInfo 记录，提取运行库(Server.MirDB)的真实 Icon。"""
import struct, json, re

DB = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Server.MirDB"
D = open(DB, "rb").read()

spell_enum = json.load(open("tools/mir2db/spell_enum.json", encoding="utf-8"))

# 官网 cn -> id
cdn = {}
for lid in [49,50,51,52,53]:
    d = json.load(open(f"tools/mir2db/sg_{lid}.json", encoding="utf-8"))
    for r in d["data"]:
        try: cdn[r["name_cn"]] = int(r["id"])
        except: pass

# Envir.cs: Name -> (SpellName, Icon)
src = open(r"D:\mir2-20241027\Server\MirEnvir\Envir.cs", encoding="utf-8").read()
env = {}
for m in re.finditer(r'MagicInfoList\.Add\(new MagicInfo\s*\{(.{0,900}?)\}\);', src, re.S):
    b = m.group(1)
    nm = re.search(r'Name\s*=\s*"([^"]+)"', b)
    ic = re.search(r'\bIcon\s*=\s*(\d+)', b)
    sp = re.search(r'Spell\s*=\s*Spell\.(\w+)', b)
    if nm and sp:
        env[nm.group(1)] = (sp.group(1), int(ic.group(1)) if ic else -1)

def find_occ(name):
    b = name.encode("utf-8"); n = len(b)
    if n < 128: pat = bytes([n]) + b
    else: pat = bytes([(n & 0x7F) | 0x80, (n >> 7) & 0x7F]) + b
    out, start = [], 0
    while True:
        i = D.find(pat, start)
        if i < 0: break
        out.append(i); start = i + 1
    return out

def skip_len_prefix(pos):
    n = 0; shift = 0; p = pos
    while True:
        if p >= len(D): return None
        c = D[p]; p += 1
        n |= (c & 0x7F) << shift
        if not (c & 0x80): break
        shift += 7
        if shift > 14: return None
    return p + n

def parse(pos, want_spell):
    p = skip_len_prefix(pos)
    if p is None or p + 27 > len(D): return None
    spell, basecost, levelcost, icon = D[p], D[p+1], D[p+2], D[p+3]
    l1, l2, l3 = D[p+4], D[p+5], D[p+6]
    if spell != want_spell: return None
    need = struct.unpack_from("<HHH", D, p+7)
    delaybase = struct.unpack_from("<I", D, p+13)[0]
    if icon > 129 or basecost > 200 or levelcost > 40: return None
    if not (1 <= l1 <= 150 and 1 <= l2 <= 150 and 1 <= l3 <= 150): return None
    if not (l1 <= l2 <= l3): return None
    if delaybase > 3_600_000: return None
    return dict(icon=icon, basecost=basecost, l1=l1, l2=l2, l3=l3, spell=spell)

print(f"{'技能名':<14} {'Spell枚举':<20} {'DB.Icon':>7} {'Envir.Icon':>10} {'官网id':>6} {'DB对错':>8}")
bad, ok, nodb, nooff = [], 0, [], []
for name, (sp, eicon) in sorted(env.items(), key=lambda x: x[1][1]):
    want = spell_enum.get(sp)
    if want is None: continue
    recs = {}
    for o in find_occ(name):
        r = parse(o, want)
        if r: recs[r["icon"]] = r
    cid = cdn.get(name) or cdn.get(name.replace("秘籍","秘笈"))
    if not recs:
        nodb.append(name); continue
    for icon, r in sorted(recs.items()):
        if cid is None:
            nooff.append((name, icon)); continue
        good = (icon == cid - 1)
        mark = "✔" if good else f"✘应{cid-1}"
        envmark = "同Envir" if icon == eicon else f"Envir={eicon}"
        print(f"{name:<14} {sp:<20} {icon:>7} {envmark:>10} {cid:>6} {mark:>8}")
        (bad if not good else list).append((name, icon, cid, eicon)) if not good else None
        if good: ok += 1
print(f"\nDB与官网一致: {ok} 条   不一致: {len(bad)} 条   官网无对照: {len(nooff)} 条   DB中未找到: {len(nodb)} 条")
print("\n【不一致明细】")
for name, icon, cid, eicon in bad:
    print(f"  {name:<14} DB.Icon={icon:<4} 官网id={cid}(帧{(cid-1)*2}) Envir.Icon={eicon}")
print("\n【DB中未找到的技能】")
print("  " + ", ".join(nodb))
