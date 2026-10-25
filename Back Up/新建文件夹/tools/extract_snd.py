# -*- coding: utf-8 -*-
"""抽取 Spell / Monster 枚举 与 客户端实际 PlaySound 调用"""
import re, json, os, collections

SRC = r"D:\mir2-20241027"
CLIENT = os.path.join(SRC, "Client")
SHARED = os.path.join(SRC, "Shared")

def read(p):
    return open(p, 'rb').read().decode('utf-8', errors='replace')

enums = read(os.path.join(SHARED, "Enums.cs"))

def parse_enum(name):
    m = re.search(r'public enum %s\s*:\s*\w+\s*\{(.*?)\n\}' % name, enums, re.S)
    if not m:
        return []
    body = m.group(1)
    out = []
    val = -1
    for ln in body.splitlines():
        ln = ln.split('//')[0].strip().rstrip(',')
        if not ln:
            continue
        mm = re.match(r'^(\w+)\s*(?:=\s*(\d+))?$', ln)
        if not mm:
            continue
        n = mm.group(1)
        if mm.group(2) is not None:
            val = int(mm.group(2))
        else:
            val += 1
        out.append((n, val))
    return out

spells = parse_enum("Spell")
monsters = parse_enum("Monster")
print("Spell 枚举:", len(spells))
print("Monster 枚举:", len(monsters))

# 抽取客户端所有 PlaySound 调用及其表达式
calls = []   # (file, line, expr)
pat = re.compile(r'SoundManager\.PlaySound\(([^;]*?)\);')
for root, _d, files in os.walk(CLIENT):
    for fn in files:
        if not fn.endswith('.cs'):
            continue
        p = os.path.join(root, fn)
        for i, ln in enumerate(read(p).splitlines(), 1):
            for m in pat.finditer(ln):
                calls.append((os.path.relpath(p, SRC), i, m.group(1).strip()))
print("PlaySound 调用点:", len(calls))

EXPR_STATS = collections.Counter()
for f, i, e in calls:
    EXPR_STATS[e[:70]] += 1

data = {
    "spells": spells,
    "monsters": monsters,
    "calls": calls,
}
outp = os.path.join(os.path.dirname(os.path.abspath(__file__)), "snd_data.json")
json.dump(data, open(outp, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print("已写出:", outp)

# 统计：涉及 20000 段 / 怪物段的调用
n20000 = [c for c in calls if '20000' in c[2]]
nmon = [c for c in calls if 'Monster.' in c[2]]
print("含 20000 的调用:", len(n20000), " 含 Monster. 的调用:", len(nmon))

print("\n=== 出现最多的表达式 Top25 ===")
for e, c in EXPR_STATS.most_common(25):
    print("%4d  %s" % (c, e))

print("\n=== 前 40 个 Spell 枚举 ===")
for n, v in spells[:40]:
    print("  %-28s = %d   -> M%d-0.wav" % (n, v, v))
