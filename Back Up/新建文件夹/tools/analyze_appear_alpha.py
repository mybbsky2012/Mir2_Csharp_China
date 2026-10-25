# -*- coding: utf-8 -*-
"""分析召唤出场帧的 alpha / 颜色分布，判断该用普通 alpha 还是加法混合。
重点：神兽 170.Lib 帧0-79（红碎片汇聚）—— 用户反馈『太透明/颜色淡』。"""
import struct, gzip, os
from collections import Counter
from PIL import Image

DATA = r"E:\micromir2\Data\Monster"

def load(path):
    D = open(path, "rb").read()
    ver, count = struct.unpack_from("<ii", D, 0)
    # ver>=3 时 8 字节处是 frameSeek，图像索引从 12 开始
    base = 12
    il = list(struct.unpack_from("<%di" % count, D, base))
    return D, il, count

def frame(D, off):
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln])
    need = w * h * 4
    if len(px) < need:
        px = px + b"\x00" * (need - len(px))
    im = Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")
    return im

def analyze(libname, start, end, label):
    path = os.path.join(DATA, libname)
    D, il, count = load(path)
    print(f"\n===== {label}   {libname}  帧{start}-{end}  (总图数={count}) =====")
    total_px = total_op = 0
    alpha_hist = Counter()
    pure_black = 0
    color_sum = [0, 0, 0]
    bright = 0
    for i in range(max(0, start), min(end, count - 1) + 1):
        try:
            im = frame(D, il[i]).convert("RGBA")
        except Exception as e:
            continue
        px = im.load()
        for yy in range(im.height):
            for xx in range(im.width):
                r, g, b, a = px[xx, yy]
                total_px += 1
                alpha_hist[a] += 1
                if a > 0:
                    total_op += 1
                    color_sum[0] += r; color_sum[1] += g; color_sum[2] += b
                    if r == 0 and g == 0 and b == 0:
                        pure_black += 1
                    if r + g + b > 380:
                        bright += 1
    if total_op == 0:
        print("  无非透明像素")
        return
    lv = sorted(alpha_hist.keys())
    print(f"  alpha 取值级别: {lv[:20]}{' ...' if len(lv)>20 else ''}   (共{len(lv)}级)")
    hard = sum(v for k, v in alpha_hist.items() if k in (0, 255))
    print(f"  硬边占比(alpha∈{{0,255}}) = {hard*100.0/total_px:.2f}%")
    print(f"  不透明像素: {total_op}  (占全图 {total_op*100.0/total_px:.1f}%)")
    print(f"    ├ 纯黑(0,0,0): {pure_black}  = 不透明像素的 {pure_black*100.0/total_op:.1f}%")
    print(f"    └ 高亮(sum>380): {bright} = 不透明像素的 {bright*100.0/total_op:.1f}%")
    ar = color_sum[0]/total_op; ag = color_sum[1]/total_op; ab = color_sum[2]/total_op
    print(f"  不透明像素平均色: R={ar:.0f} G={ag:.0f} B={ab:.0f}")

analyze("170.Lib", 0, 79,   "神兽 出场特效 0-79")
analyze("170.Lib", 80, 89,  "神兽 正常站立 参照(80-89)")
analyze("036.Lib", 224, 233,"骷髅 出场特效 224-233")
analyze("036.Lib", 0, 9,    "骷髅 正常站立 参照(0-9)")
analyze("172.Lib", 224, 233,"月灵 出场特效 224-233")
analyze("172.Lib", 0, 9,    "月灵 正常站立 参照(0-9)")
