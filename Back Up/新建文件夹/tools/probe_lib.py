# -*- coding: utf-8 -*-
import struct, os, sys

def read_lib(path):
    with open(path,'rb') as f: d = f.read()
    # Mir2 .Lib: [int32 version][int32 count] then count entries
    version, count = struct.unpack_from('<ii', d, 0)
    off = 8
    frames = []
    for i in range(count):
        # Frame: int16 width, int16 height, int16 x, int16 y,
        #        byte shadowType?, byte shadowSize? ... 实际: w,h,x,y(4x int16), shadow x,y (int16), shadowType byte, shadowSize byte, then length int32, then data
        w, h, x, y = struct.unpack_from('<hhhh', d, off)
        sx, sy, st, ss = struct.unpack_from('<hhBB', d, off+8)
        ln = struct.unpack_from('<i', d, off+14)[0]
        frames.append((w,h,ln))
        off += 18 + ln
    return version, count, frames

def seg(frames, a, b, label):
    print("  %-34s %d..%d" % (label, a, b))
    ok=0; empty=0; blank=0
    detail=[]
    for i in range(a, min(b+1, len(frames))):
        w,h,ln = frames[i]
        if w==0 or h==0: empty+=1; s='EMPTY'
        elif ln==0: blank+=1; s='BLANK'
        else: ok+=1; s='OK(%dx%d,%dB)'%(w,h,ln)
        detail.append((i,s))
    print("     OK=%d EMPTY=%d BLANK=%d" % (ok,empty,blank))
    # 打印细节，帧数 <=40 时
    if b-a+1 <= 60:
        print("     " + " | ".join("%d:%s"%(i,s.split('(')[0]) for i,s in detail))

files = [
  r"E:/micromir2/Data/Magic_32bit.Lib",
  r"E:/BaiduNetdiskDownload/韩Mir2/20260915更新/Mir2Server/ClientResources/Data/Magic_32bit.Lib",
]
for p in files:
    if not os.path.exists(p): print("MISSING", p); continue
    v,c,frames = read_lib(p)
    print("="*84)
    print(p)
    print("  Lib version=%d frames=%d" % (v,c))
    seg(frames, 718, 748, "强击 721-724 / 秘籍 721-744")
    seg(frames, 1388, 1410, "影华连击 1391+ 起")
    seg(frames, 1542, 1570, "影华连击尾 1546 / 收招 1561-1566")
    seg(frames, 1628, 1690, "吸魔炎风 1631-1688")
    seg(frames, 1698, 1760, "万效符 1701-1756")
    seg(frames, 1768, 1796, "落雷杀 1771-1793")
    seg(frames, 1806, 1828, "灭杀 1810-1824")
    seg(frames, 758, 782, "月华乱舞-秘籍 761+")
