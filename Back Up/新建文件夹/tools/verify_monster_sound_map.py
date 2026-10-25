# -*- coding: utf-8 -*-
"""用音频指纹交叉验证：.lst 给某个怪物的偏移指向的文件，是否真的就是那个动作的声音。

思路：
  资源包里同时存在两套命名
    A 系列（3位）：{图像索引:03d}-{偏移}.wav   ← 文件名自带「图像+偏移」，语义明确
    B 系列（4位/描述名）：由 SoundList.lst 映射到索引
  若两套是同一批声音，A 文件应与 B 文件内容一致（或明显同源）。
  把 A 的每个偏移文件与 B 的每个文件做粗略波形比对，就能看出 .lst 有没有把偏移对错。
"""
import os, wave, struct, collections, math

D = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
LST = r"E:\mirromir2\Sound\SoundList.lst"
LST = r"E:\micromir2\Sound\SoundList.lst"


def fingerprint(path, buckets=40):
    """返回 (时长秒, 归一化 RMS 包络)"""
    try:
        with wave.open(path, 'rb') as w:
            n, rate, sw, frames = w.getnchannels(), w.getframerate(), w.getsampwidth(), w.getnframes()
            if n == 0 or sw not in (1, 2) or rate == 0:
                return None
            take = min(frames, int(rate * 10))          # 只取前 10 秒，够分辨音色
            raw = w.readframes(take)
    except Exception:
        return None
    fmt = {1: 'b', 2: 'h'}[sw]
    step = n * sw
    vals = []
    for i in range(0, len(raw) - step + 1, step):
        vals.append(struct.unpack_from('<' + fmt, raw, i)[0])
    if len(vals) < buckets * 2:
        return None
    seg = max(1, len(vals) // buckets)
    env = []
    for b in range(buckets):
        chunk = vals[b * seg:(b + 1) * seg]
        if not chunk:
            env.append(0.0); continue
        env.append(math.sqrt(sum(float(x) * x for x in chunk) / len(chunk)))
    mx = max(env) or 1.0
    return (frames / float(rate), [round(e / mx, 3) for e in env])


def dist(a, b):
    return sum(abs(x - y) for x, y in zip(a[1], b[1])) / len(a[1])


lst = {}
for ln in open(LST, encoding='utf-8', errors='replace').read().splitlines():
    p = ln.replace(' ', '').replace('\t', ':').split(':')
    if len(p) <= 1: continue
    try: i = int(p[0])
    except ValueError: continue
    if i not in lst: lst[i] = p[-1]

files = set(os.listdir(D))
TARGETS = [(20, '骷髅 Skeleton'), (25, '多钩猫 HookingCat'), (27, '稻草人 Scarecrow'),
           (10, '食人花 CannibalPlant'), (30, '沃玛战士 WoomaSoldier'), (100, '半兽人')]

for img, name in TARGETS:
    print("=" * 78)
    print("【%s】图像索引 %d  →  索引区 %d..%d" % (name, img, img * 10, img * 10 + 9))
    print("  .lst 已写条目:")
    mapped = {}
    for o in range(10):
        k = img * 10 + o
        f = lst.get(k)
        if f is None:
            print("     +%d 索引 %4d -> （表里没这条，客户端会兜底猜 %03d-%d.wav）" % (o, k, img, o))
            continue
        if not f:
            print("     +%d 索引 %4d -> （空，等于没声音）" % (o, k)); continue
        ok = '存在' if f in files else '★文件不存在'
        mapped[o] = f
        print("     +%d 索引 %4d -> %-26s %s" % (o, k, f, ok))
    # A 系列（3位命名）实际都有哪些偏移
    a_series = {}
    for o in range(10):
        for cand in ("%03d-%d.wav" % (img, o), "%03d-%d.WAV" % (img, o)):
            if cand in files:
                a_series[o] = cand
    print("  资源包 A 系列（%03d-*.wav）实际偏移: %s" % (img, sorted(a_series)))
    if not a_series or not mapped:
        continue
    # 指纹比对
    fpA = {o: fingerprint(os.path.join(D, f)) for o, f in a_series.items()}
    fpB = {o: fingerprint(os.path.join(D, f)) for o, f in mapped.items()}
    print("  最相似的配对（A偏移 -> 最接近的B文件）:")
    for o, fa in sorted(fpA.items()):
        if not fa: continue
        best = sorted(((dist(fa, fb), o2, fb) for o2, fb in fpB.items() if fb))[:2]
        s = ' , '.join("B+%d(%s) 差异%.3f" % (o2, mapped[o2], d) for d, o2, _ in best)
        print("     A+%d(%s) 时长%.2fs  →  %s" % (o, a_series[o], fa[0], s))
    for o, fb in sorted(fpB.items()):
        if fb:
            print("     B+%d(%s) 时长%.2fs" % (o, mapped[o], fb[0]))
