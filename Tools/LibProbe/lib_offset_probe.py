"""解析 .Lib，量化「绘制位置」与「命中框」的偏差。
MImage 头: w,h,X,Y,ShadowX,ShadowY(int16) + Shadow(byte) + Length(int32) + gzip BGRA
TrueSize = 可见像素包围盒尺寸；X/Y = 库自带的偏移。
控件 Size 用 TrueSize、DisplayLocation 用 Location(未加 X/Y) 时，命中框比视觉内容偏左上 (l,t)。
"""
import gzip
import struct
import sys

PATH = r"E:\micromir2\Data\Prguse.Lib" if len(sys.argv) < 2 or not sys.argv[1].lower().endswith(".lib") else sys.argv[1]
TARGETS = [int(x) for x in sys.argv[1:] if x.isdigit()] or list(range(318, 340))

buf = open(PATH, "rb").read()
pos = 0
ver, count = struct.unpack_from("<ii", buf, pos)
pos += 8
if ver >= 3:
    pos += 4
offsets = struct.unpack_from("<%di" % count, buf, pos)

print("Prguse.Lib  version=%d  count=%d" % (ver, count))
print("%5s %5s %5s %5s %5s %5s | %5s %5s %5s %5s | %s" %
      ("idx", "w", "h", "X", "Y", "len", "l", "t", "r", "b", "visual origin vs Location"))


def info(i):
    off = offsets[i]
    w, h, X, Y, sx, sy = struct.unpack_from("<hhhhhh", buf, off)
    shadow = buf[off + 12]
    length = struct.unpack_from("<i", buf, off + 13)[0]
    start = off + 17
    blob = buf[start:start + length]
    try:
        raw = gzip.decompress(blob)
    except Exception:
        return None
    if len(raw) < w * h * 4:
        return None
    l, t, r, b = w, h, 0, 0
    found = False
    for y in range(h):
        row = y * w * 4
        for x in range(w):
            if raw[row + x * 4] != 0:          # 与原 VisiblePixel 判定一致
                found = True
                if x < l: l = x
                if x >= r: r = x + 1
                if y < t: t = y
                if y >= b: b = y + 1
    if not found:
        return (w, h, X, Y, length, 0, 0, 0, 0)
    return (w, h, X, Y, length, l, t, r, b)


for i in TARGETS:
    if i < 0 or i >= count:
        continue
    d = info(i)
    if d is None:
        print("%5d  解析失败" % i)
        continue
    w, h, X, Y, length, l, t, r, b = d
    tw, th = r - l, b - t
    note = ""
    if l or t:
        note = "视觉起点比 Location 右下偏移 (%d,%d)；命中框尺寸 %dx%d 反而从 Location 算起" % (l, t, tw, th)
    print("%5d %5d %5d %5d %5d %5d | %5d %5d %5d %5d | %s"
          % (i, w, h, X, Y, length, l, t, r, b, note))
