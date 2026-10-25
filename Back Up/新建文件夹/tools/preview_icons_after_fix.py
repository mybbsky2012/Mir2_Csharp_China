# -*- coding: utf-8 -*-
"""修复后全量核对：每个技能 = 官网图标(CDN) | 数据库Icon按客户端公式取出的帧。
应该左右一致。输出 tools/sheets/图标修复后全量核对.png
"""
import struct, gzip, json, os, re
from PIL import Image, ImageDraw, ImageFont

DATA = r"E:\micromir2\Data"
LIB = os.path.join(DATA, "MagIcon.Lib")
DB = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Server.MirDB"
ROOT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59"
CDN = os.path.join(ROOT, "tools", "mir2db", "icons")

spell_enum = json.load(open(os.path.join(ROOT, "tools/mir2db/spell_enum.json"), encoding="utf-8"))

# ---- 解码 MagIcon.Lib ----
D = open(LIB, "rb").read()
ver, cnt = struct.unpack_from("<ii", D, 0)
il = list(struct.unpack_from("<%di" % cnt, D, 12))
def frame(f):
    o = il[f]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, o)
    ln = struct.unpack_from("<i", D, o + 13)[0]
    px = gzip.decompress(D[o + 17:o + 17 + ln]); need = w * h * 4
    if len(px) < need: px += b"\x00" * (need - len(px))
    return Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")

# ---- 读数据库 Icon ----
DBB = bytearray(open(DB, "rb").read())
def find_occ(name):
    b = name.encode("utf-8"); n = len(b)
    pat = bytes([n]) + b if n < 128 else bytes([(n & 0x7F) | 0x80, (n >> 7) & 0x7F]) + b
    out, s = [], 0
    while True:
        i = DBB.find(pat, s)
        if i < 0: break
        out.append(i); s = i + 1
    return out
def skip_prefix(pos):
    n = 0; shift = 0; p = pos
    while True:
        if p >= len(DBB): return None
        c = DBB[p]; p += 1
        n |= (c & 0x7F) << shift
        if not (c & 0x80): break
        shift += 7
        if shift > 14: return None
    return p + n

src = open(r"D:\mir2-20241027\Server\MirEnvir\Envir.cs", encoding="utf-8").read()
env = {}
for m in re.finditer(r'MagicInfoList\.Add\(new MagicInfo\s*\{(.{0,900}?)\}\);', src, re.S):
    b = m.group(1)
    nm = re.search(r'Name\s*=\s*"([^"]+)"', b)
    sp = re.search(r'Spell\s*=\s*Spell\.(\w+)', b)
    l1 = re.search(r'Level1\s*=\s*(\d+)', b)
    if nm and sp:
        env[nm.group(1)] = dict(spell=sp.group(1), l1=int(l1.group(1)) if l1 else None)

rows = []
for lid, job in [(49, "战士"), (50, "法师"), (51, "道士"), (52, "刺客"), (53, "弓手")]:
    d = json.load(open(os.path.join(ROOT, f"tools/mir2db/sg_{lid}.json"), encoding="utf-8"))
    for r in d["data"]:
        cn = r["name_cn"]; oid = int(r["id"])
        # 在 env 里找同名
        info = env.get(cn)
        if not info: continue
        want = spell_enum.get(info["spell"])
        hits = [p for p in (skip_prefix(o) for o in find_occ(cn)) if p and p + 7 <= len(DBB) and DBB[p] == want]
        if not hits: continue
        icon = DBB[hits[0] + 3]
        rows.append((job, cn, oid, icon, icon * 2))

print("对照条数:", len(rows))
bad = [r for r in rows if r[3] != r[2] - 1]
print("仍不一致:", len(bad))
for r in bad: print("  ", r)

# ---- 绘制 ----
try: fnt = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 14)
except: fnt = ImageFont.load_default()
CW, CH, PER = 200, 72, 4
N = len(rows)
cols = PER
r_n = (N + cols - 1) // cols
W = CW * cols + 20; H = 46 + r_n * CH
c = Image.new("RGB", (W, H), (26, 28, 34)); dd = ImageDraw.Draw(c)
dd.text((12, 8), "技能图标修复后全量核对   左=官网图标(CDN)   右=数据库Icon按客户端公式(帧=Icon*2)取出的实际帧", fill=(255, 214, 120), font=fnt)
for k, (job, cn, oid, icon, fr) in enumerate(rows):
    x = 10 + (k % cols) * CW; y = 46 + (k // cols) * CH
    # CDN 图标
    p = os.path.join(CDN, f"{oid}.jpg")
    if os.path.exists(p):
        im = Image.open(p).convert("RGB").resize((48, 44))
        c.paste(im, (x + 2, y + 12))
    # 实际帧
    try:
        f = frame(fr).resize((48, 44), Image.NEAREST)
        bg = Image.new("RGBA", f.size, (70, 70, 80, 255)); bg.alpha_composite(f)
        c.paste(bg.convert("RGB"), (x + 56, y + 12))
    except Exception:
        pass
    dd.text((x + 2, y + 58 - 58), f"{cn}", fill=(200, 230, 255), font=fnt)
    dd.text((x + 108, y + 2), f"id{oid}", fill=(150, 190, 150), font=fnt)
c.save(os.path.join(ROOT, "tools", "sheets", "图标修复后全量核对.png"))
print("saved")
