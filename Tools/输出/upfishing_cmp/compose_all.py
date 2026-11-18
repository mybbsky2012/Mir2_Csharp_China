# 羽化钓鱼五态合成：握杆静默 / 握杆行走 / 握杆攻击 / 抛竿 / 收杆（只读预览）
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT  = os.path.join(BASE, "out_final"); os.makedirs(OUT, exist_ok=True)
DIRNAME = ["上","右上","右","右下","下","左下","左","左上"]

class Lib:
    def __init__(self, path):
        rows = list(csv.reader(open(os.path.join(path,"_index.csv"), encoding="utf-8-sig")))[1:]
        self.meta = {int(r[0]): (int(r[1]),int(r[2]),int(r[3]),int(r[4])) for r in rows}
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
        m = self.meta[i]; im = self.img(i)
        x0, y0 = ax + m[2], ay + m[3]
        cx0, cy0 = max(0,x0), max(0,y0)
        cx1, cy1 = min(canvas.width, x0+im.width), min(canvas.height, y0+im.height)
        if cx1 <= cx0 or cy1 <= cy0: return
        canvas.alpha_composite(im.crop((cx0-x0,cy0-y0,cx1-x0,cy1-y0)), (cx0,cy0))

BODY = Lib(f"{BASE}/upc/00")       # 通用甲（男半）
ROD  = Lib(f"{BASE}/fishing/00M")  # 羽化竿

# 动作定义: 名称, 身体段起, 竿段起, 每方向帧数, 竿每方向帧数(循环)
ACTS = [
    ("握杆静默", 0,   256, 4, 6),
    ("握杆行走", 64,  64,  6, 6),
    ("握杆攻击", 256, 256, 6, 6),
    ("抛竿",     632, 0,   6, 8),
    ("收杆",     728, 128, 8, 8),
]

CW, CH, AX, AY = 150, 165, 72, 132
BG = (56,56,64,255)

def cell(bi, ri):
    s = Image.new("RGBA", (CW,CH), BG)
    BODY.draw(bi, s, AX, AY); ROD.draw(ri, s, AX, AY)
    return s

def sheet(name, b0, r0, nb, nr):
    PAD = 6
    W = PAD + 8*(CW+PAD); H = PAD + 16 + 8*(CH+PAD)
    c = Image.new("RGBA",(W,H),(30,30,36,255)); d = ImageDraw.Draw(c)
    d.text((PAD, 3), f"{name}  body{b0} + rod{r0}", fill=(255,220,120,255))
    for dn in range(8):
        y = PAD+16+dn*(CH+PAD)
        d.text((PAD, y-1), f"{dn}{DIRNAME[dn]}", fill=(160,200,255,255))
        for i in range(nb):
            c.alpha_composite(cell(b0+dn*8+i, r0+dn*8+(i % nr)), (PAD+i*(CW+PAD), y))
    p = os.path.join(OUT, f"{name}.png")
    c = c.resize((W*2, H*2), Image.NEAREST); c.save(p); print("->", p)

def gif(name, b0, r0, nb, nr, dn, path):
    frames = []
    for i in range(nb):
        cc = cell(b0+dn*8+i, r0+dn*8+(i % nr)).convert("RGB")
        frames.append(cc.resize((CW*3, CH*3), Image.NEAREST))
    p = os.path.join(OUT, path)
    frames[0].save(p, save_all=True, append_images=frames[1:], duration=130, loop=0)
    print("->", p)

for name,b0,r0,nb,nr in ACTS:
    sheet(name,b0,r0,nb,nr)
for name,b0,r0,nb,nr in ACTS:
    gif(name,b0,r0,nb,nr,2,f"gif_{name}_dir2.gif")

# 总览：每个动作取第 2 帧、8 方向横向排列
OW, OH, OAX, OAY = 120, 135, 58, 108
rowsImg = []
for name,b0,r0,nb,nr in ACTS:
    row = Image.new("RGBA", (8*OW, OH+14), (30,30,36,255))
    d = ImageDraw.Draw(row); d.text((3,1), name, fill=(255,220,120,255))
    for dn in range(8):
        s = Image.new("RGBA",(OW,OH),(48,48,56,255))
        BODY.draw(b0+dn*8+min(1,nb-1), s, OAX, OAY)
        ROD.draw(r0+dn*8+min(1,nr-1), s, OAX, OAY)
        row.alpha_composite(s,(dn*OW, 14))
    rowsImg.append(row)
W = 8*OW; H = sum(r.height for r in rowsImg)
ov = Image.new("RGBA",(W,H),(18,18,22,255)); y=0
for r in rowsImg: ov.alpha_composite(r,(0,y)); y+=r.height
ov = ov.resize((W*2,H*2), Image.NEAREST)
ov.save(os.path.join(OUT,"总览.png")); print("->", os.path.join(OUT,"总览.png"))
