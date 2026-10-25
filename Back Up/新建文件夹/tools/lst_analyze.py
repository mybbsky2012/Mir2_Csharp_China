# -*- coding: utf-8 -*-
"""解析 SoundList.lst，还原索引表结构与规则"""
import os, collections, io

P = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\SoundList.lst"
SND = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\ClientResources\Sound"

raw = open(P, 'rb').read()
for enc in ('utf-8', 'gbk', 'utf-16'):
    try:
        text = raw.decode(enc)
        print("编码:", enc)
        break
    except Exception:
        continue

lines = text.splitlines()
print("总行数:", len(lines))

entries = []     # (lineno, index, filename)
comments = []
skipped = []
for i, ln in enumerate(lines, 1):
    s = ln.strip()
    if not s:
        continue
    if s.startswith(';'):
        comments.append((i, s))
        continue
    parts = ln.replace(" ", "").split(':', 1)
    if len(parts) < 2:
        parts = ln.replace(" ", "").split('\t', 1)
    if len(parts) < 2 or not parts[0].isdigit():
        skipped.append((i, ln))
        continue
    entries.append((i, int(parts[0]), parts[1].strip()))

print("有效条目:", len(entries), "注释行:", len(comments), "未解析行:", len(skipped))
for x in skipped[:10]:
    print("  跳过:", x)

idx = {e[1]: e[2] for e in entries}
print("唯一索引数:", len(idx), "重复索引:", len(entries) - len(idx))

# 索引区间分布
rng = collections.Counter()
for _, i, _f in entries:
    if i < 10: rng['0-9'] += 1
    elif i < 200: rng['10-199'] += 1
    elif i < 1000: rng['200-999'] += 1
    elif i < 10000: rng['1000-9999'] += 1
    elif i < 20000: rng['10000-19999'] += 1
    else: rng['20000+'] += 1
print("索引区间分布:", dict(rng))

print("\n=== 索引 >= 20000 的条目（技能/魔法段）===")
sp = [e for e in entries if e[1] >= 20000]
print("数量:", len(sp))
for e in sp[:80]:
    print("  %6d -> %s" % (e[1], e[2]))

print("\n=== 未定义索引时的默认命名验证 ===")
def default_name(i):
    if i > 20000:
        return "M%d-%d" % ((i - 20000) // 10, i % 10)
    return "%03d-%d" % (i // 10, i % 10)

# 检查 lst 中文件名是否与默认命名一致
same = diff = 0
diff_samples = []
for _, i, f in entries:
    base = os.path.splitext(os.path.basename(f))[0]
    if base.lower() == default_name(i).lower():
        same += 1
    else:
        diff += 1
        if len(diff_samples) < 12:
            diff_samples.append((i, f, default_name(i)))
print("与默认命名一致:", same, " 不一致(自定义映射):", diff)
for d in diff_samples:
    print("   idx %-6d 文件 %-22s 默认应为 %s" % d)

# 文件是否存在
print("\n=== 声音文件实际存在性抽查 ===")
files = set(os.listdir(SND)) if os.path.isdir(SND) else set()
print("Sound 目录文件数:", len(files))
missing = miss_sample = 0
misses = []
for _, i, f in entries:
    name = os.path.basename(f)
    if name and name not in files:
        missing += 1
        if len(misses) < 15:
            misses.append((i, name))
print("lst 指向但不存在的文件数:", missing)
for m in misses:
    print("   缺失 idx %d -> %s" % m)

# 反查：目录里存在但未被 lst 引用的音频
used = set(os.path.basename(f) for _, _, f in entries if f)
unused = sorted(x for x in files if x.lower().endswith(('.wav', '.mp3')) and x not in used)
print("\n目录中未被 lst 引用的音频数:", len(unused))
print("样例:", unused[:25])
