# -*- coding: utf-8 -*-
"""扫描 .Lib 内嵌帧段：列出每个图库的动作段，并标记『重复动作码』
（重复会让 MLibrary.Initialize() 里 _frames.Add() 抛 ArgumentException）。"""
import struct, os, sys
from collections import Counter

DIRS = [
    (r"E:\micromir2\Data\Monster", "Monster"),
    (r"E:\micromir2\Data", "Data"),
]


def parse(path):
    D = open(path, "rb").read()
    if len(D) < 12:
        return None, None
    ver, cnt = struct.unpack_from("<ii", D, 0)
    if ver < 3:
        return ver, None
    fs, = struct.unpack_from("<i", D, 8)
    if fs <= 0 or fs >= len(D):
        return ver, None
    fc, = struct.unpack_from("<i", D, fs)
    if fc <= 0 or fc > 2000:
        return ver, None
    segs = []
    p = fs + 4
    for i in range(fc):
        if p + 35 > len(D):
            break
        act = D[p]; p += 1
        st, ct, sk, iv, es, ec, esk, eiv = struct.unpack_from("<8i", D, p); p += 32
        rev, bl = D[p], D[p + 1]; p += 2
        segs.append((act, st, ct, sk, iv, rev, bl))
    return ver, segs


def report(path, label, verbose=False):
    ver, segs = parse(path)
    if segs is None:
        if verbose:
            print("%-34s ver=%s  无内嵌帧段" % (label, ver))
        return None
    acts = [s[0] for s in segs]
    dup = sorted(k for k, v in Counter(acts).items() if v > 1)
    flag = "  ★★★ 重复动作码=%s" % dup if dup else ""
    print("%-34s ver=%s  帧段数=%-3d%s" % (label, ver, len(segs), flag))
    if verbose:
        for a, st, ct, sk, iv, rev, bl in segs:
            print("      action=%-4d start=%-6d count=%-5d skip=%-6d interval=%-6d rev=%d blend=%d"
                  % (a, st, ct, sk, iv, rev, bl))
    return dup


print("=" * 90)
print("【召唤物四个图库 详细帧段】")
print("=" * 90)
for n, lab in ((36, "骷髅 BoneFamiliar"), (170, "神兽 Shinsu"), (171, "神兽1 Shinsu1"), (172, "月灵 HolyDeva")):
    p = os.path.join(DIRS[0][0], "%03d.Lib" % n)
    if os.path.exists(p):
        print("\n>>> %s  %03d.Lib" % (lab, n))
        report(p, "    ", verbose=True)
    else:
        print("\n>>> %s  %03d.Lib 【文件不存在】" % (lab, n))

print()
print("=" * 90)
print("【重复动作码扫描：Monster 目录】")
print("=" * 90)
bad = []
for f in sorted(os.listdir(DIRS[0][0])):
    if not f.lower().endswith(".lib"):
        continue
    dup = report(os.path.join(DIRS[0][0], f), f)
    if dup:
        bad.append((f, dup))
print("\n有重复动作码的 Monster 图库：", bad if bad else "无")

print()
print("=" * 90)
print("【重复动作码扫描：Data 目录（UI/魔法/特效等）】")
print("=" * 90)
bad2 = []
for f in sorted(os.listdir(DIRS[1][0])):
    if not f.lower().endswith(".lib"):
        continue
    p = os.path.join(DIRS[1][0], f)
    if not os.path.isfile(p) or os.path.getsize(p) < 1024:
        continue
    dup = report(p, f)
    if dup:
        bad2.append((f, dup))
print("\n有重复动作码的 Data 图库：", bad2 if bad2 else "无")
