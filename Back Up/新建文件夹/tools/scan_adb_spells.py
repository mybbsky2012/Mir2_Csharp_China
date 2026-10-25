# -*- coding: utf-8 -*-
DB = r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/Server.MirADB"
D = open(DB,'rb').read()

NEW = {108:'ThunderBoltRare',109:'AbsorbFlame',110:'AbsorbFlameRare',111:'GreatTalisman',
       112:'GreatTalismanRare',113:'SoulFireBallPlus',114:'SoulFireBallPlusRare',115:'PetEnhancerRare',
       116:'CrescentSlashRare',117:'ShadowCombo',118:'ShadowComboRare',119:'SkyThunderShot',
       120:'SkyThunderShotRare',142:'DeathBlow',143:'FlashDashRare',144:'MoonMistRare',
       145:'DelayedExplosionRare',146:'ConcentrationRare'}

print("ADB size:", len(D))
print("=== 新技能 ID 在 ADB 中的出现 ===")
for v, nm in sorted(NEW.items()):
    b = bytes([v])
    pos = []
    st = 0
    while True:
        i = D.find(b, st)
        if i < 0: break
        pos.append(i); st = i+1
    print("  %3d %-22s x%d  %s" % (v, nm, len(pos), pos[:12]))

# 寻找「技能列表」模式：紧邻的一串值 <= 150 且相邻位置连续
print()
print("=== 扫描疑似技能列表（连续 >=8 字节且值全部 <= 200） ===")
runs = []
i = 0
n = len(D)
while i < n:
    if D[i] <= 200:
        j = i
        while j < n and D[j] <= 200:
            j += 1
        if j - i >= 12:
            runs.append((i, j-i, D[i:j]))
        i = j
    else:
        i += 1
print("  共 %d 段" % len(runs))
cnt_new_in_run = 0
for off, ln, seg in runs:
    hits = [ (v, NEW[v]) for v in seg if v in NEW ]
    if hits:
        cnt_new_in_run += 1
        print("  @%d len=%d 含新技能ID: %s" % (off, ln, hits))
        print("      seg=%s" % list(seg))
print("含新技能ID的段数:", cnt_new_in_run)
