# -*- coding: utf-8 -*-
"""从 Server.MirDB 里按技能名解析 MagicInfo 的伤害相关字段（Power/MPower/Range/Multiplier）"""
import sys, struct, json
sys.stdout.reconfigure(encoding='utf-8')

DB = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Server.MirDB"
D = open(DB, "rb").read()

spell_enum = json.load(open(r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\mir2db\spell_enum.json", encoding="utf-8"))
rev = {}
for k, v in spell_enum.items():
    rev.setdefault(v, k)

TARGETS = [
    "雷电术", "强击-秘籍", "吸魔炎风", "吸魔炎风-秘籍",
    "影华连击", "影华连击-秘笈", "拔刀术", "拔刀术-秘籍",
    "月影雾", "月影雾-秘籍", "毒雾", "龙血兽", "龙血兽-秘籍",
    "落雷杀", "落雷杀-秘笈", "月华乱舞", "月华乱舞-秘籍",
    "灵魂火符", "灵魂火符(万效)", "灵魂火符(万效-秘)",
    "万效符", "万效符-秘籍", "灭杀", "爆闪-秘籍", "气流术-秘籍",
    "攻杀剑术", "刺杀剑术", "半月弯刀", "烈火剑法",
]


def find_occ(name):
    b = name.encode("utf-8"); n = len(b)
    if n < 128:
        pat = bytes([n]) + b
    else:
        pat = bytes([(n & 0x7F) | 0x80, (n >> 7) & 0x7F]) + b
    out, start = [], 0
    while True:
        i = D.find(pat, start)
        if i < 0:
            break
        out.append(i); start = i + 1
    return out


def skip_len_prefix(pos):
    n = 0; shift = 0; p = pos
    while True:
        if p >= len(D):
            return None
        c = D[p]; p += 1
        n |= (c & 0x7F) << shift
        if not (c & 0x80):
            break
        shift += 7
        if shift > 14:
            return None
    return p + n


def parse(pos):
    p = skip_len_prefix(pos)
    if p is None or p + 27 + 1 + 8 > len(D):
        return None
    spell = D[p]
    basecost, levelcost, icon = D[p + 1], D[p + 2], D[p + 3]
    l1, l2, l3 = D[p + 4], D[p + 5], D[p + 6]
    need1, need2, need3 = struct.unpack_from("<HHH", D, p + 7)
    delaybase, delayred = struct.unpack_from("<II", D, p + 13)
    pbase, pbonus, mpbase, mbonus = struct.unpack_from("<HHHH", D, p + 21)
    rng = D[p + 29]
    multbase, multbonus = struct.unpack_from("<ff", D, p + 30)
    return dict(spell=spell, basecost=basecost, levelcost=levelcost, icon=icon,
                l1=l1, l2=l2, l3=l3, need=(need1, need2, need3),
                delaybase=delaybase, delayred=delayred,
                pbase=pbase, pbonus=pbonus, mpbase=mpbase, mbonus=mbonus,
                rng=rng, multbase=round(multbase, 3), multbonus=round(multbonus, 3))


print(f"{'技能名':<16}{'Spell':>5} {'枚举名':<22}{'Icn':>4}{'Lv':>12}{'PB+':>10}{'MPB+':>10}{'Rng':>4}{'Mult':>12}")
print("-" * 108)
for name in TARGETS:
    recs = []
    for o in find_occ(name):
        r = parse(o)
        if r:
            recs.append(r)
    if not recs:
        print(f"{name:<16}{'--':>5} 【DB 中未找到】")
        continue
    seen = set()
    for r in recs:
        key = (r["spell"], r["icon"])
        if key in seen:
            continue
        seen.add(key)
        sp = rev.get(r["spell"], f"?{r['spell']}")
        lv = f"{r['l1']}/{r['l2']}/{r['l3']}"
        pb = f"{r['pbase']}+{r['pbonus']}"
        mpb = f"{r['mpbase']}+{r['mbonus']}"
        mult = f"{r['multbase']}+{r['multbonus']}"
        print(f"{name:<16}{r['spell']:>5} {sp:<22}{r['icon']:>4}{lv:>12}{pb:>10}{mpb:>10}{r['rng']:>4}{mult:>12}")
