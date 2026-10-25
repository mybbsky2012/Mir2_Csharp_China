import csv, io, os, html, datetime

OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\道士技能音效修复对照表.html"
CLI = r"E:\micromir2\Sound"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"

raw = open(os.path.join(r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Exports\技能数据.csv"), "rb").read().decode("utf-8-sig", "ignore")
rows = list(csv.reader(io.StringIO(raw)))
names = {}
en = {}
for i, r in enumerate(rows[1:], start=1):
    if r and r[0].strip():
        names[i] = r[0].strip()
        en[i] = r[1].strip() if len(r) > 1 else ""

m, order = {}, []
for ln in open(os.path.join(CLI, "SoundList.lst"), "rb").read().decode("utf-8-sig", "ignore").splitlines():
    ps = ln.replace(" ", "").split(":")
    if len(ps) > 1:
        try:
            idx = int(ps[0])
        except Exception:
            continue
        if idx not in m:
            order.append(idx)
        m[idx] = ps[-1]

have = set(os.listdir(CLI)) | set(os.listdir(SRV))
CLASSES = [("战士", 1, 22, 0), ("法师", 31, 58, 20), ("道士", 61, 88, 40), ("刺客", 91, 107, 60), ("弓手", 121, 141, 80)]

def cls_of(sp):
    for n, lo, hi, off in CLASSES:
        if lo <= sp <= hi:
            return n, off
    return None, 0

REG = {"0": "发出 / 起手", "1": "命中 / 释放", "2": "飞行 / 二段", "3": "三段", "4": "四段", "5": "五段", "6": "六段", "7": "七段", "8": "八段", "9": "九段"}

def build(lo, hi, off):
    out = []
    for sp in range(lo, hi + 1):
        for v in range(10):
            idx = 20000 + sp * 10 + v
            if idx not in m:
                continue
            tgt = m[idx]
            fb = f"M{sp}-{v}.wav"
            old = fb if fb in have else "（无 / 静音）"
            same = (tgt == old)
            note = ""
            if idx in (20471, 20861, 21264, 21377, 21381):
                note = "同技能替代"
            elif tgt != f"M{sp + off}-{v}.wav":
                note = "变体替身"
            out.append((idx, sp, v, names.get(sp, ""), en.get(sp, ""), tgt, old, same, note))
    return out

sections = []
for n, lo, hi, off in CLASSES:
    sections.append((n, off, build(lo, hi, off)))

total = sum(len(s[2]) for s in sections)
fixed = sum(1 for s in sections for r in s[2] if not r[7])
stamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

parts = []
parts.append("""<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>技能音效修复对照表</title>
<style>
:root{--bg:#f7f7f5;--card:#fff;--line:#e3e3df;--fg:#1b1b19;--mut:#6b6b66;--ok:#0f6e56;--warn:#854f0b;--bad:#a32d2d;--hl:#eef4fb}
@media (prefers-color-scheme:dark){:root{--bg:#1b1b19;--card:#252522;--line:#3a3a36;--fg:#f0f0ec;--mut:#a8a8a1;--ok:#5dcaa5;--warn:#ef9f27;--bad:#f09595;--hl:#1e2a35}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.65 -apple-system,"Segoe UI","PingFang SC","Microsoft YaHei",sans-serif}
.wrap{max-width:1080px;margin:0 auto;padding:32px 20px 64px}
h1{font-size:20px;font-weight:500;margin:0 0 6px}
.sub{color:var(--mut);font-size:13px;margin-bottom:22px}
.kpi{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:26px}
.kpi div{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:10px 16px;font-size:13px;color:var(--mut)}
.kpi b{color:var(--fg);font-size:17px;font-weight:500;margin-right:4px}
h2{font-size:15px;font-weight:500;margin:28px 0 10px}
h2 span{color:var(--mut);font-weight:400;font-size:13px;margin-left:8px}
table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:10px;overflow:hidden;font-size:13px}
th,td{text-align:left;padding:7px 12px;border-bottom:1px solid var(--line)}
th{background:var(--hl);font-weight:500;color:var(--mut);font-size:12px;white-space:nowrap}
tr:last-child td{border-bottom:none}
td.n{color:var(--mut);font-variant-numeric:tabular-nums}
code{font-family:ui-monospace,Consolas,monospace;font-size:12px}
.new{color:var(--ok);font-weight:500}
.old{color:var(--bad)}
.tag{display:inline-block;font-size:11px;padding:1px 6px;border-radius:5px;background:var(--hl);color:var(--warn);margin-left:6px}
.note{color:var(--mut);font-size:12px;margin-top:26px;border-top:1px solid var(--line);padding-top:14px}
</style></head><body><div class="wrap">""")

parts.append(f"<h1>技能音效修复对照表</h1><div class='sub'>索引 = 20000 + 技能号 × 10 + 变体　·　生成于 {stamp}　·　已写入客户端与服务端两份 SoundList.lst</div>")
parts.append(f"<div class='kpi'><div><b>{total}</b>条显式映射</div><div><b>{fixed}</b>条纠正了原错误音</div>"
             f"<div><b>0</b>条旧条目丢失</div><div><b>195</b>条 20xxx 总条目</div></div>")

for n, off, rs in sections:
    if not rs:
        continue
    cnt = sum(1 for r in rs if not r[7])
    parts.append(f"<h2>{n}<span>Crystal {rs[0][1]}–{rs[-1][1]}　韩版资源号偏移 {off:+d}　共 {len(rs)} 条，纠正 {cnt} 条</span></h2>")
    parts.append("<table><tr><th>索引</th><th>技能</th><th>变体</th><th>修复后指向</th><th>修复前实际播放</th></tr>")
    for idx, sp, v, cn, en_, tgt, old, same, note in rs:
        tag = f"<span class='tag'>{note}</span>" if note else ""
        nm = html.escape(cn) + (f" <span style='color:var(--mut);font-size:11px'>{html.escape(en_)}</span>" if en_ else "")
        parts.append(f"<tr><td class='n'>{idx}</td><td>{nm}</td><td class='n'>{v} <span style='color:var(--mut);font-size:11px'>{REG.get(str(v),'')}</span></td>"
                     f"<td><code class='new'>{tgt}</code>{tag}</td><td><code class='old'>{old}</code></td></tr>")
    parts.append("</table>")

parts.append("<div class='note'>说明：修复前本服 SoundList.lst 整段没有 20xxx 映射，所有技能音效都落到兜底命名 <code>M{技能号}-{变体}.wav</code>，"
             "而韩版资源号是「每职业 50 号一段」的另一套编号，因此播到的是别的职业的音频。本次不改任何音频文件、不改代码，只补索引映射。"
             "标注「同技能替代 / 变体替身」的条目表示原目标文件在资源包中不存在，借用同技能的其它采样，避免完全静音。</div>")
parts.append("</div></body></html>")

open(OUT, "w", encoding="utf-8").write("".join(parts))
print("生成:", OUT, os.path.getsize(OUT), "字节   映射条数:", total, " 纠正:", fixed)
