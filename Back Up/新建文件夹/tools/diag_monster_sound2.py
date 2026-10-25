# -*- coding: utf-8 -*-
"""排查：怪物叫声为什么不对（v2，解析规则完全复刻客户端）。

链路：
  服务端 MonsterImage -> 客户端 BaseImage -> BaseSound = BaseImage * 10
  -> SoundManager 用 SoundList.lst 查「索引 -> 文件名」-> 播 Sound/<文件名>
     查不到就用默认命名：<=20000 -> {索引/10:000}-{索引%10}.wav
"""
import re, os, collections

ENUMS = r"D:\mir2-20241027\Shared\Enums.cs"
LST = r"E:\micromir2\Sound\SoundList.lst"
SOUND_DIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
CSV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Exports\3_怪物数据.csv"


def parse_like_client(path):
    """完全按 Client/MirSounds/SoundList.cs 的解析写。"""
    table = {}
    for ln in open(path, encoding='utf-8', errors='replace').read().splitlines():
        parts = ln.replace(' ', '').replace('\t', ':').split(':')
        if len(parts) <= 1:
            continue
        try:
            i = int(parts[0])
        except ValueError:
            continue
        if i not in table:
            table[i] = parts[-1]
    return table


def default_name(index):
    """客户端在索引表里查不到时的兜底命名。"""
    if index > 20000:
        return "%d-%d.wav" % ((index - 20000) // 10, index % 10)
    return "%03d-%d.wav" % (index // 10, index % 10)


lst = parse_like_client(LST)
files = set(os.listdir(SOUND_DIR))
print("索引表条目 %d；音效文件 %d" % (len(lst), len(files)))

src = open(ENUMS, encoding='utf-8-sig').read()
body = re.search(r'public enum Monster : ushort\s*\{(.*?)\n\s*\}', src, re.S).group(1)
name2idx, cur = {}, -1
for line in body.splitlines():
    line = line.split('//')[0].strip().rstrip(',')
    if not line:
        continue
    if '=' in line:
        n, v = line.split('=', 1); cur = int(v.strip()); name2idx[n.strip()] = cur
    else:
        cur += 1; name2idx[line.strip()] = cur
idx2name = {v: k for k, v in name2idx.items()}

rows = open(CSV, encoding='utf-8-sig').read().splitlines()
hdr = rows[0].split(',')
ni, imi = hdr.index('MonsterName'), hdr.index('MonsterImage')
monsters = []
for r in rows[1:]:
    c = r.split(',')
    if len(c) > imi:
        monsters.append((c[ni], c[imi]))

OFFSET = {1: '受击1', 2: '受击2', 3: '受击3', 6: '攻击', 7: '攻击2', 8: '死亡', 9: '被击'}

# 服务端怪物 -> 图像索引
used = {}
for name, img in monsters:
    if img in name2idx:
        used.setdefault(name2idx[img], 0)
        used[name2idx[img]] += 1
print("服务端怪物 %d 种，涉及图像索引 %d 个（%d ~ %d）"
      % (len(monsters), len(used), min(used), max(used)))

stat = collections.Counter()
bucket = collections.defaultdict(lambda: [0, 0, 0])
detail = {}
for img in sorted(used):
    ok, bad, fallback_ok = [], [], []
    for o, label in OFFSET.items():
        k = img * 10 + o
        f = lst.get(k)
        if f and f in files:
            ok.append((o, f))
        elif f:
            bad.append((o, f))
        else:
            guess = default_name(k)
            (fallback_ok if guess in files else bad).append((o, '兜底:' + guess))
    n = len(ok)
    if n >= 3: stat['齐全'] += 1
    elif n: stat['部分'] += 1
    else: stat['全无'] += 1
    detail[img] = (ok, bad)
    b = (img // 50) * 50
    bucket[b][0] += 1
    if n >= 3: bucket[b][1] += 1
    elif n == 0: bucket[b][2] += 1

print()
print("=== 逐图像统计 ===")
for k in ['齐全', '部分', '全无']:
    print("  %s: %d 个图像" % (k, stat[k]))
print()
print("图像索引区间 -> (怪物数, 有声音, 完全没声音)")
for b in sorted(bucket):
    t, ok, none = bucket[b]
    print("  %4d-%4d : %3d, 有 %3d, 无 %3d" % (b, b + 49, t, ok, none))

print()
print("=== 举例：几个常见怪物的实际发声 ===")
names = ['鸡', '鹿', '稻草人', '骷髅', '多钩猫', '羊', '半兽人', '祖玛教主', '赤月恶魔']
for name, img in monsters:
    if name in names and img in name2idx:
        i = name2idx[img]
        if i in detail:
            ok, bad = detail[i]
            print("  %-8s 图像 %4d : 可用 %s | 有问题 %s"
                  % (name, i, [(OFFSET[o], f) for o, f in ok][:4], [(OFFSET[o], f) for o, f in bad][:3]))

print()
print("=== 表里指向的文件不存在（表与资源包对不上）===")
bad_entries = [(k, v) for k, v in sorted(lst.items()) if v and v not in files]
print("共 %d 条" % len(bad_entries))
for k, v in bad_entries[:15]:
    print("  索引 %5d -> %s" % (k, v))
