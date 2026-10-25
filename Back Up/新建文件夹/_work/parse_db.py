# -*- coding: utf-8 -*-
"""解析 Mir2 Server.MirDB (数据库版本 110) —— 只读，绝不写回。"""
import struct, io, json, sys, os

class R:
    def __init__(self, data):
        self.d = data
        self.p = 0
    def i32(self):
        v = struct.unpack_from('<i', self.d, self.p)[0]; self.p += 4; return v
    def u32(self):
        v = struct.unpack_from('<I', self.d, self.p)[0]; self.p += 4; return v
    def i16(self):
        v = struct.unpack_from('<h', self.d, self.p)[0]; self.p += 2; return v
    def u16(self):
        v = struct.unpack_from('<H', self.d, self.p)[0]; self.p += 2; return v
    def u8(self):
        v = self.d[self.p]; self.p += 1; return v
    def i8(self):
        v = struct.unpack_from('<b', self.d, self.p)[0]; self.p += 1; return v
    def b(self):
        return self.u8() != 0
    def s(self):
        # 7-bit encoded length + utf8
        n = 0; shift = 0
        while True:
            b = self.d[self.p]; self.p += 1
            n |= (b & 0x7F) << shift
            if (b & 0x80) == 0:
                break
            shift += 7
        if n == -1 or n == 0x7FFFFFFF:
            return None
        v = self.d[self.p:self.p + n].decode('utf-8', 'replace')
        self.p += n
        return v
    def stats(self):
        c = self.i32()
        out = {}
        for _ in range(c):
            k = self.u8(); v = self.i32()
            out[k] = v
        return out


def main(path, outdir):
    data = open(path, 'rb').read()
    r = R(data)
    version = r.i32(); custom = r.i32()
    hdr = dict(version=version, custom=custom)
    MapIndex = r.i32(); ItemIndex = r.i32(); MonsterIndex = r.i32()
    NPCIndex = r.i32(); QuestIndex = r.i32()
    GameshopIndex = r.i32(); ConquestIndex = r.i32(); RespawnIndex = r.i32()
    hdr.update(MapIndex=MapIndex, ItemIndex=ItemIndex, MonsterIndex=MonsterIndex,
               NPCIndex=NPCIndex, QuestIndex=QuestIndex, GameshopIndex=GameshopIndex,
               ConquestIndex=ConquestIndex, RespawnIndex=RespawnIndex)
    print('DB', hdr, file=sys.stderr)

    # ---------- maps ----------
    maps = []
    cnt = r.i32()
    for _ in range(cnt):
        m = {}
        m['Index'] = r.i32()
        m['FileName'] = r.s(); m['Title'] = r.s()
        m['MiniMap'] = r.u16(); m['Light'] = r.u8(); m['BigMap'] = r.u16()
        n = r.i32(); sz = []
        for _ in range(n):
            sz.append(dict(x=r.i32(), y=r.i32(), size=r.u16(), start=r.b()))
        m['SafeZones'] = sz
        n = r.i32(); rp = []
        for _ in range(n):
            ri = {}
            ri['MonsterIndex'] = r.i32()
            ri['x'] = r.i32(); ri['y'] = r.i32()
            ri['Count'] = r.u16(); ri['Spread'] = r.u16(); ri['Delay'] = r.u16()
            ri['Direction'] = r.u8()
            ri['RoutePath'] = r.s()
            ri['RandomDelay'] = r.u16()
            ri['RespawnIndex'] = r.i32()
            ri['SaveRespawnTime'] = r.b()
            ri['RespawnTicks'] = r.u16()
            rp.append(ri)
        m['Respawns'] = rp
        n = r.i32(); mv = []
        for _ in range(n):
            mv.append(dict(MapIndex=r.i32(), sx=r.i32(), sy=r.i32(), dx=r.i32(), dy=r.i32(),
                           NeedHole=r.b(), NeedMove=r.b(), ConquestIndex=r.i32(),
                           ShowOnBigMap=(r.b() if version >= 95 else False),
                           Icon=(r.i32() if version >= 95 else 0)))
        m['Movements'] = mv
        for _ in range(4):  # NoTeleport NoReconnect (+string)
            pass
        m['NoTeleport'] = r.b()
        m['NoReconnect'] = r.b()
        m['NoReconnectMap'] = r.s()
        m['NoRandom'] = r.b(); m['NoEscape'] = r.b(); m['NoRecall'] = r.b()
        m['NoDrug'] = r.b(); m['NoPosition'] = r.b(); m['NoThrowItem'] = r.b()
        m['NoDropPlayer'] = r.b(); m['NoDropMonster'] = r.b(); m['NoNames'] = r.b()
        m['Fight'] = r.b(); m['Fire'] = r.b(); m['FireDamage'] = r.i32()
        m['Lightning'] = r.b(); m['LightningDamage'] = r.i32()
        m['MapDarkLight'] = r.u8()
        n = r.i32()
        mz = []
        for _ in range(n):
            mz.append(dict(x=r.i32(), y=r.i32(), size=r.u16(), mine=r.u8()))
        m['MineZones'] = mz
        m['MineIndex'] = r.u8()
        m['NoMount'] = r.b(); m['NeedBridle'] = r.b(); m['NoFight'] = r.b()
        m['Music'] = r.u16()
        if version >= 78:
            m['NoTownTeleport'] = r.b()
        if version >= 79:
            m['NoReincarnation'] = r.b()
        if version >= 110:
            m['WeatherParticles'] = r.u16()
        maps.append(m)

    # ---------- items ----------
    items = []
    cnt = r.i32()
    for _ in range(cnt):
        it = {}
        it['Index'] = r.i32(); it['Name'] = r.s()
        it['Type'] = r.u8(); it['Grade'] = r.u8()
        it['RequiredType'] = r.u8(); it['RequiredClass'] = r.u8(); it['RequiredGender'] = r.u8()
        it['Set'] = r.u8()
        it['Shape'] = r.i16(); it['Weight'] = r.u8(); it['Light'] = r.u8()
        it['RequiredAmount'] = r.u8()
        it['Image'] = r.u16(); it['Durability'] = r.u16()
        it['StackSize'] = r.u16()
        it['Price'] = r.u32()
        it['StartItem'] = r.b()
        it['Effect'] = r.u8()
        bools = r.u8()
        it['flags'] = bools
        it['Bind'] = r.i16(); it['Unique'] = r.i16()
        it['RandomStatsId'] = r.u8()
        it['CanFastRun'] = r.b(); it['CanAwakening'] = r.b()
        if version > 83:
            it['Slots'] = r.u8()
        if version > 84:
            it['Stats'] = r.stats()
        has_tt = r.b()
        if has_tt:
            it['ToolTip'] = r.s()
        items.append(it)

    # ---------- monsters ----------
    monsters = []
    cnt = r.i32()
    for _ in range(cnt):
        mo = {}
        mo['Index'] = r.i32(); mo['Name'] = r.s()
        mo['Image'] = r.u16(); mo['AI'] = r.u16(); mo['Effect'] = r.u8()
        if version < 62:
            mo['Level'] = r.u8()
        else:
            mo['Level'] = r.u16()
        mo['ViewRange'] = r.u8(); mo['CoolEye'] = r.u8()
        if version > 84:
            mo['Stats'] = r.stats()
        if version <= 84:
            mo['HP'] = r.u32()
            for _k in range(10):
                r.u16()
            r.u8(); r.u8()
        mo['Light'] = r.u8()
        mo['AttackSpeed'] = r.u16(); mo['MoveSpeed'] = r.u16()
        mo['Experience'] = r.u32()
        mo['CanPush'] = r.b(); mo['CanTame'] = r.b()
        if version >= 18:
            mo['AutoRev'] = r.b(); mo['Undead'] = r.b()
        if version >= 89:
            mo['DropPath'] = r.s()
        else:
            mo['DropPath'] = None
        monsters.append(mo)

    print('maps=%d items=%d monsters=%d pos=%d/%d' % (len(maps), len(items), len(monsters), r.p, len(data)), file=sys.stderr)

    os.makedirs(outdir, exist_ok=True)
    json.dump(hdr, open(os.path.join(outdir, 'db_header.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    json.dump(maps, open(os.path.join(outdir, 'db_maps.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    json.dump(items, open(os.path.join(outdir, 'db_items.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    json.dump(monsters, open(os.path.join(outdir, 'db_monsters.json'), 'w', encoding='utf-8'), ensure_ascii=False)
    return hdr, maps, items, monsters


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
