# -*- coding: utf-8 -*-
"""
导出本服所有怪物立绘缩略图 → sprites.json（image_id -> dataURI）
缩略图统一装进 96x96 透明底，量化为 256 色 PNG 以减小体积。
"""
import os
import io
import csv
import json
import base64
from collections import defaultdict
from PIL import Image
import libdec

HERE = os.path.dirname(os.path.abspath(__file__))
LIBDIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\data\monster"
OUT = os.path.join(HERE, "sprites.json")
CELL = 96

libs = libdec.load_dir(LIBDIR)
print("可用图库:", len(libs))

rows = list(csv.DictReader(open(os.path.join(HERE, "monsters.csv"), encoding="utf-8-sig")))
need = sorted(set(int(r["Image"]) for r in rows))
print("需要导出的唯一 Image:", len(need))

ok, miss = 0, []
sprites = {}
cache = {}
for idx in need:
    path = libs.get(idx)
    if not path:
        miss.append(idx)
        continue
    try:
        lb = cache.setdefault(idx, libdec.Lib(path))
        fi = lb.best_static_frame()
        im = lb.get(fi)
        if not im or im.w <= 1 or im.h <= 1:
            miss.append(idx)
            continue
        sprite = im.to_image()
        # 等比缩到 CELL 内
        w, h = sprite.size
        k = min(CELL / w, CELL / h, 1.0)
        if k < 1.0:
            sprite = sprite.resize((max(1, int(w * k)), max(1, int(h * k))), Image.LANCZOS)
        canvas = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
        canvas.paste(sprite, ((CELL - sprite.width) // 2, (CELL - sprite.height) // 2), sprite)
        q = canvas.quantize(colors=256, method=Image.FASTOCTREE)
        buf = io.BytesIO()
        q.save(buf, "PNG", optimize=True)
        sprites[idx] = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
        ok += 1
    except Exception as e:
        miss.append(idx)
        print("  ! %d 失败: %s" % (idx, e))

json.dump(sprites, open(OUT, "w"))
print("导出成功:", ok, " 失败/缺库:", len(miss))
if miss:
    print("缺库样例:", miss[:30])
print("sprites.json 大小: %.1f MB" % (os.path.getsize(OUT) / 1048576))
