# 跑动摆竿预览：静止版（改前） vs 位移场版（改后），复刻客户端新逻辑
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT = os.path.join(BASE, "preview_new"); os.makedirs(OUT, exist_ok=True)
DIRNAME = ["上", "右上", "右", "右下", "下", "左下", "左", "左上"]
ROD_BEHIND = {0, 5, 6, 7}

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

DX = [[0,0,0,0,0,0],[0,0,2,2,1,1],[0,1,4,4,4,3],[0,2,2,-1,1,3],
      [0,0,0,0,0,0],[0,-2,-2,1,-1,-3],[0,-1,-4,-4,-4,-3],[0,0,-2,-2,-1,-1]]
DY = [[0,-2,-3,0,-2,-3],[0,-2,-5,-4,-2,-1],[0,0,-1,-1,0,-1],[0,-1,-2,3,-3,-3],
      [0,0,-2,0,0,-2],[0,-1,-2,3,-3,-3],[0,0,-1,-1,0,-1],[0,-2,-5,-4,-2,-1]]

def cell(d, i, swing, CW=170, CH=195, AX=86, AY=152):
    s = Image.new("RGBA", (CW, CH), (58, 58, 66, 255))
    body = 128 + 8 * d + i
    rod  = 256 + d * 8 + i
    ox, oy = (DX[d][i], DY[d][i]) if swing else (0, 0)
    if d in ROD_BEHIND:
        ROD.draw(rod, s, AX + ox, AY + oy); BODY.draw(body, s, AX, AY)
    else:
        BODY.draw(body, s, AX, AY); ROD.draw(rod, s, AX + ox, AY + oy)
    return s

# 逐帧对比：上排静止 / 下排摆动，8 方向
PAD, CW, CH = 6, 170, 195
W = PAD + 6 * (CW + PAD); H = PAD + 20 + 8 * (2 * CH + 24)
c = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dr = ImageDraw.Draw(c)
dr.text((PAD, 3), "跑动持竿  上排=改前(竿静死)  下排=改后(竿随身体摆)", fill=(255, 220, 120, 255))
for d in range(8):
    y = PAD + 20 + d * (2 * CH + 24)
    dr.text((PAD, y), DIRNAME[d], fill=(150, 190, 245, 255))
    for i in range(6):
        c.alpha_composite(cell(d, i, False), (PAD + i * (CW + PAD), y + 14))
        c.alpha_composite(cell(d, i, True),  (PAD + i * (CW + PAD), y + 14 + CH + 6))
c.resize((W * 2, H * 2), Image.NEAREST).save(os.path.join(OUT, "跑动_摆竿对比.png"))
print("-> 跑动_摆竿对比.png")

# GIF
for d in (2, 6, 1):
    fr = [cell(d, i, True).convert("RGB").resize((510, 585), Image.NEAREST) for i in range(6)]
    p = os.path.join(OUT, f"gif_跑动摆竿_{DIRNAME[d]}.gif")
    fr[0].save(p, save_all=True, append_images=fr[1:], duration=110, loop=0)
    print("->", os.path.basename(p))
