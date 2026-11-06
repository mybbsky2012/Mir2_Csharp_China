# -*- coding: utf-8 -*-
"""生成自包含 HTML 动画报告（机理A/机理B 对比）"""
import json, os

d = json.load(open("sim_result.json", encoding="utf-8"))
A, B = d["A"], d["B"]

CELL_W, CELL_H = 48, 32
CAM = [6, 5]                       # 动画用固定镜头（镜头移动不改变机理：虚影随世界滚动）

TREE = [505, 95]                   # 树在画面上的像素位置（树冠中心）

def sprite(name, x, y, w, h, colour, additive=False, alpha=1.0):
    return dict(x=x, y=y, w=w, h=h, colour=list(colour), alpha=alpha,
                additive=additive, label=name)

GRASS = [58, 106, 48]

def build_frame(i):
    """帧0-8 锁鹿B（在树后）；帧9-13 锁NPC；帧14+ 无锁。坐标均为画布像素。"""
    lock = "鹿B" if i < 9 else ("NPC" if i < 14 else None)
    sp = [
        sprite("NPC", 140, 180, 34, 40, [222, 186, 140]),
        sprite("鹿A", 300, 220, 40, 26, [238, 234, 224]),
        sprite("鹿B", 505, 128, 40, 26, [238, 234, 224]),   # 树后：树冠会盖住它上半身
        sprite("玩家", 240, 160, 26, 40, [70, 90, 160]),
    ]
    if lock == "鹿B":
        sp.append(dict(x=505, y=128, w=40, h=26, colour=[238, 234, 224], alpha=0.3,
                       additive=True, label="鹿B·高亮副本"))
    elif lock == "NPC":
        sp.append(dict(x=140, y=180, w=34, h=40, colour=[222, 186, 140], alpha=0.3,
                       additive=True, label="NPC·高亮副本"))
    cap = "帧%d  锁定=%s" % (i, lock or "无")
    if lock == "鹿B":
        cap += "（实体被树挡住，高亮副本画在树上面 → 飘在遮挡上的黄影）"
    elif lock == "NPC":
        cap += "（锁转移到 NPC → 鹿B 黄影消失，NPC 出现黄影）"
    else:
        cap += "（无锁 → 无虚影）"
    return dict(frame=i, lock=lock, tree=list(TREE), sprites=sp, caption=cap)

anim_frames = [build_frame(i) for i in range(17)]

rows = []
for fr in B:
    g = ", ".join("%s → RGB%s" % (x["label"], tuple(x["colour"])) for x in fr["ghosts"]) or "无"
    note = "换目标 → 旧虚影消失" if fr["frame"] >= 10 else "锁定对象每帧被 0.3 加法再画一遍"
    rows.append("<tr><td>%d</td><td>%s</td><td class='%s'>%d</td><td>%s（%s）</td></tr>"
                % (fr["frame"], fr["lock"], "bad" if fr["ghost_count"] else "ok",
                   fr["ghost_count"], g, note))

HTML = """<!DOCTYPE html>
<html lang="zh">
<head>
<meta charset="utf-8">
<title>黄色残影机理仿真报告</title>
<style>
  body{font-family:"Microsoft YaHei",sans-serif;background:#1e2229;color:#d8dee5;margin:0;padding:24px;}
  h1{font-size:22px;margin:0 0 4px;}
  h2{font-size:17px;margin:28px 0 8px;color:#ffd479;}
  .sub{color:#8a93a0;font-size:13px;margin-bottom:18px;}
  .panel{background:#262b34;border:1px solid #3a4150;border-radius:10px;padding:16px;margin-bottom:18px;}
  canvas{background:#0e1116;border-radius:6px;display:block;}
  .row{display:flex;gap:16px;flex-wrap:wrap;align-items:flex-start;}
  .col{flex:1 1 380px;min-width:340px;}
  table{border-collapse:collapse;font-size:13px;margin-top:10px;width:100%;}
  th,td{border:1px solid #3a4150;padding:5px 9px;text-align:left;vertical-align:top;}
  th{background:#2e3440;color:#ffd479;}
  .ok{color:#7ec97e;} .bad{color:#e88;} .warn{color:#ffd479;}
  code{background:#11151b;padding:1px 5px;border-radius:4px;font-size:12px;color:#9cd3ff;}
  pre{background:#11151b;padding:8px;border-radius:6px;font-size:12px;color:#9cd3ff;overflow:auto;}
  .note{font-size:13px;color:#a8b2bf;line-height:1.7;}
  .verdict{background:#3a3320;border:1px solid #ffd479;border-radius:8px;padding:12px 16px;margin:14px 0;line-height:1.8;}
  .legend{font-size:12px;color:#8a93a0;margin-top:6px;}
</style>
</head>
<body>
<h1>黄色残影机理仿真报告</h1>
<div class="sub">按 Client 源码语义建模：CreateTexture 缓存重建（GameScene.cs:11358-11474）/ 全局混合状态 / 高亮二次绘制（11848-11859）。2026-11-06</div>

<div class="verdict">
<b>仿真结论（先说答案）：</b><br>
① <b>「混合状态泄漏」产生不了你看到的残影</b> —— 异常会让本帧中止、缓存贴图因"未标记有效"而在下一帧被整体清空重画（<code>TextureValid=true</code> 在 CreateTexture 最后一行），残影最多存在一帧。旧代码与补丁版一样：<b>缓存内残影数 = 0</b>。<br>
② <b>真正来源是「目标高亮二次绘制」</b>：你的配置 <code>HighlightTarget=True</code> + <code>MagicLock=True</code>，被锁定/指向的对象每帧会被用 <b>30% 加法混合再画一遍</b>，而且画在<b>所有遮挡物之上</b>——树、墙、草丛都挡不住它 → 一只"浮在场景上的半透明黄影"，换目标即消失。<b>NPC 同样会中招（魔法锁对 NPC 生效）</b>，与你"NPC 也出现过"吻合。
</div>

<h2>机理 A：混合状态泄漏（异常型）—— 判定：产生不了残影</h2>
<div class="panel">
<div class="row">
  <div class="col"><canvas id="cvA" width="720" height="288"></canvas>
  <div class="legend">故障注入：第 3 帧起「鹿B」绘制抛异常（模拟素材缺失）。注意画面上从未出现黄影。</div></div>
  <div class="col note">
    为什么留不下残影？客户端是<b>整屏缓存渲染</b>：
    <ol>
      <li>每约 100ms 重建一次缓存贴图，重建第一步 <code>Device.Clear()</code> 清空；</li>
      <li><code>TextureValid = true</code> 在<b>最后一行</b>（GameScene.cs:11473）——中途抛异常 = "没画完"；</li>
      <li>没画完 → 下一帧<b>从头重画</b> → 清空 → 任何泄漏状态都活不过一帧。</li>
    </ol>
    补丁的价值：异常不再把全局混合状态留给同帧后续绘制，并写 Error.txt 帮你定位故障对象。<b>但它治不了你看到的黄影——因为黄影根本不是泄漏。</b>
  </div>
</div>
<table>
<tr><th>构建</th><th>帧0-2</th><th>帧3（故障）</th><th>帧4-7</th><th>缓存内残影</th></tr>
<tr><td>旧代码</td><td class="ok">正常</td><td class="warn">异常中止，贴图无效</td><td>重画仍失败</td><td class="ok">0（被 Clear 洗掉）</td></tr>
<tr><td>补丁版</td><td class="ok">正常</td><td class="warn">异常中止 + 护栏复位混合</td><td>重画仍失败</td><td class="ok">0</td></tr>
</table>
</div>

<h2>机理 B：目标高亮二次绘制 —— 判定：<span class="bad">完整复现你的现象</span></h2>
<div class="panel">
<div class="row">
  <div class="col"><canvas id="cvB" width="720" height="288"></canvas>
  <div class="legend">帧0-8 锁定「鹿B」（站在树后：实体被树挡住，高亮副本却画在树<b>上面</b>）；帧9-13 锁 NPC；帧14+ 无锁。</div></div>
  <div class="col note">
    每帧管线：正常画完所有对象（含遮挡）之后，<code>GameScene.cs:11848</code> 追加：
<pre>if (Settings.HighlightTarget) {
    if (MouseObject != null && ... && MouseObject.Blend)
        MouseObject.DrawBlend();          // 0.3 加法
    if (TargetObject != null)
        TargetObject.DrawBlend();         // ← 没有 Dead/视野/遮挡判断
    if (Settings.MagicLock && LockObject != null && ...)
        LockObject.DrawBlend();
}</pre>
    三个触发源：鼠标指向、当前攻击目标、魔法锁定（<b>你的 MagicLock=True，锁会一直挂着</b>）。<br><br>
    <b>颜色对上了</b>：草地 RGB(58,106,48) + 鹿 RGB(238,234,224) × 0.3 加法 = <span class="warn">RGB(129,176,115)</span> —— 黄白色虚影，正是截图里的颜色。<br>
    <b>"过会儿消失"</b>：换目标 / 移开鼠标 / 锁定转移 / 对象被删（Remove 会清四个引用）。<br>
    <b>"NPC 也出现"</b>：魔法锁对 NPC 同样生效。
  </div>
</div>
<table>
<tr><th>帧</th><th>锁定对象</th><th>缓存内"虚影"数</th><th>虚影（仿真输出）</th></tr>
__ROWS_B__
</table>
</div>

<h2>怎么确认（零代码，2 分钟）</h2>
<div class="panel note">
进游戏做 A/B 实验：<b>把「目标高亮」关掉</b>（改 <code>E:\\micromir2\\Mir2Config.ini</code> 第 42 行 <code>HighlightTarget=False</code>，或内挂设置里取消勾选），回白龙潭走动一分钟。
<ul>
<li>黄影<b>不再出现</b> → 因果确认：就是高亮二次绘制，不是渲染故障。之后想要"高亮但不像鬼"，我可以加两个条件（只画视野内 + 被遮挡时不越过遮挡层）或把 0.3 调到 0.15，几行判断的事。</li>
<li>黄影<b>仍然出现</b> → 推翻本结论，把出现时的地图+动作告诉我（补丁里的泄漏日志此时也是活的，能提供旁证）。</li>
</ul>
之前打的补丁（try/finally + 护栏）属于无害保险，<b>建议照常部署</b>；但它不是这次黄影的解药。
</div>

<script>
const DATA_B = __DATA_B__;

function rgb(c){return `rgb(${c[0]},${c[1]},${c[2]})`;}
function roundRect(ctx,x,y,w,h,fill,alpha,ghost){
  ctx.globalAlpha=alpha; ctx.fillStyle=fill; ctx.beginPath();
  if(ctx.roundRect) ctx.roundRect(x,y,w,h,6); else ctx.rect(x,y,w,h);
  ctx.fill(); ctx.globalAlpha=1;
  if(ghost){ctx.strokeStyle="rgba(255,220,120,0.9)";ctx.setLineDash([4,3]);ctx.strokeRect(x-2,y-2,w+4,h+4);ctx.setLineDash([]);}
}
function drawScene(ctx,fr,showGhost){
  const W=720,H=288;
  ctx.fillStyle="#3a6a30";ctx.fillRect(0,0,W,H);
  ctx.fillStyle="rgba(255,255,255,0.04)";
  for(let gy=0;gy<H;gy+=32)for(let gx=0;gx<W;gx+=48)if(((gx/48)+(gy/32))%2===0)ctx.fillRect(gx,gy,48,32);
  for(const s of fr.sprites.filter(s=>!s.additive)){
    roundRect(ctx,s.x,s.y,s.w,s.h,rgb(s.colour),1,false);
    if(s.label){ctx.fillStyle="rgba(0,0,0,0.55)";ctx.fillRect(s.x,s.y-13,ctx.measureText(s.label).width+10,13);
      ctx.fillStyle="#fff";ctx.font="10px sans-serif";ctx.fillText(s.label,s.x+5,s.y-3);}
  }
  // 树（遮挡物，画在对象之后）
  const t=fr.tree;
  ctx.fillStyle="#5a4630";ctx.fillRect(t[0],t[1]+8,10,30);
  ctx.fillStyle="#2e5c26";ctx.beginPath();ctx.ellipse(t[0]+5,t[1],34,24,0,0,7);ctx.fill();
  if(showGhost)for(const g of fr.sprites.filter(s=>s.additive)){
    ctx.globalCompositeOperation="lighter";
    roundRect(ctx,g.x,g.y,g.w,g.h,rgb(g.colour),g.alpha,true);
    ctx.globalCompositeOperation="source-over";
    ctx.fillStyle="#ffd479";ctx.font="11px sans-serif";ctx.fillText(g.label,g.x,g.y-16);
  }
  ctx.fillStyle="#ffd479";ctx.font="13px sans-serif";ctx.fillText(fr.caption,8,16);
}
function anim(canvasId,frames,ghost){
  const cv=document.getElementById(canvasId),ctx=cv.getContext("2d");let i=0;
  setInterval(()=>{drawScene(ctx,frames[i%frames.length],ghost);i++;},650);
}
// 场景A：故障注入（无残影）
const fa=[];for(let i=0;i<8;i++){fa.push({frame:i,tree:[600,60],caption:"帧"+i+(i>=3?"（鹿B 故障→本帧中止，下一帧整体重画，无残影）":"（正常）"),sprites:[
  {x:26,y:74,w:34,h:40,colour:[222,186,140],alpha:1,additive:false,label:"NPC"},
  {x:218,y:150,w:40,h:26,colour:[238,234,224],alpha:1,additive:false,label:"鹿A"},
  {x:362,y:106,w:40,h:26,colour:i>=3?[120,120,120]:[238,234,224],alpha:1,additive:false,label:i>=3?"鹿B(故障)":"鹿B"},
  {x:290,y:190,w:26,h:40,colour:[70,90,160],alpha:1,additive:false,label:"玩家"}]});}
anim("cvA",fa,false);
anim("cvB",DATA_B,true);
</script>
</body>
</html>"""

html = HTML.replace("__ROWS_B__", "\n".join(rows)).replace("__DATA_B__", json.dumps(anim_frames, ensure_ascii=False))
open("残影机理仿真.html", "w", encoding="utf-8").write(html)
print("OK ->", os.path.abspath("残影机理仿真.html"))
