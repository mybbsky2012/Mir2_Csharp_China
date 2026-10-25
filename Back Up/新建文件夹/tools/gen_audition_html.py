# -*- coding: utf-8 -*-
"""生成道士技能音效试听页（自包含 HTML + 同目录 wav）"""
import json, os

BASE = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59"
data = json.load(open(os.path.join(BASE, "audition.json"), encoding="utf-8"))

CSS = """
:root{--bg:#f6f7f9;--card:#fff;--fg:#1b1f24;--sub:#5b6672;--line:#e3e6ea;--acc:#2f6fed;--warn:#c2410c;--ok:#15803d}
@media (prefers-color-scheme:dark){:root{--bg:#16181c;--card:#1e2126;--fg:#e8eaed;--sub:#9aa4af;--line:#30343b;--acc:#6ea8fe;--warn:#fb923c;--ok:#4ade80}}
*{box-sizing:border-box}
body{margin:0;padding:24px;background:var(--bg);color:var(--fg);font:14px/1.6 -apple-system,"Segoe UI","Microsoft YaHei",sans-serif}
h1{font-size:20px;margin:0 0 6px}
h2{font-size:16px;margin:26px 0 10px;padding-left:9px;border-left:3px solid var(--acc)}
p.lead{color:var(--sub);margin:0 0 4px}
.hint{color:var(--sub);font-size:12.5px}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px;margin-bottom:12px}
table{width:100%;border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:10px;overflow:hidden}
th,td{padding:8px 10px;text-align:left;border-bottom:1px solid var(--line);vertical-align:middle}
th{background:rgba(47,111,237,.08);font-weight:600;font-size:13px}
tbody tr:last-child td{border-bottom:none}
tbody tr:hover{background:rgba(47,111,237,.05)}
.play{border:1px solid var(--line);background:var(--card);color:var(--fg);border-radius:7px;padding:3px 9px;cursor:pointer;font-size:12px;margin:2px 3px 2px 0;white-space:nowrap}
.play:hover{border-color:var(--acc);color:var(--acc)}
.play.on{background:var(--acc);border-color:var(--acc);color:#fff}
select{max-width:330px;width:100%;padding:5px 7px;border:1px solid var(--line);border-radius:7px;background:var(--card);color:var(--fg);font-size:13px}
.sp{display:inline-block;min-width:40px;color:var(--sub);font-size:12px}
.miss{color:var(--warn);font-size:12px}
.cur{color:var(--sub);font-size:12px}
textarea{width:100%;height:150px;margin-top:8px;padding:10px;border:1px solid var(--line);border-radius:8px;background:var(--card);color:var(--fg);font:12px/1.5 ui-monospace,Consolas,monospace}
.btn{border:1px solid var(--acc);background:var(--acc);color:#fff;border-radius:8px;padding:7px 14px;cursor:pointer;font-size:13px;margin-top:8px}
.btn.ghost{background:transparent;color:var(--acc)}
.tag{display:inline-block;background:rgba(21,128,61,.15);color:var(--ok);border-radius:6px;padding:1px 7px;font-size:11.5px;margin-left:6px}
.tag.w{background:rgba(194,65,12,.15);color:var(--warn)}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:12px}
.flex{display:flex;gap:10px;flex-wrap:wrap;align-items:center}
"""

JS = r"""
const DATA = __DATA__;
const audio = {}; let curBtn = null, curFile = null;
function player(f){
  if(!audio[f]){ const a = new Audio(f); a.preload='none';
    a.addEventListener('ended', ()=>{ if(curBtn) curBtn.classList.remove('on'); }); audio[f]=a; }
  return audio[f];
}
function play(f, btn){
  if(curFile === f){ const a=player(f); a.pause(); a.currentTime=0; curFile=null; if(curBtn) curBtn.classList.remove('on'); return; }
  Object.values(audio).forEach(a=>{a.pause(); a.currentTime=0;});
  if(curBtn) curBtn.classList.remove('on');
  const a = player(f); a.currentTime = 0;
  a.play().catch(e=>alert('播放失败：'+f+'（'+e.message+'）'));
  curBtn = btn; btn.classList.add('on'); curFile = f;
}
function mkbtn(f, label){
  const b = document.createElement('button'); b.className='play'; b.textContent='\u25B6 '+(label||f);
  b.onclick = ()=>play(f,b); return b;
}
const DONE = [
 {n:'召唤骷髅', sp:65, from:'M65-0.wav', fromlab:'法师 嗜血术 的音', to:'400-1.wav', tolab:'骷髅 · 出现/站立'},
 {n:'召唤神兽', sp:78, from:'M78-0.wav', fromlab:'客户端缺文件（原本静音）', to:'1900-1.wav', tolab:'神兽 · 出现/站立'},
 {n:'精魂召唤术(圣兽)', sp:80, from:'M80-0.wav', fromlab:'原音偏怪', to:'1920-0.wav', tolab:'月灵(圣兽) · 出现'}
];
const dEl = document.getElementById('done');
DONE.forEach(d=>{
  const c = document.createElement('div'); c.className='card';
  const t = document.createElement('div');
  t.innerHTML = '<b>'+d.n+'</b> <span class="sp">('+d.sp+')</span>';
  c.appendChild(t);
  const l = document.createElement('div'); l.className='flex'; l.style.marginTop='6px';
  const s = document.createElement('span'); s.className='cur'; s.textContent='旧：';
  l.appendChild(s); l.appendChild(mkbtn(d.from, d.from+' — '+d.fromlab));
  c.appendChild(l);
  const l2 = document.createElement('div'); l2.className='flex';
  const s2 = document.createElement('span'); s2.className='cur'; s2.textContent='新：';
  l2.appendChild(s2); l2.appendChild(mkbtn(d.to, d.to+' — '+d.tolab));
  c.appendChild(l2);
  dEl.appendChild(c);
});
const POOL = DATA.pool;
const tb = document.getElementById('tb');
const sel = {};
DATA.rows.forEach(r=>{
  if([65,78,80].indexOf(r.sp) >= 0) return;
  const tr = document.createElement('tr');
  const td1 = document.createElement('td');
  td1.innerHTML = '<b>'+r.name+'</b><br><span class="sp">'+r.sp+'</span>';
  tr.appendChild(td1);
  const td2 = document.createElement('td');
  let firstFile = null;
  r.cur.forEach(c=>{
    if(c.file && !firstFile) firstFile = c.file;
    const w = document.createElement('div');
    if(c.file){ w.appendChild(mkbtn(c.file, c.file + (c.label.indexOf('\uFF08')>=0 ? ' ' + c.label.split('\uFF08')[1] : ''))); }
    else { w.innerHTML = '<span class="miss">'+c.label+'</span>'; }
    td2.appendChild(w);
  });
  tr.appendChild(td2);
  const td3 = document.createElement('td');
  const sl = document.createElement('select');
  const o0 = document.createElement('option'); o0.value=''; o0.textContent='\u2014 \u4fdd\u6301\u73b0\u503c \u2014'; sl.appendChild(o0);
  POOL.forEach(p=>{ const o=document.createElement('option'); o.value=p.file; o.textContent = p.file + '   ' + p.label; sl.appendChild(o); });
  td3.appendChild(sl);
  sel[r.sp] = {sl:sl, name:r.name, cur:firstFile};
  tr.appendChild(td3);
  const td4 = document.createElement('td');
  const pb = document.createElement('button'); pb.className='play'; pb.textContent='\u25B6';
  pb.onclick = ()=>{ const f = sl.value || sel[r.sp].cur; if(f) play(f, pb); else alert('\u8be5\u6280\u80fd\u5f53\u524d\u4e24\u7aef\u90fd\u7f3a\u6587\u4ef6'); };
  td4.appendChild(pb);
  tr.appendChild(td4);
  tb.appendChild(tr);
});
function exportSel(){
  const sps = Object.keys(sel).map(Number).sort((a,b)=>a-b);
  const lines = [];
  sps.forEach(sp=>{ const s = sel[sp]; if(s.sl.value){ lines.push('20' + String(sp+1000).slice(1) + ': ' + s.sl.value + '   // ' + s.name); } });
  const txt = lines.length ? lines.join('\n') : '\uFF08\u6ca1\u6709\u6539\u52a8\uFF09';
  document.getElementById('out').value = txt;
  document.getElementById('out').select();
  try{ document.execCommand('copy'); }catch(e){}
  alert('\u5df2\u751f\u6210\u5e76\u5c1d\u8bd5\u590d\u5236\uFF0c\u5171 ' + lines.length + ' \u6761\u3002');
}
function resetSel(){ Object.keys(sel).forEach(k=>sel[k].sl.value=''); document.getElementById('out').value=''; }
"""

html = (
    "<!DOCTYPE html>\n<html lang=\"zh-CN\">\n<head>\n<meta charset=\"utf-8\">\n"
    "<title>\u9053\u58eb\u6280\u80fd\u97f3\u6548\u8bd5\u542c\u4e0e\u9009\u914d</title>\n"
    "<style>" + CSS + "</style>\n</head>\n<body>\n"
    "<h1>\u9053\u58eb\u6280\u80fd\u97f3\u6548 \u00b7 \u8bd5\u542c\u4e0e\u9009\u914d</h1>\n"
    "<p class=\"lead\">\u8d44\u6e90\u5305\u91cc\u6ca1\u6709\u9053\u58eb\u539f\u97f3\uff08M61~M70 \u88ab\u6cd5\u5e08\u6bb5 M41~M50 \u8986\u76d6\uff09\uff0c\u6240\u4ee5\u300c\u91cd\u914d\u300d= \u7528\u5305\u5185\u5df2\u6709\u7d20\u6750\u91cd\u65b0\u6307\u5b9a\u3002</p>\n"
    "<p class=\"lead hint\">\u9009\u5b8c\u540e\u70b9\u6700\u4e0b\u65b9\u300c\u751f\u6210 / \u590d\u5236\u7ed3\u679c\u300d\uff0c\u628a\u6587\u672c\u53d1\u6211\uff0c\u6211\u6765\u5199\u5165 <code>SoundList.lst</code>\uff08\u53ea\u52a0 <code>20xxx</code> \u6620\u5c04\uff0c\u4e0d\u52a8\u4ee3\u7801\uff0c\u53ef\u56de\u6eda\uff09\u3002</p>\n"
    "<h2>\u2460 \u5df2\u6309\u4f60\u7684\u9009\u62e9\u4fee\u597d\uff08\u53ec\u5524\u7269\u672c\u4f53\u97f3\uff09<span class=\"tag\">\u5df2\u90e8\u7f72\uff0c\u91cd\u542f\u5ba2\u6237\u7aef\u751f\u6548</span></h2>\n"
    "<div class=\"grid\" id=\"done\"></div>\n"
    "<h2>\u2461 \u5176\u4f59\u9053\u58eb\u6280\u80fd \u2014\u2014 \u8bf7\u8bd5\u542c\u540e\u9009\u62e9<span class=\"tag w\">\u5f85\u4f60\u51b3\u5b9a</span></h2>\n"
    "<p class=\"lead hint\">\u300c\u73b0\u7528\u300d\u5c31\u662f\u73b0\u5728\u6e38\u620f\u91cc\u54cd\u7684\u90a3\u4e2a\uff1b\u4e0b\u62c9\u91cc\u662f\u53ef\u9009\u7d20\u6750\uff0c\u9009\u4e2d\u540e\u53ef\u70b9\u53f3\u8fb9 \u25b6 \u518d\u542c\u4e00\u904d\u3002</p>\n"
    "<table><thead><tr><th style=\"width:140px\">\u6280\u80fd</th><th style=\"width:32%\">\u73b0\u7528\u97f3</th><th>\u6539\u6210</th><th style=\"width:64px\">\u8bd5\u542c</th></tr></thead>\n"
    "<tbody id=\"tb\"></tbody></table>\n"
    "<h2>\u2462 \u590d\u5236\u7ed3\u679c</h2>\n"
    "<div class=\"card\">\n"
    "  <button class=\"btn\" onclick=\"exportSel()\">\u751f\u6210 / \u590d\u5236\u7ed3\u679c</button>\n"
    "  <button class=\"btn ghost\" onclick=\"resetSel()\">\u5168\u90e8\u6062\u590d\u300c\u73b0\u7528\u300d</button>\n"
    "  <textarea id=\"out\" placeholder=\"\u70b9\u4e0a\u9762\u7684\u6309\u94ae\u751f\u6210\u2026\"></textarea>\n"
    "</div>\n"
    "<script>" + JS.replace("__DATA__", json.dumps({"pool": data["pool"], "rows": data["rows"]}, ensure_ascii=False)) + "</script>\n"
    "</body>\n</html>\n"
)

out = os.path.join(BASE, "_work", "audio_audition", "\u9053\u58eb\u6280\u80fd\u97f3\u6548\u8bd5\u542c.html")
open(out, "w", encoding="utf-8").write(html)
print("OK ->", out)
print("size:", os.path.getsize(out))
d = os.path.join(BASE, "_work", "audio_audition")
print("dir files:", len(os.listdir(d)))
