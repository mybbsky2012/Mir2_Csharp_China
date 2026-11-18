# 弧形摆臂预览：与客户端表一致（8方向×6帧 dx/dy），跑动 R=7；行走 ×0.4
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT = os.path.join(BASE, "preview_new"); os.makedirs(OUT, exist_ok=True)
DIRNAME = ["上", "右上", "右", "右下", "下", "左下", "左", "左上"]
ROD_BEHIND = {0, 5, 6, 7}

DX = [[0,0,0,0,0,0], [-4,-3,3,4,3,-3], [-6,-4,4,6,4,-4], [-4,-3,3,4,3,-3],
      [0,0,0,0,0,0], [4,3,-3,-4,-3,3], [6,4,-4,-6,-4,4], [4,3,-3,-4,-3,3]]
DY = [[-4,-2,-2,-4,-2,-2], [-4,-1,-1,-4,-1,-1], [-4,-1,-1,-4,-1,-1], [-3,-1,-1,-3,-1,-1],
      [-2,-1,-1,-2,-1,-1], [-3,-1,-1,-3,-1,-1], [-4,-1,-1,-4,-1,-1], [-4,-1,-1,-4,-1,-1]]
WDX = [[v * 40 // 100 for v in row] for row in DX]
WDY = [[v * 40 // 100 for v in row] for row in DY]

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

def cell(d, i, body0, swing, DX_, DY_, CW=170, CH=200, AX=86, AY=158):
    s = Image.new("RGBA", (CW, CH), (58, 58, 66, 255))
    body = body0 + 8 * d + i
    rod  = 256 + d * 8 + i
    ox, oy = (DX_[d][i], DY_[d][i]) if swing else (0, 0)
    if d in ROD_BEHIND:
        ROD.draw(rod, s, AX + ox, AY + oy); BODY.draw(body, s, AX, AY)
    else:
        BODY.draw(body, s, AX, AY); ROD.draw(rod, s, AX + ox, AY + oy)
    return s

def sheet(body0, DX_, DY_, name):
    PAD, CW, CH = 6, 170, 200
    W = PAD + 6 * (CW + PAD); H = PAD + 20 + 8 * (2 * CH + 26)
    c = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dr = ImageDraw.Draw(c)
    dr.text((PAD, 3), f"{name}  上排=竿不动(改前)  下排=弧形摆臂(改后)", fill=(255, 220, 120, 255))
    for d in range(8):
        y = PAD + 20 + d * (2 * CH + 26)
        dr.text((PAD, y), DIRNAME[d], fill=(150, 190, 245, 255))
        for i in range(6):
            c.alpha_composite(cell(d, i, body0, False, DX_, DY_), (PAD + i * (CW + PAD), y + 14))
            c.alpha_composite(cell(d, i, body0, True,  DX_, DY_), (PAD + i * (CW + PAD), y + 14 + CH + 6))
    c.resize((W * 2, H * 2), Image.NEAREST).save(os.path.join(OUT, f"{name}.png"))
    print("->", f"{name}.png")

def gif(body0, DX_, DY_, d, name, CW=170, CH=200):
    fr = [cell(d, i, body0, True, DX_, DY_, CW, CH).convert("RGB").resize((510, 600), Image.NEAREST) for i in range(6)]
    p = os.path.join(OUT, f"gif_{name}_{DIRNAME[d]}.gif")
    fr[0].save(p, save_all=True, append_images=fr[1:], duration=110, loop=0)
    print("->", os.path.basename(p))

sheet(128, DX, DY, "弧形摆臂_跑动")
sheet(64, WDX, WDY, "弧形摆臂_行走")
for d in (2, 6, 1, 7, 0):
    gif(128, DX, DY, d, "弧形跑动")
for d in (2, 6):
    gif(64, WDX, WDY, d, "弧形行走")

# 轨迹示意：把 6 帧的竿位置(dx,dy)画成弧线点阵
tw = Image.new("RGBA", (420, 320), (24, 24, 28, 255)); dr = ImageDraw.Draw(tw)
dr.text((6, 4), "握点轨迹（右向跑动，屏幕上）", fill=(255, 220, 120, 255))
cx, cy = 210, 200; sc = 14
for i in range(6):
    x, y = cx + DX[2][i] * sc, cy + DY[2][i] * sc
    dr.ellipse((x-5, y-5, x+5, y+5), fill=(120, 220, 255, 255))
    dr.text((x + 8, y - 6), f"f{i}", fill=(230, 230, 230, 255))
for i in range(6):
    x1, y1 = cx + DX[2][i] * sc, cy + DY[2][i] * sc
    x2, y2 = cx + DX[2][(i+1) % 6] * sc, cy + DY[2][(i+1) % 6] * sc
    dr.line((x1, y1, x2, y2), fill=(90, 140, 180, 255), width=2)
tw.resize((840, 640), Image.NEAREST).save(os.path.join(OUT, "弧形轨迹_右向.png"))
print("-> 弧形轨迹_右向.png")
