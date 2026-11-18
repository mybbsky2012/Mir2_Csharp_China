# 摆臂（上下）预览：跑动 A=5 / 行走 A=2，8 方向 × 6 帧；上排不动、下排摆动对比
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT = os.path.join(BASE, "preview_new"); os.makedirs(OUT, exist_ok=True)
DIRNAME = ["上", "右上", "右", "右下", "下", "左下", "左", "左上"]
ROD_BEHIND = {0, 5, 6, 7}
RUN_DY  = [0, -1, -4, -5, -4, -1]
WALK_DY = [0,  0, -1, -2, -1,  0]

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
    def draw(self, i, canvas, ax, ay):
        if not self.ok(i): return
        m = self.meta[i]
        canvas.alpha_composite(self.img(i), (ax + m[2], ay + m[3]))

BODY = Lib(f"{BASE}/upc01/01")
ROD  = Lib(f"{BASE}/fishing/00M")

def cell(d, i, body0, swing, dy_tab, CW=170, CH=200, AX=86, AY=158):
    s = Image.new("RGBA", (CW, CH), (58, 58, 66, 255))
    body = body0 + 8 * d + i
    rod  = 256 + d * 8 + i
    oy = dy_tab[i] if swing else 0
    if d in ROD_BEHIND:
        ROD.draw(rod, s, AX, AY + oy); BODY.draw(body, s, AX, AY)
    else:
        BODY.draw(body, s, AX, AY); ROD.draw(rod, s, AX, AY + oy)
    return s

def sheet(body0, dy_tab, name):
    PAD, CW, CH = 6, 170, 200
    W = PAD + 6 * (CW + PAD); H = PAD + 20 + 8 * (2 * CH + 26)
    c = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dr = ImageDraw.Draw(c)
    dr.text((PAD, 3), f"{name}  上排=竿不动(改前)  下排=竿随摆臂上下(改后 {max(dy_tab)}~{min(dy_tab)}px)",
            fill=(255, 220, 120, 255))
    for d in range(8):
        y = PAD + 20 + d * (2 * CH + 26)
        dr.text((PAD, y), DIRNAME[d], fill=(150, 190, 245, 255))
        for i in range(6):
            c.alpha_composite(cell(d, i, body0, False, dy_tab), (PAD + i * (CW + PAD), y + 14))
            c.alpha_composite(cell(d, i, body0, True,  dy_tab), (PAD + i * (CW + PAD), y + 14 + CH + 6))
    c.resize((W * 2, H * 2), Image.NEAREST).save(os.path.join(OUT, f"{name}.png"))
    print("->", f"{name}.png")

def gif(body0, dy_tab, d, name, CW=170, CH=200):
    fr = [cell(d, i, body0, True, dy_tab, CW, CH).convert("RGB").resize((510, 600), Image.NEAREST) for i in range(6)]
    p = os.path.join(OUT, f"gif_{name}_{DIRNAME[d]}.gif")
    fr[0].save(p, save_all=True, append_images=fr[1:], duration=110, loop=0)
    print("->", os.path.basename(p))

sheet(128, RUN_DY,  "摆臂_跑动")
sheet(64,  WALK_DY, "摆臂_行走")
for d in (2, 6, 1, 7):
    gif(128, RUN_DY, d, "摆臂跑动")
for d in (2, 6):
    gif(64, WALK_DY, d, "摆臂行走")
