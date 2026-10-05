# -*- coding: utf-8 -*-
"""模拟服务端 InIReader 的读取逻辑，确认 [FakePlayer] 的 8 个键都能读出来。"""

p = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\20260915Mir2Server\Configs\Setup.ini"

with open(p, encoding="utf-8-sig", errors="ignore") as f:
    lines = [l.rstrip("\r\n") for l in f]

# InIReader 的套路：整份读进来，FindValue(section, key) 在该段内逐行 Split('=')[0] 精确比对
def find_value(section, key):
    inside = False
    for line in lines:
        s = line.strip()
        if s.startswith("[") and s.endswith("]"):
            inside = (s == "[%s]" % section)
            continue
        if not inside:
            continue
        if "=" not in line:
            continue
        k = line.split("=")[0].strip()
        if k == key:
            return line.split("=", 1)[1].strip()
    return None

keys = ["Enabled", "Count", "LevelMin", "LevelMax", "Maps", "Chat", "RandomLogin", "ChatFile"]

print("[FakePlayer] 段读取结果:")
ok = True
for k in keys:
    v = find_value("FakePlayer", k)
    flag = "OK " if v is not None else "MISS"
    if v is None:
        ok = False
    print("  {} {:<12} = {!r}".format(flag, k, v))

print()
print("结论:", "8 个键全部可读" if ok else "有键读不到！需要检查！")

# 顺带确认老键没被影响
print()
print("抽检老键:")
for sec, k in [("Server", "Port"), ("Server", "MapPath")]:
    print("  [{}] {:<10} = {!r}".format(sec, k, find_value(sec, k)))
