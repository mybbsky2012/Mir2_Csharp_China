# -*- coding: utf-8 -*-
"""解析 Magic_32bit.Lib 的 FrameSet，找出吸魔炎风(1581-1614)/秘籍(1630-1665)的官方帧编排"""
import struct, sys

sys.stdout.reconfigure(encoding="utf-8")

LIB = r"E:\micromir2\Data\Magic_32bit.Lib"
raw = open(LIB, "rb").read()
p = 0


def i32():
    global p
    v = struct.unpack_from("<i", raw, p)[0]
    p += 4
    return v


ver = i32()
count = i32()
frame_seek = i32()
print(f"ver={ver} count={count} frameSeek={frame_seek}")
p = frame_seek
frame_count = i32()
print(f"FrameSet 条目数 = {frame_count}")

ACTIONS = {
    0: "站立", 1: "行走", 2: "奔跑", 3: "挖矿", 4: "突斩", 5: "近距攻击1", 6: "近距攻击2",
    7: "近距攻击3", 8: "近距攻击4", 9: "近距攻击5", 10: "远程攻击1", 11: "远程攻击2",
    12: "远程攻击3", 13: "远程攻击4", 14: "远程攻击5", 15: "被击", 16: "死亡", 17: "死后尸体",
    18: "复活的尸体", 19: "复活", 20: "出现", 21: "施法动作", 22: "祈祷", 23: "蹲下(消失)",
    24: "蹲下(出现)", 25: "嘲弄", 26: "注视", 27: "恐惧", 28: "灵魂出窍(离开)", 29: "灵魂出窍(回归)",
    30: "觉醒", 31: "附身", 32: "移动攻击",
}

for i in range(frame_count):
    act = raw[p]
    p += 1
    start = i32(); cnt = i32(); skip = i32(); interval = i32()
    es = i32(); ec = i32(); esk = i32(); ei = i32()
    rev = raw[p]; p += 1
    blend = raw[p]; p += 1
    name = ACTIONS.get(act, f"动作{act}")
    mark = ""
    if 1500 <= start <= 1700 or (start + cnt) > 1500 and start < 1700:
        mark = "   <== 关注区间"
    print(f"  [{i:2}] {name:12} start={start:5} count={cnt:3} skip={skip:3} interval={interval:5} "
          f"特效=({es},{ec},{esk},{ei}) Reverse={rev} Blend={blend}{mark}")
