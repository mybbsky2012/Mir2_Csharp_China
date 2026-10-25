# -*- coding: utf-8 -*-
"""排查：怪物叫声为什么不对。

链路：
  服务端怪物数据 MonsterImage -> 客户端 BaseImage -> BaseSound = Image*10
  -> 客户端按 SoundList.lst 查「索引 -> 文件名」-> 播放 Sound/<文件>

本脚本对比「服务端实际在用的怪物」与「音效索引表覆盖范围」，找出对不上的。
"""
import re, os, collections

ENUMS = r"D:\mir2-20241027\Shared\Enums.cs"
LST = r"E:\micromir2\Sound\SoundList.lst"
SOUND_DIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
CSV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Exports\3_怪物数据.csv"

# ---- 1. 解析 Monster 枚举（名称 -> 图像索引）----
src = open(ENUMS, encoding='utf-8-sig').read()
m = re.search(r'public enum Monster : ushort\s*\{(.*?)\n\s*\}', src, re.S)
body = m.group(1)
name2idx = {}
cur = -1
for line in body.splitlines():
    line = line.split('//')[0].strip().rstrip(',')
    if not line:
        continue
    if '=' in line:
        n, v = line.split('=', 1)
        cur = int(v.strip())
        name2idx[n.strip()] = cur
    else:
        cur += 1
        name2idx[line.strip()] = cur
idx2name = {v: k for k, v in name2idx.items()}
print("Monster 枚举条目: %d，索引范围 %d - %d" % (len(name2idx), min(name2idx.values()), max(name2idx.values())))

# ---- 2. 解析音效索引表 ----
lst = {}
for ln in open(LST, encoding='utf-8', errors='replace').read().splitlines():
    parts = ln.replace(' ', '').split(':')
    if len(parts) < 2: continue
    try: i = int(parts[0])
    except: continue
    lst[i] = parts[-1]
print("音效索引表条目: %d" % len(lst))

files = set(os.listdir(SOUND_DIR))
print("音效资源文件: %d" % len(files))

# ---- 3. 服务端怪物库里实际用到的图像 ----
rows = open(CSV, encoding='utf-8-sig').read().splitlines()
hdr = rows[0].split(',')
ni, imi, ai = hdr.index('MonsterName'), hdr.index('MonsterImage'), hdr.index('MonsterAI')
used = collections.OrderedDict()
for r in rows[1:]:
    c = r.split(',')
    if len(c) <= imi: continue
    used[c[ni]] = (c[imi], c[ai])

img_used = collections.Counter()
unknown_name = []
for name, (img, ai) in used.items():
    if img in name2idx:
        img_used[name2idx[img]] += 1
    else:
        unknown_name.append(img)

print("服务端怪物 %d 种；用到的图像索引 %d 个（%d - %d）"
      % (len(used), len(img_used), min(img_used), max(img_used)))
if unknown_name:
    print("枚举里查不到的图像名: %s" % set(unknown_name))

# ---- 4. 逐个怪物核对声音 ----
OFFSETS = [0, 1, 2, 3, 6, 7, 8, 9]
covered, partial, missing = [], [], []
for img in sorted(img_used):
    hits, miss = [], []
    for o in OFFSETS:
        k = img * 10 + o
        f = lst.get(k)
        if f and f in files:
            hits.append(o)
        else:
            miss.append((o, f if f is not None else '<表里没这条>'))
    if len(hits) >= 3: covered.append(img)
    elif hits: partial.append((img, hits, miss))
    else: missing.append((img, miss))

print()
print("=== 结果 ===")
print("声音齐全(>=3 个偏移有文件)的图像: %d 个" % len(covered))
print("部分可用的图像: %d 个" % len(partial))
print("完全没声音的图像: %d 个" % len(missing))

# 按图像索引分档看覆盖率
buckets = collections.defaultdict(lambda: [0, 0, 0])
for img in img_used:
    hits = sum(1 for o in OFFSETS if (lst.get(img * 10 + o) or '') in files)
    b = (img // 50) * 50
    buckets[b][0] += 1
    if hits >= 3: buckets[b][1] += 1
    elif hits == 0: buckets[b][2] += 1
print()
print("图像索引区间 -> (怪物数, 声音齐全, 完全没声音)")
for b in sorted(buckets):
    t, ok, none = buckets[b]
    print("  %4d-%4d : %3d 种, 齐全 %3d, 全无 %3d" % (b, b + 49, t, ok, none))

print()
print("--- 举几个例子（图像索引 -> 各偏移解析到的文件）---")
sample = [img for img in sorted(img_used)]
for img in sample[:6] + sample[6:12]:
    info = []
    for o in OFFSETS:
        k = img * 10 + o
        f = lst.get(k)
        mark = '√' if (f and f in files) else ('×' if f else '空')
        info.append("+%d:%s%s" % (o, f or '-', mark))
    print("  图像 %4d %-16s %s" % (img, idx2name.get(img, '?'), ' '.join(info)))


# ---- 5. 表里有怪物条目但文件不存在（表与资源对不上）----
bad = [(k, v) for k, v in sorted(lst.items()) if k < 2000 and v and v not in files]
print()
print("索引 <2000（怪物区）里「表有、文件没有」的条目: %d 个" % len(bad))
for k, v in bad[:20]:
    print("  索引 %4d -> %s（文件不存在）" % (k, v))
blank = [k for k, v in lst.items() if k < 2000 and not v]
print("怪区里「文件名为空」的索引: %d 个 %s" % (len(blank), blank[:20]))
