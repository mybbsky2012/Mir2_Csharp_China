# 严格复刻 11-12 改动后的客户端绘制逻辑，出「鱼竿五态」效果图（只读预览，不改游戏）
# 复刻要点：
#   body  = UpCArmour 通用库（男半 0~1111），ArmourOffSet=0
#   frame = Frame.Start + (Count+Skip)*dir + FrameIndex   （OffSet = Count+Skip）
#   rod   = rodSeg + dir*8 + rodFrameIn
#           oneShot: min(FrameIndex*rodCount/Frame.Count, rodCount-1) ；loop: FrameIndex % rodCount
#   层次  = dir ∈ {Up(0), DownLeft(5), Left(6), UpLeft(7)} → 竿画在身体之后（下层）
#           其余方向 → 竿画在身体之上
#   等待 d4~7：身体/头发改画站立段帧（通用库等待段无后 4 方向素材）
import os, csv
from PIL import Image, ImageDraw

BASE = "D:/mir2-20241027/Tools/输出/upfishing_cmp"
OUT = os.path.join(BASE, "preview_new"); os.makedirs(OUT, exist_ok=True)
DIRNAME = ["上", "右上", "右", "右下", "下", "左下", "左", "左上"]
ROD_BEHIND = {0, 5, 6, 7}   # 原版方向层次：朝上/左系时远端手在身后

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
        x0, y0 = ax + m[2], ay + m[3]
        canvas.alpha_composite(im, (x0, y0))

BODY = Lib(f"{BASE}/upc01/01")        # 通用甲 01 档（战士1档 映射 1 → 同款）
ROD  = Lib(f"{BASE}/fishing/00M")  # 羽化竿 男 00M

# (名称, 身体Start, 身体Count, 身体Skip, 竿段, 竿帧数, 是否一次性, 备注)
ACTS = [
    ("1握杆静默", 0,   4, 4, 256, 6, False),
    ("2握杆等待", 664, 6, 2, 64,  6, False),
    ("3握杆行走", 64,  6, 2, 256, 6, False),
    ("4握杆攻击", 448, 6, 2, 320, 8, True),
    ("5抛竿",     600, 8, 0, 0,   8, True),
    ("6收杆",     728, 8, 0, 128, 8, True),
]

def rod_in(idx, count, one_shot, frame_count):
    if one_shot:
        return min(idx * count // max(frame_count, 1), count - 1)
    return idx % count

def cell(name, b0, bcount, bskip, rseg, rcount, one_shot, d, i, CW=150, CH=170, AX=74, AY=132):
    s = Image.new("RGBA", (CW, CH), (58, 58, 66, 255))
    off = bcount + bskip
    bodyFrame = b0 + off * d + i
    # 钓鱼等待后 4 方向：身体改画站立段（与 DrawBody 特判一致）
    if name == "2握杆等待" and d >= 4:
        bodyFrame = 8 * d + (i % 4)
    rIdx = rseg + d * 8 + rod_in(i, rcount, one_shot, bcount)
    if d in ROD_BEHIND:
        ROD.draw(rIdx, s, AX, AY); BODY.draw(bodyFrame, s, AX, AY)
    else:
        BODY.draw(bodyFrame, s, AX, AY); ROD.draw(rIdx, s, AX, AY)
    return s

def sheet(name, b0, bcount, bskip, rseg, rcount, one_shot, scale=2):
    PAD, CW, CH = 6, 150, 170
    W = PAD + bcount * (CW + PAD); H = PAD + 18 + 8 * (CH + PAD)
    c = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dr = ImageDraw.Draw(c)
    dr.text((PAD, 3), f"{name}   body{b0}({bcount}帧) + rod{rseg}   {'一次性' if one_shot else '循环'}",
            fill=(255, 220, 120, 255))
    for d in range(8):
        y = PAD + 18 + d * (CH + PAD)
        tag = f"{DIRNAME[d]}" + (" 竿在下层" if d in ROD_BEHIND else "")
        dr.text((PAD, y - 1), tag, fill=(150, 190, 245, 255))
        for i in range(bcount):
            c.alpha_composite(cell(name, b0, bcount, bskip, rseg, rcount, one_shot, d, i), (PAD + i * (CW + PAD), y))
    c.resize((W * scale, H * scale), Image.NEAREST).save(os.path.join(OUT, f"{name}.png"))
    print("->", f"{name}.png")

def gif(name, b0, bcount, bskip, rseg, rcount, one_shot, d):
    frames = [cell(name, b0, bcount, bskip, rseg, rcount, one_shot, d, i).convert("RGB").resize((450, 510), Image.NEAREST)
              for i in range(bcount)]
    p = os.path.join(OUT, f"gif_{name}_{DIRNAME[d]}.gif")
    frames[0].save(p, save_all=True, append_images=frames[1:], duration=130, loop=0)
    print("->", os.path.basename(p))

for a in ACTS:
    sheet(*a)
for a in ACTS:
    gif(*a, 2)   # 朝右

# 总览：每个动作取第 1 帧（或第 2 帧），8 方向
OW, OH, OAX, OAY = 118, 132, 58, 106
rowsImg = []
for name, b0, bcount, bskip, rseg, rcount, one_shot in ACTS:
    row = Image.new("RGBA", (8 * OW, OH + 16), (28, 28, 34, 255))
    dr = ImageDraw.Draw(row); dr.text((3, 2), name, fill=(255, 220, 120, 255))
    pick = 1 if bcount > 1 else 0
    for d in range(8):
        s = cell(name, b0, bcount, bskip, rseg, rcount, one_shot, d, min(pick, bcount - 1), OW, OH, OAX, OAY)
        row.alpha_composite(s, (d * OW, 16))
    rowsImg.append(row)
W = 8 * OW; H = sum(r.height for r in rowsImg)
ov = Image.new("RGBA", (W, H), (16, 16, 20, 255)); y = 0
for r in rowsImg:
    ov.alpha_composite(r, (0, y)); y += r.height
ov.resize((W * 2, H * 2), Image.NEAREST).save(os.path.join(OUT, "总览.png"))
print("-> 总览.png")

# 攻击动作 改前/改后 对比（旧逻辑：竿段256静止 / 新逻辑：竿段320挥砍） 朝右
CW, CH, AX, AY = 170, 190, 84, 150
cmpImg = Image.new("RGBA", (6 * CW, 2 * (CH + 20)), (24, 24, 28, 255))
dr = ImageDraw.Draw(cmpImg)
dr.text((4, 3), "改前：攻击配竿段256（静持，竿不动）", fill=(255, 160, 140, 255))
dr.text((4, CH + 23), "改后：攻击配竿段320（挥砍，竿随臂抡）", fill=(150, 235, 170, 255))
for i in range(6):
    old = cell("4握杆攻击", 448, 6, 2, 256, 6, False, 2, i, CW, CH, AX, AY)
    new = cell("4握杆攻击", 448, 6, 2, 320, 8, True, 2, i, CW, CH, AX, AY)
    cmpImg.alpha_composite(old, (i * CW, 20)); cmpImg.alpha_composite(new, (i * CW, CH + 40))
cmpImg.resize((cmpImg.width * 2, cmpImg.height * 2), Image.NEAREST).save(os.path.join(OUT, "攻击_改前改后.png"))
print("-> 攻击_改前改后.png")
