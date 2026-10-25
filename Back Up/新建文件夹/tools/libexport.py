# -*- coding: utf-8 -*-
"""从 Mir2 .Lib 导出指定帧，并拼成对照图（棋盘底，便于看透明区域）。"""
import struct, gzip, io, sys
from PIL import Image, ImageDraw

LIB = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\ClientResources\Data\Title.Lib"
D = open(LIB, "rb").read()
VER, COUNT = struct.unpack_from("<ii", D, 0)
IL = list(struct.unpack_from("<%di" % COUNT, D, 12))


def frame(idx):
    if idx < 0 or idx >= COUNT:
        return None, None
    off = IL[idx]
    w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", D, off)
    ln = struct.unpack_from("<i", D, off + 13)[0]
    raw = D[off + 17: off + 17 + ln]
    if w <= 0 or h <= 0:
        return Image.new("RGBA", (max(w, 1), max(h, 1)), (0, 0, 0, 0)), (w, h, x, y, ln)
    try:
        px = gzip.decompress(raw)
    except Exception as e:
        return Image.new("RGBA", (w, h), (255, 0, 0, 255)), (w, h, x, y, ln)
    if len(px) < w * h * 4:
        px = px + b"\x00" * (w * h * 4 - len(px))
    im = Image.frombytes("RGBA", (w, h), px[:w * h * 4], "raw", "BGRA")
    return im, (w, h, x, y, ln)


def checker(w, h, s=8):
    im = Image.new("RGB", (w, h), (255, 255, 255))
    dr = ImageDraw.Draw(im)
    for yy in range(0, h, s):
        for xx in range(0, w, s):
            if ((xx // s) + (yy // s)) % 2:
                dr.rectangle([xx, yy, xx + s - 1, yy + s - 1], fill=(205, 205, 205))
    return im


if __name__ == "__main__":
    probes = [int(x) for x in sys.argv[1:]]
    tiles = []
    print("库文件: %s" % LIB)
    print("帧总数 = %d，合法 0 基索引 0~%d" % (COUNT, COUNT - 1))
    print()
    for i in probes:
        im, meta = frame(i)
        if im is None:
            print("[%d] 第%d张 —— 越界，不存在" % (i, i + 1))
            tiles.append((i, None))
            continue
        print("[%d] 第%d张  尺寸=%dx%d  偏移=(%d,%d)  数据长=%d  最小/最大色=%s"
              % (i, i + 1, meta[0], meta[1], meta[2], meta[3], meta[4],
                 (im.getextrema() if meta[0] > 1 else "空帧")))
        tiles.append((i, im))

    # 拼图
    cell = 250
    cols = min(len(tiles), 5)
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell + 8, rows * (cell + 26) + 8), (32, 32, 40))
    dr = ImageDraw.Draw(sheet)
    for n, (idx, im) in enumerate(tiles):
        cx = 4 + (n % cols) * cell
        cy = 4 + (n // cols) * (cell + 26)
        dr.text((cx + 4, cy + 4), "index %d  (第%d张)" % (idx, idx + 1), fill=(255, 230, 120))
        if im is None:
            dr.text((cx + 4, cy + 40), "OUT OF RANGE", fill=(255, 80, 80))
            continue
        bg = checker(max(im.width, 1), max(im.height, 1))
        bg = bg.convert("RGBA")
        bg.alpha_composite(im)
        bg.thumbnail((cell - 12, cell - 12))
        sheet.paste(bg.convert("RGB"), (cx + 4, cy + 24))
    out = r"C:\Users\Administrator\WorkBuddy\2026-10-01-17-23-59\tools\title_frames.png"
    sheet.save(out)
    print("\n对照图已保存:", out)
