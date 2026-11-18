# 校验「竿位补偿」是否把竿推离手：同一帧并排三版 —— 无偏移 / 当前补偿(+8,-8) / 反补偿
# 只读预览，不改游戏
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
        m = self.meta[i]; im = self.img(i)
        canvas.alpha_composite(im, (ax + m[2], ay + m[3]))

BODY = Lib(f"{BASE}/upc01/01")
ROD  = Lib(f"{BASE}/fishing/00M")

def rod_bump(d, mode):
    """mode: none=不补偿; cur=当前代码(+8/-8/+4/-4); inv=反向"""
    if mode == "none": return (0, 0)
    dx = 0 if d in (0, 4) else (8 if d <= 3 else -8)
    dy = -8
    if mode == "inv": return (-dx, -dy)
    return (dx, dy)

def cell(b0, bcount, bskip, rseg, rcount, one_shot, d, i, mode,
         CW=150, CH=170, AX=74, AY=132, bump=None):
    s = Image.new("RGBA", (CW, CH), (58, 58, 66, 255))
    bodyFrame = b0 + (bcount + bskip) * d + i
    rodFrameIn = min(i * rcount // max(bcount, 1), rcount - 1) if one_shot else i % rcount
    rIdx = rseg + d * 8 + rodFrameIn
    ox, oy = bump if bump is not None else rod_bump(d, mode)
    if d in ROD_BEHIND:
        ROD.draw(rIdx, s, AX + ox, AY + oy); BODY.draw(bodyFrame, s, AX, AY)
    else:
        BODY.draw(bodyFrame, s, AX, AY); ROD.draw(rIdx, s, AX + ox, AY + oy)
    return s

ACTS = [
    ("攻", 448, 6, 2, 320, 8, True),
    ("施", 448, 6, 2, 320, 8, True),
    ("跑", 128, 6, 2, 256, 6, False),
    ("走", 64,  6, 2, 256, 6, False),
]

# 1) 每个动作 × 8 方向：无偏移 vs 当前补偿，两行并排
def sheet(name, b0, bcount, bskip, rseg, rcount, one_shot, scale=2):
    PAD, CW, CH = 6, 150, 170
    W = PAD + bcount * (CW + PAD)
    H = PAD + 20 + 8 * (2 * CH + 24 + PAD)
    c = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dr = ImageDraw.Draw(c)
    dr.text((PAD, 3), f"{name}  body{b0}+rod{rseg}   上排=无偏移(原素材锚点)  下排=当前+8/-8", fill=(255, 220, 120, 255))
    for d in range(8):
        y = PAD + 20 + d * (2 * CH + 24 + PAD)
        dr.text((PAD, y), f"{DIRNAME[d]}", fill=(150, 190, 245, 255))
        for i in range(bcount):
            c.alpha_composite(cell(b0, bcount, bskip, rseg, rcount, one_shot, d, i, "none"), (PAD + i * (CW + PAD), y + 14))
            c.alpha_composite(cell(b0, bcount, bskip, rseg, rcount, one_shot, d, i, "cur"), (PAD + i * (CW + PAD), y + 14 + CH + 6))
    c.resize((W * scale, H * scale), Image.NEAREST).save(os.path.join(OUT, f"align_{name}.png"))
    print("->", f"align_{name}.png")

# 2) 关键方向放大：dir 1/2/3/0 单帧 4 版对比
def zoom(name, b0, bcount, bskip, rseg, rcount, one_shot, dirs=(0,1,2,3), frames=None, scale=3):
    frames = frames or list(range(bcount))
    PAD, CW, CH = 6, 170, 190
    W = PAD + len(frames) * (CW + PAD)
    H = PAD + 20 + len(dirs) * (4 * CH + 34 + PAD)
    c = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dr = ImageDraw.Draw(c)
    dr.text((PAD, 3), f"{name} 列=帧  行内1:无偏移 2:+8/-8 3:+8/-8再上抬8 4:+8/-8再下移8", fill=(255, 220, 120, 255))
    for di, d in enumerate(dirs):
        y = PAD + 20 + di * (4 * CH + 34 + PAD)
        for i, fi in enumerate(frames):
            variants = [(0,0), rod_bump(d,"cur"), (rod_bump(d,"cur")[0], -16), (rod_bump(d,"cur")[0], 0)]
            for vi,(ox,oy) in enumerate(variants):
                s = cell(b0, bcount, bskip, rseg, rcount, one_shot, d, fi, "none", CW, CH, 84, 150, bump=(ox,oy))
                c.alpha_composite(s, (PAD + i*(CW+PAD), y + vi*(CH+8)))
            dr.text((PAD + i*(CW+PAD), y), f"{DIRNAME[d]} f{fi}", fill=(150,190,245,255))
    c.resize((W*scale, H*scale), Image.NEAREST).save(os.path.join(OUT, f"zoom_{name}.png"))
    print("->", f"zoom_{name}.png")

for a in ACTS:
    sheet(*a)
zoom("攻击", 448, 6, 2, 320, 8, True, dirs=(2,1,3,0), frames=[0,2,4])
zoom("跑动", 128, 6, 2, 256, 6, False, dirs=(2,1), frames=[0,2,4])
