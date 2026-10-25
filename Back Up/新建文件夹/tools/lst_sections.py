# -*- coding: utf-8 -*-
"""提取 SoundList.lst 章节结构：章节名 -> 图档号 -> 索引块"""
import os, collections

P = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\SoundList.lst"
text = open(P, 'rb').read().decode('utf-8', errors='replace')
lines = text.splitlines()

sections = []   # (lineno, title, [(index, file), ...])
cur = None
for i, ln in enumerate(lines, 1):
    s = ln.strip()
    if s.startswith(';;'):
        cur = (i, s.lstrip(';').strip(), [])
        sections.append(cur)
        continue
    if not s:
        continue
    body = ln.replace(" ", "")
    parts = body.split(':', 1)
    if len(parts) == 2 and parts[0].isdigit() and cur is not None:
        cur[2].append((int(parts[0]), parts[1].strip()))

print("总章节数:", len(sections))
print()
print("=" * 100)
for lineno, title, items in sections:
    if not items:
        continue
    idxs = [x[0] for x in items]
    imgs = sorted(set(i // 10 for i in idxs))
    print("[L%-5d] %-46s 条目%3d  索引 %d~%d  图档号 %s"
          % (lineno, title, len(items), min(idxs), max(idxs), imgs[:6]))
print("=" * 100)

# 只看 MON / Spell 类章节
print()
print("### MON 章节（怪物）：中文名 -> 图档号")
mon = [s for s in sections if s[1].upper().startswith('MON') and s[2]]
print("MON 章节数:", len(mon))
for lineno, title, items in mon:
    idxs = [x[0] for x in items]
    imgs = sorted(set(i // 10 for i in idxs))
    files = [x[1] for x in items]
    print("%-40s 图档号=%-18s 索引=%s 文件=%s"
          % (title.replace('MON ->', '').strip(), imgs, idxs, files[:3]))

print()
print("### Spell 章节（技能/地图音效）")
sp = [s for s in sections if s[1].upper().startswith('SPELL') and s[2]]
for lineno, title, items in sp:
    print("%-40s %s" % (title, items))
