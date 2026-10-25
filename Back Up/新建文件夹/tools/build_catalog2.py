import os, re, json, csv, hashlib
BS = chr(92)
ROOT = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新"
SRV = ROOT + BS + r"Mir2Server" + BS + "ClientResources" + BS + "Sound"
CLI = r"E:\micromir2\Sound"

enum_val = {}
src = open(r"D:\mir2-20241027\Shared\Enums.cs", encoding="utf-8-sig", errors="ignore").read()
m = re.search(r'public enum Monster : ushort\s*\{(.*?)\n\}', src, re.S)
for name, val in re.findall(r'(\w+)\s*=\s*(\d+)\s*,', m.group(1)):
    enum_val[name] = int(val)
val2name = {v: k for k, v in enum_val.items()}
cn = {}
with open(ROOT + BS + "Mir2Server" + BS + "Exports" + BS + "3_怪物数据.csv", encoding="utf-8-sig", errors="ignore") as f:
    for row in csv.DictReader(f):
        img = (row.get("MonsterImage") or "").strip(); nm = (row.get("MonsterName") or "").strip()
        if img and nm and img not in cn: cn[img] = nm

lst = {}
for line in open(CLI + BS + "SoundList.lst", encoding="utf-8-sig", errors="ignore"):
    ps = [p.strip() for p in re.split(r'[:\t]', line.replace(" ", "")) if p.strip() != ""]
    if len(ps) <= 1: continue
    try: idx = int(ps[0])
    except: continue
    if idx not in lst: lst[idx] = ps[-1]
file2mon = {}
for idx, fn in lst.items():
    if idx <= 0: continue
    img = idx // 10
    file2mon.setdefault(fn.lower(), (img, cn.get(val2name.get(img, ""), "") or val2name.get(img, "")))

h2f = {}
for f in os.listdir(SRV):
    p = os.path.join(SRV, f)
    if os.path.isfile(p) and os.path.splitext(f)[1].lower() in (".wav", ".mp3"):
        h2f.setdefault(hashlib.md5(open(p, 'rb').read()).hexdigest(), []).append(f)
cli = set(os.listdir(CLI))

skills, mons = {}, {}
for f in sorted(os.listdir(SRV)):
    if not f.lower().endswith(('.wav', '.mp3')): continue
    p = os.path.join(SRV, f)
    h = hashlib.md5(open(p, 'rb').read()).hexdigest()
    peers = sorted(x for x in h2f[h] if x.lower() != f.lower())
    ms = re.match(r'^M(\d+)-(\d+)\.', f, re.I)
    if ms:
        skills[f] = {"mid": int(ms.group(1)), "var": int(ms.group(2)), "peers": peers, "size": os.path.getsize(p), "cli": f in cli}
    else:
        mon = file2mon.get(f.lower(), (None, ""))
        mons[f] = {"img": mon[0], "name": mon[1], "peers": peers, "size": os.path.getsize(p), "cli": f in cli}
json.dump({"skill": skills, "monster": mons}, open("sound_catalog.json", "w", encoding="utf-8"), ensure_ascii=False)
print("skill:", len(skills), "monster:", len(mons))
for k in ("400-1.wav","1900-1.wav","1910-0.wav","1920-0.wav","1630-1.wav"):
    print(" ", k, "->", mons.get(k, "(不在目录)"))
