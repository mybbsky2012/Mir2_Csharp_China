# -*- coding: utf-8 -*-
"""
从源码 + 数据库导出结果，生成「本服怪物 AI 效果清单」的中间数据。
输入：
  D:/mir2-20241027/Server/MirObjects/MonsterObject.cs   (GetMonster 工厂 switch)
  D:/mir2-20241027/Server/MirObjects/Monsters/*.cs      (252 个 AI 实现类)
  D:/mir2-20241027/Tools/AiProbe/monsters.csv           (1231 个怪)
  D:/mir2-20241027/Tools/AiProbe/ai_usage.csv           (AI 使用分布)
输出：
  D:/mir2-20241027/Tools/AiProbe/ai_report.json
"""
import os
import re
import csv
import json
from collections import defaultdict, OrderedDict

ROOT = r"D:\mir2-20241027"
MON_DIR = os.path.join(ROOT, "Server", "MirObjects", "Monsters")
MO_FILE = os.path.join(ROOT, "Server", "MirObjects", "MonsterObject.cs")
TOOLS = os.path.join(ROOT, "Tools", "AiProbe")

# ---------- 1. 解析 GetMonster switch ----------
src = open(MO_FILE, encoding="utf-8", errors="replace").read()
# 截取 GetMonster 方法体：从函数签名到其后第一个 default: 所在行
m = re.search(r"public static MonsterObject GetMonster\(MonsterInfo info\)(.*?)\n\s*default:\s*\n\s*return new MonsterObject\(info\);\s*\n\s*\}\s*\n\s*\}", src, re.S)
body = m.group(1) if m else src
body = re.sub(r"//.*", "", body)  # 去掉行注释，避免注释里的 case 被误读
ai2class = OrderedDict()
pending = []          # 连续 case 标签共享一个 return（case 160: case 161: return Deer）
for line in body.splitlines():
    cm = re.match(r"\s*case\s+(\d+)\s*:", line)
    if cm:
        pending.append(int(cm.group(1)))
        continue
    rm = re.match(r"\s*return new (\w+)\(info\);", line)
    if rm and pending:
        cls = rm.group(1)
        for a in pending:
            ai2class[a] = cls
        pending = []
ai2class.setdefault(0, "MonsterObject")   # default 分支

print("工厂 switch 解析到 AI 数:", len(ai2class))
multi = defaultdict(list)
for ai, c in ai2class.items():
    multi[c].append(ai)
print("工厂涉及的实现类:", len(multi))

# ---------- 2. 读 ai_usage.csv / monsters.csv ----------
ai_use = {}
with open(os.path.join(TOOLS, "ai_usage.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        ai_use[int(row["AI"])] = {"class": row["实现类"], "count": int(row["怪物数"]), "sample": row["示例怪物(最多8)"]}

mons = []
with open(os.path.join(TOOLS, "monsters.csv"), encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        mons.append(row)

case_ais = sorted(ai2class.keys())   # 工厂里真正写了 case 的编号（含 0=default 语义上的 MonsterObject）
# 数据库在用但工厂没有专案 case 的编号 → 走 default: new MonsterObject(info)
for a in list(ai_use.keys()):
    ai2class.setdefault(a, "MonsterObject")

# 重建 multi（上面可能补了 default 映射）
multi = defaultdict(list)
for ai, c in ai2class.items():
    multi[c].append(ai)

# 每个实现类实际服务的怪物（去重 GameName）
cls_mons = defaultdict(list)
for r in mons:
    cls = ai_use.get(int(r["AI"]), {}).get("class", "")
    if not cls:
        cls = ai2class.get(int(r["AI"]), "MonsterObject")
    cls_mons[cls].append(r)

# ---------- 3. 分析每个实现类 ----------
METHOD_CAT = [
    ("ProcessAI",      "AI主循环",   "完全接管 AI 流程"),
    ("ProcessTarget",  "AI主循环",   "接管目标选择/攻击决策"),
    ("ProcessSearch",  "AI主循环",   "接管索敌"),
    ("ProcessRoam",    "AI主循环",   "接管游荡/巡逻"),
    ("ProcessStacking","AI主循环",   "接管堆叠分离"),
    ("Attack",         "攻击",       "重写普通攻击"),
    ("GetAttackPower", "攻击",       "重写伤害计算"),
    ("RangedAttack",   "攻击",       "远程攻击"),
    ("CompleteMagic",  "技能",       "施放技能"),
    ("CastMagic",      "技能",       "施放技能"),
    ("SummonSlave",    "召唤",       "召唤随从"),
    ("SpawnSlave",     "召唤",       "召唤随从"),
    ("Die",            "死亡",       "重写死亡处理"),
    ("Drop",           "死亡",       "重写掉落"),
    ("Spawned",        "出生",       "出生钩子"),
    ("Revive",         "复活",       "重写复活"),
    ("Struck",         "受击",       "重写受击反应"),
    ("RefreshBase",    "属性",       "重写基础属性"),
    ("RefreshBuffs",   "属性",       "重写 BUFF 刷新"),
    ("GetInfo",        "外观",       "重写下发数据(外观/变身)"),
    ("FindTarget",     "索敌",       "重写索敌"),
    ("IsAttackTarget", "索敌",       "重写可攻击判定"),
    ("CanMove",        "移动",       "重写能否移动"),
    ("CanAttack",      "移动",       "重写能否攻击"),
    ("CanRegen",       "移动",       "重写回血条件"),
    ("MoveTo",         "移动",       "重写寻路"),
]

FEATURES = [
    (r"SpawnSlave\s*\(|SummonSlave\s*\(|SlaveList\s*\.\s*Add|MobileList\s*\.\s*Add", "召唤随从"),
    (r"MonsterObject\s*\.\s*GetMonster|GetMonster\s*\(", "召唤随从"),
    (r"\bTeleport\s*\(", "瞬移"),
    (r"DieNextTurn\s*=\s*true|Bomb\s*\(|Explode|SelfDestruct", "自爆/引爆"),
    (r"ObjectRangeAttack", "远程攻击"),
    (r"ProjectileAttack\s*\(", "远程弹道"),
    (r"CompleteMagic|CastMagic|SpellObject|ObjectMagic", "施法/技能"),
    (r"AddBuff\s*\(\s*BuffType\.", "给目标上 BUFF"),
    (r"PoisonTarget\s*\(|CurrentPoison\s*\.\s*Add", "施毒"),
    (r"LineAttack|StraightAttack", "直线/穿透攻击"),
    (r"HealthPercent\s*[<>=]", "血量分段行为"),
    (r"Immortal|Invincible", "无敌/免伤"),
    (r"Shield", "护盾"),
    (r"Pull|Drag|Knock", "拉拽/击退"),
    (r"Freeze|Paralysis|Stun|Frozen|Dazed", "冰冻/麻痹/眩晕"),
    (r"Invisible|Hiding|Stealth", "隐身"),
    (r"ChangeHP\s*\(|Heal", "治疗/回复"),
    (r"Route\.(Add|Clear|RemoveAt)", "固定巡逻路线"),
    (r"BlockWalkThrough", "挡路(城墙/城门)"),
    (r"HenshinMode|ObjectShow|ObjectTransform", "变身/换形态"),
    (r"AutoRev|Revive\s*\(", "自动复活"),
    (r"CannotMove|MoveTo\s*\(.*0\)", "定身/不移动"),
]

def analyze_class(cls):
    path = os.path.join(MON_DIR, cls + ".cs")
    if not os.path.isfile(path):
        # 少数类可能不在此目录（如 Guard/Trainer 在别处）
        for root, _, files in os.walk(os.path.join(ROOT, "Server")):
            if cls + ".cs" in files:
                path = os.path.join(root, cls + ".cs")
                break
    if not os.path.isfile(path):
        return {"file": None, "overrides": [], "features": [], "lines": 0}

    text = open(path, encoding="utf-8", errors="replace").read()
    lines = text.count("\n") + 1

    overrides = []
    for name, cat, desc in METHOD_CAT:
        pat = re.compile(
            r"(public|protected|private|internal)\s+(override|virtual|new)\s+"
            r"(?:sealed\s+)?(?:async\s+)?(?:[\w<>\[\],\.\?]+\s+)?"
            + re.escape(name) + r"\s*[(<]", re.M)
        if pat.search(text):
            api = pat.search(text).group(2)
            overrides.append({"name": name, "cat": cat, "desc": desc, "kind": api})

    # 去重（同名可能多个重载）
    seen = set()
    uniq = []
    for o in overrides:
        if o["name"] in seen: continue
        seen.add(o["name"]); uniq.append(o)

    feats = []
    for pat, label in FEATURES:
        if re.search(pat, text):
            feats.append(label)

    # 该文件里出现的技能/BUFF 枚举名，作为线索
    magic = sorted(set(re.findall(r"Spell\.(\w+)", text)))
    buffs = sorted(set(re.findall(r"BuffType\.(\w+)", text)))
    poisons = sorted(set(re.findall(r"PoisonType\.(\w+)", text)))
    images = sorted(set(re.findall(r"Monster\.(\w+)", text)))
    # 攻击段数：ObjectAttack / ObjectRangeAttack 广播次数
    atk_segments = len(re.findall(r"ObjectAttack\s*\{", text)) + len(re.findall(r"ObjectRangeAttack\s*\{", text))
    # 攻击类型编号（服务端 ObjectAttack.Type）
    atk_types = sorted(set(re.findall(r"ObjectRange?Attack\s*\{[^}]*?Type\s*=\s*(\d+)", text, re.S)))

    return {
        "file": os.path.relpath(path, ROOT).replace("\\", "/"),
        "lines": lines,
        "overrides": uniq,
        "features": feats,
        "magics": magic[:16],
        "buffs": buffs[:16],
        "poisons": poisons[:10],
        "images": images[:10],
        "atk_segments": atk_segments,
        "atk_types": atk_types,
    }

report = {"classes": {}, "ai2class": {str(k): v for k, v in ai2class.items()}, "case_ais": case_ais}
for cls in sorted(multi.keys()):
    ai_list = sorted(multi[cls])
    used = [a for a in ai_list if a in ai_use]
    info = analyze_class(cls)
    info["ais"] = ai_list
    info["used_ais"] = used
    info["monster_count"] = sum(ai_use.get(a, {}).get("count", 0) for a in ai_list)
    info["game_names"] = sorted(set(r["GameName"] for r in cls_mons.get(cls, [])))
    report["classes"][cls] = info

json.dump(report, open(os.path.join(TOOLS, "ai_report.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("写出 ai_report.json")
print("实现类总数:", len(report["classes"]))
# 概览：只有 GetInfo / 无重写的极简类
simple = [c for c, v in report["classes"].items() if not v["overrides"]]
print("无任何行为重写的类:", len(simple))
