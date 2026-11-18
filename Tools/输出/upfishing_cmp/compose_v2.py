# 修正配对后的合成：UpC 身体 × UpFishing 竿
# 身体布局（UpC 男半 0~791 普通区）：632 抛竿(8槽)、696 等待(仅 dir0~3, 6帧)、728 收线(8方向×8帧)、792 骑乘
# 竿布局（UpFishing 6 段×8方向×8槽）：0 抛竿(8帧)、64 等待(6帧)、128 收线(8帧)、256 静持(6帧)
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT  = os.path.join(BASE, "out2")
os.makedirs(OUT, exist_ok=True)
DIRNAME = ["上","右上","右","右下","下","左下","左","左上"]

class Lib:
    def __init__(self, path):
        rows = list(csv.reader(open(os.path.join(path,"_index.csv"), encoding="utf-8-sig")))[1:]
        self.meta = {int(r[0]): (int(r[1]),int(r[2]),int(r[3]),int(r[4])) for r in rows}
        self.dir = path; self.cache = {}
    def draw(self, idx, canvas, ax, ay):
        m = self.meta.get(idx)
        if m is None: return
        p = os.path.join(self.dir, f"{idx}.png")
        if not os.path.exists(p): return
        im = self.cache.setdefault(idx, Image.open(p).convert("RGBA"))
        if m[0] <= 4 and m[1] <= 4: return   # 4x4 空白槽跳过
        x0, y0 = ax + m[2], ay + m[3]
        cx0, cy0 = max(0,x0), max(0,y0)
        cx1, cy1 = min(canvas.width, x0+im.width), min(canvas.height, y0+im.height)
        if cx1<=cx0 or cy1<=cy0: return
        canvas.alpha_composite(im.crop((cx0-x0,cy0-y0,cx1-x0,cy1-y0)), (cx0,cy0))

BODY = Lib(f"{BASE}/upc/00")
ROD  = Lib(f"{BASE}/fishing/00M")

def frame_img(bodyIdx, rodIdx, CW=150, CH=150, AX=75, AY=112):
    sub = Image.new("RGBA", (CW,CH), (58,58,66,255))
    BODY.draw(bodyIdx, sub, AX, AY)
    ROD.draw(rodIdx, sub, AX, AY)
    return sub

def sheet(rows_, out, ncol, scale=2):
    PAD, CW, CH = 6, 150, 150
    W = PAD + ncol*(CW+PAD); H = PAD + 18 + len(rows_)*(CH+PAD)
    s = Image.new("RGBA", (W,H), (34,34,40,255)); d = ImageDraw.Draw(s)
    for ri, (label, cells) in enumerate(rows_):
        d.text((PAD, PAD + ri*(CH+PAD)), label, fill=(255,220,120,255))
        for ci, cell in enumerate(cells):
            s.alpha_composite(cell, (PAD + ci*(CW+PAD), PAD + 18 + ri*(CH+PAD)))
    s = s.resize((W*scale, H*scale), Image.NEAREST)
    s.save(out); print("->", out)

def gif(segs, out):
    frames = []
    for cells in segs:
        for c in cells:
            frames.append(c.convert("RGB").resize((300,300), Image.NEAREST))
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=140, loop=0)
    print("->", out)

# 1) 抛竿：body 632+d*8+i / rod 0+d*8+i
rows_ = []
g = []
for dnum in range(8):
    cells = [frame_img(632+dnum*8+i, 0+dnum*8+i) for i in range(8)]
    rows_.append((f"{dnum} {DIRNAME[dnum]}", cells)); g.append(cells)
sheet(rows_, f"{OUT}/抛竿_修正.png", 8)
gif(g, f"{OUT}/抛竿_修正.gif")

# 2) 等待：body 696+d*8+i / rod 64+d*8+i（身体素材只有 dir0~3）
rows_ = []; g = []
for dnum in range(4):
    cells = [frame_img(696+dnum*8+i, 64+dnum*8+i) for i in range(6)]
    rows_.append((f"{dnum} {DIRNAME[dnum]}", cells)); g.append(cells)
sheet(rows_, f"{OUT}/等待_修正.png", 6)
gif(g, f"{OUT}/等待_修正.gif")

# 3) 收线：body 728+d*8+i / rod 128+d*8+i
rows_ = []; g = []
for dnum in range(8):
    cells = [frame_img(728+dnum*8+i, 128+dnum*8+i) for i in range(8)]
    rows_.append((f"{dnum} {DIRNAME[dnum]}", cells)); g.append(cells)
sheet(rows_, f"{OUT}/收线_修正.png", 8)
gif(g, f"{OUT}/收线_修正.gif")

# 4) 静持：body 等待段(696) / rod 256+d*8+i（dir0~3）
rows_ = []; g = []
for dnum in range(4):
    cells = [frame_img(696+dnum*8+i%6, 256+dnum*8+i) for i in range(6)]
    rows_.append((f"{dnum} {DIRNAME[dnum]}", cells)); g.append(cells)
sheet(rows_, f"{OUT}/静持_修正.png", 6)
gif(g, f"{OUT}/静持_修正.gif")
