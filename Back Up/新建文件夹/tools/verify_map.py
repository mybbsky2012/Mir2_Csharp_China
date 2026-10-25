import os, re, hashlib
BS = chr(92)
CLI = r"E:\micromir2\Sound" + BS + "SoundList.lst"
SRV = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound" + BS + "SoundList.lst"
OLDC = r"E:\micromir2\Sound" + BS + "SoundList.lst.20261007_233810.bak"
SDIR = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
CDIR = r"E:\micromir2\Sound"
srv=set(os.listdir(SDIR)); cli=set(os.listdir(CDIR))

def load(path):
    idx={}
    for line in open(path, encoding="utf-8-sig", errors="ignore").read().splitlines():
        sp = re.split(r'[:\t]', line.replace(" ",""))
        if len(sp)<=1: continue
        try: i=int(sp[0])
        except Exception: continue
        if i not in idx: idx[i]=sp[-1]
    return idx

USE = {61:[0,1],62:[0],63:[0,1],64:[0,1,2],65:[0],67:[0],68:[0],69:[0],70:[0,1],71:[0],72:[0],73:[0],
       74:[0],75:[0,1],76:[0],77:[0],78:[0],79:[0],80:[0],81:[0],82:[3],83:[0],84:[0],85:[0],
       86:[0,1],87:[1],88:[0]}
NAMES={61:'治愈术',62:'精神力战法',63:'施毒术',64:'灵魂火符',65:'召唤骷髅',67:'隐身术',68:'集体隐身术',
 69:'幽灵盾',70:'心灵启示',71:'神圣战甲术',72:'气功波',73:'困魔咒',74:'净化术',75:'群体治疗术',76:'迷魂术',
 77:'无极真气',78:'召唤神兽',79:'苏生术',80:'精魂召唤术',81:'诅咒术',82:'烦脑',83:'毒雾',84:'先天气功',
 85:'血龙水',86:'阴阳五行阵',87:'治愈术-秘籍',88:'阴阳五行阵-秘籍'}
for path,tag,old in ((CLI,"客户端",OLDC),(SRV,"服务端",None)):
    d = load(path)
    print(f"===== {tag} =====  条目数={len(d)}")
    bad=0
    for sp in sorted(USE):
        for v in USE[sp]:
            i=20000+sp*10+v
            got=d.get(i,"★未定义")
            exp=f"M{sp+40}-{v}.wav"
            exists = (got in srv) or (got in cli)
            ok = (got==exp) or (got.startswith(f"M{sp+40}-") )
            if not ok: bad+=1
            print(f"  {'OK ' if ok else '!! '}{i} {NAMES[sp]:12s}+{v} -> {got:14s} {'(文件在)' if exists else '(文件缺!)'}")
    print(f"  >>> 不符 {bad} 条")
    if old:
        o=load(old)
        lost=[k for k in o if k<20000 and k not in d]
        print(f"  >>> 旧表非20xxx条目 {len([k for k in o if k<20000])} 条，丢失 {len(lost)} 条")
    print()
