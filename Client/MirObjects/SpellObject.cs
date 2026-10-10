using Client.MirGraphics;
using Client.MirScenes;
using Client.MirSounds;
using S = ServerPackets;

namespace Client.MirObjects
{
    class SpellObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Spell; }
        }

        public override bool Blocking
        {
            get { return false; }
        }

        public Spell Spell;
        public Point AnimationOffset = new(0, 0);
        public int FrameCount, FrameInterval, FrameIndex;
        public int[] FrameList;                 //新增：非连续帧序列（跳过库中空帧），为空时按 DrawFrame + FrameIndex 线性播放
        public int LoopStart = -1;              //新增：循环段起始索引（-1 = 每轮从头循环）。用于"起手动画播一次、之后只循环稳定帧"的特效（如毒雾云）
        public int LoopInterval = -1;           //新增：循环段帧间隔（-1 = 沿用 FrameInterval），让稳定段的波动更从容
        public bool Repeat, Ended, DrawBehind;


        public SpellObject(uint objectID) : base(objectID)
        {
        }

        public void Load(S.ObjectSpell info)
        {
            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);
            Spell = info.Spell;
            Direction = info.Direction;
            Repeat = true;
            Ended = false;
            DrawBehind = false;

            switch (Spell)
            {
                case Spell.TrapHexagon:
                    BodyLibrary = Libraries.Magic;
                    DrawFrame = 1390;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Blend = true;
                    break;
                case Spell.FireWall:
                    BodyLibrary = Libraries.Magic;
                    DrawFrame = 1630;
                    FrameInterval = 120;
                    FrameCount = 6;
                    Light = 3;
                    Blend = true;
                    break;
                case Spell.PoisonCloud:
                    //毒雾：Magic2 1280-1299 这一段里，只有 1280-1289 是云（1290-1299 全是空白帧）。
                    //这 10 帧是"云从小团扩散成一大片"的动画：1280 是小团，1287-1289 才是最大且稳定的云形态。
                    //起手 1280-1289 完整扩散一次，之后只循环 1287-1289（LoopStart=7）——
                    //云会一直保持一大片，不再缩回小团重来（之前每 1.5 秒重头循环，看起来云中途消失）。
                    //整片毒云由服务端按网格铺的 9 朵云拼成（见 Server/MirEnvir/Map.cs）。
                    BodyLibrary = Libraries.Magic2;
                    FrameList = new[] { 1280, 1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289 };
                    FrameCount = FrameList.Length;
                    FrameInterval = 150;
                    LoopStart = 7;          //稳定段 = 帧 1287/1288/1289
                    LoopInterval = 250;     //稳定段 3 帧 750ms 一轮，波动更从容
                    Light = 3;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(20000 + (ushort)Spell.MassHiding * 10);
                    break;
                //新增：吸魔炎风 / 吸魔炎风-秘籍（火焰旋风，Magic_32bit）
                //★实测 Magic_32bit.Lib 的帧分布（w=0 才是真空帧）：
                //   普通版  1580-1583 火焰升起(起手) → 1590-1601 稳定燃烧(12帧) → 1610-1613 熄灭(alpha 逐帧降到 2.4)
                //   秘籍版  1630-1633 升起      → 1640-1651 稳定燃烧(12帧) → 1660-1663 熄灭 → 1680-1687 消散
                //   旧帧表把「熄灭段」也塞进了循环（秘籍还多带了 1634/1652/1664/1688 四个 w=0 真空帧），
                //   于是每转一圈火焰都会灭一次再重燃 —— 这就是"中间图像帧中断"的根因。
                //   现在只保留 起手 + 稳定段，起手播一次后只循环稳定段（LoopStart=4），火焰一直烧不断。
                case Spell.AbsorbFlame:
                    DrawColour = Color.White;
                    BodyLibrary = Libraries.Magic_32bit;
                    FrameList = new[]
                    {
                        1580, 1581, 1582, 1583,
                        1590, 1591, 1592, 1593, 1594, 1595, 1596, 1597, 1598, 1599, 1600, 1601,
                    };
                    FrameCount = FrameList.Length;
                    FrameInterval = 80;
                    LoopStart = 4;      //起手 4 帧播一次，之后循环 1590-1601
                    Light = 4;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(20000 + (ushort)Spell.FireWall * 10 + 1);
                    break;
                case Spell.AbsorbFlameRare:
                    DrawColour = Color.White;
                    BodyLibrary = Libraries.Magic_32bit;
                    FrameList = new[]
                    {
                        1630, 1631, 1632, 1633,
                        1640, 1641, 1642, 1643, 1644, 1645, 1646, 1647, 1648, 1649, 1650, 1651,
                    };
                    FrameCount = FrameList.Length;
                    FrameInterval = 80;
                    LoopStart = 4;      //起手 4 帧播一次，之后循环 1640-1651
                    Light = 5;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(20000 + (ushort)Spell.FireWall * 10 + 1);
                    break;
                //新增：落雷杀 / 落雷杀-秘笈（定点持续落雷，Magic_32bit 1770-1809；1770/1774-1780/1789-1790/1794+ 是空帧）
                case Spell.SkyThunderShot:
                case Spell.SkyThunderShotRare:
                    BodyLibrary = Libraries.Magic_32bit;
                    FrameList = new[] { 1771, 1772, 1773, 1781, 1782, 1783, 1784, 1785, 1786, 1787, 1788, 1791, 1792, 1793 };
                    FrameCount = FrameList.Length;
                    FrameInterval = 90;
                    Light = 5;
                    Blend = true;
                    Repeat = true;
                    break;
                case Spell.DigOutZombie:
                    BodyLibrary = (ushort)Monster.DigOutZombie < Libraries.Monsters.Count() ? Libraries.Monsters[(ushort)Monster.DigOutZombie] : Libraries.Magic;
                    DrawFrame = 304 + (byte) Direction;
                    FrameCount = 0;
                    Blend = false;
                    break;
                case Spell.Blizzard:
                    AnimationOffset = new Point(0, -20);
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 1550;
                    FrameInterval = 100;
                    FrameCount = 30;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.MeteorStrike:
                    AnimationOffset = new Point(0, -20);
                    MapControl.Effects.Add(new Effect(Libraries.Magic2, 1600, 10, 800, CurrentLocation) { DrawBehind = true, Repeat = true, RepeatUntil = CMain.Time + 3000 });
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 1610;
                    FrameInterval = 100;
                    FrameCount = 30;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.HealingcircleRare:
                    DrawColour = Color.FromArgb(100, 255, 200, 150); 
                    BodyLibrary = Libraries.Magic3;
                    DrawFrame = 630;
                    FrameInterval = 100;
                    FrameCount = 11;
                    Light = 5;
                    Blend = true;
                    Repeat = true;
                    DrawBehind = true;
                    break;
                case Spell.Rubble:
                    if (Direction == 0)
                        BodyLibrary = null;
                    else
                    {
                        BodyLibrary = Libraries.Effect;
                        DrawFrame = 64 + Math.Min(4, (int)(Direction - 1));
                        FrameCount = 1;
                        FrameInterval = 10000;
                    }
                    break;
                case Spell.Reincarnation:
                    BodyLibrary = Libraries.Magic;
                    DrawFrame = 4020;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Light = 1;
                    Blend = true;
                    Repeat = true;
                    break;
                case Spell.ExplosiveTrap:
                    BodyLibrary = Libraries.Magic3;
                    if (info.Param)
                    {
                        DrawFrame = 1570;
                        FrameInterval = 100;
                        FrameCount = 9;
                        Repeat = false;
                        SoundManager.PlaySound(20000 + 124 * 10 + 5);//Boom for all players in range
                    }
                    else
                    {
                        DrawFrame = 1560;
                        FrameInterval = 100;
                        FrameCount = 10;
                        Repeat = true;
                    }
                    //Light = 1;
                    Blend = true;
                    break;
                case Spell.Trap:
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 2360;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Blend = true;
                    break;
                case Spell.MapLightning:
                    MapControl.Effects.Add(new Effect(Libraries.Dragon, 400 + (CMain.Random.Next(3) * 10), 5, 600, CurrentLocation));
                    SoundManager.PlaySound(8301);
                    break;
                case Spell.MapLava:
                    MapControl.Effects.Add(new Effect(Libraries.Dragon, 440, 20, 1600, CurrentLocation) { Blend = false });
                    MapControl.Effects.Add(new Effect(Libraries.Dragon, 470, 10, 800, CurrentLocation));
                    SoundManager.PlaySound(8302);
                    break;
                case Spell.MapQuake1:
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HellLord], 27, 12, 1200, CurrentLocation) { Blend = false });
                    SoundManager.PlaySound(8304);
                    break;
                case Spell.MapQuake2:
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HellLord], 39, 13, 1300, CurrentLocation) { Blend = false });
                    SoundManager.PlaySound(8304);
                    break;
                case Spell.DigOutArmadillo:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Armadillo];
                    DrawFrame = 472 + (byte)Direction;
                    FrameCount = 0;
                    Blend = false;
                    break;
                case Spell.GeneralMeowMeowThunder:                
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.GeneralMeowMeow], 562, 7, 700, CurrentLocation) { Blend = true });
                    SoundManager.PlaySound(8321);
                    break;
                case Spell.StoneGolemQuake:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.StoneGolem];
                    DrawFrame = 368 + (int)Direction * 8;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Light = 0;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8304);
                    break;
                case Spell.EarthGolemPile:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.EarthGolem];
                    DrawFrame = 441;
                    FrameInterval = 100;
                    FrameCount = 12;
                    Light = 0;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8331);
                    break;
                case Spell.TreeQueenMassRoots:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TreeQueen];
                    DrawFrame = 82;
                    FrameInterval = 100;
                    FrameCount = 15;
                    Blend = false;
                    Repeat = false;
                    DrawBehind = true;
                    SoundManager.PlaySound(8341);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TreeQueen], 97, 15, 1400, CurrentLocation) { Blend = true });
                    break;
                case Spell.TreeQueenGroundRoots:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TreeQueen];
                    DrawFrame = 48;
                    FrameInterval = 100;
                    FrameCount = 9;
                    Blend = false;
                    Repeat = false;
                    DrawBehind = true;
                    SoundManager.PlaySound(8342);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TreeQueen], 57, 9, 900, CurrentLocation) { Blend = true });
                    break;
                case Spell.TreeQueenRoot:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TreeQueen];
                    DrawFrame = 112;
                    FrameInterval = 100;
                    FrameCount = 15;
                    Blend = false;
                    Repeat = false;
                    DrawBehind = true;
                    SoundManager.PlaySound(8343);
                    break;
                case Spell.TucsonGeneralRock:
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TucsonGeneral], 552, 20, 2000, CurrentLocation) { Repeat = false, Blend = false });
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TucsonGeneral];
                    DrawFrame = 572;
                    FrameInterval = 100;
                    FrameCount = 20;
                    Light = 1;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.Portal:
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 2360;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Blend = true;
                    break;
                case Spell.HealingCircle:
                    DrawColour = Color.FromArgb(128, 255, 255, 100); 
                    BodyLibrary = Libraries.Magic3;
                    DrawFrame = 630;
                    FrameInterval = 80;
                    FrameCount = 11;
                    Light = 3;
                    Blend = true;
                    break;
                case Spell.FlyingStatueIceTornado:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.FlyingStatue];
                    DrawFrame = 314;
                    FrameInterval = 100;
                    FrameCount = 20;
                    Blend = true;
                    Repeat = false;
                    SoundManager.PlaySound(8303);
                    break;
                case Spell.DarkOmaKingNuke:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.DarkOmaKing];
                    DrawFrame = 1670 + (int)Direction * 9;
                    FrameInterval = 100;
                    FrameCount = 9;
                    Blend = true;
                    Repeat = false;
                    SoundManager.PlaySound(((ushort)Monster.DarkOmaKing * 10) + 9);
                    break;
                case Spell.HornedSorcererDustTornado:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedSorceror];
                    DrawFrame = 634;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    break;
                case Spell.HornedCommanderRockFall:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedCommander];
                    DrawFrame = 1178;
                    FrameInterval = 100;
                    FrameCount = 12;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(4089);
                    break;
                case Spell.HornedCommanderRockSpike:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedCommander];
                    DrawFrame = 1358;
                    FrameInterval = 100;
                    FrameCount = 9;
                    Blend = false;
                    Repeat = true;
                    SoundManager.PlaySound(8457);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HornedCommander], 1367, 9, 900, CurrentLocation) { Blend = true });
                    break;
                case Spell.HornedCommanderShield:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedCommander];
                    DrawColour = Color.White;
                    DrawFrame = 1341;
                    FrameInterval = 100;
                    FrameCount = 17;
                    Light = 5;
                    Blend = true;
                    Repeat = true;
                    DrawBehind = true;
                    break;
                case Spell.YangDragonFlame:
                    DrawColour = Color.FromArgb(200, 200, 200, 200);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.ChieftainSword];                    
                    DrawFrame = 1088;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.YangDragonIcyBurst:
                    DrawColour = Color.FromArgb(180, 200, 200, 180);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.ChieftainSword];
                    DrawFrame = 1314;
                    FrameInterval = 200;
                    FrameCount = 5;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.ChieftainSword], 1319, 12, 1200, CurrentLocation, CMain.Time + 1000) { Blend = true });
                    break;               
                case Spell.ShardGuardianIceBomb:
                    DrawColour = Color.FromArgb(158, 158, 158, 255);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.ShardGuardian];
                    DrawFrame = 502;
                    FrameInterval = 100;
                    FrameCount = 15;
                    Light = 2;
                    Blend = true;
                    Repeat = false;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.ShardGuardian], 536, 8, 800, CurrentLocation, CMain.Time + 1500) { Blend = true, DrawBehind = true });
                    break;
                case Spell.GroundFissure:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.MysteriousMonk];
                    DrawFrame = 744 + (int)Direction * 10;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Light = 2;
                    Blend = true;
                    Repeat = false;
                    DrawBehind = true;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.MysteriousMonk], 824 + (int)Direction * 10, 10, 1000, CurrentLocation) { Blend = true });
                    break;
                case Spell.SkeletonBomb:
                    DrawColour = Color.FromArgb(158, 158, 158, 255);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Swain];
                    DrawFrame = 528;
                    FrameInterval = 100;
                    FrameCount = 13;
                    Light = 2;
                    Blend = true;
                    Repeat = false;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Swain], 541, 10, 1000, CurrentLocation, CMain.Time + 1300) { Blend = true, DrawBehind = true });
                    break;
                case Spell.FlameExplosion:
                    DrawColour = Color.FromArgb(180, 180, 180, 255);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Swain1];
                    DrawFrame = 699;
                    FrameInterval = 200;
                    FrameCount = 9;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    DrawBehind = true;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Swain1], 708, 5, 1000, CurrentLocation, CMain.Time + 1800) { Blend = true});
                    break;
                case Spell.ButcherFlyAxe:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Butcher];
                    DrawFrame = 685;
                    FrameInterval = 100;
                    FrameCount = 5;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Butcher], 680, 5, 1000, CurrentLocation) { Blend = true });
                    break;
                case Spell.RiklebitesRollCall:           
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Riklebites], 1102, 10, 2000, CurrentLocation) { Blend = true, DrawBehind = true });
                    break;
                case Spell.RiklebitesBlast:
                    DrawColour = Color.FromArgb(200, 200, 200, 200);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Riklebites];
                    DrawFrame = 1067;
                    FrameInterval = 200;
                    FrameCount = 6;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    DrawBehind = false;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Riklebites], 1073, 11, 1100, CurrentLocation) { Blend = true, DrawBehind = true });
                    break;
                case Spell.SwordFormation:
                    DrawColour = Color.FromArgb(180, 200, 200, 160);
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon550N];
                    DrawFrame = 450;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    DrawBehind = true;
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon550N], 460, 8, 800, CurrentLocation, CMain.Time + 1000) { Blend = true});
                    break;
                case Spell.Mon564NWhirlwind:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon564N];
                    DrawColour = Color.FromArgb(180, 255, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 916;
                    FrameInterval = 100;
                    FrameCount = 20;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    break;
                case Spell.Mon570NRupture:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon570N];
                    DrawColour = Color.FromArgb(180, 255, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 573 + (int)Direction * 10;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Blend = true;
                    Repeat = false;
                    DrawBehind = true;
                    SoundManager.PlaySound(8306);
                    break;
                case Spell.Mon570NLightningCloud:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon570N];
                    DrawColour = Color.FromArgb(180, 200, 200, 180);
                    LightColour = Color.White;
                    Light = 1;
                    DrawFrame = 544;
                    FrameInterval = 100;
                    FrameCount = 16;
                    Blend = true;
                    Repeat = false;
                    DrawBehind = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon570N], 566, 8, 300, CurrentLocation, CMain.Time + 1000) { Blend = true });
                    break;
                case Spell.Mon571NFireBomb:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon571N];
                    DrawColour = Color.FromArgb(180, 200, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 574;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon571N], 584, 6, 600, CurrentLocation) { Blend = true });
                    break;
                case Spell.Mon572NFlame:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon572N];
                    DrawColour = Color.FromArgb(180, 200, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 475;
                    FrameInterval = 100;
                    FrameCount = 13;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon572N], 488, 4, 600, CurrentLocation) { Blend = true });
                    break;
                case Spell.Mon572NDarkVortex:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon572N];
                    DrawColour = Color.FromArgb(180, 200, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 492;
                    FrameInterval = 100;
                    FrameCount = 24;
                    Blend = false;
                    Repeat = true;
                    DrawBehind = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon572N], 516, 7, 600, CurrentLocation) { Blend = false });
                    break;
                case Spell.Mon573NBigCobweb:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon573N];
                    DrawColour = Color.FromArgb(180, 255, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 535;
                    FrameInterval = 100;
                    FrameCount = 5;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon573N], 539, 4, 1000, CurrentLocation) { Blend = true });
                    break;
                case Spell.Mon609NBomb:
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Mon609N];
                    DrawColour = Color.FromArgb(180, 255, 200, 180);
                    LightColour = Color.White;
                    Light = 3;
                    DrawFrame = 584;
                    FrameInterval = 100;
                    FrameCount = 13;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.Mon609N], 597, 8, 1000, CurrentLocation) { Blend = true });
                    break;
            }

            NextMotion = CMain.Time + FrameInterval;
            NextMotion -= NextMotion % 100;
        }

        public override void Process()
        {
            if (CMain.Time >= NextMotion)
            {
                if (++FrameIndex >= FrameCount && Repeat)
                {
                    //循环回绕：配置了 LoopStart 的特效（如毒雾云）回到稳定段起始帧，
                    //而不是第 0 帧，避免起手动画反复重播、特效中途"消失重来"。
                    FrameIndex = (LoopStart >= 0 && LoopStart < FrameCount) ? LoopStart : 0;
                    Ended = true;
                }

                //循环段可用独立的帧间隔（LoopInterval），让稳定段的波动更从容
                int interval = (LoopStart >= 0 && FrameIndex >= LoopStart && LoopInterval > 0)
                    ? LoopInterval : FrameInterval;

                NextMotion = CMain.Time + interval;

                switch (Spell)
                {
                    case Spell.AbsorbFlame:          //吸魔炎风：每圈循环开始时响一次旋风音效（起手那次在 Load 里已播过）
                    case Spell.AbsorbFlameRare:
                        if (FrameIndex == LoopStart) SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                        break;
                    case Spell.SkyThunderShot:       //落雷杀：每圈播放一次雷电术的音效（M36-0，与雷电术完全同一个）
                    case Spell.SkyThunderShotRare:   //落雷杀-秘笈：同上
                        if (FrameIndex == 0) SoundManager.PlaySound(20000 + (ushort)Spell.ThunderBolt * 10);
                        break;
                    case Spell.TucsonGeneralRock:
                        if (FrameIndex == 10) SoundManager.PlaySound(8305);
                        break;
                    case Spell.HornedSorcererDustTornado:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.ButcherFlyAxe:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.HornedCommanderRockSpike:
                        if (Ended)
                        {
                            DrawFrame = 1366;
                            FrameCount = 1;
                            FrameIndex = 0;
                        }
                        break;
                    case Spell.Mon564NWhirlwind:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon570NRupture:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon570NLightningCloud:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon571NFireBomb:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon572NFlame:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon572NDarkVortex:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon573NBigCobweb:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.Mon609NBomb:
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                }
            }

            DrawLocation = new Point((CurrentLocation.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth, (CurrentLocation.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(GlobalDisplayLocationOffset);
            DrawLocation.Offset(User.OffSetMove);
        }

        public override void Draw()
        {
            if (FrameIndex >= FrameCount && !Repeat) return;
            if (BodyLibrary == null) return;

            int frame = FrameList != null && FrameIndex >= 0 && FrameIndex < FrameList.Length
                ? FrameList[FrameIndex]
                : DrawFrame + FrameIndex;

            if (Blend)
            {
                BodyLibrary.DrawBlend(
                    frame,
                    AnimationOffset == default ? DrawLocation : GetDrawWithOffset(),
                    DrawColour, true,
                    0.8F);
            }
            else
            {
                BodyLibrary.Draw(frame,
                    AnimationOffset == default ? DrawLocation : GetDrawWithOffset(),
                    DrawColour,
                    true);
            }
        }

        public override bool MouseOver(Point p)
        {
            return false;
        }

        public override void DrawBehindEffects(bool effectsEnabled)
        {
        }

        public override void DrawEffects(bool effectsEnabled)
        { 
        }

        private Point GetDrawWithOffset()
        {
            Point newDrawLocation = new (
                (CurrentLocation.X + AnimationOffset.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                (CurrentLocation.Y + AnimationOffset.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);

            newDrawLocation.Offset(GlobalDisplayLocationOffset);
            newDrawLocation.Offset(User.OffSetMove);

            return newDrawLocation;
        }
    }
}
