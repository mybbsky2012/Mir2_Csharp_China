# 跑动 6 帧「上半身质心 / 头顶 / 脚底」相对锚点位移：用于给竿叠加摆臂位移（比找"最远像素"稳）
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

def stats(frame):
    if not BODY.ok(frame): return None
    im = BODY.img(frame); w, h = im.size; px = im.load()
    fx, fy = BODY.meta[frame][2], BODY.meta[frame][3]
    tot = 0; sx = 0; sy = 0; top = 10**9; bot = -10**9
    up_tot = 0; up_sx = 0; up_sy = 0
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 40:
                gx, gy = x + fx, y + fy          # 锚点坐标系
                tot += 1; sx += gx; sy += gy
                top = min(top, gy); bot = max(bot, gy)
                if gy < 0:                        # 上半身（锚点以上）
                    up_tot += 1; up_sx += gx; up_sy += gy
    return dict(cx=sx/tot, cy=sy/tot, top=top, bot=bot,
                ucx=up_sx/max(up_tot,1), ucy=up_sy/max(up_tot,1), n=tot)

RUN = (128, 6, 2)
for d in range(8):
    base = None; out = []
    for i in range(6):
        s = stats(RUN[0] + (RUN[1]+RUN[2])*d + i)
        if s is None: out.append(None); continue
        if base is None: base = s
        out.append(s)
    print(f"--- dir{d}")
    for i, s in enumerate(out):
        if s is None: print(f"  f{i} 空"); continue
        print(f"  f{i} 上半质心 相对f0 dx={s['ucx']-base['ucx']:+6.1f} dy={s['ucy']-base['ucy']:+6.1f}"
              f" | 整体 dx={s['cx']-base['cx']:+5.1f} dy={s['cy']-base['cy']:+5.1f}"
              f" | top={s['top']-base['top']:+4.0f} bot={s['bot']-base['bot']:+4.0f}")
