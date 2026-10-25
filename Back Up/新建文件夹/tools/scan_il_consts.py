# -*- coding: utf-8 -*-
import struct, os

def load(fn):
    with open(fn,'rb') as f: return f.read()

def pat_ldc_i4(v):
    # IL: 0x20 <int32 LE>
    return bytes([0x20]) + struct.pack('<i', v)

probes_new = {
    721:  "强击/强击-秘籍 落雷帧基址 (Magic_32bit 721)",
    1771: "落雷杀 起雷帧 (Magic_32bit 1771)",
    1701: "万效符 有效帧基址 (Magic_32bit 1701)",
    1731: "万效符-秘籍 有效帧基址 (Magic_32bit 1731)",
    1810: "灭杀 帧基址 (Magic_32bit 1810)",
    1391: "影华连击 方向帧基址 (Magic_32bit 1391)",
    1561: "影华连击-秘笈 收招 (Magic_32bit 1561)",
    761:  "月华乱舞-秘籍 方向帧基址 (Magic_32bit 761)",
    1280: "毒雾 毒云帧 (Magic2 1280)",
    1631: "吸魔炎风-秘籍 有效帧基址 (Magic_32bit 1631)",
    1581: "吸魔炎风 帧基址 (Magic_32bit 1581)",
}
probes_old = {
    0:   "火球术 FireBall 帧",
    200: "治愈术 Healing 帧",
    900: "抗拒火环 帧",
}

for tag, fn in [("Client.dll", r"E:/micromir2/Client.dll"),
                ("Client.dll(build)", r"D:/mir2-20241027/Build/Client/Release/Client.dll")]:
    if not os.path.exists(fn):
        print("MISSING", fn); continue
    d = load(fn)
    print("="*78)
    print(tag, fn, len(d))
    print("-- 新增技能特效常量 --")
    for v, desc in sorted(probes_new.items()):
        print("   %-6d %-52s %s" % (v, desc, "FOUND x%d" % d.count(pat_ldc_i4(v)) if d.count(pat_ldc_i4(v)) else "*** NOT FOUND ***"))
    print("-- 老技能对照 --")
    for v, desc in sorted(probes_old.items()):
        print("   %-6d %-52s %s" % (v, desc, "FOUND x%d" % d.count(pat_ldc_i4(v)) if d.count(pat_ldc_i4(v)) else "*** NOT FOUND ***"))
