# 量「跑动」6 帧里手部的实际位移：用于让鱼竿跟着手摆（不用猜像素）
import os, csv
from PIL import Image

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"

class Lib:
    def __init__(self, path):
        rows = list(csv.reader(open(os.path.join(path, "_index.csv"), encoding="utf-8-sig")))[1:]
        self.meta = {int(r[0]): (int(r[1]), int(r[2]), int(r[3]), int(r[4])) for r in rows}
        self.dir = path; self.cache = {}
    def img(self, i):
        if i not in self.cache:
            p = os.path.join(self.dir, f"{i}.png")
            self.cache[i] = Image.open(p).convert("RGBA") if os.path.exists(p) else None
        return self.cache[i]
    def ok(self, i):
        m = self.meta.get(i)
        return not (m is None or (m[0] <= 4 and m[1] <= 4) or self.img(i) is None)

BODY = Lib(f"{BASE}/upc01/01")

HAND_FRAC = 0.55   # 只在上半身找手

def hand_pos(frame, d):
    """返回该帧手臂末端像素簇质心（图内坐标）。朝右系取最右簇，朝左系取最左簇。"""
    if not BODY.ok(frame): return None
    im = BODY.img(frame); w, h = im.size
    px = im.load()
    y_lo, y_hi = 0, int(h * HAND_FRAC)   # 上半身＝图像上部（锚点在图像外下方）
    xs = []
    for y in range(y_lo, y_hi):
        for x in range(w):
            if px[x, y][3] > 40: xs.append((x, y))
    if not xs: return None
    # 朝右方向：取最右 3 列像素的质心；朝左：最左 3 列
    cand = sorted(xs, key=lambda p: -p[0]) if d in (0, 1, 2, 3, 4) else sorted(xs, key=lambda p: p[0])
    edge = cand[0][0]
    pick = [p for p in cand if abs(p[0] - edge) <= 2]
    # 只取离边缘最近那一簇（y 连续段）：用 y 中位数附近的点
    ys = sorted(p[1] for p in pick)
    med = ys[len(ys) // 2]
    cluster = [p for p in pick if abs(p[1] - med) <= 4]
    cx = sum(p[0] for p in cluster) / len(cluster)
    cy = sum(p[1] for p in cluster) / len(cluster)
    return (cx, cy)

RUN_START, RUN_CNT, RUN_SKIP = 128, 6, 2

for d in range(8):

    print(f"--- dir{d}")
    base = None
    for i in range(RUN_CNT):
        f = RUN_START + (RUN_CNT + RUN_SKIP) * d + i
        p = hand_pos(f, d)
        if p is None:
            print(f"  f{i} 无帧"); continue
        if base is None: base = p
        print(f"  f{i} 手=({p[0]:.1f},{p[1]:.1f})  相对f0 dx={p[0]-base[0]:+.1f} dy={p[1]-base[1]:+.1f}")
