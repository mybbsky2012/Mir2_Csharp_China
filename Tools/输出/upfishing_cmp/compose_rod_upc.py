# 羽化钓竿(UpFishing) + 羽化通用甲(UpCArmour) 合成预览（只读，不改游戏代码）
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT  = os.path.join(BASE, "out")
os.makedirs(OUT, exist_ok=True)

DIRNAME = ["上","右上","右","右下","下","左下","左","左上"]

class Lib:
    def __init__(self, path):
        rows = list(csv.reader(open(os.path.join(path, "_index.csv"), encoding="utf-8-sig")))[1:]
        self.meta = {int(r[0]): (int(r[1]),int(r[2]),int(r[3]),int(r[4])) for r in rows}
        self.dir = path
        self.cache = {}
    def img(self, idx):
        if idx not in self.cache:
            p = os.path.join(self.dir, f"{idx}.png")
            self.cache[idx] = Image.open(p).convert("RGBA") if os.path.exists(p) else None
        return self.cache[idx]
    def draw(self, idx, canvas, ax, ay):
        im = self.img(idx)
        if im is None: return
        _,_,fx,fy = self.meta.get(idx, (0,0,0,0))
        x0, y0 = ax + fx, ay + fy
        cx0, cy0 = max(0,x0), max(0,y0)
        cx1, cy1 = min(canvas.width, x0+im.width), min(canvas.height, y0+im.height)
        if cx1<=cx0 or cy1<=cy0: return
        canvas.alpha_composite(im.crop((cx0-x0,cy0-y0,cx1-x0,cy1-y0)), (cx0,cy0))

BODY = Lib(f"{BASE}/upc/00")        # 通用甲库（男 0~1111）
ROD  = Lib(f"{BASE}/fishing/00M")   # 羽化竿 00M
BG   = (58,58,66,255)

def frame_img(bodyIdx, rodIdx, CW=150, CH=150, AX=75, AY=112):
    sub = Image.new("RGBA", (CW,CH), BG)
    BODY.draw(bodyIdx, sub, AX, AY)
    ROD.draw(rodIdx, sub, AX, AY)
    return sub

# 段定义：名称, 身体起始, 竿起始, 帧数
SEGS = [("抛竿", 632, 0, 8), ("等待", 696, 64, 6), ("收线", 744, 112, 8), ("静持", 696, 256, 6)]

PAD, CW, CH = 6, 150, 150
for name, b0, r0, n in SEGS:
    W = PAD + n*(CW+PAD); H = PAD + 18 + 8*(CH+PAD)
    sheet = Image.new("RGBA", (W,H), (34,34,40,255))
    d = ImageDraw.Draw(sheet)
    for dnum in range(8):
        d.text((PAD, PAD + dnum*(CH+PAD)), f"{dnum} {DIRNAME[dnum]}", fill=(255,220,120,255))
        for i in range(n):
            cell = frame_img(b0 + dnum*8 + i, r0 + dnum*8 + i)
            sheet.alpha_composite(cell, (PAD + i*(CW+PAD), PAD + 18 + dnum*(CH+PAD)))
    sheet = sheet.resize((W*2, H*2), Image.NEAREST)
    p = os.path.join(OUT, f"sheet_{name}.png")
    sheet.save(p); print("->", p)

# GIF：每个段 8 方向轮播（每方向完整循环一遍）
for name, b0, r0, n in SEGS:
    frames = []
    for dnum in range(8):
        for i in range(n):
            im = frame_img(b0 + dnum*8 + i, r0 + dnum*8 + i, CW=170, CH=170, AX=85, AY=126)
            frames.append(im.convert("RGB").resize((170*2, 170*2), Image.NEAREST))
    p = os.path.join(OUT, f"anim_{name}.gif")
    frames[0].save(p, save_all=True, append_images=frames[1:], duration=140, loop=0, optimize=False)
    print("->", p)
