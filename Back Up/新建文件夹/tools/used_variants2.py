import re, os, json
SRC = r"D:\mir2-20241027"
src = open(os.path.join(SRC,"Shared","Enums.cs"), encoding="utf-8-sig", errors="ignore").read()
m = re.search(r'public enum Spell : byte\s*\{(.*?)\n\s*\}', src, re.S)
spell = {}
for name, val in re.findall(r'(\w+)\s*=\s*(\d+)\s*,', m.group(1)):
    spell[name] = int(val)
rev = {v:k for k,v in spell.items()}

CASE = re.compile(r'case\s+Spell\.(\w+)\s*:')
CALL = re.compile(r'PlaySound\(\s*20000\s*\+\s*(?:\(ushort\))?\s*(\w+)\s*\*\s*10\s*(?:\+\s*(\d+))?')
used = {}
detail = {}
for fn in ["Client/MirObjects/PlayerObject.cs","Client/MirObjects/MapObject.cs","Client/MirObjects/Effect.cs",
           "Client/MirObjects/MonsterObject.cs","Client/MirObjects/SpellObject.cs","Client/MirObjects/ItemObject.cs",
           "Client/MirObjects/NPCObject.cs","Client/MirObjects/UserObject.cs"]:
    p = os.path.join(SRC, fn)
    if not os.path.exists(p): continue
    cur = None
    for i,l in enumerate(open(p, encoding="utf-8-sig", errors="ignore").read().splitlines(),1):
        mm = CASE.search(l)
        if mm: cur = mm.group(1); continue
        cc = CALL.search(l)
        if not cc: continue
        a, b = cc.group(1), cc.group(2)
        if a == "Spell":
            if cur is None or cur not in spell: continue
            sp = spell[cur]
        elif a in spell: sp = spell[a]
        elif a.isdigit(): sp = int(a)
        else: continue
        v = int(b) if b else 0
        used.setdefault(sp, set()).add(v)
        detail.setdefault(sp, []).append(f"{os.path.basename(fn)}:{i}")
print("=== 代码实际用到的 (技能 -> 变体) ===")
for sp in sorted(used):
    print(f"  {sp:4d} {rev.get(sp,'(无名字)'):26s} variants={sorted(used[sp])}  @{detail[sp][:4]}")
json.dump({str(k):sorted(v) for k,v in used.items()}, open("used_variants.json","w"), indent=1)
print()
print("道士段(61-88):")
for sp in range(61,89):
    print(f"  {sp:4d} {rev.get(sp,'(空号)'):26s} variants={sorted(used.get(sp,[])) if sp in used else '—'}")
