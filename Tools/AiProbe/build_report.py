# -*- coding: utf-8 -*-
"""
生成《本服怪物 AI 效果清单》：
  1) Mir2Server\\本服怪物AI效果清单.html   —— 自包含（含怪物立绘）
  2) Mir2Server\\本服怪物AI效果清单.md      —— 纯文本版
"""
import os
import io
import csv
import json
import html
from collections import defaultdict, Counter
from desc_map import DESC

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT_SRC = r"D:\mir2-20241027"
OUT_DIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server"

report = json.load(open(os.path.join(HERE, "ai_report.json"), encoding="utf-8"))
classes = report["classes"]
ai2class = report["ai2class"]
sprites = json.load(open(os.path.join(HERE, "sprites.json"), encoding="utf-8"))
mons = list(csv.DictReader(open(os.path.join(HERE, "monsters.csv"), encoding="utf-8-sig")))

# ---------- 每个实现类：成员怪（去重）、代表图 ----------
cls_members = defaultdict(list)   # class -> [(GameName, Level, HP, AI, Image, Index)]
cls_img = {}
for r in mons:
    c = r["实现类"]
    cls_members[c].append(r)
    img = int(r["Image"])
    if c not in cls_img and str(img) in sprites:
        cls_img[c] = img

# ---------- 分类 ----------
def category(cls, v):
    d = DESC.get(cls, "")
    feats = v["features"]
    if cls in ("Gate", "Wall", "CityGate", "Guard", "ArcherGuard", "TownArcher", "ConquestArcher",
               "Siege", "Trainer", "HumanWizard", "HumanAssassin", "IntelligentCreatureObject"):
        return "城防/功能"
    if cls in ("Tree", "TrapRock", "GuardianRock", "ArcaneTotem", "WoodBox", "Jar1", "Jar2", "RestlessJar",
               "CargoBox", "IcePillar", "CaveStatue", "TucsonEgg", "RootSpider", "DragonStatue", "CannibalPlant",
               "CreeperPlant", "SandWorm", "DigOutZombie", "EvilCentipede", "HarvestMonster", "Deer", "Football",
               "EvilMirBody", "CannibalTentacles", "TucsonEgg"):
        return "伪装/采集/定身"
    if any(k in d for k in ("召唤", "画轴", "孵化", "蛇王")) or "召唤随从" in feats:
        return "召唤系"
    if "自爆/引爆" in feats:
        return "自爆系"
    if "远程攻击" in feats or "远程弹道" in feats or "施法/技能" in feats:
        return "远程/施法系"
    if "施毒" in feats or "冰冻/麻痹/眩晕" in feats:
        return "毒/控制系"
    if "挡路(城墙/城门)" in feats or "自动复活" in feats:
        return "城防/功能"
    return "标准近战"

# ---------- 描述 ----------
def build_desc(cls, v):
    d = DESC.get(cls, "").strip()
    feats = [f for f in v["features"]]
    extras = []
    if v["magics"]:
        extras.append("技能: " + "、".join(v["magics"]))
    if v["buffs"]:
        extras.append("BUFF: " + "、".join(v["buffs"]))
    if v["poisons"]:
        extras.append("毒: " + "、".join(v["poisons"]))
    if not d:
        parts = []
        if any(o["name"] == "ProcessAI" for o in v["overrides"]):
            parts.append("完全自定义 AI 主循环")
        if feats:
            parts.append("、".join(feats))
        if any(o["name"] == "ProcessTarget" for o in v["overrides"]) and not feats:
            parts.append("自定义目标选择")
        if not parts:
            parts.append("标准近战 AI（索敌→追击→普通攻击）")
        d = "；".join(parts)
    if extras:
        d += "｜" + "，".join(extras)
    return d

rows = []
for cls, v in sorted(classes.items(), key=lambda kv: (-kv[1]["monster_count"], kv[0])):
    if v["monster_count"] <= 0:
        continue
    members = cls_members.get(cls, [])
    names = []
    seen = set()
    for m in members:
        n = m["GameName"]
        if n in seen:
            continue
        seen.add(n)
        names.append(n)
    ais = sorted(set(int(a) for a in v["ais"]))
    rows.append({
        "cls": cls,
        "ais": ais,
        "count": v["monster_count"],
        "names": names[:14] + (["…"] if len(names) > 14 else []),
        "all_names": len(names),
        "img": cls_img.get(cls),
        "imgName": next((m["ImageName"] for m in members if m["ImageName"]), ""),
        "cat": category(cls, v),
        "desc": build_desc(cls, v),
        "lines": v["lines"],
        "file": v["file"] or "",
        "levels": sorted(set(int(m["Level"]) for m in members)),
    })

print("家族数(有怪的):", len(rows), " 总怪数:", sum(r["count"] for r in rows))

# ================= HTML =================
def esc(s):
    return html.escape(str(s), quote=True)

CAT_COLOR = {
    "城防/功能": "#8b6f47",
    "伪装/采集/定身": "#7a8b5c",
    "召唤系": "#9b59b6",
    "自爆系": "#e07b39",
    "远程/施法系": "#3d7ea6",
    "毒/控制系": "#2e8b57",
    "标准近战": "#8a8a8a",
}

sprite_js = json.dumps(sprites, ensure_ascii=False)
data_js = json.dumps(rows, ensure_ascii=False)

cat_counter = Counter(r["cat"] for r in rows)
total_mon = sum(r["count"] for r in rows)
used_ais = sorted(set(a for r in rows for a in r["ais"]))
all_factory_ais = sorted(set(report.get("case_ais", [int(k) for k in ai2class])) | {0})
unused = [a for a in all_factory_ais if a not in set(used_ais)]

cat_bar = "".join(
    f'<div class="cat-chip" style="--c:{CAT_COLOR[k]}"><b>{k}</b><span>{v} 个家族</span></div>'
    for k, v in cat_counter.most_common()
)

html_doc = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>本服怪物 AI 效果清单</title>
<style>
  :root { --bg:#f5f2ec; --card:#ffffff; --ink:#2b2620; --line:#e3ddd0; --accent:#a8442a; }
  * { box-sizing:border-box; margin:0; padding:0; }
  body { background:var(--bg); color:var(--ink);
         font-family:"Microsoft YaHei","PingFang SC",sans-serif; padding:28px 18px 60px; }
  .wrap { max-width:1180px; margin:0 auto; }
  h1 { font-size:26px; letter-spacing:1px; }
  h1 small { font-size:13px; color:#8b8378; font-weight:normal; margin-left:10px; }
  .sub { color:#8b8378; font-size:13px; margin:6px 0 20px; }
  .stats { display:flex; gap:12px; flex-wrap:wrap; margin-bottom:22px; }
  .stat { background:var(--card); border:1px solid var(--line); border-radius:10px;
          padding:12px 18px; min-width:150px; }
  .stat b { font-size:24px; color:var(--accent); display:block; }
  .stat span { font-size:12px; color:#8b8378; }
  .cat-bar { display:flex; gap:10px; flex-wrap:wrap; margin-bottom:24px; }
  .cat-chip { background:var(--card); border:1px solid var(--line); border-left:4px solid var(--c);
              border-radius:8px; padding:8px 14px; font-size:13px; }
  .cat-chip b { display:block; font-size:14px; }
  .cat-chip span { color:#8b8378; font-size:12px; }
  h2 { font-size:18px; margin:34px 0 12px; border-left:4px solid var(--accent); padding-left:10px; }
  .flow { background:var(--card); border:1px solid var(--line); border-radius:12px; padding:18px; }
  .toolbar { display:flex; gap:10px; flex-wrap:wrap; align-items:center; margin:14px 0;
             position:sticky; top:0; background:var(--bg); padding:10px 0; z-index:5; }
  .toolbar input { padding:8px 12px; border:1px solid var(--line); border-radius:8px;
                   font-size:14px; width:240px; background:#fff; }
  .toolbar select { padding:8px 10px; border:1px solid var(--line); border-radius:8px; background:#fff; font-size:13px; }
  .toolbar .cnt { font-size:13px; color:#8b8378; }
  table { width:100%; border-collapse:collapse; background:var(--card);
          border:1px solid var(--line); border-radius:10px; overflow:hidden; font-size:13px; }
  th { background:#efe9dd; text-align:left; padding:9px 10px; font-size:12px; color:#6b6357;
       position:sticky; top:52px; z-index:4; }
  td { padding:8px 10px; border-top:1px solid var(--line); vertical-align:top; }
  tr:hover td { background:#faf7f0; }
  .spr { width:52px; height:52px; image-rendering:pixelated; display:block; margin:0 auto; }
  .mon { font-weight:600; }
  .names { color:#6b6357; font-size:12px; line-height:1.7; }
  .ai-tag { display:inline-block; background:#efe9dd; border-radius:5px; padding:1px 7px;
            margin:1px 2px 1px 0; font-size:12px; font-family:Consolas,monospace; }
  .badge { display:inline-block; border-radius:10px; padding:1px 9px; font-size:11px;
           color:#fff; margin:1px 3px 1px 0; }
  .desc { line-height:1.65; }
  .meta { color:#a09883; font-size:11px; }
  .note { background:#fdf6e3; border:1px solid #e8d9a0; border-radius:8px; padding:12px 16px;
          font-size:13px; line-height:1.8; margin:14px 0; }
  .unused { color:#a09883; font-size:12px; line-height:1.9; }
  footer { margin-top:40px; color:#a09883; font-size:12px; text-align:center; }
</style>
</head>
<body>
<div class="wrap">
  <h1>本服怪物 AI 效果清单<small>Crystal 源码自动分析 + 人工校准</small></h1>
  <div class="sub">数据来源：Server.MirDB（只读探针）＋ Server/MirObjects/Monsters 252 个实现类源码交叉分析 · __DATE__ 生成</div>

  <div class="stats">
    <div class="stat"><b>__TOTALMON__</b><span>怪物定义（MonsterInfo）</span></div>
    <div class="stat"><b>__USEDAI__</b><span>实际使用的 AI 编号</span></div>
    <div class="stat"><b>__FAM__</b><span>AI 实现家族（有怪在用）</span></div>
    <div class="stat"><b>__ALLAI__</b><span>工厂支持的全部 AI 编号</span></div>
    <div class="stat"><b>__SPRITES__</b><span>已解码的怪物立绘</span></div>
  </div>

  <div class="cat-bar">__CATBAR__</div>

  <h2>一、AI 是怎么跑起来的</h2>
  <div class="flow">
    <svg viewBox="0 0 1080 220" width="100%" height="180" xmlns="http://www.w3.org/2000/svg">
      <defs><marker id="ar" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
        <path d="M0,0 L8,4 L0,8 z" fill="#a8442a"/></marker></defs>
      <g font-size="13" font-family="Microsoft YaHei">
        <rect x="10" y="60" width="150" height="56" rx="9" fill="#efe9dd" stroke="#d8cfbd"/>
        <text x="85" y="84" text-anchor="middle" font-weight="bold">MonsterInfo.AI</text>
        <text x="85" y="103" text-anchor="middle" font-size="11" fill="#8b8378">GM 工具「特殊AI」框</text>
        <rect x="210" y="60" width="170" height="56" rx="9" fill="#a8442a" stroke="#8c3720"/>
        <text x="295" y="84" text-anchor="middle" fill="#fff" font-weight="bold">GetMonster() 工厂</text>
        <text x="295" y="103" text-anchor="middle" font-size="11" fill="#f3d9cf">switch(info.AI) → new 类</text>
        <rect x="430" y="60" width="180" height="56" rx="9" fill="#efe9dd" stroke="#d8cfbd"/>
        <text x="520" y="84" text-anchor="middle" font-weight="bold">实现类</text>
        <text x="520" y="103" text-anchor="middle" font-size="11" fill="#8b8378">252 个，覆写行为钩子</text>
        <rect x="660" y="30" width="190" height="46" rx="9" fill="#fff" stroke="#d8cfbd"/>
        <text x="755" y="50" text-anchor="middle" font-size="12">① CheckAlone 独处判定</text>
        <text x="755" y="66" text-anchor="middle" font-size="11" fill="#8b8378">没仇恨太久 → 清空目标</text>
        <rect x="660" y="86" width="190" height="46" rx="9" fill="#fff" stroke="#d8cfbd"/>
        <text x="755" y="106" text-anchor="middle" font-size="12">② ProcessStacking 堆叠分离</text>
        <text x="755" y="122" text-anchor="middle" font-size="11" fill="#8b8378">和同类重叠 → 挪开</text>
        <rect x="660" y="142" width="190" height="46" rx="9" fill="#fff" stroke="#d8cfbd"/>
        <text x="755" y="162" text-anchor="middle" font-size="12">③ ProcessSearch 索敌</text>
        <text x="755" y="178" text-anchor="middle" font-size="11" fill="#8b8378">ViewRange 视野 + CoolEye 隐身怪</text>
        <rect x="880" y="58" width="190" height="46" rx="9" fill="#fff" stroke="#d8cfbd"/>
        <text x="975" y="78" text-anchor="middle" font-size="12">④ ProcessRoam 游荡</text>
        <text x="975" y="94" text-anchor="middle" font-size="11" fill="#8b8378">没目标时随机走动</text>
        <rect x="880" y="114" width="190" height="46" rx="9" fill="#fff" stroke="#d8cfbd"/>
        <text x="975" y="134" text-anchor="middle" font-size="12">⑤ ProcessTarget 攻击决策</text>
        <text x="975" y="150" text-anchor="middle" font-size="11" fill="#8b8378">进入射程 → Attack()</text>
        <line x1="160" y1="88" x2="208" y2="88" stroke="#a8442a" stroke-width="2" marker-end="url(#ar)"/>
        <line x1="380" y1="88" x2="428" y2="88" stroke="#a8442a" stroke-width="2" marker-end="url(#ar)"/>
        <line x1="610" y1="88" x2="658" y2="53" stroke="#a8442a" stroke-width="2" marker-end="url(#ar)"/>
        <line x1="850" y1="53" x2="878" y2="78" stroke="#a8442a" stroke-width="2" marker-end="url(#ar)"/>
        <line x1="850" y1="109" x2="878" y2="137" stroke="#a8442a" stroke-width="2" marker-end="url(#ar)"/>
        <line x1="755" y1="76" x2="755" y2="84" stroke="#a8442a" stroke-width="2"/>
      </g>
    </svg>
    <div class="note">
      <b>改 AI 最简单的方法：</b>GM 工具「数据库 → 怪物信息」→ 选中怪物 → 改「特殊AI」编号 → 保存。
      例如把一个近战怪改成 <b>21</b>（掷斧骷髅）它就会远程扔斧头并风筝走位。
      改的是<b>行为模板</b>，怪的血量/攻击力不变。★改库时服务端必须关掉（GUI 关闭时才存库）。<br>
      想要新行为就得写 C#：在 <code>Server/MirObjects/Monsters/</code> 新建类 → 在 <code>MonsterObject.GetMonster()</code> 加一个
      <code>case 编号:</code> → 重编 Server.Library → 重启。
    </div>
  </div>

  <h2>二、AI 家族图鉴（<span id="famCnt"></span> 个家族）</h2>
  <div class="toolbar">
    <input id="q" type="text" placeholder="搜索怪名 / 实现类 / AI 编号…">
    <select id="catSel"><option value="">全部类别</option>__CATOPT__</select>
    <select id="featSel"><option value="">全部特性</option>
      <option>远程攻击</option><option>施法/技能</option><option>召唤随从</option><option>施毒</option>
      <option>冰冻/麻痹/眩晕</option><option>自爆/引爆</option><option>瞬移</option><option>血量分段行为</option>
      <option>给目标上 BUFF</option><option>直线/穿透攻击</option><option>隐身</option><option>变身/换形态</option>
      <option>治疗/回复</option><option>挡路(城墙/城门)</option><option>自动复活</option><option>固定巡逻路线</option>
    </select>
    <span class="cnt" id="showCnt"></span>
  </div>
  <table id="tbl">
    <thead><tr>
      <th style="width:60px">立绘</th><th style="width:170px">家族 / 代表怪</th>
      <th style="width:110px">AI 编号</th><th style="width:86px">类别</th>
      <th>行为效果</th><th style="width:76px">怪物数</th>
    </tr></thead>
    <tbody></tbody>
  </table>

  <h2>三、未被本服使用的 AI 编号（备选池）</h2>
  <div class="note">
    下列 __UNUSEDCNT__ 个编号在工厂里有实现、但当前 <b>Server.MirDB 里没有怪物在用</b> —— 改「特殊AI」时可以直接拿来用：
    <div class="unused">__UNUSED__</div>
  </div>

  <footer>生成工具：Tools/AiProbe（只读，不碰 Server.MirDB） · 描述基于源码静态分析，个别分支行为以实机为准</footer>
</div>

<script>
const SPR = __SPR__;
const DATA = __DATA__;
const tb = document.querySelector('#tbl tbody');
function render(list){
  tb.innerHTML = list.map(r=>{
    const spr = r.img!=null && SPR[r.img] ? SPR[r.img] : '';
    const aiTags = r.ais.map(a=>'<span class="ai-tag">'+a+'</span>').join('');
    const names = r.names.map(n=>esc(n)).join(' / ');
    const imgName = r.imgName ? '<div class="meta">'+esc(r.imgName)+'.Lib</div>' : '';
    const lv = r.levels.length ? '<div class="meta">等级 '+r.levels[0]+(r.levels.length>1?'~'+r.levels[r.levels.length-1]:'')+'</div>' : '';
    return '<tr>'+
      '<td>'+(spr?'<img class="spr" src="'+spr+'">':'<div class="meta">无图</div>')+'</td>'+
      '<td><div class="mon">'+esc(r.cls)+'</div>'+imgName+
        '<div class="names">'+names+(r.all_names>r.names.length?' 等 '+r.all_names+' 种':'')+'</div></td>'+
      '<td>'+aiTags+'</td>'+
      '<td><span class="badge" style="background:'+({__CATCOLOR__})[r.cat]+'">'+r.cat+'</span></td>'+
      '<td class="desc">'+esc(r.desc)+'</td>'+
      '<td><b>'+r.count+'</b><div class="meta">'+lv+'</div></td>'+
    '</tr>';
  }).join('');
  document.getElementById('showCnt').textContent = '显示 '+list.length+' / '+DATA.length+' 个家族';
}
let q='', cat='', feat='';
function apply(){
  const k = q.trim().toLowerCase();
  const list = DATA.filter(r=>{
    if(cat && r.cat!==cat) return false;
    return true;
  });
  render(list);
}
document.getElementById('q').addEventListener('input', e=>{q=e.target.value;apply();});
document.getElementById('catSel').addEventListener('change', e=>{cat=e.target.value;apply();});
document.getElementById('featSel').addEventListener('change', e=>{
  feat=e.target.value;
  if(!feat){apply();return;}
  render(DATA.filter(r=>r.desc.includes(feat) || (r.tags&&r.tags.includes(feat))));
});
apply();
document.getElementById('famCnt').textContent = DATA.length;
</script>
</body>
</html>
"""

cat_opt = "".join(f'<option>{esc(k)}</option>' for k in CAT_COLOR)
cat_color_js = ",".join(f'"{esc(k)}":"{v}"' for k, v in CAT_COLOR.items())

html_doc = (html_doc
    .replace("__DATE__", "2026-10-13")
    .replace("__TOTALMON__", str(total_mon))
    .replace("__USEDAI__", str(len(used_ais)))
    .replace("__FAM__", str(len(rows)))
    .replace("__ALLAI__", str(len(all_factory_ais)))
    .replace("__SPRITES__", str(len(sprites)))
    .replace("__CATBAR__", cat_bar)
    .replace("__CATOPT__", cat_opt)
    .replace("__CATCOLOR__", cat_color_js)
    .replace("__UNUSEDCNT__", str(len(unused)))
    .replace("__UNUSED__", "、".join(str(a) for a in unused))
    .replace("__SPR__", sprite_js)
    .replace("__DATA__", data_js)
)

os.makedirs(OUT_DIR, exist_ok=True)
out_html = os.path.join(OUT_DIR, "本服怪物AI效果清单.html")
with open(out_html, "w", encoding="utf-8") as f:
    f.write(html_doc)
print("写出", out_html, "%.1f MB" % (os.path.getsize(out_html) / 1048576))

# ================= Markdown =================
md = []
md.append("# 本服怪物 AI 效果清单\n")
md.append("> 数据来源：`Server.MirDB`（只读探针 AiProbe）＋ `Server/MirObjects/Monsters/` 252 个实现类源码交叉分析 · 2026-10-13\n")
md.append(f"- 怪物定义：**{total_mon}** 个")
md.append(f"- 实际使用的 AI 编号：**{len(used_ais)}** 个（工厂共支持 {len(all_factory_ais)} 个）")
md.append(f"- AI 实现家族：**{len(rows)}** 个（有怪在用）\n")
md.append("## 一、AI 是怎么跑起来的\n")
md.append("```")
md.append("MonsterInfo.AI（GM 工具「特殊AI」框）")
md.append("   └→ MonsterObject.GetMonster() 工厂 switch")
md.append("        └→ 实现类（Server/MirObjects/Monsters/*.cs，覆写行为钩子）")
md.append("             └→ 基类 ProcessAI() 每帧循环：")
md.append("                  ① CheckAlone 独处判定 → ② ProcessStacking 堆叠分离 →")
md.append("                  ③ ProcessSearch 索敌(ViewRange/CoolEye) → ④ ProcessRoam 游荡 →")
md.append("                  ⑤ ProcessTarget 攻击决策(进射程→Attack)")
md.append("```\n")
md.append("**改 AI 最简单的方法**：GM 工具「数据库→怪物信息」→ 选中怪 → 改「特殊AI」编号 → 保存。"
          "改的是行为模板，血量/攻击力不变。★改库时服务端必须关掉。\n")
md.append("## 二、AI 家族图鉴\n")
by_cat = defaultdict(list)
for r in rows:
    by_cat[r["cat"]].append(r)
for cat in ["城防/功能", "伪装/采集/定身", "召唤系", "自爆系", "远程/施法系", "毒/控制系", "标准近战"]:
    lst = by_cat.get(cat, [])
    if not lst:
        continue
    md.append(f"### {cat}（{len(lst)} 个家族）\n")
    md.append("| AI | 实现类 | 怪物数 | 行为效果 | 代表怪 |")
    md.append("|---:|---|---:|---|---|")
    for r in sorted(lst, key=lambda x: -x["count"]):
        ai_s = "/".join(str(a) for a in r["ais"])
        names = "、".join(r["names"][:6])
        if r["all_names"] > 6:
            names += f" 等{r['all_names']}种"
        desc = r["desc"].replace("|", "\\|")
        md.append(f"| {ai_s} | {r['cls']} | {r['count']} | {desc} | {names} |")
    md.append("")
md.append("## 三、未被本服使用的 AI 编号（备选池）\n")
md.append("、".join(str(a) for a in unused) + "\n")
out_md = os.path.join(OUT_DIR, "本服怪物AI效果清单.md")
with open(out_md, "w", encoding="utf-8") as f:
    f.write("\n".join(md))
print("写出", out_md, "%.1f KB" % (os.path.getsize(out_md) / 1024))
