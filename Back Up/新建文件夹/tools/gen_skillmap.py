import os, re, json, sys
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
CLI = r"E:\micromir2\Sound"
# Crystal Spell 职业段 -> 目标偏移（韩版资源编号 = 每职业 50 号一段）
CLASSES = [
    ("法师", 31, 58, 20),
    ("道士", 61, 88, 40),
    ("刺客", 91, 107, 60),
    ("弓手", 121, 141, 80),
]
srv = set(os.listdir(SRV)); cli = set(os.listdir(CLI))
entries = []
for name, lo, hi, off in CLASSES:
    got = 0
    for sp in range(lo, hi+1):
        for v in range(0, 10):
            tgt = f"M{sp+off}-{v}.wav"
            if tgt in srv or tgt in cli:
                entries.append((20000 + sp*10 + v, tgt, name, sp, v, tgt in cli))
                got += 1
    print(f"{name}({lo}-{hi}) 偏移+{off}: 生成 {got} 条")
print("合计:", len(entries))
# 道士明细预览
print()
print("=== 道士明细 ===")
for idx,tgt,cls,sp,v,local in entries:
    if cls=="道士":
        print(f"  {idx}: {tgt}{'' if local else '   (需微端下载)'}")
json.dump([list(e) for e in entries], open("skillmap.json","w"), ensure_ascii=False, indent=1)
