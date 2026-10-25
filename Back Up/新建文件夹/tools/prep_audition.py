import os, json, shutil, re
BS = chr(92)
CLI = r"E:\micromir2\Sound"
OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\_work\audio_audition"
cat = json.load(open(r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\sound_catalog.json", encoding="utf-8"))

# 道士技能（Crystal Spell 编号 -> 中文名），来自 技能数据.csv
TAO = [("治愈术",61),("精神力战法",62),("施毒术",63),("灵魂火符",64),("召唤骷髅",65),
       ("隐身术",67),("集体隐身术",68),("幽灵盾",69),("心灵启示",70),("神圣战甲术",71),
       ("气功波",72),("困魔咒",73),("净化术",74),("群体治疗术",75),("迷魂术",76),
       ("无极真气",77),("召唤神兽",78),("苏生术",79),("精魂召唤术",80),("诅咒术",81),
       ("烦脑(瘟疫)",82),("毒雾",83),("先天气功",83+1),("血龙水(宠物强化)",85),
       ("阴阳五行阵",86),("治愈术-秘籍",87),("阴阳五行阵-秘籍",88)]
TAO = [(n,s) for n,s in TAO]

# 代码实际用到的变体（来自 PlayerObject.cs 核对）
USED = {61:[0,1],62:[0],63:[0,1],64:[0,1,2],65:[0],67:[0],68:[0,1],69:[0,1],70:[0,1],71:[0,1],
        72:[0],73:[0,1],74:[0],75:[0,1],76:[0],77:[0],78:[0],79:[0,1],80:[0],81:[0],82:[3],
        83:[0],84:[0,1],85:[0],86:[0,1],87:[1],88:[0]}

# 候选池（全部在客户端本地存在，改完立即生效、无需下载）
POOL_SPECIAL = [
 ("400-1.wav",  "骷髅 · 出现/站立"), ("400-2.wav","骷髅 · 攻击"), ("400-4.wav","骷髅 · 受击"),
 ("1900-1.wav", "神兽 · 出现/站立"), ("1900-5.wav","神兽 · 死亡"),
 ("1910-0.wav", "神兽1 · 出现"), ("1910-2.wav","神兽1 · 攻击"),
 ("1920-0.wav", "月灵(圣兽) · 出现"), ("1920-1.wav","月灵 · 站立"), ("1920-2.wav","月灵 · 攻击"),
]
POOL_SKILL = [
 ("M3-0.wav","独立技能音"),("M4-0.wav","独立技能音"),("M6-0.wav","独立技能音"),
 ("M8-1.wav","独立技能音"),("M9-0.wav","独立技能音"),("M9-1.wav","独立技能音"),
 ("M10-0.wav","独立技能音"),("M11-0.wav","独立技能音"),("M12-0.wav","独立技能音"),
 ("M13-0.wav","独立技能音"),("M15-0.wav","独立技能音"),("M20-0.wav","独立技能音"),
 ("M72-0.wav","道士·气功波 原音"),("M73-0.wav","道士·困魔咒 原音"),
 ("M79-0.wav","道士·苏生术 原音"),("M80-0.wav","道士·精魂召唤 原音"),
 ("M81-0.wav","道士·诅咒术 原音"),("M81-1.wav","道士·诅咒术 原音2"),
]

def peers_label(f):
    d = cat["skill"].get(f) or cat["monster"].get(f)
    if not d: return ""
    ps = [p for p in d.get("peers", []) if not re.match(r'^M(6\d|7\d|8\d)-', p, re.I)]
    ps = [p for p in ps if p not in ("M61-1.wav","M101-0.wav")]
    return ("＝"+ "、".join(ps[:4])) if ps else ""

copied = set()
def ensure(f):
    src = os.path.join(CLI, f)
    if os.path.exists(src):
        if f not in copied:
            shutil.copy2(src, os.path.join(OUT, f)); copied.add(f)
        return True
    return False

rows = []
for name, sp in TAO:
    cur = []
    for v in USED.get(sp, [0]):
        f = f"M{sp}-{v}.wav"
        if ensure(f): cur.append({"file": f, "label": f"现用 {f}" + (" " + peers_label(f) if v == 0 else "")})
        else: cur.append({"file": None, "label": f"M{sp}-{v}.wav（两端都缺，当前静音）"})
    rows.append({"name": name, "sp": sp, "cur": cur})
pool = []
for f, lab in POOL_SPECIAL + POOL_SKILL:
    if ensure(f): pool.append({"file": f, "label": lab})
for r in rows:
    for c in r["cur"]:
        if c["file"] and c["file"] not in [p["file"] for p in pool]:
            pool.append({"file": c["file"], "label": "现用音 " + c["file"]})
json.dump({"rows": rows, "pool": pool}, open("audition.json", "w", encoding="utf-8"), ensure_ascii=False)
print("技能行:", len(rows), " 候选池:", len(pool), " 已复制文件:", len(copied))
print("缺文件的技能:", [ (r["name"], c["label"]) for r in rows for c in r["cur"] if not c["file"] ])
