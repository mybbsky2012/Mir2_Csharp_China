# -*- coding: utf-8 -*-
"""解析 Mir2 .Lib 图库：版本、帧数、指定索引的尺寸/偏移。
格式（MLibrary.Initialize）：
  int32 version
  int32 count
  int32 frameSeek      (version >= 3)
  int32 indexList[count]
每帧头部 17 字节：Width/Height/X/Y/ShadowX/ShadowY (int16 x6) + Shadow (byte) + Length (int32)
"""
import struct
import sys


def parse(path, probes):
    with open(path, "rb") as f:
        data = f.read()

    version, count = struct.unpack_from("<ii", data, 0)
    frame_seek = struct.unpack_from("<i", data, 8)[0] if version >= 3 else 0
    off = 12 if version >= 3 else 8
    index_list = list(struct.unpack_from("<%di" % count, data, off))

    print("文件      :", path)
    print("大小      :", len(data), "字节")
    print("lib 版本  :", version)
    print("帧总数    :", count)
    print("帧表偏移  :", frame_seek)
    print("最大合法序号(0基) :", count - 1)
    print("最大合法序号(1基) :", count)
    print()

    for idx in probes:
        if idx < 0 or idx >= count:
            print("[%d] 越界！该图库只有 0~%d 帧" % (idx, count - 1))
            continue
        w, h, x, y, sx, sy, sh = struct.unpack_from("<hhhhhhB", data, index_list[idx])
        ln = struct.unpack_from("<i", data, index_list[idx] + 13)[0]
        label = "第%d张(1基)" % (idx + 1)
        print("[%d] %s 尺寸=%dx%d  偏移(X,Y)=(%d,%d)  阴影(X,Y)=(%d,%d)  Shadow=0x%02X  数据长度=%d"
              % (idx, label, w, h, x, y, sx, sy, sh, ln))
    print()

    # 全库尺寸分布，帮助判断哪些帧是"面板底图"
    from collections import Counter
    sizes = Counter()
    big = []
    for i in range(count):
        w, h = struct.unpack_from("<hh", data, index_list[i])
        sizes[(w, h)] += 1
        if w >= 100 and h >= 100:
            big.append((i, w, h))
    print("尺寸 Top10（尺寸: 帧数）:")
    for (w, h), c in sizes.most_common(10):
        print("   %dx%d : %d" % (w, h, c))
    print()
    print("宽高均 >=100 的帧共 %d 张，前 60 张（0基索引 / 尺寸）:" % len(big))
    for i, w, h in big[:60]:
        print("   [%d] 第%d张  %dx%d" % (i, i + 1, w, h))


if __name__ == "__main__":
    p = sys.argv[1]
    probes = [int(x) for x in sys.argv[2:]] or [1297]
    parse(p, probes)
