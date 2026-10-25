import os, re, json, csv, hashlib
BS = chr(92)
ROOT = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新"
SRV = ROOT + BS + r"Mir2Server" + BS + "ClientResources" + BS + "Sound"
CLI = r"E:\micromir2\Sound"

# 1) Monster 枚举名 -> 数值
enum_val = {}
src = open(r"D:\mir2-20241027\Shared\Enums.cs", encoding="utf-8-sig", errors="ignore").read()
m = re.search(r'public enum Monster : ushort\s*\{(.*?)\n\}', src, re.S)
for name, val in re.findall(r'(\w+)\s*=\s*(\d+)\s*,', m.group(1)):
    enum_val[name] = int(val)

# 2) 怪物中文名：CSV 里 MonsterImage(枚举名) -> 中文名
cn = {}
with open(ROOT + BS + "Mir2Server" + BS + "Exports" + BS + "3_怪物数据.csv", encoding="utf-8-sig", errors="ignore") as f:
    for row in csv.DictReader(f):
        img = (row.get("MonsterImage") or "").strip()
        nm  = (row.get("MonsterName") or "").strip()
        if img and nm and img not in cn:
            cn[img] = nm

# 3) SoundList.lst: 索引 -> 文件
lst = {}
for line in open(CLI + BS + "SoundList.lst", encoding="utf-8-sig", errors="ignore"):
    parts = line.replace(" ", "").split(":")
    parts = [p for p in re.split(r'[:\t]', line.replace(" ", "")) if p != ""]
    if len(parts) <= 1: continue
    try: idx = int(parts[0])
    except: continue
    if idx not in lst: lst[idx] = parts[-1]

# 4) 反向：文件 -> (怪物图片号, 中文名)
file2mon = {}
for idx, fn in lst.items():
    if idx <= 0: continue
    img = idx // 10
    key = fn.lower()
    if key not in file2mon:
        file2mon[key] = (img, cn.get(next((k for k, v in enum_val.items() if v == img), ""), ""))

# 5) 内容分组（判断是否撞车）
h2f = {}
for f in os.listdir(SRV):
    p = os.path.join(SRV, f)
    if not os.path.isfile(p): continue
    if os.path.splitext(f)[1].lower() not in (".wav", ".mp3"): continue
    h2f.setdefault(hashlib.md5(open(p, 'rb').read()).hexdigest(), []).append(f)
cli = set(os.listdir(CLI))

out = {"monster": {}, "skill": {}, "has_client": sorted(cli)}
for f in sorted(os.listdir(SRV)):
    if not f.lower().endswith(('.wav', '.mp3')): continue
    p = os.path.join(SRV, f)
    h = hashlib.md5(open(p, 'rb').read()).hexdigest()
    peers = sorted(x for x in h2f[h] if x.lower() != f.lower())
    ms = re.match(r'^M(\d+)-(\d+)\.', f, re.I)
    if ms:
        out["skill"][f] = {"mid": int(ms.group(1)), "var": int(ms.group(2)), "peers": peers, "size": os.path.getsize(p), "cli": f in cli}
    else:
        mon = file2mon.get(f.lower())
        out["monster"][f] = {"img": mon[0] if mon else None, "name": mon[1] if mon else "", "peers": peers, "size": os.path.getsize(p), "cli": f in cli}
json.dump(out, open("sound_catalog.json", "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("skill 文件:", len(out["skill"]), " monster 文件:", len(out["monster"]))
print("枚举样本:", list(enum_val.items())[:3], " 中文名样本:", list(cn.items())[:3])
for k in ("400-1.wav","1900-1.wav","1910-0.wav","1920-0.wav","8100-4.wav"):
    print(k, "->", out["monster"].get(k) or "(未匹配)")
