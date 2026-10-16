# -*- coding: utf-8 -*-
"""精简版：只输出 AI 家族清单（无立绘/无交互页）"""
import os, json, csv
from collections import defaultdict
from desc_map import DESC

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server"

report = json.load(open(os.path.join(HERE, "ai_report.json"), encoding="utf-8"))
classes = report["classes"]
mons = list(csv.DictReader(open(os.path.join(HERE, "monsters.csv"), encoding="utf-8-sig")))

members = defaultdict(list)
for r in mons:
    members[r["实现类"]].append(r)

def build_desc(cls, v):
    d = DESC.get(cls, "").strip()
    if not d:
        parts = []
        if any(o["name"] == "ProcessAI" for o in v["overrides"]):
            parts.append("完全自定义 AI 主循环")
        if v["features"]:
            parts.append("、".join(v["features"]))
        if not parts:
            parts.append("标准近战 AI（索敌→追击→普通攻击）")
        d = "；".join(parts)
        ex = []
        if v["magics"]:
            ex.append("技能: " + "、".join(v["magics"]))
        if v["buffs"]:
            ex.append("BUFF: " + "、".join(v["buffs"]))
        if v["poisons"]:
            ex.append("毒: " + "、".join(v["poisons"]))
        if ex:
            d += "｜" + "，".join(ex)
    return d

rows = []
for cls, v in classes.items():
    if v["monster_count"] <= 0:
        continue
    ms = members.get(cls, [])
    seen, names = set(), []
    for m in ms:
        if m["GameName"] in seen:
            continue
        seen.add(m["GameName"])
        names.append(m["GameName"])
    rows.append({
        "ais": "/".join(str(a) for a in sorted(set(int(a) for a in v["ais"]))),
        "cls": cls,
        "count": v["monster_count"],
        "desc": build_desc(cls, v).replace("|", "\\|"),
        "names": names[:6],
        "n_all": len(names),
    })
rows.sort(key=lambda x: (-x["count"], x["cls"]))

md = []
md.append("# 本服 AI 家族清单\n")
md.append("> 1231 个怪共用到 **253 个 AI 编号 / 245 个 AI 家族**。改 AI：GM 工具「数据库→怪物信息」→「特殊AI」框填编号。\n")
md.append("| AI | 家族（实现类） | 怪物数 | 效果 | 代表怪 |")
md.append("|---:|---|---:|---|---|")
for r in rows:
    names = "、".join(r["names"]) + (f" 等{r['n_all']}种" if r["n_all"] > len(r["names"]) else "")
    md.append(f"| {r['ais']} | {r['cls']} | {r['count']} | {r['desc']} | {names} |")
md.append("")
md.append("**没被本服使用的编号（可直接拿来做新怪）**：471、991、994（不足的在工厂走默认通用近战 AI）")

out = os.path.join(OUT_DIR, "AI家族清单.md")
with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(md))
print("写出", out, "%.1f KB / %d 个家族" % (os.path.getsize(out) / 1024, len(rows)))

# 控制台打印怪数 >= 5 的（回复用）
print()
print("| AI | 家族（实现类） | 怪物数 | 效果 | 代表怪 |")
print("|---:|---|---:|---|---|")
for r in rows:
    if r["count"] < 5:
        continue
    names = "、".join(r["names"][:4]) + (f" 等{r['n_all']}种" if r["n_all"] > 4 else "")
    print(f"| {r['ais']} | {r['cls']} | {r['count']} | {r['desc'][:70]} | {names} |")
