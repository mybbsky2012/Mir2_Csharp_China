import os
C = r"E:\micromir2\Sound"
S = r"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\ClientResources\Sound"
cli = set(os.listdir(C)); srv = set(os.listdir(S))
tao = {
 "Healing(治愈)":61, "SpiritSword(精神力战法)":62, "Poisoning(施毒术)":63,
 "SoulFireBall(灵魂火符)":64, "SummonSkeleton(召唤骷髅)":65, "Hiding(隐身术)":67,
 "MassHiding(集体隐身)":68, "SoulShield(幽灵盾)":69, "Revelation(心灵启示)":70,
 "BlessedArmour(神圣战甲)":71, "EnergyRepulsor(移形换位)":72, "TrapHexagon(困魔咒)":73,
 "Purification(清心诀)":74, "MassHealing(群体治愈)":75, "Hallucination(幻影分身)":76,
 "UltimateEnhancer(无极真气)":77, "SummonShinsu(召唤神兽)":78, "Reincarnation(复活术)":79,
 "SummonHolyDeva(召唤圣兽)":80, "Curse(诅咒术)":81, "Plague(瘟疫)":82,
 "PoisonCloud(毒云)":83, "EnergyShield(魔法盾)":84, "PetEnhancer(宠物强化)":85,
 "HealingCircle(治愈环)":86, "HealingRare(治愈术-稀有)":87, "HealingcircleRare(治愈环-稀有)":88,
}
print(f"{'技能':28s} {'编号':>4s}  客户端  服务端   备注")
for name,sp in tao.items():
    have_c=[]; miss=[]
    for v in range(0,6):
        fn=f"M{sp}-{v}.wav"
        if fn in cli: have_c.append(v)
        elif fn in srv: pass
        else: miss.append(v)
    srv_only=[v for v in range(0,6) if f"M{sp}-{v}.wav" in srv and f"M{sp}-{v}.wav" not in cli]
    note=""
    if miss: note += "两端皆缺变体:"+",".join("+"+str(v) for v in miss)+"  "
    if srv_only: note += "仅服务端有:"+",".join("+"+str(v) for v in srv_only)
    print(f"{name:28s} {sp:>4d}  {','.join('+'+str(v) for v in have_c):>8s}  {note}")
