# -*- coding: utf-8 -*-
"""解析 Monster/*.Lib 的 V3 帧段，检查召唤物是否自带『出场动作』(MirAction=27 召唤初现)"""
import struct, os, sys

ACTIONS = {
    0:"站立动作",1:"行走动作",2:"跑步动作",3:"推开动作",4:"左冲动作",5:"右冲动作",
    6:"冲击失败",7:"站立姿势",8:"站立姿势2",9:"近距攻击1",10:"近距攻击2",11:"近距攻击3",
    12:"近距攻击4",13:"近距攻击5",14:"远程攻击1",15:"远程攻击2",16:"远程攻击3",
    17:"特殊攻击",18:"被击动作",19:"挖矿展示",20:"施法动作",21:"死亡动作",22:"死后尸体",
    23:"挖后尸骸",24:"石化苏醒",25:"切换LIB",26:"石化状态",27:"召唤初现",28:"复活动作",
    29:"坐下动作",30:"挖矿动作",31:"刺客潜行",32:"刺客冲击",33:"刺客步刺",
    34:"弓箭行走",35:"弓箭奔跑",36:"弓箭跳跃",37:"坐骑站立",38:"坐骑行走",39:"坐骑奔跑",
    40:"坐骑被击",41:"坐骑攻击",42:"钓鱼抛竿",43:"钓鱼等待",44:"钓鱼收线",
}

def dump(path):
    D = open(path, "rb").read()
    ver, cnt = struct.unpack_from("<ii", D, 0)
    off = 8
    frameSeek = 0
    if ver >= 3:
        frameSeek, = struct.unpack_from("<i", D, 8)
        off = 12
    print(f"== {os.path.basename(path)}  ver={ver} imgs={cnt} frameSeek={frameSeek} size={len(D)}")
    if ver >= 3 and frameSeek > 0 and frameSeek < len(D):
        fc, = struct.unpack_from("<i", D, frameSeek)
        print(f"   帧段: frameCount={fc}")
        p = frameSeek + 4
        for _ in range(fc):
            a = D[p]; p += 1
            st, ct, sk, iv, es, ec, esk, eiv = struct.unpack_from("<8i", D, p); p += 32
            rev = D[p]; blend = D[p+1]; p += 2
            print(f"     action {a:>2} {ACTIONS.get(a,'?'):<8} start={st} count={ct} skip={sk} interval={iv} rev={rev} blend={blend}")
    else:
        print("   无帧段（走 FrameSet.DefaultMonster）")

BASE = sys.argv[1] if len(sys.argv) > 1 else r"E:\micromir2\Data\Monster"
for name in ["036.Lib", "170.Lib", "171.Lib", "172.Lib"]:
    p = os.path.join(BASE, name)
    if os.path.exists(p):
        dump(p)
    else:
        print(f"== {name}  不存在")
    print()
