# -*- coding: utf-8 -*-
"""用【客户端读图库的逻辑】验证数据库 Icon 值:
客户端: SkillButton.Index = Magic.Icon * 2; Library = MagIcon2  (底栏)
        Cells[i-1].Index = magic.Icon * 2; Library = MagIcon (技能面板)
故: 拿 DB 里的 Icon 算出帧号 Icon*2, 取出该帧, 与正确帧 (官网id-1)*2 对比。
"""
import struct, gzip, json, re
from PIL import Image, ImageDraw, ImageFont

LIB = r"E:\micromir2\Data\MagIcon2.Lib"      # 底栏图库 36x34
DB  = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Server.MirDB"
OUT = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\sheets\图标验证_DB值vs正确值.png"

D_LIB = open(LIB, "rb").read()
_, LCNT = struct.unpack_from("<ii", D_LIB, 0)
LIL = list(struct.unpack_from("<%di" % LCNT, D_LIB, 12))

def libframe(idx):
    if idx < 0 or idx >= LCNT: return None
    D, off = D_LIB, LIL[idx]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    px = gzip.decompress(D[off + 17: off + 17 + ln]); need = w * h * 4
    if len(px) < need: px += b"\x00" * (need - len(px))
    return Image.frombytes("RGBA", (w, h), px[:need], "raw", "BGRA")

D = open(DB, "rb").read()
spell_enum = json.load(open("tools/mir2db/spell_enum.json", encoding="utf-8"))
cdn = {}
for lid in [49,50,51,52,53]:
    d = json.load(open(f"tools/mir2db/sg_{lid}.json", encoding="utf-8"))
    for r in d["data"]:
        try: cdn[r["name_cn"]] = int(r["id"])
        except: pass
ALIAS = {"嗜血术":"噬血术", "万金闪":"万斤闪"}

src = open(r"D:\mir2-20241027\Server\MirEnvir\Envir.cs", encoding="utf-8").read()
env = {}
for m in re.finditer(r'MagicInfoList\.Add\(new MagicInfo\s*\{(.{0,900}?)\}\);', src, re.S):
    b = m.group(1)
    nm = re.search(r'Name\s*=\s*"([^"]+)"', b)
    sp = re.search(r'Spell\s*=\s*Spell\.(\w+)', b)
    l1 = re.search(r'Level1\s*=\s*(\d+)', b)
    if nm and sp:
        env[nm.group(1)] = dict(spell=sp.group(1), l1=int(l1.group(1)) if l1 else None)

def find_occ(name):
    b = name.encode(); n = len(b)
    pat = bytes([n]) + b if n < 128 else bytes([(n & 0x7F) | 0x80, (n >> 7) & 0x7F]) + b
    out, s = [], 0
    while True:
        i = D.find(pat, s)
        if i < 0: break
        out.append(i); s = i + 1
    return out

def skip_prefix(pos):
    n = 0; shift = 0; p = pos
    while True:
        if p >= len(D): return None
        c = D[p]; p += 1; n |= (c & 0x7F) << shift
        if not (c & 0x80): break
        shift += 7
        if shift > 14: return None
    return p + n

rows = []
for name, info in env.items():
    want = spell_enum.get(info["spell"])
    if want is None: continue
    oid = cdn.get(name) or cdn.get(ALIAS.get(name, ""))
    if oid is None: continue
    for o in find_occ(name):
        p = skip_prefix(o)
        if p is None or p + 7 > len(D) or D[p] != want: continue
        if info["l1"] is not None and D[p+4] != info["l1"]: continue
        db_icon = D[p+3]
        rows.append((name, db_icon, oid, info["spell"]))
        break

# 排序：错的在前
rows.sort(key=lambda r: (r[1] == r[2] - 1, r[2]))
wrong = [r for r in rows if r[1] != r[2] - 1]

S = 3
CW = 36 * S + 12
CH = 34 * S + 30
try: fnt = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 15)
except: fnt = ImageFont.load_default()

show = wrong[:30] if len(wrong) > 30 else wrong
cols = 5
c = Image.new("RGB", (CW * cols + 20, 56 + CH * cols), (24, 26, 32))
d = ImageDraw.Draw(c)
d.text((12, 8), "验证：客户端按数据库 Icon 值画出来的图标 (帧=Icon×2)", fill=(255, 190, 120), font=fnt)
d.text((12, 30), f"库里 {len(rows)} 条技能，其中 {len(wrong)} 条画出来是错的（90%）", fill=(240, 120, 120), font=fnt)

for k, (name, db_icon, oid, sp) in enumerate(show):
    x = 10 + (k % cols) * CW
    y = 56 + (k // cols) * CH
    d.text((x, y), f"{name}", fill=(200, 230, 160), font=fnt)
    d.text((x, y + 16), f"DB={db_icon}→帧{db_icon*2}  应={oid-1}→帧{(oid-1)*2}", fill=(210, 210, 210), font=fnt)
    got = libframe(db_icon * 2)
    wantf = libframe((oid - 1) * 2)
    def paste(im, ox):
        if im is None: return
        bg = Image.new("RGBA", im.size, (70, 70, 80, 255)); bg.alpha_composite(im)
        bg = bg.resize((36 * S, 34 * S), Image.NEAREST)
        c.paste(bg, (x + ox, y + 28), bg)
    paste(got, 0)
    paste(wantf, 36 * S + 4)
c.save(OUT)
print(f"共 {len(rows)} 条, 错误 {len(wrong)} 条 -> {OUT} (显示 {len(show)} 条)")
