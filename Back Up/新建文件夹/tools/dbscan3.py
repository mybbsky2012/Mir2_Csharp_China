# -*- coding: utf-8 -*-
import re

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Server.MirDB"
data = open(p, "rb").read()

for kw in ["套", "软玉", "红玉", "白金", "青玉", "记忆", "白骨", "祈祷", "贵人", "监视", "天龙"]:
    b = kw.encode("utf-8")
    idxs = [m.start() for m in re.finditer(re.escape(b), data)]
    print("%-6s n=%-4d %s" % (kw, len(idxs), idxs[:12]))

print()
print("=== 含『套』的上下文 ===")
seen = set()
for m in re.finditer(re.escape("套".encode("utf-8")), data):
    s = data[max(0, m.start()-60):m.start()+30]
    txt = s.decode("utf-8", errors="replace")
    txt = "".join(ch if ch.isprintable() and ch != "\ufffd" else "." for ch in txt)
    if txt not in seen:
        seen.add(txt)
        print("  @%d %s" % (m.start(), txt))
    if len(seen) > 40:
        break
