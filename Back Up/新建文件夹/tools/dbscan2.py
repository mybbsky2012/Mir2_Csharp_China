# -*- coding: utf-8 -*-
import re

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Server.MirDB"
data = open(p, "rb").read()
txt = data.decode("utf-8", errors="ignore")

sets = ["昆仑套装", "软玉套装", "天龙套装", "监视套装", "暴压套装", "血龙套装", "青宝套装",
        "双戒套装", "赤兰套装", "灵玉套装", "祈祷套装", "红玉套装", "白金套装", "强红玉套",
        "强软玉套", "强白金套", "强青玉套", "青玉套装", "记忆套装", "白骨套装", "密火套装",
        "五玄套装", "破碎套装", "幻魔石套", "虫血套装", "贝玉套装", "黑术套装", "鏃未套装",
        "世轮套装", "绿翠套装", "道护套装", "贵人战套", "贵人法套", "贵人道套", "贵人刺套",
        "贵人弓套"]

print("=== 各套装名在 DB 中出现的次数与位置 ===")
for s in sets:
    idxs = [m.start() for m in re.finditer(re.escape(s.encode("utf-8")), data)]
    print("  %-8s n=%d  %s" % (s, len(idxs), idxs[:8]))

print()
print("=== offset 596344 附近 1200 字节 ===")
print(data[595800:597200].decode("utf-8", errors="replace"))
