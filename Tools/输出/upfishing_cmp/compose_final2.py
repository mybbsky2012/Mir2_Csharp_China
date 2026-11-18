# 修正配对后的五态合成（身体段已按 Δ=600 实测配准）
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT  = os.path.join(BASE, "out_final2"); os.makedirs(OUT, exist_ok=True)
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

BODY = Lib(f"{BASE}/upc/00"); ROD = Lib(f"{BASE}/fishing/00M")
CW, CH, AX, AY = 150, 165, 72, 132

def cell(bi, ri):
    s = Image.new("RGBA", (CW,CH), (56,56,64,255))
    BODY.draw(bi, s, AX, AY); ROD.draw(ri, s, AX, AY)
    return s

def sheet(name, b0, r0, nb, nr, ndir=8):
    PAD = 6
    W = PAD + nb*(CW+PAD); H = PAD + 16 + ndir*(CH+PAD)
    c = Image.new("RGBA",(W,H),(30,30,36,255)); d = ImageDraw.Draw(c)
    d.text((PAD, 3), f"{name}  body{b0} + rod{r0}", fill=(255,220,120,255))
    for dn in range(ndir):
        y = PAD+16+dn*(CH+PAD)
        d.text((PAD, y-1), f"{dn}{DIRNAME[dn]}", fill=(160,200,255,255))
        for i in range(nb):
            c.alpha_composite(cell(b0+dn*8+i, r0+dn*8+(i % nr)), (PAD+i*(CW+PAD), y))
    p = os.path.join(OUT, f"{name}.png")
    c.resize((W*2,H*2), Image.NEAREST).save(p); print("->", p)

def gif(name, b0, r0, nb, nr, dn):
    frames = [cell(b0+dn*8+i, r0+dn*8+(i % nr)).convert("RGB").resize((CW*3,CH*3), Image.NEAREST) for i in range(nb)]
    p = os.path.join(OUT, f"gif_{name}_dir{dn}.gif")
    frames[0].save(p, save_all=True, append_images=frames[1:], duration=130, loop=0)
    print("->", p)

ACTS = [
    ("1握杆静默", 0,   256, 4, 6, 8),   # 站立+静持竿
    ("2握杆等待", 664, 64,  6, 6, 4),   # 钓鱼待机（素材只有前4方向）
    ("3握杆行走", 64,  256, 6, 6, 8),   # 行走+静持竿
    ("4握杆攻击", 256, 256, 6, 6, 8),   # 攻击+静持竿
    ("5抛竿",     600, 0,   8, 8, 8),   # 修正：身体600
    ("6收杆",     728, 128, 8, 8, 8),   # 收线
]
for name,b0,r0,nb,nr,nd in ACTS:
    sheet(name,b0,r0,nb,nr,nd)
for name,b0,r0,nb,nr,nd in ACTS:
    gif(name,b0,r0,nb,nr,2)

# 总览：每动作取第2帧，8方向
OW,OH,OAX,OAY = 120,135,58,108
rowsImg=[]
for name,b0,r0,nb,nr,nd in ACTS:
    row = Image.new("RGBA",(8*OW,OH+14),(30,30,36,255))
    d=ImageDraw.Draw(row); d.text((3,1),name,fill=(255,220,120,255))
    for dn in range(8):
        s=Image.new("RGBA",(OW,OH),(48,48,56,255))
        BODY.draw(b0+dn*8+min(1,nb-1), s, OAX, OAY)
        ROD.draw(r0+dn*8+min(1,nr-1), s, OAX, OAY)
        row.alpha_composite(s,(dn*OW,14))
    rowsImg.append(row)
W=8*OW; H=sum(r.height for r in rowsImg)
ov=Image.new("RGBA",(W,H),(18,18,22,255)); y=0
for r in rowsImg: ov.alpha_composite(r,(0,y)); y+=r.height
ov.resize((W*2,H*2),Image.NEAREST).save(os.path.join(OUT,"总览.png")); print("-> 总览.png")
