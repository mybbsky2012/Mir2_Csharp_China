# -*- coding: utf-8 -*-
"""生成补齐报告（HTML + CSV）"""
import os, json, csv, collections, html, datetime

WORK = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(WORK, '..', '爆率补齐报告')
rep = json.load(open(os.path.join(WORK, 'gen_report.json'), encoding='utf-8'))
stage = os.path.join(WORK, 'stage')

filled = [r for r in rep if r['kind'] != 'skip']
skipped = [r for r in rep if r['kind'] == 'skip']
TIER = {'normal': '普通怪档', 'boss': '精英/BOSS档', 'topboss': '顶级BOSS档',
        'resource': '资源实体', 'container': '宝箱/容器'}
ORDER = ['topboss', 'boss', 'normal', 'container', 'resource']

filled.sort(key=lambda r: (ORDER.index(r['kind']), -r['hp']))

# ---------------- CSV ----------------
with open(OUT + '.csv', 'w', newline='', encoding='utf-8-sig') as f:
    w = csv.writer(f)
    w.writerow(['怪物名', '等级', '生命', '经验', '档位', '刷怪地图', '刷点数', '刷怪总量', '生成物品条数', '金币'])
    for r in filled:
        w.writerow([r['name'], r['level'], r['hp'], r['exp'], TIER[r['kind']],
                    '、'.join(r['maps']), r['points'], r['count'], r['items'], r['gold']])
    for r in skipped:
        w.writerow([r['name'], r['level'], r['hp'], r['exp'], '未配置（场景/守卫实体）',
                    '、'.join(r['maps']), r['points'], r['count'], 0, 0])

# ---------------- HTML ----------------
def esc(s):
    return html.escape(str(s))


samples = ['阎魔太子', '幽灵船员', '雪原斧兵', '月之镰神针', '大食人花', '灌木', '被诅咒的银箱', '半兽人']
samples = [s for s in ['阎魔太子', '幽灵船员', '雪原斧兵', '大食人花', '灌木', '被诅咒的银箱', '半兽人',
                       '魔风石怪', '异秘蛋', '毒妖花1'] if os.path.exists(os.path.join(stage, s + '.txt'))]

rows = []
for r in filled:
    rows.append('''<tr><td class="mono">%s</td><td>%s</td><td>%s</td><td>%s</td>
<td><span class="tag t-%s">%s</span></td><td class="maps">%s</td>
<td class="num">%s</td><td class="num">%s</td><td class="num">%s</td><td class="num">%s</td></tr>''' % (
        esc(r['name']), r['level'], r['hp'], format(r['exp'], ','), r['kind'], TIER[r['kind']],
        esc('、'.join(r['maps'])), r['points'], r['count'], r['items'], format(r['gold'], ',')))

skip_rows = ''.join('<tr><td class="mono">%s</td><td>%s</td><td>%s</td><td>%s</td><td class="maps">%s</td><td class="num">%s</td></tr>' % (
    esc(r['name']), r['level'], r['hp'], format(r['exp'], ','), esc('、'.join(r['maps'])), r['points'])
    for r in sorted(skipped, key=lambda x: -x['points']))

sample_html = []
for s in samples:
    txt = open(os.path.join(stage, s + '.txt'), encoding='utf-8').read().replace('\r\n', '\n')
    sample_html.append('<div class="samp"><div class="samp-h">%s</div><pre>%s</pre></div>' % (esc(s), esc(txt)))

cnt = collections.Counter(r['kind'] for r in filled)
total_items = sum(r['items'] for r in filled)

CSS = """
*{box-sizing:border-box}
body{margin:0;background:#f5f7fa;color:#1f2733;font:14px/1.7 "Microsoft YaHei","PingFang SC",system-ui,sans-serif}
.wrap{max-width:1180px;margin:0 auto;padding:32px 24px 64px}
h1{font-size:26px;margin:0 0 6px}
.sub{color:#6b7a8d;margin-bottom:26px}
h2{font-size:18px;margin:34px 0 12px;padding-left:10px;border-left:4px solid #2f6fdf}
.cards{display:flex;gap:14px;flex-wrap:wrap;margin-bottom:8px}
.card{flex:1;min-width:150px;background:#fff;border:1px solid #e3e9f0;border-radius:10px;padding:16px 18px}
.card .n{font-size:26px;font-weight:700;color:#2f6fdf}
.card .l{color:#6b7a8d;font-size:13px;margin-top:2px}
table{width:100%;border-collapse:collapse;background:#fff;border:1px solid #e3e9f0;border-radius:10px;overflow:hidden;font-size:13px}
th{background:#eef3fa;text-align:left;padding:10px 12px;font-weight:600;color:#3b4a5c;white-space:nowrap}
td{padding:8px 12px;border-top:1px solid #eef2f7;vertical-align:top}
tr:nth-child(even) td{background:#fafcff}
.mono{font-family:Consolas,"Courier New",monospace;white-space:nowrap}
.num{text-align:right;font-variant-numeric:tabular-nums}
.maps{color:#5a6b7f;font-size:12px;max-width:260px}
.tag{display:inline-block;padding:1px 8px;border-radius:10px;font-size:12px;white-space:nowrap}
.t-normal{background:#eef3fa;color:#3b6bb5}
.t-boss{background:#fdeee2;color:#b5670f}
.t-topboss{background:#fbe2e2;color:#b53030}
.t-resource{background:#e6f5ea;color:#2c7a45}
.t-container{background:#f1e9fb;color:#6b3fb0}
.note{background:#fff;border:1px solid #e3e9f0;border-left:4px solid #2f6fdf;border-radius:8px;padding:14px 18px;margin:12px 0}
.note.warn{border-left-color:#e08b2f;background:#fffaf3}
.note ul{margin:6px 0 0;padding-left:20px}
.samp{display:inline-block;vertical-align:top;background:#fff;border:1px solid #e3e9f0;border-radius:10px;margin:8px 10px 8px 0;width:calc(50% - 14px);overflow:hidden}
.samp-h{background:#eef3fa;padding:8px 14px;font-weight:600;color:#3b4a5c;border-bottom:1px solid #e3e9f0}
.samp pre{margin:0;padding:12px 14px;font-family:Consolas,"Courier New",monospace;font-size:12px;line-height:1.6;color:#33404f;max-height:330px;overflow:auto;white-space:pre-wrap}
code{background:#eef3fa;padding:1px 5px;border-radius:4px;font-family:Consolas,monospace;font-size:12.5px}
"""

HTML = """<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>怪物爆率补齐报告 — 20260915Mir2Server</title>
<style>%s</style></head><body><div class="wrap">

<h1>怪物爆率补齐报告</h1>
<div class="sub">目标服务端：<code>E:\\BaiduNetdiskDownload\\韩Mir2\\20260915更新\\20260915Mir2Server</code>　｜　
数据来源：<code>Server.MirDB</code>（版本 110）　｜　生成时间：%s</div>

<h2>一、做了什么</h2>
<div class="note">
扫描 <code>Server.MirDB</code> 里全部 <b>626 张地图 / 8621 个刷怪点</b>，得到地图上真实会刷出的怪物
<b>442 种</b>；再逐个比对 <code>Envir\\Drops</code> 下的爆率文件，找出
<b>爆率表为空的怪物 263 种</b>（其中 252 种文件里一条物品都没有，11 种只有金币没有物品），
按数据库中的怪物属性与现有爆率体系，为其中 <b>242 种</b>生成了完整爆率文件。
</div>
<div class="cards">
  <div class="card"><div class="n">442</div><div class="l">地图上实际会刷出的怪物种类</div></div>
  <div class="card"><div class="n">263</div><div class="l">修复前无任何物品爆率</div></div>
  <div class="card"><div class="n">242</div><div class="l">本次补齐爆率的怪物</div></div>
  <div class="card"><div class="n">%d</div><div class="l">新写入的掉落条目</div></div>
  <div class="card"><div class="n">421 / 442</div><div class="l">修复后有物品爆率的怪物（95.2%%）</div></div>
</div>

<h2>二、判定依据（全部来自数据库与既有配置）</h2>
<div class="note">
<ul>
<li><b>谁在地图上刷</b>：直接解析 <code>Server.MirDB</code> 的 <code>MapInfo.Respawns</code>，不依赖任何导出文本。</li>
<li><b>怪物强弱</b>：数据库里的等级 / 生命 / 经验 / AI，用于定位「同档参照怪」。</li>
<li><b>物品是否合法</b>：所有写出的物品名都取自数据库物品表（4238 件），并已逐一校验 100%% 命中——
不会出现服务端启动时报「未能加载 XXX 所需物品」的情况。</li>
<li><b>掉率强度</b>：以服务端<b>已有真实爆率表的 139 只怪</b>为标尺。对每只待补怪物，
按「生命 + 经验 + 等级 + 是否 BOSS + 是否同地图」加权找到最近邻的 4 只参照怪，
取其物品池与掉率的加权中位数作为基准，再按档位微调。</li>
</ul>
</div>

<h2>三、分档与差异化规则</h2>
<div class="note">
<ul>
<li><b>%s</b>：%d 只 —— 生命 ≥ 30000 或经验 ≥ 20000。承接顶级参照怪（丹墨、凰天魔神、浮龙金蛇、
邪牛天王、祖玛教主、骸骨盘王）的稀有掉落，并额外追加一批顶级稀有物品，掉率在参照基础上再放宽 2~3 倍，金币加成。</li>
<li><b>%s</b>：%d 只 —— 生命 ≥ 7000 或经验 ≥ 8000。药水掉率在参照基础上收紧约 1.5 倍。</li>
<li><b>%s</b>：%d 只 —— 其余战斗怪。药水掉率收紧 3 倍，并剔除分母 &gt; 20000 的「传说级」条目，避免小怪爆神器。</li>
<li><b>%s</b>：%d 只 —— 树、草、蘑菇、食人花等资源实体，只掉对应采集物（木材／果实／花叶等）。</li>
<li><b>%s</b>：%d 只 —— 木盒／银箱／异秘蛋等容器，掉消耗品、宝玉与少量技能书。</li>
<li><b>刷怪密度修正</b>：刷点越多、总量越大的怪，单只掉率按
<code>√(该怪总量 ÷ 参照怪中位总量)</code> 额外收紧（上限 2.5 倍），金币数额同步下调，
避免高密度怪变成刷钱刷装备的漏洞。</li>
<li><b>金币</b>：沿用参照怪的金币加权中位数；原文件里已经写好的金币行<b>原样保留</b>，不做改动。</li>
</ul>
</div>

<h2>四、刻意未配置的 %d 种（场景／守卫实体）</h2>
<div class="note warn">
这些实体虽然出现在刷怪表里，但属于城门、传送门、陷阱、护栏、雕像、城管守卫与练功木桩，
给它们配掉落会形成「刷墙／刷守卫」的漏洞，因此保持原样不动。如果你确实希望其中某些也能掉落，
告诉我名字，我再单独补。
</div>
<table><thead><tr><th>怪物</th><th>等级</th><th>生命</th><th>经验</th><th>刷怪地图</th><th style="text-align:right">刷点数</th></tr></thead>
<tbody>%s</tbody></table>

<h2>五、补齐清单（%d 种）</h2>
<table><thead><tr><th>怪物</th><th>等级</th><th>生命</th><th>经验</th><th>档位</th><th>刷怪地图</th>
<th style="text-align:right">刷点</th><th style="text-align:right">总量</th><th style="text-align:right">条目</th><th style="text-align:right">金币</th></tr></thead>
<tbody>%s</tbody></table>

<h2>六、生成样例</h2>
%s

<h2>七、部署与回滚</h2>
<div class="note">
<ul>
<li>全部 242 个文件已写入 <code>Envir\\Drops\\&lt;怪物名&gt;.txt</code>（UTF-8 无 BOM + CRLF，与目录内 1439 个既有文件一致）。</li>
<li>原文件已备份至服务端目录：<code>Back Up\\auto-20261003-151854-爆率补齐前\\</code>，直接覆盖回去即可完全回滚。</li>
<li><b>爆率文件是服务端启动时加载的</b>，服务端当前正在运行（进程已加载旧表），
需要重启服务端（或用 GM 重载掉落指令）后新爆率才会生效。</li>
</ul>
</div>

</div></body></html>
""" % (CSS, datetime.datetime.now().strftime('%Y-%m-%d %H:%M'),
       total_items,
       TIER['topboss'], cnt['topboss'], TIER['boss'], cnt['boss'], TIER['normal'], cnt['normal'],
       TIER['resource'], cnt['resource'], TIER['container'], cnt['container'],
       len(skipped), skip_rows, len(filled), ''.join(rows), ''.join(sample_html))

open(OUT + '.html', 'w', encoding='utf-8').write(HTML)
print('已生成：', os.path.abspath(OUT + '.html'), os.path.abspath(OUT + '.csv'))
