# -*- coding: utf-8 -*-
"""解码兜底命名 0NN-o.wav（三位数）到底是谁的声音：
把它和索引表用的 NNNN-o.wav（四位数）按内容(md5)配对，
反推「图档 0NN」的兜底文件真正属于哪个图档。
"""
import os, re, hashlib, collections

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
files = [f for f in os.listdir(D) if f.lower().endswith('.wav')]
h2n = collections.defaultdict(list)
for f in files:
    p = os.path.join(D, f)
    if os.path.getsize(p) < 100:
        continue
    h2n[hashlib.md5(open(p, 'rb').read()).hexdigest()].append(f)

p3 = re.compile(r'^(\d{3})-(\d)\.wav$', re.I)
p4 = re.compile(r'^(\d{4})-(\d)(?:\.wav|.*)$', re.I)

print("三位数兜底文件 -> 同内容的四位数文件（B系列）-> 反推归属图档")
print("-" * 78)
rows = []
for f in sorted(files):
    m = p3.match(f)
    if not m:
        continue
    img, off = int(m.group(1)), int(m.group(2))
    grp = h2n[hashlib.md5(open(os.path.join(D, f), 'rb').read()).hexdigest()]
    mates = [x for x in grp if x != f]
    b4 = []
    for mt in mates:
        mm = re.match(r'^(\d{4})-\d', mt)
        if mm:
            b4.append((int(mm.group(1)) - 200) // 10)
    rows.append((img, off, sorted(set(b4)), mates))

for img, off, b4, mates in rows[:40]:
    print("  %03d-%d.wav  兜底给图档%3d  -> 实为图档 %s   (同内容: %s)" %
          (img, off, img, b4 or '?', ', '.join(mates[:4]) or '-'))
print("... 共 %d 个三位数文件" % len(rows))
print()
same = sum(1 for img, off, b4, m in rows if img in b4)
diff = sum(1 for img, off, b4, m in rows if b4 and img not in b4)
print("总数 %d | 兜底文件确实属于该图档(正确): %d | 属于别的图档(张冠李戴): %d | 无对照: %d"
      % (len(rows), same, diff, len(rows) - same - diff))
