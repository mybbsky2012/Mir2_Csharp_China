#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Stage 2 排版脚本：final_draft.md -> business-report 风格完整 HTML。
自律约束（对应 doc-formatter §2.5）：
  · 正文宋体 12pt + 首行缩进 2em，标题黑体
  · 章节标题用 h1/h2/h3 并带 id，供 TOC 锚点
  · 封面标题不用 h1（避免被 TOC 收录）
  · 封面 / 目录 / 正文 三个顶层 section，正文 data-page-restart="1"
  · @page 页脚居中页码，封面命名页无页码
"""
import json
import re
import sys
from pathlib import Path

SRC = Path(sys.argv[1])
OUT_HTML = Path(sys.argv[2])
OUT_TOKENS = Path(sys.argv[3])

md = SRC.read_text(encoding="utf-8")

def esc(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))

def inline(s):
    s = esc(s)
    s = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", s)
    s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
    return s

lines = md.split("\n")
i = 0
body = []
toc = []          # [(level, id, text)]
h1_n = 0
h2_n = 0
in_list = None    # 'ul' / 'ol'
para = []

TITLE = None

def flush_para():
    global para
    if para:
        text = " ".join(x.strip() for x in para)
        body.append(f'<p class="body-p">{inline(text)}</p>')
        para = []

def close_list():
    global in_list
    if in_list:
        body.append(f"</{in_list}>")
        in_list = None

while i < len(lines):
    line = lines[i]
    stripped = line.strip()

    # 代码块
    if stripped.startswith("```"):
        flush_para(); close_list()
        i += 1
        code = []
        while i < len(lines) and not lines[i].strip().startswith("```"):
            code.append(lines[i])
            i += 1
        i += 1
        body.append('<pre class="code">' + esc("\n".join(code)) + "</pre>")
        continue

    # 表格
    if stripped.startswith("|") and i + 1 < len(lines) and re.match(r"^\|[\s:\-|]+\|$", lines[i + 1].strip()):
        flush_para(); close_list()
        header = [c.strip() for c in stripped.strip("|").split("|")]
        i += 2
        rows = []
        while i < len(lines) and lines[i].strip().startswith("|"):
            rows.append([c.strip() for c in lines[i].strip().strip("|").split("|")])
            i += 1
        t = ['<table class="tbl"><thead><tr>']
        for h in header:
            t.append(f'<th class="th">{inline(h)}</th>')
        t.append("</tr></thead><tbody>")
        for r in rows:
            t.append("<tr>")
            for c in r:
                t.append(f'<td class="td">{inline(c)}</td>')
            t.append("</tr>")
        t.append("</tbody></table>")
        body.append("".join(t))
        continue

    # 标题
    if stripped.startswith("#### "):
        flush_para(); close_list()
        body.append(f'<h3 class="h3">{inline(stripped[5:])}</h3>')
        i += 1
        continue

    if stripped.startswith("### "):
        flush_para(); close_list()
        text = stripped[4:]
        h2_n += 1
        sid = f"sub-{h2_n}"
        toc.append((2, sid, text))
        body.append(f'<h2 class="h2" id="{sid}">{inline(text)}</h2>')
        i += 1
        continue

    if stripped.startswith("## "):
        flush_para(); close_list()
        text = stripped[3:]
        h1_n += 1
        sid = f"section-{h1_n}"
        toc.append((1, sid, text))
        body.append(f'<h1 class="h1" id="{sid}">{inline(text)}</h1>')
        i += 1
        continue

    if stripped.startswith("# "):
        flush_para(); close_list()
        text = stripped[2:]
        if TITLE is None:
            TITLE = text          # 文档主标题 → 只用于封面/文档属性
            i += 1
            continue
        h1_n += 1
        sid = f"section-{h1_n}"
        toc.append((1, sid, text))
        body.append(f'<h1 class="h1" id="{sid}">{inline(text)}</h1>')
        i += 1
        continue

    # 分隔线
    if stripped == "---":
        flush_para(); close_list()
        body.append('<div class="divider"></div>')
        i += 1
        continue

    # 引用 / 提示块
    if stripped.startswith("> "):
        flush_para(); close_list()
        note = []
        while i < len(lines) and lines[i].strip().startswith("> "):
            note.append(lines[i].strip()[2:])
            i += 1
        body.append('<div class="callout">' + inline(" ".join(note)) + "</div>")
        continue

    # 列表
    m_ul = re.match(r"^\s*[-*] (.+)$", line)
    m_ol = re.match(r"^\s*\d+\. (.+)$", line)
    if m_ul or m_ol:
        flush_para()
        want = "ul" if m_ul else "ol"
        if in_list != want:
            close_list()
            body.append(f'<{want} class="{"ul" if want == "ul" else "ol"}">')
            in_list = want
        text = (m_ul.group(1) if m_ul else m_ol.group(1))
        body.append(f'<li class="li">{inline(text)}</li>')
        i += 1
        continue

    # 空行
    if not stripped:
        flush_para(); close_list()
        i += 1
        continue

    # 复选框行（附录 C 清单）
    if stripped.startswith("- [ ]"):
        flush_para()
        if in_list != "ul":
            close_list()
            body.append('<ul class="ul">')
            in_list = "ul"
        body.append(f'<li class="li">{inline(stripped[5:].strip())}</li>')
        i += 1
        continue

    para.append(line)
    i += 1

flush_para()
close_list()

# ---------- 目录 ----------
# 目录只列一级章节：转换引擎对嵌套 ol 的域生成不稳定，扁平结构可确保 TOC 域落地
toc_items = [f'<li><a href="#{sid}">{esc(text)}</a></li>' for level, sid, text in toc if level == 1]
toc_html = "\n".join(toc_items)

# ---------- 设计令牌 ----------
tokens = json.loads(Path(
    "C:/Users/86191/.workbuddy/plugins/cache/workbuddy-builtin/workbuddy-docx/"
    "5.9.3-wb.42398134.gd4692421.h480e8d1fee0f/skills/design-token/tokens/compiled/business-report.json"
).read_text(encoding="utf-8"))

# 转换层硬约束：正文 eastAsia 字体取第一项，必须是宋体；标题黑体（原 token 为微软雅黑，此处覆盖并留痕）
tokens.setdefault("overrides", {})["typography.fontFamily.body"] = '"宋体", SimSun, serif'
tokens.setdefault("overrides", {})["typography.fontFamily.heading"] = '"黑体", SimHei, sans-serif'
OUT_TOKENS.write_text(json.dumps(tokens, ensure_ascii=False, indent=2), encoding="utf-8")

CSS = """
@page { @bottom-center { content: counter(page); } }
@page cover { @bottom-center { content: none; } }
section[role="cover"] { page: cover; }

:root {
  --ff-heading: "黑体", SimHei, sans-serif;
  --ff-body: "宋体", SimSun, serif;
  --ff-mono: "Consolas", "Courier New", monospace;
  --fs-h1: 17pt;
  --fs-h2: 13.5pt;
  --fs-h3: 11.5pt;
  --fs-body: 12pt;
  --fs-small: 10.5pt;
  --fs-tiny: 9.5pt;
  --fs-cover-title: 30pt;
  --fs-cover-sub: 13pt;
  --fs-cover-cat: 12pt;
  --fs-toc-title: 16pt;
  --fs-toc-1: 11pt;
  --fs-toc-2: 10.5pt;
  --fs-card: 17pt;
  --fs-summary: 14pt;
  --color-primary: #14507d;
  --color-primary-dark: #0f3c60;
  --color-text: #1a1a1a;
  --color-muted: #4a5a68;
  --color-meta: #40484f;
  --color-border: #c9d6e2;
  --color-border-strong: #b9cddd;
  --color-bg: #ffffff;
  --color-highlight: #f2f7fb;
  --color-table-head: #e8f0f7;
  --color-table-head-text: #0f3452;
  --color-code-bg: #f3f6f9;
  --color-code: #16324a;
  --color-code-inline: #a03a1f;
  --color-callout-bg: #fdf6e3;
  --color-callout-border: #e6d08a;
  --color-callout-text: #4a3a10;
  --color-card-label: #5a6a78;
  --spacing-0: 0;
  --spacing-1: 3pt;
  --spacing-2: 6pt;
  --spacing-3: 8pt;
  --spacing-4: 10pt;
  --spacing-5: 14pt;
  --spacing-6: 22pt;
  --spacing-7: 34pt;
  --spacing-indent-1: 2.2em;
  --spacing-indent-2: 1.4em;
}

* { box-sizing: border-box; }
body { margin: var(--spacing-0); padding: var(--spacing-0); font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-body, 12pt); line-height: 1.75; color: var(--color-text, #1a1a1a); }

.cover { padding: var(--page-cover-pad, 3.2cm 2.4cm); min-height: 70vh; border-top: 10pt solid var(--color-primary, #14507d); }
.cover-cat { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-cover-cat, 12pt); font-weight: bold; color: var(--color-primary, #14507d); letter-spacing: 0.22em; margin: 0 0 var(--spacing-5) 0; text-align: left; }
.cover-title { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-cover-title, 30pt); font-weight: bold; color: var(--color-primary-dark, #0f3c60); margin: 0 0 var(--spacing-4) 0; text-align: left; line-height: 1.3; }
.cover-sub { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-cover-sub, 13pt); color: var(--color-muted, #4a5a68); margin: 0 0 var(--spacing-7) 0; text-align: left; }
.cover-rule { height: 2pt; background: var(--color-primary, #14507d); width: 42%; margin: 0 0 var(--spacing-6) 0; text-align: left; }
.cover-meta { margin: var(--spacing-0); padding: var(--spacing-0); text-align: left; }
.cover-meta p { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-small, 10.5pt); color: var(--color-meta, #40484f); margin: var(--spacing-1) 0; text-align: left; }

.toc-sec { padding: var(--page-toc-pad, 1.6cm 2.4cm); }
.toc-title { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-toc-title, 16pt); font-weight: bold; color: var(--color-primary, #14507d); margin: 0 0 var(--spacing-5) 0; text-align: left; letter-spacing: 0.1em; }
.toc-list, .toc-list ol, .toc-list ul { list-style: none; list-style-type: none; padding-left: 0; margin: var(--spacing-0); }
.toc-list > li { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-toc-1, 11pt); font-weight: bold; color: var(--color-primary, #14507d); margin: var(--spacing-3) 0 var(--spacing-1) 0; text-indent: 0; }
.toc-list > li > ol { padding-left: var(--spacing-indent-2, 1.4em); margin: var(--spacing-1) 0 0 0; }
.toc-list > li > ol > li { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-toc-2, 10.5pt); font-weight: normal; color: var(--color-text, #1a1a1a); margin: var(--spacing-1) 0; text-indent: 0; }
.toc-list a { color: inherit; text-decoration: none; }

.report-body { padding: var(--spacing-2) 0 var(--spacing-5) 0; }
.h1 { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-h1, 17pt); font-weight: bold; color: var(--color-bg, #ffffff); background: var(--color-primary, #14507d); padding: var(--spacing-2) var(--spacing-4); margin: var(--spacing-6) 0 var(--spacing-4) 0; text-align: left; }
.h2 { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-h2, 13.5pt); font-weight: bold; color: var(--color-primary, #14507d); border-bottom: 1pt solid var(--color-border, #c9d6e2); padding-bottom: var(--spacing-1); margin: var(--spacing-5) 0 var(--spacing-2) 0; text-align: left; }
.h3 { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-h3, 11.5pt); font-weight: bold; color: var(--color-primary-dark, #0f3c60); margin: var(--spacing-4) 0 var(--spacing-2) 0; text-align: left; }
.body-p { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-body, 12pt); color: var(--color-text, #1a1a1a); text-indent: 2em; line-height: 1.75; margin: 0 0 var(--spacing-2) 0; text-align: justify; }
.ul, .ol { margin: var(--spacing-1) 0 var(--spacing-3) 0; padding-left: var(--spacing-indent-1, 2.2em); }
.li { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-body, 12pt); color: var(--color-text, #1a1a1a); line-height: 1.7; margin: var(--spacing-1) 0; text-indent: 0; text-align: left; }

.tbl { width: 100%; border-collapse: collapse; margin: var(--spacing-2) 0 var(--spacing-4) 0; }
.th { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-small, 10.5pt); font-weight: bold; color: var(--color-table-head-text, #0f3452); background: var(--color-table-head, #e8f0f7); border: 0.75pt solid var(--color-border-strong, #b9cddd); padding: var(--spacing-1) var(--spacing-2); text-align: left; }
.td { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-tiny, 9.5pt); color: var(--color-text, #1a1a1a); border: 0.75pt solid var(--color-border, #c9d6e2); padding: var(--spacing-1) var(--spacing-2); text-align: left; line-height: 1.55; }

.code { font-family: var(--ff-mono, "Consolas", "Courier New", monospace); font-size: var(--fs-tiny, 9.5pt); line-height: 1.5; color: var(--color-code, #16324a); background: var(--color-code-bg, #f3f6f9); border: 0.75pt solid var(--color-border, #c9d6e2); padding: var(--spacing-2) var(--spacing-3); margin: var(--spacing-2) 0 var(--spacing-4) 0; text-align: left; }
code { font-family: var(--ff-mono, "Consolas", "Courier New", monospace); font-size: var(--fs-tiny, 9.5pt); color: var(--color-code-inline, #a03a1f); }

.callout { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-small, 10.5pt); color: var(--color-callout-text, #4a3a10); background: var(--color-callout-bg, #fdf6e3); border: 0.75pt solid var(--color-callout-border, #e6d08a); padding: var(--spacing-2) var(--spacing-3); margin: var(--spacing-3) 0; text-align: left; text-indent: 0; }

.divider { height: 0.75pt; background: var(--color-border, #c9d6e2); margin: var(--spacing-4) 0; }

.summary { background: var(--color-highlight, #f2f7fb); border-top: 2pt solid var(--color-primary, #14507d); padding: var(--spacing-4) var(--spacing-5); margin: var(--spacing-1) 0 var(--spacing-5) 0; }
.summary-h { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-summary, 14pt); font-weight: bold; color: var(--color-primary, #14507d); margin: 0 0 var(--spacing-3) 0; text-align: left; }
.cards { width: 100%; border-collapse: separate; border-spacing: 6pt 0; margin: 0 0 var(--spacing-3) 0; }
.card-h { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-small, 10.5pt); font-weight: normal; color: var(--color-card-label, #5a6a78); background: var(--color-bg, #ffffff); border: 0.75pt solid var(--color-border, #c9d6e2); padding: var(--spacing-1) var(--spacing-2); text-align: center; }
.card { width: 25%; background: var(--color-bg, #ffffff); border: 0.75pt solid var(--color-border, #c9d6e2); padding: var(--spacing-2) var(--spacing-3); text-align: center; }
.card-v { font-family: var(--ff-heading, "黑体", SimHei, sans-serif); font-size: var(--fs-card, 17pt); font-weight: bold; color: var(--color-primary, #14507d); text-align: center; margin: var(--spacing-0); }
.card-l { font-family: var(--ff-body, "宋体", SimSun, serif); font-size: var(--fs-small, 10.5pt); color: var(--color-card-label, #5a6a78); text-align: center; margin: var(--spacing-1) 0 0 0; }
"""

HTML = f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="docx-page-size" content="A4">
<title>{esc(TITLE)}</title>
<style>{CSS}</style>
</head>
<body>

<section role="cover" class="cover">
  <p class="cover-cat" style="text-align: left;">技术报告 · 版本 v1.0</p>
  <p class="cover-title" style="text-align: left;">{esc(TITLE)}</p>
  <p class="cover-sub" style="text-align: left;">功能开发 · 缺陷修复 · 数据迁移 · 部署回滚</p>
  <div class="cover-rule" style="text-align: left;"></div>
  <div class="cover-meta" style="text-align: left;">
    <p>项目：Mir2 羽化登仙（仙体）系统　|　工程：D:\\mir2-20241027</p>
    <p>报告日期：2026 年 11 月 12 日　|　报告范围：2026-11-11 至 2026-11-12</p>
    <p>密级：内部　|　编制：客户端 / 服务端改造组</p>
  </div>
</section>

<section role="body" class="report-body" data-page-restart="1">

<div class="toc-sec">
  <p class="toc-title" style="text-align: left;">目　录</p>
  <ol class="toc-list">
{toc_html}
  </ol>
</div>

<div class="executive-summary summary">
  <p class="summary-h">执行摘要</p>
  <table class="cards">
    <thead>
      <tr>
        <th class="card-h">修复缺陷</th>
        <th class="card-h">羽化专属装备（件）</th>
        <th class="card-h">改动文件（客户端 + 服务端）</th>
        <th class="card-h">验证角色 / 羽化号</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td class="card"><p class="card-v">20</p></td>
        <td class="card"><p class="card-v">111</p></td>
        <td class="card"><p class="card-v">13</p></td>
        <td class="card"><p class="card-v">25 / 6</p></td>
      </tr>
    </tbody>
  </table>
  <p class="body-p">本轮改造把羽化登仙从"素材已导入、系统只接通一半"补成闭环：外观侧统一接管人物、发型、武器、坐骑、鱼竿与内观；规则侧禁止未羽化角色穿戴专属装备、退回前强制脱装；判定侧修正羽化武器 2000 段 Shape 与旧职业判定规则的冲突；数据侧把羽化状态从自定义 BUFF 槽位迁到专用类型，并补齐存档迁移与未知类型容错。改造后，全套流程在账号库副本上验证 25 个角色、6 个羽化号迁移前后属性逐行一致，构建与部署均已校验哈希。</p>
  <p class="body-p">核心结论：羽化外观的判定完全由装备 Shape 与 BUFF 类型驱动，两端口径必须严格一致；凡"竿偏了、衣服穿成别人的"这类问题，根因都是段位或档位选错，而不是缺少像素补偿。</p>
</div>

{chr(10).join(body)}

</section>
</body>
</html>
"""

OUT_HTML.write_text(HTML, encoding="utf-8")
print(json.dumps({"html": str(OUT_HTML), "h1": h1_n, "h2": h2_n, "toc": len(toc),
                  "blocks": len(body), "bytes": OUT_HTML.stat().st_size}, ensure_ascii=False))
