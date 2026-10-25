# -*- coding: utf-8 -*-
import os

def has(fn, s, enc):
    try:
        with open(fn, 'rb') as f:
            d = f.read()
    except Exception as e:
        return "ERR:%s" % e
    return d.count(s.encode(enc))

checks = [
    (r"E:/micromir2/Client.dll", [("气功石","u16"),("影华连击","u16"),("落雷杀","u16"),("吸魔炎风","u16"),("黄毒粉","u16"),("月影雾","u16"),("万效符","u16"),("龙血兽","u16"),("灭杀窗口","u16")]),
    (r"D:/mir2-20241027/Build/Client/Release/Client.dll", [("气功石","u16"),("影华连击","u16")]),
    (r"E:/micromir2/Shared.dll", [("AbsorbFlameRare","u8"),("ShadowComboRare","u8"),("SkyThunderShotRare","u8"),("DeathBlow","u8"),("FlashDashRare","u8")]),
    (r"E:/micromir2/MicroClient.dll", [("x","u8")]),
]

for fn, items in checks:
    print("=" * 70)
    print(fn, "exists=", os.path.exists(fn))
    for s, enc in items:
        e = "utf-16-le" if enc == "u16" else "utf-8"
        print("   %-22s count=%s" % (s, has(fn, s, e)))
