import re, os, json
BS = chr(92)
SRC = r"D:\mir2-20241027"
# 1) Spell 枚举名->值
src = open(os.path.join(SRC,"Shared","Enums.cs"), encoding="utf-8-sig", errors="ignore").read()
m = re.search(r'public enum Spell : byte\s*\{(.*?)\n\s*\}', src, re.S)
spell = {}
for name, val in re.findall(r'(\w+)\s*=\s*(\d+)\s*,', m.group(1)):
    spell[name] = int(val)
print("Spell 枚举条目:", len(spell), " 例:", {k:spell[k] for k in list(spell)[:4]})

# 2) 抓 PlayerObject.cs 里 case Spell.X 到下一个 case 之间的 PlaySound
pat = re.compile(r'case\s+Spell\.(\w+)\s*:')
call = re.compile(r'PlaySound\(\s*20000\s*\+\s*(\d+|\w+)\s*\*\s*10\s*(?:\+\s*(\d+))?')
used = {}
for fn in ["Client/MirObjects/PlayerObject.cs","Client/MirObjects/MapObject.cs","Client/MirObjects/Effect.cs","Client/MirObjects/MonsterObject.cs","Client/MirObjects/SpellObject.cs"]:
    p = os.path.join(SRC, fn)
    if not os.path.exists(p): continue
    txt = open(p, encoding="utf-8-sig", errors="ignore").read()
    lines = txt.splitlines()
    cur = None
    for i,l in enumerate(lines,1):
        mm = pat.search(l)
        if mm: cur = mm.group(1); continue
        cc = call.search(l)
        if cc:
            a, b = cc.group(1), cc.group(2)
            if cur and (a == "ushort" or a==cur or a in spell or a.isdigit()):
                if a == "ushort":
                    if cur in spell: sp = spell[cur]
                    else: continue
                elif a in spell: sp = spell[a]
                elif a.isdigit(): sp = int(a)
                else: continue
                v = int(b) if b else 0
                used.setdefault(sp, set()).add(v)
print()
print("=== 代码实际用到的 (技能 -> 变体) ===")
for sp in sorted(used):
    nm = [k for k,v in spell.items() if v==sp]
    print(f"  {sp:4d} {(nm[0] if nm else '?'):26s} variants={sorted(used[sp])}")
json.dump({str(k):sorted(v) for k,v in used.items()}, open("used_variants.json","w"), indent=1)
