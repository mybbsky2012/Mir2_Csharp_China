using System.Drawing;
﻿using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;
using Server.MirObjects.Monsters;

namespace Server.MirObjects
{
    public class HeroObject : HumanObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Hero; }
        }

        public override MirConnection Connection
        {
            get { return connection; }
            set { throw new NotSupportedException(); }
        }

        public HeroInfo HInfo;
        public new PlayerObject Owner;
        public override int PotionBeltMinimum => 0;
        public override int PotionBeltMaximum => 2;
        public override int AmuletBeltMinimum => 1;
        public override int AmuletBeltMaximum => 2;
        public override int BeltSize => 2;

        /// <summary>英雄跟随主人的超负重开关：主人开启后英雄同样不受负重限制</summary>
        public override bool IgnoreWeight => OverWeight || (Owner != null && Owner.OverWeight);

        /// <summary>离线挂机免扣守护石电量（10-26）：主人离线挂机时，英雄也一起免损耗。</summary>
        public override bool AfkFreeConsumableDura => Owner != null && Owner.OfflineAFK && Settings.OfflineAfkEnabled && Settings.OfflineAfkNoConsumableDura;

        /// <summary>离线挂机免扣装备持久（10-26）：主人离线挂机时，英雄装备也不磨损。</summary>
        public override bool AfkFreeEquipDura => Owner != null && Owner.OfflineAFK && Settings.OfflineAfkEnabled && Settings.OfflineAfkNoEquipDura;

        public override bool CanMove
        {
            get
            {
                return base.CanMove && !ActiveBlizzard && !ActiveReincarnation && (Owner.Info.HeroBehaviour == HeroBehaviour.跟随 || Owner.Info.HeroBehaviour == HeroBehaviour.跑回 || Owner.Info.HeroBehaviour == HeroBehaviour.攻击 || Owner.Info.HeroBehaviour == HeroBehaviour.自定 || Owner.Info.HeroBehaviour == HeroBehaviour.反击);
            }
        }

        public override bool CanWalk
        {
            get
            {
                return base.CanWalk && !ActiveBlizzard && !ActiveReincarnation && (Owner.Info.HeroBehaviour == HeroBehaviour.跟随 || Owner.Info.HeroBehaviour == HeroBehaviour.跑回 || Owner.Info.HeroBehaviour == HeroBehaviour.攻击 || Owner.Info.HeroBehaviour == HeroBehaviour.自定 || Owner.Info.HeroBehaviour == HeroBehaviour.反击);
            }
        }
        public override bool CanRun
        {
            get
            {
                return base.CanRun && !ActiveBlizzard && !ActiveReincarnation && (Owner.Info.HeroBehaviour == HeroBehaviour.跟随 || Owner.Info.HeroBehaviour == HeroBehaviour.跑回 || Owner.Info.HeroBehaviour == HeroBehaviour.攻击 || Owner.Info.HeroBehaviour == HeroBehaviour.自定 || Owner.Info.HeroBehaviour == HeroBehaviour.反击);
            }
        }
        public override bool CanAttack
        {
            get
            {
                return base.CanAttack && !ActiveBlizzard && !ActiveReincarnation && (Owner.Info.HeroBehaviour == HeroBehaviour.守护 || Owner.Info.HeroBehaviour == HeroBehaviour.攻击 || Owner.Info.HeroBehaviour == HeroBehaviour.自定 || Owner.Info.HeroBehaviour == HeroBehaviour.反击);
            }
        }
        protected override bool CanCast
        {
            get
            {
                return base.CanCast && !ActiveBlizzard && !ActiveReincarnation && (Owner.Info.HeroBehaviour == HeroBehaviour.守护 || Owner.Info.HeroBehaviour == HeroBehaviour.攻击 || Owner.Info.HeroBehaviour == HeroBehaviour.自定 || Owner.Info.HeroBehaviour == HeroBehaviour.反击);
            }
        }

        public const int SearchDelay = 3000, ViewRange = 8, RoamDelay = 1000, RevivalDelay = 2000, AutoPotDelay = 1000, HeroRecallDelay = 180000, SupplySearchDelay = 2000;
        public long RoamTime, AutoPotTime, HeroRecallTime, SupplyTime;

        //＝＝＝智能战斗层状态（[Hero] SmartAI，10-24）＝＝＝
        private Point _smartLastLocation;   // 卡位检测：上次采样位置
        private int _stuckTicks;            // 连续原地没动的采样次数
        private long _nextStuckCheck;       // 下次卡位采样时间
        private long _nextDodgeMove;        // 下次躲避走位时间（风筝节拍）
        private long _nextPushTime;         // 下次「击退脱围」可用时间
        private long _nextMicroMove;        // 下次攻击间隙微走位时间
        protected bool _kiting;             // 残血风筝中：够不着目标就不硬贴
        private int _escapeFails;           // 被围死脱困：连续「切不开口子」的次数
        private long _nextEscapeTry;        // 被围死脱困：下次尝试切口子的时间（间隙里照常单体反击）
        private long _nextBreakoutTeleport; // 被围死脱困：瞬移回主人身边的冷却

        public override long BrownTime
        {
            get { return Owner.BrownTime; }
            set { brownTime = value; }
        }
        public byte Grade
        {
            get { return HInfo.Grade; }
            set { HInfo.Grade = value; }
        }
        public bool AutoPot
        {
            get { return HInfo.AutoPot; }
            set { HInfo.AutoPot = value; }
        }

        public byte AutoHPPercent
        {
            get { return HInfo.AutoHPPercent; }
            set { HInfo.AutoHPPercent = value; }
        }

        public byte AutoMPPercent
        {
            get { return HInfo.AutoMPPercent; }
            set { HInfo.AutoMPPercent = value; }
        }

        public int HPItemIndex
        {
            get { return HInfo.HPItemIndex; }
            set { HInfo.HPItemIndex = value; }
        }

        public int MPItemIndex
        {
            get { return HInfo.MPItemIndex; }
            set { HInfo.MPItemIndex = value; }
        }

        protected Spell NextMagicSpell;
        protected MirDirection NextMagicDirection;
        protected uint NextMagicTargetID;
        protected Point NextMagicLocation;
        /// <summary>排队施法的入队时间：超过 5 秒还没放出去就丢掉，避免 CanCast 抖动把队列卡死。</summary>
        protected long NextMagicSetTime;

        //＝＝＝施法失败保护（10-31）＝＝＝
        //背景：AI 把技能排进 NextMagicSpell 后，ProcessAI 会直接 return，等 ProcessTarget 真正去放。
        //      如果那个技能「排得出来但放不出来」（服务端没实现/缺弹药/缺元素珠/距离不合格），
        //      NextMagicSpell 被清掉、magic.CastTime 却没更新，下一拍 AI 又会挑中同一个技能 ——
        //      无限循环，英雄从此一箭不放（弓手学「精神状态」时就是这么废掉的）。
        //对策：排出去的技能下一拍回头核对一次，没真正放出去就禁止 3 秒，让 AI 换别的技能。
        private Spell _pendingSpell = Spell.None;
        private long _pendingAt;
        private readonly Dictionary<Spell, long> _castFailUntil = new Dictionary<Spell, long>();
        /// <summary>伏击型 buff（烈火身/月影术）下一次允许补放的时间。</summary>
        private readonly Dictionary<Spell, long> _ambushNext = new Dictionary<Spell, long>();
        /// <summary>
        /// 同档轮换游标：记录每一类上一次真正放出去的是哪本技能。
        /// 弓手的 无我闪/天日闪 威力相当，只按评分排序的话天日闪永远排第二、一次都放不出来，
        /// 所以这两本按「上一发不是哪本，这一发就换哪本」交替使用（见 TryCast 的 rotate 参数）。
        /// </summary>
        private readonly Dictionary<HeroSpellKind, Spell> _lastRotated = new Dictionary<HeroSpellKind, Spell>();
        /// <summary>
        /// 同档轮换的共用冷却：两本技能各自记 CastTime 的话，「这本在冷却就换那本」等于把施法间隔
        /// 砍半（输出直接翻倍）。这里再按「上一次真正放出去的时间 + 那本的施法延迟」统一闸一次，
        /// 保证轮换只改变放哪本、不改变放多快。
        /// </summary>
        private readonly Dictionary<HeroSpellKind, long> _rotateReady = new Dictionary<HeroSpellKind, long>();

        protected int TargetDistance;
        public override GuildObject MyGuild
        {
            get { return Owner.MyGuild; }
            set { throw new NotSupportedException(); }
        }

        public override MapObject Master
        {
            get { return Owner; }
            set { Owner = (PlayerObject)value; }
        }

        public HeroObject(CharacterInfo info, PlayerObject owner)
        {
            CheckCellTime = false;

            NoRunUp = true;   // 英雄免助跑：无需先走一步即可直接奔跑（否则打几秒怪计数器清零后只会走路）

            Owner = owner;

            base.Report = owner.Report;

            Load(info, null);           
        }

        protected override void Load(CharacterInfo info, MirConnection connection)
        {
            info.Mount = new MountInfo(this);

            Info = info;
            HInfo = (HeroInfo)info;

            Stats = new Stats();            

            if (Level == 0) NewCharacter();

            RefreshStats();
            SendInfo();

            switch (HP)
            {
                case 0:
                    Dead = true;
                    break;
                case -1:
                    SetHP(Stats[Stat.HP]);
                    SetMP(Stats[Stat.MP]);
                    break;
            }
        }
        protected override void NewCharacter()
        {
            base.NewCharacter();
            Grade = (byte)Envir.Random.Next(4);
        }

        /// <summary>英雄没有自己的网络连接（Connection 恒为 null），客户端物品校验统一转发给主人，否则一捡东西就 NullReferenceException。</summary>
        public override void CheckItemInfo(ItemInfo info, bool dontLoop = false)
        {
            if (Owner != null) Owner.CheckItemInfo(info, dontLoop);
        }

        public override void CheckItem(UserItem item)
        {
            if (Owner != null) Owner.CheckItem(item);
        }

        public override void Enqueue(Packet p)        {
            if (p == null) return;

            switch ((ServerPacketIds)p.Index)
            {
                case ServerPacketIds.AddBuff:
                case ServerPacketIds.RemoveBuff:
                case ServerPacketIds.PauseBuff:
                case ServerPacketIds.MagicDelay:
                case ServerPacketIds.MagicLeveled:
                case ServerPacketIds.DeleteItem:
                case ServerPacketIds.UseItem:
                    Owner.Enqueue(p);
                    break;
            }
        }

        public override void RefreshNameColour()
        {
            Color colour = Color.MediumOrchid;

            if (colour == NameColour) return;

            NameColour = colour;
            if ((Owner.MyGuild == null) || (!Owner.MyGuild.IsAtWar()))
                Enqueue(new S.ColourChanged { NameColour = NameColour });

            BroadcastColourChange();
        }

        public void Spawn(Map map, Point p)
        {
            CurrentLocation = p;
            map.AddObject(this);
            CurrentMap = map;            
            Envir.Heroes.Add(this);
            Spawned();
        }

        public void Despawn(bool cleanup)
        {
            Despawn();

            if (cleanup)
                CleanUp();
        }

        public override void Despawn()
        {
            if (Node != null)
            {
                Envir.Heroes.Remove(this);
                CurrentMap.RemoveObject(this);

                for (int i = Buffs.Count - 1; i >= 0; i--)
                {
                    var buff = Buffs[i];
                    buff.Caster = null;
                    buff.ObjectID = 0;

                    if (buff.Properties.HasFlag(BuffProperty.RemoveOnExit))
                    {
                        Buffs.RemoveAt(i);
                    }
                }

                base.Despawn();
            }            
        }

        protected override void CleanUp()
        {
            Owner = null;
            Info = null;
        }

        public override void Spawned()
        {
            base.Spawned();

            BroadcastHealthChange();
            BroadcastManaChange();
        }

        protected virtual void GetItemInfo()
        {
            UserItem item;
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                item = Info.Inventory[i];
                if (item == null) continue;

                Owner.CheckItem(item);
            }

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                item = Info.Equipment[i];

                if (item == null) continue;

                Owner.CheckItem(item);
            }

            if (HPItemIndex > 0)
                Owner.CheckItemInfo(Envir.GetItemInfo(HPItemIndex));
            if (MPItemIndex > 0)
                Owner.CheckItemInfo(Envir.GetItemInfo(MPItemIndex));
        }
        public override void SendMagicInfo(UserMagic magic)
        {
            Owner.Enqueue(magic.GetInfo(true));
        }
        protected bool CanUseMagic(UserMagic magic)
        {
            if (magic == null) return false;
            if (MagicCost(magic) > MP) return false;

            // 自主施法开关（Setup.ini [Hero] AutoSkill，默认开）：
            //   开 = 不要求玩家给技能设快捷键，英雄按职业 AI 自主选技能（远攻/近攻/群攻/自保增益）；
            //   关 = 恢复原版行为：只自动释放玩家设过快捷键（Key > 0）的技能。
            if (!Settings.HeroAutoSkill && magic.Key <= 0) return false;

            return true;
        }
        protected bool HasMagic(Spell spell) => Info.Magics.Any(x => x.Spell == spell);
        public override bool TryMagic()
        {
            return true;
        }
        public override void BeginMagic(Spell spell, MirDirection dir, uint targetID, Point location, bool spellTargetLock = false)
        {
            NextMagicSpell = spell;
            NextMagicDirection = dir;
            NextMagicTargetID = targetID;
            NextMagicLocation = location;
            NextMagicSetTime = Envir.Time;
        }
        public override MapObject DefaultMagicTarget => Owner;
        public override void UseItem(ulong id)
        {
            S.UseItem p = new S.UseItem { UniqueID = id, Grid = MirGridType.HeroInventory, Success = false };

            UserItem item = null;
            int index = -1;

            if (Owner.Hero != null && Owner.Hero.Dead) return;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                item = Info.Inventory[i];
                if (item == null || item.UniqueID != id) continue;
                index = i;
                break;
            }

            if (item == null || index == -1 || !CanUseItem(item))
            {
                Owner.Enqueue(p);
                return;
            }

            if (Dead && !(item.Info.Type == ItemType.卷轴 && item.Info.Shape == 6))
            {
                Owner.Enqueue(p);
                return;
            }

            switch (item.Info.Type)
            {
                case ItemType.药水:
                    switch (item.Info.Shape)
                    {
                        case 0: //NormalPotion
                            PotHealthAmount = (ushort)Math.Min(ushort.MaxValue, PotHealthAmount + item.Info.Stats[Stat.HP]);
                            PotManaAmount = (ushort)Math.Min(ushort.MaxValue, PotManaAmount + item.Info.Stats[Stat.MP]);
                            break;
                        case 1: //SunPotion
                            ChangeHP(item.Info.Stats[Stat.HP]);
                            ChangeMP(item.Info.Stats[Stat.MP]);
                            break;
                        case 2: //MysteryWater
                            if (UnlockCurse)
                            {
                                ReceiveChat("诅咒已被解除", ChatType.Hint);
                                Owner.Enqueue(p);
                                return;
                            }
                            ReceiveChat("诅咒将被解除", ChatType.Hint);
                            UnlockCurse = true;
                            break;
                        case 3: //Buff
                            {
                                int time = item.Info.Durability;

                                if (item.GetTotal(Stat.MaxDC) > 0)
                                    AddBuff(BuffType.攻击力提升, this, time * Settings.Minute, new Stats { [Stat.MaxDC] = item.GetTotal(Stat.MaxDC) });

                                if (item.GetTotal(Stat.MaxMC) > 0)
                                    AddBuff(BuffType.魔法力提升, this, time * Settings.Minute, new Stats { [Stat.MaxMC] = item.GetTotal(Stat.MaxMC) });

                                if (item.GetTotal(Stat.MaxSC) > 0)
                                    AddBuff(BuffType.道术力提升, this, time * Settings.Minute, new Stats { [Stat.MaxSC] = item.GetTotal(Stat.MaxSC) });

                                if (item.GetTotal(Stat.攻击速度) > 0)
                                    AddBuff(BuffType.攻击速度提升, this, time * Settings.Minute, new Stats { [Stat.攻击速度] = item.GetTotal(Stat.攻击速度) });

                                if (item.GetTotal(Stat.HP) > 0)
                                    AddBuff(BuffType.生命值提升, this, time * Settings.Minute, new Stats { [Stat.HP] = item.GetTotal(Stat.HP) });

                                if (item.GetTotal(Stat.MP) > 0)
                                    AddBuff(BuffType.法力值提升, this, time * Settings.Minute, new Stats { [Stat.MP] = item.GetTotal(Stat.MP) });

                                if (item.GetTotal(Stat.MaxAC) > 0)
                                    AddBuff(BuffType.防御提升, this, time * Settings.Minute, new Stats { [Stat.MaxAC] = item.GetTotal(Stat.MaxAC) });

                                if (item.GetTotal(Stat.MaxMAC) > 0)
                                    AddBuff(BuffType.魔法防御提升, this, time * Settings.Minute, new Stats { [Stat.MaxMAC] = item.GetTotal(Stat.MaxMAC) });

                                if (item.GetTotal(Stat.背包负重) > 0)
                                    AddBuff(BuffType.背包负重提升, this, time * Settings.Minute, new Stats { [Stat.背包负重] = item.GetTotal(Stat.背包负重) });

                                if (item.GetTotal(Stat.准确) > 0)
                                    AddBuff(BuffType.准确命中提升, this, time * Settings.Minute, new Stats { [Stat.准确] = item.GetTotal(Stat.准确) });

                                if (item.GetTotal(Stat.敏捷) > 0)
                                    AddBuff(BuffType.敏捷躲避提升, this, time * Settings.Minute, new Stats { [Stat.敏捷] = item.GetTotal(Stat.敏捷) });
                            }
                            break;
                        case 4: //Exp
                            {
                                int time = item.Info.Durability;
                                AddBuff(BuffType.获取经验提升, this, Settings.Minute * time, new Stats { [Stat.经验增长数率] = item.GetTotal(Stat.幸运) });
                            }
                            break;
                        case 5: //Drop
                            {
                                int time = item.Info.Durability;
                                AddBuff(BuffType.物品掉落提升, this, Settings.Minute * time, new Stats { [Stat.物品掉落数率] = item.GetTotal(Stat.幸运) });
                            }
                            break;
                        case 6:
                            PotHealthAmount = (ushort)Math.Min(ushort.MaxValue, PotHealthAmount + (Stats[Stat.HP] / 100) * (item.Info.Stats[Stat.生命值数率]));
                            PotManaAmount = (ushort)Math.Min(ushort.MaxValue, PotManaAmount + (Stats[Stat.MP] / 100) * (item.Info.Stats[Stat.法力值数率]));
                            break;
                        case 7:
                            ChangeHP((Stats[Stat.HP] / 100) * (item.Info.Stats[Stat.生命值数率]));
                            ChangeMP((Stats[Stat.MP] / 100) * (item.Info.Stats[Stat.法力值数率]));
                            break;
                        case 8:
                            {
                                int time = item.Info.Durability;
                                AddBuff(BuffType.技能经验提升, this, Settings.Minute * time, new Stats { [Stat.技能熟练度倍率] = 2 });
                            }
                            break;
                    }
                    break;
                case ItemType.卷轴:
                    UserItem temp;
                    switch (item.Info.Shape)
                    {
                        case 3: //BenedictionOil
                            if (!TryLuckWeapon())
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            break;
                        case 22: //SuperBenedictionOil 超级祝福油：必成、不诅咒、上限10
                            if (!TryLuckWeapon(10, true))
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            break;
                        case 4: //RepairOil
                            temp = Info.Equipment[(int)EquipmentSlot.武器];
                            if (temp == null || temp.MaxDura == temp.CurrentDura)
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            if (temp.Info.Bind.HasFlag(BindMode.DontRepair))
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            temp.MaxDura = (ushort)Math.Max(0, temp.MaxDura - Math.Min(5000, temp.MaxDura - temp.CurrentDura) / 30);

                            temp.CurrentDura = (ushort)Math.Min(temp.MaxDura, temp.CurrentDura + 5000);
                            temp.DuraChanged = false;

                            ReceiveChat("英雄的武器得到部分修复", ChatType.Hint);
                            Owner.Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                            break;
                        case 5: //WarGodOil
                            temp = Info.Equipment[(int)EquipmentSlot.武器];
                            if (temp == null || temp.MaxDura == temp.CurrentDura)
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            if (temp.Info.Bind.HasFlag(BindMode.DontRepair) || (temp.Info.Bind.HasFlag(BindMode.NoSRepair)))
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            temp.CurrentDura = temp.MaxDura;
                            temp.DuraChanged = false;

                            ReceiveChat("英雄的武器得到完全修复", ChatType.Hint);
                            Owner.Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                            break;
                        case 23: //RepairAllOil 全身修复神水：修满英雄全身已装备物品，不损持久上限
                            if (!RepairAllEquipment())
                            {
                                Owner.Enqueue(p);
                                return;
                            }
                            break;
                        case 6: //ResurrectionScroll
                            if (CurrentMap.Info.NoReincarnation)
                            {
                                ReceiveChat(string.Format("当前地图禁用复活"), ChatType.System);
                                Owner.Enqueue(p);
                                return;
                            }
                            if (Dead)
                            {
                                MP = Stats[Stat.MP];
                                Revive(MaxHealth, true);
                            }
                            break;
                    }
                    break;
                case ItemType.技能书:
                    UserMagic magic = new UserMagic((Spell)item.Info.Shape);

                    if (magic.Info == null)
                    {
                        Owner.Enqueue(p);
                        return;
                    }

                    Info.Magics.Add(magic);
                    SendMagicInfo(magic);
                    RefreshStats();
                    break;
                case ItemType.坐骑食物:
                    temp = Info.Equipment[(int)EquipmentSlot.坐骑];
                    if (temp == null || temp.MaxDura == temp.CurrentDura)
                    {
                        Owner.Enqueue(p);
                        return;
                    }

                    switch (item.Info.Shape)
                    {
                        case 0:
                            temp.MaxDura = (ushort)Math.Max(0, temp.MaxDura - Math.Min(1000, temp.MaxDura - (temp.CurrentDura / 30)));
                            break;
                        case 1:
                            break;
                    }

                    temp.CurrentDura = (ushort)Math.Min(temp.MaxDura, temp.CurrentDura + item.CurrentDura);
                    temp.DuraChanged = false;

                    ReceiveChat("英雄的坐骑已经吃饱", ChatType.Hint);
                    Owner.Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });

                    RefreshStats();
                    break;                
                case ItemType.外形物品: //Transforms
                    {
                        AddBuff(BuffType.变形效果, this, (Settings.Second * item.Info.Durability), new Stats(), values: item.Info.Shape);
                    }
                    break;
                case ItemType.装饰:

                    DecoObject decoOb = new DecoObject
                    {
                        Image = item.Info.Shape,
                        CurrentMap = CurrentMap,
                        CurrentLocation = CurrentLocation,
                    };

                    CurrentMap.AddObject(decoOb);
                    decoOb.Spawned();

                    Owner.Enqueue(decoOb.GetInfo());

                    break;
                case ItemType.怪物蛋:

                    var monsterID = item.Info.Stats[Stat.HP];
                    var spawnAsPet = item.Info.Shape == 1;
                    var conquestOnly = item.Info.Shape == 2;

                    var monsterInfo = Envir.GetMonsterInfo(monsterID);
                    if (monsterInfo == null) break;

                    MonsterObject monster = MonsterObject.GetMonster(monsterInfo);
                    if (monster == null) break;

                    if (spawnAsPet)
                    {
                        if (Pets.Count(t => !t.Dead && t.Race != ObjectType.Creature) >= Globals.MaxPets)
                        {
                            ReceiveChat("宠物数量已达到上限", ChatType.Hint);
                            Owner.Enqueue(p);
                            return;
                        }

                        monster.Master = this;
                        monster.PetLevel = 0;
                        monster.MaxPetLevel = 7;
                    }

                    if (conquestOnly)
                    {
                        var con = CurrentMap.GetConquest(CurrentLocation);
                        if (con == null)
                        {
                            ReceiveChat(string.Format("{0} 只能在攻城战期间召唤", monsterInfo.GameName), ChatType.Hint);
                            Owner.Enqueue(p);
                            return;
                        }
                    }

                    monster.Direction = Direction;
                    monster.ActionTime = Envir.Time + 5000;

                    //★11-23：先落地、后入列表（对齐 PlayerObject 载入时的写法）——
                    //  两个落点都不可走时 Spawn 返回 false，先入 Pets 就是僵尸宠物；
                    //  且原来 conquestOnly 提前 return 时宠物已进 Pets，同样产生僵尸。
                    if (!monster.Spawn(CurrentMap, Front) && !monster.Spawn(CurrentMap, CurrentLocation))
                    {
                        if (spawnAsPet) monster.Master = null;   // 没落地，不进 Pets
                    }
                    else if (spawnAsPet)
                    {
                        Pets.Add(monster);
                    }
                    break;
                case ItemType.攻城弹药:
                    //TODO;
                    break;
                case ItemType.特殊消耗品:
                    //★10-26：修复神水（Shape=84）在英雄侧也必须修「英雄自己的装备」。
                    //原先直接落到 CallDefaultNPC(UseItem,84)，本服没有 84 号脚本页 → 白扣物品、什么都不修。
                    if (item.Info.Shape == 84)
                    {
                        if (!RepairAllEquipment())
                        {
                            Owner.Enqueue(p);
                            return;
                        }
                        break;
                    }

                    //★10-25 补：特殊消耗品 = 脚本触发类道具（对应 NPC 脚本 UseItem(Shape) 页）。
                    //原实现英雄侧没这个分支 → 落 default: return，静默失败且连失败包都不回（客户端格子卡住）。
                    //英雄没有自己的 NPC 脚本上下文，统一由主人触发，与玩家使用同一行为；
                    //byHero=true → 脚本里 CHECKHEROUSE 1 成立，可区分是谁用的；
                    //Effect=2 用后不消失（可重复触发的道具，如一键回收令）。
                    Owner.CallDefaultNPC(DefaultNPCType.UseItem, true, item.Info.Shape);

                    if (item.Info.Effect == 2)
                    {
                        Owner.Enqueue(p);   // Success 保持 false，客户端不减数量
                        return;
                    }
                    break;
                default:
                    return;
            }

            if (item.Count > 1) item.Count--;
            else Info.Inventory[index] = null;
            RefreshBagWeight();

            Report?.ItemChanged(item, 1, 1);

            p.Success = true;
            Owner.Enqueue(p);
        }

        /// <summary>
        /// ★全身修复（10-26）：把英雄身上所有「持久有损」的装备直接修满，不损耗持久上限。
        /// 供「卷轴 Shape=23（全身修复神水）」和「特殊消耗品 Shape=84（修复神水）」在英雄侧共用。
        /// 与玩家侧同规则：DontRepair 一律不修，NoSRepair 只能用普通修理（这里按特修处理，故也不修）。
        /// 返回 false 表示一件可修装备都没有（调用方据此不消耗物品）。
        /// </summary>
        public bool RepairAllEquipment()
        {
            List<UserItem> fixedItems = new List<UserItem>();

            PlayerObject.RepairAllEquipmentCore(Info.Equipment, fixedItems);

            if (fixedItems.Count == 0)
            {
                ReceiveChat("英雄身上没有需要修复的装备", ChatType.Hint);
                return false;
            }

            foreach (UserItem eq in fixedItems)
                Owner.Enqueue(new S.ItemRepaired { UniqueID = eq.UniqueID, MaxDura = eq.MaxDura, CurrentDura = eq.CurrentDura });

            ReceiveChat("英雄的全身装备已完全修复", ChatType.Hint);
            return true;
        }

        public override void Die()
        {
            if (SpecialMode.HasFlag(SpecialItemMode.Revival) && Envir.Time > LastRevivalTime)
            {
                LastRevivalTime = Envir.Time + 300000;

                for (var i = (int)EquipmentSlot.左戒指; i <= (int)EquipmentSlot.右戒指; i++)
                {
                    var item = Info.Equipment[i];

                    if (item == null) continue;
                    if (!(item.Info.Unique.HasFlag(SpecialItemMode.Revival)) || item.CurrentDura < 1000) continue;
                    SetHP(Stats[Stat.HP]);
                    item.CurrentDura = (ushort)(item.CurrentDura - 1000);
                    Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                    RefreshStats();
                    ReceiveChat("复活之力使你的英雄从死亡中获得的重生", ChatType.System);
                    return;
                }
            }

            for (int i = Pets.Count - 1; i >= 0; i--)
            {
                if (Pets[i].Dead) continue;
                Pets[i].Die();
            }

            RemoveBuff(BuffType.魔法盾);
            RemoveBuff(BuffType.金刚术);

            if (!InSafeZone)
                DeathDrop(LastHitter);

            HP = 0;
            Dead = true;

            LogTime = Envir.Time;
            BrownTime = Envir.Time;

            Broadcast(new S.ObjectDied { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            Owner.Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.Dead });

            for (int i = 0; i < Buffs.Count; i++)
            {
                Buff buff = Buffs[i];

                if (!buff.Properties.HasFlag(BuffProperty.RemoveOnDeath)) continue;

                RemoveBuff(buff.Type);
            }

            PoisonList.Clear();
            InTrapRock = false;
        }

        public override void Revive(int hp, bool effect)
        {
            if (!Dead) return;

            SetHP(hp);
            SetMP(Stats[Stat.MP]);

            CurrentMap.RemoveObject(this);
            Broadcast(new S.ObjectRemove { ObjectID = ObjectID });

            Dead = false;
            ActionTime = Envir.Time + RevivalDelay;

            CurrentMap.AddObject(this);
            BroadcastInfo();
            Broadcast(new S.ObjectRevived { ObjectID = ObjectID, Effect = effect });
        }

        public override void LevelUp()
        {
            base.LevelUp();

            Owner.Enqueue(new S.HeroLevelChanged { Level = Level, Experience = Experience, MaxExperience = MaxExperience });
        }
        protected override void SendHealthChanged()
        {
            Owner.Enqueue(new S.HeroHealthChanged { HP = HP, MP = MP });
            base.SendHealthChanged();
            BroadcastManaChange();
        }

        public override void BroadcastHealthChange()
        {
            byte time = Math.Min(byte.MaxValue, (byte)Math.Max(5, (RevTime - Envir.Time) / 1000));
            Packet p = new S.ObjectHealth { ObjectID = ObjectID, Percent = PercentHealth, Expire = time, CurrentHP = Health, MaxHP = MaxHealth };

            if (Envir.Time < RevTime)
            {
                CurrentMap.Broadcast(p, CurrentLocation);
                return;
            }

            Owner.Enqueue(p);

            if (Owner.GroupMembers != null)
            {
                for (int i = 0; i < Owner.GroupMembers.Count; i++)
                {
                    PlayerObject member = Owner.GroupMembers[i];

                    if (Master == member) continue;

                    if (member.CurrentMap != CurrentMap || !Functions.InRange(member.CurrentLocation, CurrentLocation, Globals.DataRange)) continue;
                    member.Enqueue(p);
                }
            }
        }

        public byte PercentMana
        {
            get { return (byte)(MP / (float)Stats[Stat.MP] * 100); }
        }

        public void BroadcastManaChange()
        {
            Packet p = new S.ObjectMana { ObjectID = ObjectID, Percent = PercentMana };
            Owner.Enqueue(p);
        }

        public override void Process(DelayedAction action)
        {
            if (action.FlaggedToRemove)
                return;

            switch (action.Type)
            {
                case DelayedType.Magic:
                    CompleteMagic(action.Params);
                    break;
                case DelayedType.Damage:
                    CompleteAttack(action.Params);
                    break;
                case DelayedType.Mine:
                    CompleteMine(action.Params);
                    break;
                case DelayedType.Poison:
                    CompletePoison(action.Params);
                    break;
                case DelayedType.DamageIndicator:
                    CompleteDamageIndicator(action.Params);
                    break;
                case DelayedType.SpellEffect:
                    CompleteSpellEffect(action.Params);
                    break;
            }
        }

        public override void Process()
        {
            base.Process();

            if (Node == null || Info == null) return;

            if (Target != null && (Target.CurrentMap != CurrentMap || !Target.IsAttackTarget(this) || !Functions.InRange(CurrentLocation, Target.CurrentLocation, Globals.DataRange)))
                Target = null;

            if ((!Functions.InRange(CurrentLocation, Owner.CurrentLocation, Globals.DataRange) || CurrentMap != Owner.CurrentMap) && CanMove)
                OwnerRecall();

            if (Dead) return;

            if (Owner.Info.HeroBehaviour == HeroBehaviour.跟随 || Owner.Info.HeroBehaviour == HeroBehaviour.跑回)
            {
                Target = null;

                if (Owner != null)
                {
                    MoveTo(Owner.Back);

                    if (Owner.Info.HeroBehaviour == HeroBehaviour.跑回)
                    {
                        Owner.Info.HeroBehaviour = HeroBehaviour.反击;
                    }
                }
            }

            if (Owner.Info.HeroBehaviour == HeroBehaviour.瞬回)
            {
                if (Envir.Time < HeroRecallTime)
                {
                    Owner.Info.HeroBehaviour = HeroBehaviour.攻击;
                    //ReceiveChat(string.Format("英雄传送再次使用 {0} 秒", (HeroRecallTime - Envir.Time) / 1000), ChatType.System);
                    return;
                }               
                else if (Owner != null)
                {
                    OwnerRecall();
                    Owner.Info.HeroBehaviour = HeroBehaviour.跟随;
                }

                HeroRecallTime = Envir.Time + HeroRecallDelay;
            };

            ProcessAutoPot();
            ProcessSupply();
            ProcessStacking();
            ProcessSearch();
            ProcessSmartCombat();
            ProcessAI();
            ProcessTarget();
            ProcessRoam();
        }

        protected void ProcessAutoPot()
        {
            if (Envir.Time < AutoPotTime) return;

            AutoPotTime = Envir.Time + AutoPotDelay;

            if (PercentHealth < AutoHPPercent && HPItemIndex > 0 && PotHealthAmount <= 0)
                TryAutoPot(HPItemIndex);

            if (PercentMana < AutoMPPercent && MPItemIndex > 0 && PotManaAmount <= 0)
                TryAutoPot(MPItemIndex);
        }

        protected void TryAutoPot(int ItemIndex)
        {
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item == null) continue;
                if (item.Info.Index != ItemIndex) continue;

                UseItem(item.UniqueID);
                return;
            }
        }

        /// <summary>
        /// 补给意识：开启自动喝药且对应药水已耗尽时，主动寻找视野内地面上的同类药水跑去拾取，补充进英雄背包。
        /// 走 A* 寻路绕过障碍（PathFinder，失败退回直线移动）；仅在无战斗目标时执行，不打断战斗；金币与不匹配的药水不捡。
        /// </summary>
        protected void ProcessSupply()
        {
            if (!AutoPot || Target != null) { SupplyPath.Clear(); SupplyGoal = Point.Empty; return; }
            if (HPItemIndex <= 0 && MPItemIndex <= 0) { SupplyGoal = Point.Empty; return; }

            bool needHP = HPItemIndex > 0 && FindInventoryItem(HPItemIndex) == null;
            bool needMP = MPItemIndex > 0 && FindInventoryItem(MPItemIndex) == null;
            if (!needHP && !needMP) { SupplyPath.Clear(); SupplyGoal = Point.Empty; return; }

            // 每 2 秒重新搜索目标并规划路径
            if (Envir.Time >= SupplyTime)
            {
                SupplyTime = Envir.Time + SupplySearchDelay;
                SupplyPath.Clear();
                SupplyGoal = Point.Empty;   // 先清旧目标，防止药被人捡走后还朝失效点走

                ItemObject supply = FindSupplyItem(needHP, needMP);
                if (supply == null) return;

                if (supply.CurrentLocation == CurrentLocation)
                {
                    if (CanGainItem(supply.Item))
                    {
                        GainItem(supply.Item);
                        CurrentMap.RemoveObject(supply);
                        supply.Despawn();

                        Owner.ReceiveChat(string.Format("{0} 拾取了 {1}", Name, supply.Item.FriendlyName), ChatType.Hint);
                    }
                    return;
                }

                SupplyGoal = supply.CurrentLocation;
                List<Point> path = PathFinder.FindPath(CurrentMap, CurrentLocation, SupplyGoal);
                if (path != null)
                    for (int i = 0; i < path.Count; i++) SupplyPath.Enqueue(path[i]);
            }

            // 每 tick 沿路径走一步；路径走完还没到（半路被堵）就直线走，等下轮重算
            if (SupplyPath.Count > 0)
            {
                Point next = SupplyPath.Dequeue();
                if (next != CurrentLocation) MoveTo(next);
            }
            else if (SupplyGoal != Point.Empty && SupplyGoal != CurrentLocation)
                MoveTo(SupplyGoal);
        }

        private readonly Queue<Point> SupplyPath = new Queue<Point>();
        private Point SupplyGoal;

        private UserItem FindInventoryItem(int itemIndex)
        {
            if (itemIndex <= 0) return null;
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null && item.Info.Index == itemIndex) return item;
            }
            return null;
        }

        /// <summary>视野内由近及远寻找可拾取的药水（无归属或归属主人/队伍成员，且是当前缺的那种）。</summary>
        private ItemObject FindSupplyItem(bool needHP, bool needMP)
        {
            for (int d = 0; d <= ViewRange; d++)
            {
                for (int y = CurrentLocation.Y - d; y <= CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = CurrentLocation.X - d; x <= CurrentLocation.X + d; x += Math.Abs(y - CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;

                        Cell cell = CurrentMap.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            if (cell.Objects[i].Race != ObjectType.Item) continue;
                            ItemObject ob = (ItemObject)cell.Objects[i];
                            if (ob.Item == null) continue; // 金币不处理
                            if (ob.Owner != null && ob.Owner != Owner &&
                                (Owner.GroupMembers == null || !Owner.GroupMembers.Contains(ob.Owner))) continue;
                            if (ob.Item.Info.Type != ItemType.药水) continue;

                            int index = ob.Item.Info.Index;
                            if ((needHP && index == HPItemIndex) || (needMP && index == MPItemIndex)) return ob;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>贴身（相邻1格内）的敌人数量，用于被围判断与群攻决策。</summary>
        protected int SurroundedCount => FindAllTargets(1, CurrentLocation).Count;

        /// <summary>
        /// 向远离威胁的方向移动（风筝/脱围走位用）。
        /// 8 个方向逐一评分：离威胁越远越好、候选落点周围敌人越少越好、可走优先，选最高分——
        /// 从「背对目标蒙眼后退」变成「看着退」，避开墙角和怪堆。全被堵时退回背向直线。
        /// </summary>
        protected void MoveAwayFrom(MapObject threat)
        {
            if (threat == null || threat.CurrentMap != CurrentMap) return;

            MirDirection awayDir = Functions.DirectionFromPoint(threat.CurrentLocation, CurrentLocation);
            Point best = Point.Empty;
            int bestScore = int.MinValue;

            for (int i = 0; i < 8; i++)
            {
                MirDirection dir = (MirDirection)i;
                Point dest = Functions.PointMove(CurrentLocation, dir, 2);

                if (!CurrentMap.ValidPoint(dest)) continue;

                int score = Functions.MaxDistance(dest, threat.CurrentLocation) * 10   // 离威胁越远越好
                          - FindAllTargets(1, dest).Count * 20;                        // 退进怪堆重罚

                if (dir == awayDir) score += 5;                                        // 与背向一致小幅加成

                if (score > bestScore)
                {
                    bestScore = score;
                    best = dest;
                }
            }

            MoveTo(best == Point.Empty
                ? Functions.PointMove(CurrentLocation, awayDir, 2)   // 8 个方向全被地形挡死：按原背向退
                : best);
        }

        //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
        // ★智能战斗层（[Hero] SmartAI，默认开）。借假人 FakePlayerBrain 的四样本事：
        //   ① 主人打谁我打谁（FindTarget 已有，不动）
        //   ② 残血会躲：血量低于 DodgePercent 转入风筝——评分式往远处走，够得着就继续出手，
        //      近战拉开距离等血线回升，不死顶
        //   ③ 被围会一个个击退：贴身敌人 ≥ PushMinSurround 时把周围敌人逐一推开（Pushed 走原生
        //      S.ObjectPushed 表现，客户端无需改动），冷却 PushCooldownMs
        //   ④ 卡住会绕：持续 ~2.5 秒原地没动就强制沿目标左右 45°/90° 绕行
        //   ⑤ 不站木头：攻击冷却间隙有小概率绕着目标侧翼挪一格，像真人一样不停找位置
        // ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
        protected virtual void ProcessSmartCombat()
        {
            if (!Settings.SmartHeroAI || Dead) { _kiting = false; _stuckTicks = 0; return; }

            // ---- ② 残血躲避（风筝） ----
            _kiting = Target != null && PercentHealth <= Settings.HeroDodgePercent;
            if (_kiting && CanMove && Envir.Time >= _nextDodgeMove)
            {
                MoveAwayFrom(Target);
                _nextDodgeMove = Envir.Time + 700 + Envir.Random.Next(500);
            }

            if (Target == null) { _stuckTicks = 0; return; }

            // ---- ③ 被围击退脱围 ----
            if (CanAttack && Envir.Time >= _nextPushTime && SurroundedCount >= Settings.HeroPushMinSurround)
            {
                List<MapObject> adjacent = FindAllTargets(1, CurrentLocation, false);
                int pushed = 0;
                for (int i = 0; i < adjacent.Count; i++)
                {
                    MapObject ob = adjacent[i];
                    if (ob.InSafeZone) continue;
                    MirDirection pushDir = Functions.DirectionFromPoint(CurrentLocation, ob.CurrentLocation);
                    if (ob.Pushed(this, pushDir, 1) > 0) pushed++;
                }

                // 推动了就给个像样的冷却；一个都推不动（等级差/被地形卡死）也别狂试
                _nextPushTime = Envir.Time + (pushed > 0
                    ? Settings.HeroPushCooldownMs + Envir.Random.Next(3000)
                    : 2000);
                return;
            }

            // ---- ④ 卡位检测（试图接近目标却原地不动） ----
            if (Envir.Time >= _nextStuckCheck)
            {
                _nextStuckCheck = Envir.Time + 600 + Envir.Random.Next(300);

                if (CanMove && !InAttackRange() && CurrentLocation == _smartLastLocation)
                    _stuckTicks++;
                else
                    _stuckTicks = 0;
                _smartLastLocation = CurrentLocation;

                if (_stuckTicks >= 4)
                {
                    TrySidestepAround();
                    _stuckTicks = 0;
                }
            }

            // ---- ⑤ 攻击间隙微走位（绕着目标找侧翼，不做木头桩） ----
            if (Envir.Time >= _nextMicroMove)
            {
                _nextMicroMove = Envir.Time + 900 + Envir.Random.Next(1000);

                if (CanMove && InAttackRange() && Envir.Random.Next(4) == 0)
                {
                    MirDirection backDir = Functions.DirectionFromPoint(Target.CurrentLocation, CurrentLocation);
                    MirDirection side = (MirDirection)(((int)backDir + (Envir.Random.Next(2) == 0 ? 1 : 7)) % 8);
                    Point flank = Functions.PointMove(Target.CurrentLocation, side, 1);
                    if (flank != CurrentLocation && CurrentMap.ValidPoint(flank))
                        MoveTo(flank);
                }
            }
        }

        /// <summary>卡位脱困：沿「朝目标」方向的左右 45°/90° 依次试走，先挪出去再说。</summary>
        protected void TrySidestepAround()
        {
            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            for (int off = 1; off <= 2; off++)
            {
                MirDirection d1 = (MirDirection)(((int)dir + off) % 8);
                MirDirection d2 = (MirDirection)(((int)dir - off + 8) % 8);

                if (Walk(d1) || Run(d1)) return;
                if (Walk(d2) || Run(d2)) return;
            }
        }

        /// <summary>
        /// 被围脱困（10-24）：修「被围死只会站桩」的老毛病。
        ///   - 还没围到 minSurround 个 → 按原节奏评分式撤退（有隙可逃就逃）；
        ///   - 围死了 → 节拍式（约 1.2 秒一次）尝试切口子，间隙里照常单体反击，不再一味白试；
        ///   - 连续 3 次都切不开 → 瞬移回主人身边（10 秒冷却），回来接着打。
        /// 返回 true = 本拍已用于脱困/瞬移（调用方直接 return）；false = 照常单体攻击。
        /// </summary>
        protected bool TryBreakout(MapObject threat, int minSurround = 3)
        {
            if (threat == null || threat.Dead || !CanMove) return false;

            if (SurroundedCount < minSurround)
            {
                _escapeFails = 0;
                MoveAwayFrom(threat);            // 还有空隙：保持原来的撤退节奏
                return true;
            }

            // 被围死：不到尝试节拍就先让位给单体反击
            if (Envir.Time < _nextEscapeTry) return false;
            _nextEscapeTry = Envir.Time + 1200 + Envir.Random.Next(400);

            Point before = CurrentLocation;
            MoveAwayFrom(threat);
            if (CurrentLocation != before)       // 切开了口子，挪出去了
            {
                _escapeFails = 0;
                return true;
            }

            _escapeFails++;
            if (_escapeFails < 3) return false;  // 这次没切开：不站桩，下一拍继续单体反击

            // 连续 3 次切不开 → 瞬移回主人身边
            _escapeFails = 0;
            if (Owner != null && !Owner.Dead && Envir.Time >= _nextBreakoutTeleport)
            {
                _nextBreakoutTeleport = Envir.Time + 10000;
                if (!Teleport(Owner.CurrentMap, Owner.Back))
                    Teleport(Owner.CurrentMap, Owner.CurrentLocation);
                MessageQueue.Instance.Enqueue("[英雄] " + Name + " 被围死无法脱身，瞬移回主人身边");
                return true;
            }

            return false;                        // 瞬移冷却中：继续就地反击
        }

        //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
        // ★技能决策层（10-28）：技能从哪来、什么时候放，统一走 HeroSpellBook
        //   ① 玩家给英雄设的技能快捷键（UserMagic.Key = 17~24，即英雄技能栏 1~8）最优先，按 1→8；
        //   ② 其余按「学习等级 + 威力/延迟」评分从高到低 —— 学了更高级的技能自动顶掉低级的；
        //   ③ 开关型（烈火/半月/狂风/双龙/天务）已经开着就绝不再点一次，也不主动关；
        //   ④ 增益/防护缺什么补什么，长 buff（金刚不坏/万效符/血龙印）不重复叠。
        // ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
        /// <param name="rotate">
        /// true = 同档轮换：上一发放过哪本，这一发就优先换一本威力相当的（弓手 无我闪 ⇄ 天日闪）。
        /// 只会和「评分不低于最强那本 90%」的技能轮换，明显更强的技能永远优先，输出不会因为换花样下降。
        /// </param>
        protected bool TryCast(HeroSpellKind kind, MapObject castTarget = null, bool rotate = false)
        {
            // 先核对上一拍排出去的技能到底放出去没有（见 _castFailUntil 的注释）
            CheckPendingCast();

            if (!CanCast) return false;

            // 轮换组共用冷却（见 _rotateReady 注释）：换着放也不能比单本技能的间隔更快
            if (rotate)
            {
                long ready;
                if (_rotateReady.TryGetValue(kind, out ready) && Envir.Time < ready) return false;
            }

            MapObject target = castTarget;
            int distance;

            switch (kind)
            {
                case HeroSpellKind.Buff:
                case HeroSpellKind.Heal:
                case HeroSpellKind.Summon:
                    if (target == null) target = this;
                    if (target.Dead) return false;
                    distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
                    break;

                default:
                    if (Target == null || Target.Dead || Target.CurrentMap != CurrentMap) return false;
                    target = Target;
                    distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
                    break;
            }

            List<UserMagic> candidates = HeroSpellBook.Rank(Info.Magics, kind, SurroundedCount, distance,
                b => target.HasBuff(b), IsToggleActive, Settings.HeroAutoSkill);

            if (rotate) ApplyRotation(kind, candidates);

            for (int i = 0; i < candidates.Count; i++)
            {
                UserMagic magic = candidates[i];
                HeroSpellProfile profile = HeroSpellBook.Get(magic.Spell);

                // 伏击型 buff（烈火身/月影术）：任何动作都会消耗掉它，战斗中补等于白扔一拍施法
                // → 英雄会「一直在放技能、就是不攻击」。只在没目标时补，且带冷却。
                if (profile != null && profile.Ambush)
                {
                    long ambushNext;
                    _ambushNext.TryGetValue(magic.Spell, out ambushNext);

                    bool inCombat = Target != null && !Target.Dead;
                    if (!HeroSpellBook.CanRefreshAmbushBuff(inCombat, Envir.Time, ambushNext)) continue;
                }

                // 子类判定「这个 buff 现在放了有没有意义」（如刺客已有活着的分身就别再放烈火身）
                if (!IsBuffWorthCasting(magic)) continue;

                if (!CanUseMagic(magic)) continue;

                // 刚放不出来的技能先晾 3 秒（否则会一直挑它、把每一拍都吃掉）
                long failUntil;
                if (_castFailUntil.TryGetValue(magic.Spell, out failUntil) && Envir.Time < failUntil) continue;

                // 自己再判一次冷却：冷却中的技能选出来也放不出去，会白占一拍
                if (Envir.Time < magic.CastTime + magic.GetDelay()) continue;

                bool cast = CastSelected(magic, target);

                if (cast)
                {
                    _lastRotated[kind] = magic.Spell;
                    _rotateReady[kind] = Envir.Time + magic.GetDelay();
                }

                if (cast && profile != null && profile.Ambush)
                    _ambushNext[magic.Spell] = Envir.Time + HeroSpellBook.AmbushCooldownMs;

                return cast;
            }

            return false;
        }

        /// <summary>
        /// 同档轮换：在「评分不低于第一名 90%」的技能里，按排名顺序循环取「上一发的下一本」。
        /// 具体规则见 <see cref="HeroSpellBook.ApplyRotation"/>。
        /// </summary>
        private void ApplyRotation(HeroSpellKind kind, List<UserMagic> candidates)
        {
            Spell last;
            if (!_lastRotated.TryGetValue(kind, out last)) return;

            HeroSpellBook.ApplyRotation(candidates, last);
        }

        /// <summary>
        /// 回查上一拍排队的技能：若「已被施法流程消费（NextMagicSpell 清空）」但 magic.CastTime 没更新，
        /// 说明 Magic() 根本没放出去（服务端无实现 / 缺弹药 / 距离不合格 / 目标没了），记 3 秒禁用。
        /// 排队后 1.5 秒内还没被消费（战斗中切目标、CanCast 抖动）不算失败，避免误伤。
        /// </summary>
        private void CheckPendingCast()
        {
            if (_pendingSpell == Spell.None) return;

            Spell spell = _pendingSpell;
            long at = _pendingAt;
            _pendingSpell = Spell.None;

            if (Envir.Time - at > 1500) return;      // 还没轮到放（没目标/暂时不能施法），不记失败

            UserMagic magic = GetMagic(spell);
            if (magic == null) return;               // 技能被删了，下一拍自然不会再选中
            if (magic.CastTime >= at) return;        // 真的放出去了

            _castFailUntil[spell] = Envir.Time + 3000;
        }

        /// <summary>
        /// 「只开不关」：没开才开，已经开着什么都不做（绝不点第二次把技能关掉）。
        /// 返回 true 表示这一拍用掉了（真的点开了）。
        /// </summary>
        protected bool ToggleIfOff(Spell spell)
        {
            if (IsToggleActive(spell)) return false;

            UserMagic magic = GetMagic(spell);
            if (magic == null || !CanUseMagic(magic)) return false;

            if (spell == Spell.FlamingSword && Envir.Time < FlamingSwordTime) return false;
            if (spell == Spell.CounterAttack && Envir.Time < CounterAttackTime) return false;
            if (Envir.Time < magic.CastTime + magic.GetDelay()) return false;

            SpellToggle(spell, SpellToggleState.True);
            return true;
        }

        /// <summary>
        /// 子类判定「这个自身 buff 现在放了有没有意义」。
        /// 返回 false 表示这一发别放（不消耗施法机会，让后面的攻击/别的技能顶上）。
        /// </summary>
        protected virtual bool IsBuffWorthCasting(UserMagic magic)
        {
            return true;
        }

        protected bool CastSelected(UserMagic magic, MapObject target)
        {
            HeroSpellProfile profile = HeroSpellBook.Get(magic.Spell);

            if (profile != null && profile.Kind == HeroSpellKind.Toggle)
            {
                SpellToggle(magic.Spell, SpellToggleState.True);
                return true;
            }

            uint targetID = target.ObjectID;
            Point location = target.CurrentLocation;
            MirDirection dir = target == this ? Direction : Functions.DirectionFromPoint(CurrentLocation, target.CurrentLocation);

            // 攻击类统一传「目标 + 目标位置」：以自身为原点的技能（地狱火/疾光电影/月华乱舞）内部只用方向，
            // 以落点为中心的技能（爆裂火焰/天霜冰环/落雷杀）需要目标位置，两边的实现都能吃这一套。
            BeginMagic(magic.Spell, dir, targetID, location);

            // 记一笔：下一拍回头核对这一发到底放出去没有（放不出去就短暂禁用，见 CheckPendingCast）
            _pendingSpell = magic.Spell;
            _pendingAt = Envir.Time;
            return true;
        }

        /// <summary>开关型技能当前是否已经开着（开着的绝不再点一次，否则就关掉了）。</summary>
        protected bool IsToggleActive(Spell spell)
        {
            switch (spell)
            {
                case Spell.Thrusting: return Info.Thrusting;
                case Spell.HalfMoon: return Info.HalfMoon;
                case Spell.CrossHalfMoon: return Info.CrossHalfMoon;
                case Spell.DoubleSlash: return Info.DoubleSlash;
                case Spell.TwinDrakeBlade: return TwinDrakeBlade;
                case Spell.FlamingSword: return FlamingSword;
                case Spell.CounterAttack: return CounterAttack;
            }

            HeroSpellProfile profile = HeroSpellBook.Get(spell);
            return profile != null && profile.Buff != BuffType.None && HasBuff(profile.Buff);
        }

        protected void ProcessAI()
        {
            // 排队超时保护：CanCast 抖动（被眩晕/换形态/切行为）时队列可能一直没人消费，
            // 原写法会让 ProcessAI 永远在这里 return，表现为「英雄突然再也不放技能」。
            if (NextMagicSpell != Spell.None && Envir.Time > NextMagicSetTime + 5000)
            {
                NextMagicSpell = Spell.None;
            }

            if (NextMagicSpell != Spell.None) return;            
            ProcessFriend();

            if (NextMagicSpell != Spell.None) return;
            ProcessAttack();
        }

        protected virtual void ProcessStacking()
        {
            Stacking = CheckStacked();

            if (CanMove && ((Owner != null && Owner.Front == CurrentLocation) || Stacking))
            {
                if (!Walk(Direction))
                {
                    MirDirection dir = Direction;

                    switch (Envir.Random.Next(3)) 
                    {
                        case 0:
                            for (int i = 0; i < 7; i++)
                            {
                                dir = Functions.NextDir(dir);

                                if (Walk(dir))
                                    break;
                            }
                            break;
                        default:
                            for (int i = 0; i < 7; i++)
                            {
                                dir = Functions.PreviousDir(dir);

                                if (Walk(dir))
                                    break;
                            }
                            break;
                    }
                }

                return;
            }
        }

        protected virtual void ProcessSearch()
        {
            if (Envir.Time < SearchTime) return;
            if (Owner.Info.HeroBehaviour == HeroBehaviour.跟随 || !Mount.CanAttack) return;

            SearchTime = Envir.Time + SearchDelay;

            if (Target == null || Envir.Random.Next(3) == 0)
                FindTarget();
        }

        protected virtual void ProcessRoam()
        {
            if (Target != null || Envir.Time < RoamTime) return;

            // 正在前往拾取补给时不拉回主人背后，避免两个移动指令互相拉扯
            if (SupplyPath.Count > 0 || SupplyGoal != Point.Empty) return;

            if (Owner != null)
            {
                MoveTo(Owner.Back);
                return;
            }

            RoamTime = Envir.Time + RoamDelay;
        }
        protected virtual void ProcessFriend() { }
        protected virtual void ProcessAttack() { }
        protected virtual void ProcessTarget()
        {
            if (CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            if (Target == null || !CanAttack) return;

            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);

            if (InAttackRange())
            {
                Attack();

                if (Target != null && Target.Dead)
                {
                    FindTarget();
                }

                return;
            }

            // 残血风筝中：够不着就不硬贴——远程英雄继续且退且打，近战英雄拉开距离等血线回升
            if (_kiting) return;

            MoveTo(Target.CurrentLocation);
        }

        protected virtual void Attack()
        {
            if (!Target.IsAttackTarget(Owner))
            {
                Target = null;
                return;
            }

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Attack(Direction, Spell.None);
        }

        protected virtual bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }

        protected List<MapObject> FindAllTargets(int dist, Point location, bool needSight = true)
        {
            List<MapObject> targets = new List<MapObject>();
            for (int d = 0; d <= dist; d++)
            {
                for (int y = location.Y - d; y <= location.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = location.X - d; x <= location.X + d; x += Math.Abs(y - location.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;

                        Cell cell = CurrentMap.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            switch (ob.Race)
                            {
                                case ObjectType.Monster:
                                case ObjectType.Player:
                                case ObjectType.Hero:
                                    if (!ob.IsAttackTarget(this)) continue;
                                    if (ob.Hidden && (!CoolEye || Level < ob.Level) && needSight) continue;
                                    if (ob.Race == ObjectType.Player)
                                    {
                                        PlayerObject player = ((PlayerObject)ob);
                                        if (player.GMGameMaster) continue;
                                    }
                                    targets.Add(ob);
                                    continue;
                                default:
                                    continue;
                            }
                        }
                    }
                }
            }
            return targets;
        }

        protected virtual void MoveTo(Point location)
        {
            if (CurrentLocation == location) return;

            bool inRange = Functions.InRange(location, CurrentLocation, 1);
            bool inRunRange = RidingMount ? Functions.InRange(location, CurrentLocation, 2) : inRange;

            if (inRange)
            {
                if (!CurrentMap.ValidPoint(location)) return;
                Cell cell = CurrentMap.GetCell(location);
                if (cell.Objects != null)
                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (!ob.Blocking) continue;
                        return;
                    }
            }

            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, location);

            // 优先尝试奔跑（与假人 StepTo 同款：Run 失败自动回落 Walk）。
            // 跑不过去（2 格终点被挡/超重/被冻等）时 Run 返回 false，自然走一格。
            if (!inRunRange && Run(dir))
                return;

            if (Walk(dir)) return;

            switch (Envir.Random.Next(2))
            {
                case 0:
                    for (int i = 0; i < 7; i++)
                    {
                        dir = Functions.NextDir(dir);

                        if (Walk(dir))
                            return;
                    }
                    break;
                default:
                    for (int i = 0; i < 7; i++)
                    {
                        dir = Functions.PreviousDir(dir);

                        if (Walk(dir))
                            return;
                    }
                    break;
            }
        }

        public void OwnerRecall()
        {
            if (Owner == null) return;

            if (Dead)
            {
                Despawn(false);
                return;
            }

            if (!Teleport(Owner.CurrentMap, Owner.Back))
                Teleport(Owner.CurrentMap, Owner.CurrentLocation);

            if (!Dead)
            {
                BroadcastManaChange();
            }
        }        

        protected virtual void FindTarget()
        {
            // 配合主人：主人正在攻击的目标就是英雄的首要目标（视野内且可攻击）
            MapObject ownerTarget = Owner.Target;
            if (ownerTarget != null && ownerTarget != Target && !ownerTarget.Dead &&
                ownerTarget.CurrentMap == CurrentMap &&
                Functions.InRange(CurrentLocation, ownerTarget.CurrentLocation, ViewRange) &&
                !(ownerTarget is TownArcher) &&
                ownerTarget.IsAttackTarget(this) &&
                !(ownerTarget.Race == ObjectType.Player && ((PlayerObject)ownerTarget).GMGameMaster))
            {
                Target = ownerTarget;
                return;
            }

            Map Current = CurrentMap;

            for (int d = 0; d <= ViewRange; d++)
            {
                for (int y = CurrentLocation.Y - d; y <= CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= Current.Height) break;

                    for (int x = CurrentLocation.X - d; x <= CurrentLocation.X + d; x += Math.Abs(y - CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= Current.Width) break;
                        Cell cell = Current.Cells[x, y];
                        if (cell.Objects == null || !cell.Valid) continue;
                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            switch (ob.Race)
                            {
                                case ObjectType.Monster:
                                case ObjectType.Hero:
                                    if (ob is TownArcher) continue;
                                    if (!ob.IsAttackTarget(Owner)) continue;
                                    if (ob.Hidden && (!CoolEye || Level < ob.Level)) continue;
                                    if (ob.Master != null && Target != ob) continue;
                                    if (Owner.Info.HeroBehaviour == HeroBehaviour.反击 && ob.Target != this && ob.Target != Owner) continue;

                                    Target = ob;
                                    return;
                                case ObjectType.Player:
                                    PlayerObject playerob = (PlayerObject)ob;
                                    if (!ob.IsAttackTarget(Owner)) continue;
                                    if (playerob.GMGameMaster || ob.Hidden && (!CoolEye || Level < ob.Level)) continue;
                                    if (Target != ob && Owner.LastHitter != ob && ob.LastHitter != Owner) continue;

                                    Target = ob;

                                    if (Owner != null)
                                    {
                                        for (int j = 0; j < playerob.Pets.Count; j++)
                                        {
                                            MonsterObject pet = playerob.Pets[j];

                                            if (!pet.IsAttackTarget(this)) continue;
                                            Target = pet;
                                            break;
                                        }
                                    }
                                    return;
                                default:
                                    continue;
                            }
                        }
                    }
                }
            }
        }
        public override bool IsAttackTarget(HumanObject attacker)
        {
            if (Owner == null) return false;
            if (Dead) return false;

            return Owner.IsAttackTarget(attacker);
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {
            if (Owner == null) return false;
            if (Dead) return false;

            return Owner.IsAttackTarget(attacker);
        }
        public override bool IsFriendlyTarget(HumanObject ally)
        {
            if (Owner == null) return false;
            return Owner.IsFriendlyTarget(ally);
        }
        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            if (Owner == null) return false;
            return Owner.IsFriendlyTarget(ally);
        }

        public override void WinExp(uint amount, uint targetLevel = 0)
        {
            Owner.WinExp(amount, targetLevel);
        }

        public override void GainExp(uint amount)
        {
            if (amount == 0) return;

            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject monster = Pets[i];
                if (monster.CurrentMap == CurrentMap && Functions.InRange(monster.CurrentLocation, CurrentLocation, Globals.DataRange) && !monster.Dead)
                    monster.PetExp(amount);
            }

            if (!CanGainExp) return;

            if (Stats[Stat.经验增长数率] > 0)
            {
                amount += (uint)Math.Max(0, (amount * Stats[Stat.经验增长数率]) / 100);
            }

            Experience += amount;

            Owner.Enqueue(new S.GainHeroExperience { Amount = amount });

            if (Experience < MaxExperience) return;
            if (Level >= ushort.MaxValue) return;

            //Calculate increased levels
            var experience = Experience;

            while (experience >= MaxExperience)
            {
                Level++;
                experience -= MaxExperience;

                RefreshLevelStats();

                if (Level >= ushort.MaxValue) break;
            }

            Experience = experience;

            LevelUp();
        }

        /// <summary>
        /// ★英雄（11-20）：调整英雄经验（NPC 命令 HEROEXP / GM 命令 @HEROEXP 共用入口）。
        /// 正数=加经验（走 GainExp 正规通道：含经验加成属性、可连升多级、客户端经验条同步）；
        /// 负数=只扣当前经验条，绝不掉级（口径 11-20 用户确认）。
        /// 返回反馈文案（由调用方用主人的 ReceiveChat 发出 —— 英雄自己的聊天包会被 Enqueue 丢弃）；
        /// 参数非法返回 null。
        /// </summary>
        public string ApplyExpDelta(long delta)
        {
            if (delta == 0) return null;

            if (delta > 0)
            {
                uint gain = delta > uint.MaxValue ? uint.MaxValue : (uint)delta;
                GainExp(gain);
                return string.Format("英雄获得 {0} 经验，当前等级 {1}", gain, Level);
            }

            long reduce = -delta;
            Experience = reduce >= Experience ? 0 : Experience - reduce;
            Owner.Enqueue(new S.HeroLevelChanged { Level = Level, Experience = Experience, MaxExperience = MaxExperience });
            return string.Format("英雄扣除 {0} 经验，剩余 {1}/{2}", reduce, Experience, MaxExperience);
        }

        /// <summary>
        /// ★英雄（11-20）：直接设定英雄等级（NPC 命令 SETHEROLEVEL / GM 现有 @LEVELHERO 同一口径）。
        /// 走 LevelUp 正规通道（RefreshStats 重算 MaxExperience/属性 + HeroLevelChanged 同步）。
        /// 返回反馈文案；等级非法返回 null。
        /// </summary>
        public string ApplySetLevel(ushort level)
        {
            if (level == 0) return null;

            ushort old = Level;
            Level = level;
            LevelUp();

            return string.Format("英雄等级 {0} -> {1}", old, Level);
        }

        private void SendInfo()
        {
            GetItemInfo();
            S.HeroInformation packet = new S.HeroInformation
            {
                ObjectID = ObjectID,
                Name = Name,
                Class = Class,
                Gender = Gender,
                Level = Level,
                Hair = Hair,

                HP = HP,
                MP = MP,

                Experience = Experience,
                MaxExperience = MaxExperience,

                Inventory = new UserItem[Info.Inventory.Length],
                Equipment = new UserItem[Info.Equipment.Length],

                AutoPot = AutoPot,
                AutoHPPercent = AutoHPPercent,
                AutoMPPercent = AutoMPPercent,
                HPItemIndex = HPItemIndex,
                MPItemIndex = MPItemIndex
            };

            for (int i = 0; i < Info.Magics.Count; i++)
                packet.Magics.Add(Info.Magics[i].CreateClientMagic());

            Info.Inventory.CopyTo(packet.Inventory, 0);
            Info.Equipment.CopyTo(packet.Equipment, 0);

            Owner.Enqueue(packet);

            SendBaseStats();
        }

        public override void SendHealth(HumanObject player)
        {
            byte time = Math.Min(byte.MaxValue, (byte)Math.Max(5, (RevTime - Envir.Time) / 1000));

            Packet p = new S.ObjectHealth { ObjectID = ObjectID, Percent = PercentHealth, Expire = time, CurrentHP = Health, MaxHP = MaxHealth };

            if (Envir.Time < RevTime)
            {
                CurrentMap.Broadcast(p, CurrentLocation);
                return;
            }

            Owner.Enqueue(p);

            if (Owner.GroupMembers != null)
            {
                for (int i = 0; i < Owner.GroupMembers.Count; i++)
                {
                    PlayerObject member = Owner.GroupMembers[i];

                    if (Master == member) continue;

                    if (member.CurrentMap != CurrentMap || !Functions.InRange(member.CurrentLocation, CurrentLocation, Globals.DataRange)) continue;
                    member.Enqueue(p);
                }
            }
        }
        protected override void SendBaseStats()
        {
            Owner.Enqueue(new S.HeroBaseStatsInfo { Stats = Settings.ClassBaseStats[(byte)Class] });
        }

        public override Packet GetInfo()
        {
            return new S.ObjectHero
            {
                ObjectID = ObjectID,
                Name = CurrentMap.Info.NoNames ? "?????" : Name,
                NameColour = NameColour,
                Class = Class,
                Gender = Gender,
                Level = Level,
                Location = CurrentLocation,
                Direction = Direction,
                Hair = Hair,
                Weapon = Looks_Weapon,
                WeaponEffect = Looks_WeaponEffect,
                Armour = Looks_Armour,

                // 装备品质：客户端据此给英雄的衣服/武器画品质轮廓光
                ArmourGrade = Info.Equipment[(int)EquipmentSlot.盔甲] != null ? (byte)Info.Equipment[(int)EquipmentSlot.盔甲].Info.Grade : (byte)0,
                WeaponGrade = Info.Equipment[(int)EquipmentSlot.武器] != null ? (byte)Info.Equipment[(int)EquipmentSlot.武器].Info.Grade : (byte)0,
                Light = Light,
                Poison = CurrentPoison,
                Dead = Dead,
                Hidden = Hidden,
                Effect = HasBuff(BuffType.魔法盾, out _) ? SpellEffect.MagicShieldUp : HasBuff(BuffType.金刚术, out _) ? SpellEffect.ElementalBarrierUp : SpellEffect.None,
                WingEffect = Looks_Wings,
                MountType = Mount.MountType,
                RidingMount = RidingMount,

                TransformType = TransformType,

                ElementOrbEffect = (uint)GetElementalOrbCount(),
                ElementOrbLvl = (uint)ElementsLevel,
                ElementOrbMax = (uint)Settings.OrbsExpList[Settings.OrbsExpList.Count - 1],

                Buffs = Buffs.Where(d => d.Info.Visible).Select(e => e.Type).ToList(),

                LevelEffects = LevelEffects,
                OwnerName = Owner.Name
            };
        }
    }
}
