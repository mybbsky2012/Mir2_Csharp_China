# -*- coding: utf-8 -*-
import re, os

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Server.MirDB"
data = open(p, "rb").read()
print("size:", len(data))
print("head:", data[:64].hex())

for enc, tag in (("utf-8", "U8"), ("utf-16-le", "U16")):
    try:
        txt = data.decode(enc, errors="ignore")
    except Exception:
        continue
    for name in ["昆仑套装", "软玉套装", "贵人战套", "破天的核心", "摩鲁的赤色药剂"]:
        idxs = [m.start() for m in re.finditer(re.escape(name), txt)]
        if idxs:
            print("%s %s -> %d hits, first@%d" % (tag, name, len(idxs), idxs[0]))

# 找出所有形如 XXX套装 的串
txt16 = data.decode("utf-16-le", errors="ignore")
names = re.findall(r"[\u4e00-\u9fa5]{2,8}套装", txt16)
print("U16 套装串:", sorted(set(names))[:60])
txt8 = data.decode("utf-8", errors="ignore")
names8 = re.findall(r"[\u4e00-\u9fa5]{2,8}套装", txt8)
print("U8 套装串:", sorted(set(names8))[:60])
