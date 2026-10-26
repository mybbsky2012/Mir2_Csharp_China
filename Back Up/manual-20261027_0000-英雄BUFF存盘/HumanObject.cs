using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects.Monsters;
using System.Numerics;
using S = ServerPackets;

namespace Server.MirObjects
{
    public class HumanObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Player; }
        }

        public CharacterInfo Info;

        protected MirConnection connection;
        public virtual MirConnection Connection
        {
            get { return connection; }
            set { connection = value; }
        }
        public override string Name
        {
            get { return Info.Name; }
            set { /*Check if Name exists.*/ }
        }
        public override int CurrentMapIndex
        {
            get { return Info.CurrentMapIndex; }
            set { Info.CurrentMapIndex = value; }
        }
        public override Point CurrentLocation
        {
            get { return Info.CurrentLocation; }
            set { Info.CurrentLocation = value; }
        }
        public override MirDirection Direction
        {
            get { return Info.Direction; }
            set { Info.Direction = value; }
        }
        public override ushort Level
        {
            get { return Info.Level; }
            set { Info.Level = value; }
        }
        public override int Health
        {
            get { return HP; }
        }
        public override int MaxHealth
        {
            get { return Stats[Stat.HP]; }
        }
        public int HP
        {
            get { return Info.HP; }
            set { Info.HP = value; }
        }
        public int MP
        {
            get { return Info.MP; }
            set { Info.MP = value; }
        }
        public override AttackMode AMode
        {
            get { return Info.AMode; }
            set { Info.AMode = value; }
        }
        public override PetMode PMode
        {
            get { return Info.PMode; }
            set { Info.PMode = value; }
        }
        public long Experience
        {
            set { Info.Experience = value; }
            get { return Info.Experience; }
        }

        public long MaxExperience;

        // 「最近主动打了谁」——只在目标是玩家（真人 / 假人）时才记录。
        // 组队跟着玩家的假人靠这个字段知道「队长现在在跟谁打架」，从而上去帮忙 PK。
        // 之所以不直接用受害者的 LastHitter：那个字段会被下一发攻击覆盖（假人自己打一下就把队长的记录顶掉了）。
        public MapObject RecentPvPTarget;
        public long RecentPvPTime;

        public byte Hair
        {
            get { return Info.Hair; }
            set { Info.Hair = value; }
        }
        public MirClass Class
        {
            get { return Info.Class; }
        }
        public MirGender Gender
        {
            get { return Info.Gender; }
        }
        public override List<Buff> Buffs
        {
            get { return Info.Buffs; }
            set { Info.Buffs = value; }
        }
        public override List<Poison> PoisonList
        {
            get { return Info.Poisons; }
            set { Info.Poisons = value; }
        }

        public bool RidingMount;
        public MountInfo Mount
        {
            get { return Info.Mount; }
        }        

        public Reporting Report;
        public virtual bool CanMove
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen);
            }
        }
        public virtual bool CanWalk
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && !InTrapRock && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen);
            }
        }
        public virtual bool CanRun
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && (_stepCounter > 0 || FastRun || NoRunUp) && (!Sneaking || ActiveSwiftFeet) && (IgnoreWeight || CurrentBagWeight <= Stats[Stat.背包负重]) && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen);
            }
        }
        public virtual bool CanAttack
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && Envir.Time >= AttackTime && !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.LRParalysis) && !CurrentPoison.HasFlag(PoisonType.Frozen) && !CurrentPoison.HasFlag(PoisonType.Dazed) && Mount.CanAttack;
            }
        }
        public bool CanRegen
        {
            get
            {
                return Envir.Time >= RegenTime;
            }
        }
        protected virtual bool CanCast
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && Envir.Time >= SpellTime && !CurrentPoison.HasFlag(PoisonType.Stun) && !CurrentPoison.HasFlag(PoisonType.Dazed) &&
                    !CurrentPoison.HasFlag(PoisonType.Paralysis) && !CurrentPoison.HasFlag(PoisonType.Frozen) && Mount.CanAttack;
            }
        }

        protected bool CheckCellTime = true;

        public short TransformType;
        public short Looks_Armour = 0, Looks_Weapon = -1, Looks_WeaponEffect = 0;
        public byte Looks_Wings = 0;

        public int CurrentHandWeight,
                   CurrentWearWeight,
                   CurrentBagWeight;

        public bool HasElemental;
        public int ElementsLevel;

        public bool Stacking;
        public bool IsGM, GMNeverDie, GMGameMaster;
        public bool HasUpdatedBaseStats = true;

        //辅助开关（内挂）：由服务器校验后设置，见 PlayerObject.SetPlayerOption
        public bool NoLamp;         // 免蜡：夜晚无需照明（客户端渲染用，服务器仅记录/回执）
        public bool WalkThrough;    // 穿人：移动时忽略其他玩家/英雄/怪物/NPC 的阻挡
        public bool NoRunUp;        // 免助跑：无需先走一步即可直接奔跑
        public bool OverWeight;     // 超负重：负重超限仍可奔跑、装备不受腕力/装备负重限制
        public bool MountTai;       // 泰山：被攻击时不后仰（客户端表现，服务器仅记录/回执）

        /// <summary>
        /// 是否忽略负重限制（超负重）。自身开启即生效；英雄跟随主人的开关，见 HeroObject。
        /// </summary>
        public virtual bool IgnoreWeight => OverWeight;

        public virtual int PotionBeltMinimum => 0;
        public virtual int PotionBeltMaximum => 4;
        public virtual int AmuletBeltMinimum => 4;
        public virtual int AmuletBeltMaximum => 6;
        public virtual int BeltSize => 6;

        public LevelEffects LevelEffects = LevelEffects.None;

        public const long LoyaltyDelay = 1000, ItemExpireDelay = 60000, DuraDelay = 10000, RegenDelay = 10000, PotDelay = 200, HealDelay = 600, VampDelay = 500, MoveDelay = 600, StoneDelay = 4000;
        // 气血石 / 魔血石 每跳消耗的电量（1000 = 显示 1 点电量）。60000 电量 ≈ 600 跳 ≈ 40 分钟
        public const int StoneDuraCost = 100;
        public long StruckTime, RunTime, ActionTime, AttackTime, RegenTime, SpellTime, StackingTime, IncreaseLoyaltyTime, ItemExpireTime, TorchTime, DuraTime, PotTime, HealTime, VampTime, LogTime, DecreaseLoyaltyTime, SearchTime, StoneTime;

        protected int _stepCounter, _runCounter;

        private GuildObject myGuild = null;
        public virtual GuildObject MyGuild
        {
            get { return myGuild; }
            set { myGuild = value; }
        }
        public GuildRank MyGuildRank = null;

        public IntelligentCreatureType SummonedCreatureType = IntelligentCreatureType.None;
        public bool CreatureSummoned;

        public SpecialItemMode SpecialMode;

        public List<ItemSets> ItemSets = new List<ItemSets>();
        public List<EquipmentSlot> MirSet = new List<EquipmentSlot>();

        public bool FatalSword, Slaying, TwinDrakeBlade, FlamingSword, MPEater, Hemorrhage, CounterAttack;
        public int MPEaterCount, HemorrhageAttackCount;
        public long FlamingSwordTime, CounterAttackTime;
        public bool ActiveBlizzard, ActiveReincarnation, ActiveSwiftFeet, ReincarnationReady;
        public HumanObject ReincarnationTarget, ReincarnationHost;
        public long ReincarnationExpireTime;

        public long LastRevivalTime;
        public float HpDrain = 0;

        public bool UnlockCurse = false;
        public bool FastRun = false;
        public bool CanGainExp = true;
        public override bool Blocking
        {
            get
            {
                return !Dead && !Observer;
            }
        }
        public HumanObject() { }
        public HumanObject(CharacterInfo info, MirConnection connection)
        {
            Load(info, connection);
        }
        protected virtual void Load(CharacterInfo info, MirConnection connection) { }
        protected virtual void NewCharacter()
        {
            Level = 1;
            Hair = (byte)Envir.Random.Next(0, 9);

            for (int i = 0; i < Envir.StartItems.Count; i++)
            {
                ItemInfo info = Envir.StartItems[i];
                if (!CorrectStartItem(info)) continue;

                AddItem(Envir.CreateFreshItem(info));
            }
        }
        public long GetDelayTime(long original)
        {
            if (CurrentPoison.HasFlag(PoisonType.Slow))
            {
                return original * 2;
            }
            return original;
        }
        public override void Process()
        {
            if ((Race == ObjectType.Player && Connection == null) || Node == null || Info == null) return;

            // 「最近主动打了哪个玩家」的记录过期就清掉，免得一直攥着一个已经下线的对象
            if (RecentPvPTarget != null && (Envir.Time > RecentPvPTime || RecentPvPTarget.Node == null))
            {
                RecentPvPTarget = null;
                RecentPvPTime = 0;
            }

            if (CellTime + 700 < Envir.Time) _stepCounter = 0;

            if (Sneaking) CheckSneakRadius();

            if (FlamingSword && Envir.Time >= FlamingSwordTime)
            {
                FlamingSword = false;
                Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.FlamingSword, CanUse = false });
            }

            if (CounterAttack && Envir.Time >= CounterAttackTime)
            {
                CounterAttack = false;
            }

            //if (ReincarnationReady && Envir.Time >= ReincarnationExpireTime)
            //{
            //    ReincarnationReady = false;
            //    ActiveReincarnation = false;
            //    ReincarnationTarget = null;
            //    ReceiveChat("复活失败", ChatType.System);
            //}
            //if ((ReincarnationReady || ActiveReincarnation) && (ReincarnationTarget == null || !ReincarnationTarget.Dead))
            //{
            //    ReincarnationReady = false;
            //    ActiveReincarnation = false;
            //    ReincarnationTarget = null;
            //}

            if (Envir.Time > RunTime && _runCounter > 0)
            {
                RunTime = Envir.Time + 1500;
                _runCounter--;
            }

            if (Stacking && Envir.Time > StackingTime)
            {
                Stacking = false;

                for (int i = 0; i < 8; i++)
                {
                    if (Pushed(this, (MirDirection)i, 1) == 1) break;
                }
            }

            if (Mount.HasMount && Envir.Time > IncreaseLoyaltyTime)
            {
                IncreaseLoyaltyTime = Envir.Time + (LoyaltyDelay * 60);
                IncreaseMountLoyalty(1);
            }

            if (Envir.Time > ItemExpireTime)
            {
                ItemExpireTime = Envir.Time + ItemExpireDelay;

                ProcessItems();
            }

            for (int i = Pets.Count() - 1; i >= 0; i--)
            {
                MonsterObject pet = Pets[i];
                if (pet.Dead) Pets.Remove(pet);
            }

            ProcessBuffs();
            ProcessRegen();
            ProcessPoison();

            UserItem item;
            if (Envir.Time > TorchTime)
            {
                TorchTime = Envir.Time + 10000;
                item = Info.Equipment[(int)EquipmentSlot.照明物];
                if (item != null)
                {
                    // 离线挂机免损耗：火把/蜡烛不烧（照明物也是「消耗类物品」，挂一晚不该烧光）
                    if (!AfkFreeConsumableDura) DamageItem(item, 5);

                    if (item.CurrentDura == 0)
                    {
                        Info.Equipment[(int)EquipmentSlot.照明物] = null;
                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        RefreshStats();
                    }
                }
            }

            if (Envir.Time > DuraTime)
            {
                DuraTime = Envir.Time + DuraDelay;

                for (int i = 0; i < Info.Equipment.Length; i++)
                {
                    item = Info.Equipment[i];
                    if (item == null || !item.DuraChanged) continue; // || item.Info.Type == ItemType.Mount
                    item.DuraChanged = false;
                    Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                }
            }

            base.Process();

            RefreshNameColour();
        }

        public override void OnSafeZoneChanged()
        {
            base.OnSafeZoneChanged();

            bool needsUpdate = false;

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].ObjectID == 0) continue;
                if (!Buffs[i].Properties.HasFlag(BuffProperty.PauseInSafeZone)) continue;

                needsUpdate = true;

                if (InSafeZone)
                {
                    PauseBuff(Buffs[i]);
                }
                else
                {
                    UnpauseBuff(Buffs[i]);
                }
            }

            if (needsUpdate)
            {
                RefreshStats();
            }
        }

        public override void SetOperateTime()
        {
            OperateTime = Envir.Time;
        }
        public override void Die() { }
        protected virtual void ProcessBuffs()
        {
            bool refresh = false;
            bool clearRing = false, skill = false, gm = false, mentor = false, lover = false;

            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buff buff = Buffs[i];

                switch (buff.Type)
                {
                    case BuffType.气流术:
                        if (buff.Get<bool>("Interrupted") && buff.Get<long>("InterruptTime") <= Envir.Time)
                        {
                            buff.Set("Interrupted", false);
                            buff.Set("InterruptTime", (long)0);
                            UpdateConcentration(true, false);
                        }
                        break;
                    case BuffType.隐身戒指:
                        clearRing = true;
                        if (!SpecialMode.HasFlag(SpecialItemMode.ClearRing)) buff.FlagForRemoval = true;
                        break;
                    case BuffType.技巧项链:
                        skill = true;
                        if (!SpecialMode.HasFlag(SpecialItemMode.Skill)) buff.FlagForRemoval = true;
                        break;
                    case BuffType.游戏管理:
                        gm = true;
                        if (!IsGM) buff.FlagForRemoval = true;
                        break;
                    case BuffType.火传穷薪:
                    case BuffType.衣钵相传:
                        mentor = true;
                        if (Info.Mentor == 0) buff.FlagForRemoval = true;
                        break;
                    case BuffType.心心相映:
                        lover = true;
                        if (Info.Married == 0) buff.FlagForRemoval = true;
                        break;
                }

                if (buff.NextTime > Envir.Time) continue;

                if (!buff.Paused && buff.StackType != BuffStackType.Infinite)
                {
                    var change = Envir.Time - buff.LastTime;
                    buff.ExpireTime -= change;
                }

                buff.LastTime = Envir.Time;
                buff.NextTime = Envir.Time + 1000;

                if ((buff.ExpireTime > 0 || buff.StackType == BuffStackType.Infinite) && !buff.FlagForRemoval) continue;

                Buffs.RemoveAt(i);
                Enqueue(new S.RemoveBuff { Type = buff.Type, ObjectID = ObjectID });

                if (buff.Info.Visible)
                {
                    Broadcast(new S.RemoveBuff { Type = buff.Type, ObjectID = ObjectID });
                }

                switch (buff.Type)
                {
                    case BuffType.隐身术:
                    case BuffType.月影术:
                    case BuffType.烈火身:
                    case BuffType.隐身戒指:
                        if (!HasAnyBuffs(buff.Type, BuffType.隐身戒指, BuffType.隐身术, BuffType.月影术, BuffType.烈火身))
                        {
                            Hidden = false;
                        }
                        if (buff.Type == BuffType.月影术 || buff.Type == BuffType.烈火身)
                        {
                            if (!HasAnyBuffs(buff.Type, BuffType.月影术, BuffType.烈火身))
                            {
                                Sneaking = false;
                            }
                            break;
                        }
                        break;
                    case BuffType.影化连击: //影华连击的「化为虚影」结束，恢复显形
                        Hidden = false;
                        break;
                    case BuffType.绝对封锁:
                        if (!HasAnyBuffs(BuffType.绝对封锁))
                        {
                            BufffNoDrug = false;
                        }
                        break;
                    case BuffType.气流术:
                        UpdateConcentration(false, false);
                        break;
                    case BuffType.轻身步:
                        ActiveSwiftFeet = false;
                        break;
                    case BuffType.魔法盾:
                        CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MagicShieldDown }, CurrentLocation);
                        break;
                    case BuffType.金刚术:
                        CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.ElementalBarrierDown }, CurrentLocation);
                        break;
                }

                refresh = true;
            }

            if (IsGM && !gm)
            {
                UpdateGMBuff();
            }

            if (SpecialMode.HasFlag(SpecialItemMode.ClearRing) && !clearRing)
            {
                AddBuff(BuffType.隐身戒指, this, 0, new Stats());
            }

            if (SpecialMode.HasFlag(SpecialItemMode.Skill) && !skill)
            {
                AddBuff(BuffType.技巧项链, this, 0, new Stats { [Stat.技能熟练度倍率] = 3 }, false);
            }

            if (Info.Mentor != 0 && !mentor)
            {
                CharacterInfo partnerC = Envir.GetCharacterInfo(Info.Mentor);
                PlayerObject partnerP = partnerC != null ? Envir.GetPlayer(partnerC.Name) : null;

                if (partnerP != null)
                {
                    if (Info.IsMentor)
                    {
                        AddBuff(BuffType.火传穷薪, partnerP, 0, new Stats { [Stat.师徒专享伤害数率] = Settings.MentorDamageBoost });
                    }
                    else
                    {
                        AddBuff(BuffType.衣钵相传, partnerP, 0, new Stats { [Stat.师徒专享经验数率] = Settings.MentorExpBoost });
                    }
                }
            }

            if (Info.Married != 0 && !lover)
            {
                CharacterInfo loverC = Envir.GetCharacterInfo(Info.Married);
                PlayerObject loverP = loverC != null ? Envir.GetPlayer(loverC.Name) : null;

                if (loverP != null)
                {
                    AddBuff(BuffType.心心相映, loverP, 0, new Stats { [Stat.伴侣专享经验数率] = Settings.LoverEXPBonus });
                }
            }

            if (MyGuild != null && MyGuild.Name == Settings.NewbieGuild && Settings.NewbieGuildBuffEnabled == true)
            {
                AddBuff(BuffType.新人特效, this, 0, new Stats { [Stat.经验增长数率] = Settings.NewbieGuildExpBuff });
            }

            if (refresh)
            {
                RefreshStats();
            }
        }
        /// <summary>
        /// 生命魔法石（气血石 / 魔血石）：装备在「石头」格（EquipmentSlot.守护石）时自动生效。
        /// 全部参数在服务端「系统配置 → 游戏系统 → 守护石」选项卡里设置（存 Configs\GemSystem.ini [Stone] 节）：
        ///   触发阈值：HP / MP 低于总量的百分之多少时开启（HP、MP 各自可调）；
        ///   回复间隔：HP、MP 各自可调（秒）；
        ///   每跳增加值 = 石头总持久（Durability）的百分之多少（HP、MP 各自可调），固定值不随电量消耗衰减；
        ///   每跳消耗持久：HP、MP 各自可调（点）。
        /// 加什么仍由物品的「外观值 Shape」决定：1 = 只加体力，2 = 只加魔力，3 = 体力魔力一起加，其它值不生效。
        /// 触发后中途回到阈值以上也继续加，直到补满才停（满后复位，等下次再掉到阈值以下）。
        /// Shape=3 时 HP、MP 各按自己的节奏跳、各扣各的持久。
        /// 玩家与英雄共用（原逻辑在 PlayerObject，现整体下沉到基类）；子类可按需重写。
        /// </summary>
        protected bool StoneHPActive, StoneMPActive;
        protected long StoneHPNextTime, StoneMPNextTime;

        /// <summary>
        /// 离线挂机免扣消耗类物品持久（10-26）：PlayerObject / HeroObject 按「自己或主人是否在离线挂机」覆盖。
        /// 为 true 时：气血石/魔血石照常回血回蓝但不掉电量；照明物（火把/蜡烛）不再被烧。
        /// </summary>
        public virtual bool AfkFreeConsumableDura => false;

        /// <summary>
        /// 离线挂机免扣装备持久（10-26）：为 true 时被击不再磨损身上装备（含武器、坐骑忠诚）。
        /// 只拦战斗磨损（DamageDura / DamageWeapon / 坐骑忠诚），脚本、精炼等显式扣持久照旧生效。
        /// </summary>
        public virtual bool AfkFreeEquipDura => false;

        protected virtual void ProcessStones(ref int healthRegen, ref int manaRegen)
        {
            if (Dead) return;

            UserItem item = Info.Equipment[(int)EquipmentSlot.守护石];
            if (item == null || (item.Info.Type != ItemType.气血石 && item.Info.Type != ItemType.魔血石))
            {
                // 装备槽没有生命魔法石：开启「允许背包内使用」时，取背包里第一块有电量的气血石/魔血石
                item = null;
                if (Settings.StoneAllowBagUse)
                {
                    for (int i = 0; i < Info.Inventory.Length; i++)
                    {
                        UserItem bagItem = Info.Inventory[i];
                        if (bagItem == null || bagItem.CurrentDura == 0) continue;
                        if (bagItem.Info.Type != ItemType.气血石 && bagItem.Info.Type != ItemType.魔血石) continue;
                        item = bagItem;
                        break;
                    }
                }

                if (item == null)
                {
                    StoneHPActive = false;
                    StoneMPActive = false;
                    StoneHPNextTime = 0;
                    StoneMPNextTime = 0;
                    return; // 普通守护石不处理
                }
            }

            long now = Envir.Time;

            // ---- HP（气血石）----
            if ((item.Info.Shape == 1 || item.Info.Shape == 3) && Settings.StoneHPGainPercent > 0)
            {
                if (now >= StoneHPNextTime)
                {
                    if (NeedStoneHeal(ref StoneHPActive, HP, Stats[Stat.HP], Settings.StoneHPTriggerPercent))
                    {
                        if (item.CurrentDura == 0) { RemoveStone(item); return; }

                        healthRegen += Math.Max(1, item.Info.Durability * Settings.StoneHPGainPercent / 100);

                        // 离线挂机免损耗：回血照给，电量不扣（否则挂一晚上石头就被烧干）
                        if (!AfkFreeConsumableDura && ConsumeStoneDura(item, Settings.StoneHPDuraCost)) { RemoveStone(item); return; }

                        StoneHPNextTime = now + Math.Max(1, Settings.StoneHPDelay) * 1000;
                    }
                    else StoneHPNextTime = now + 1000; // 未触发：1 秒后再查
                }
            }
            else { StoneHPActive = false; StoneHPNextTime = 0; }

            // ---- MP（魔血石）----
            if ((item.Info.Shape == 2 || item.Info.Shape == 3) && Settings.StoneMPGainPercent > 0)
            {
                if (now >= StoneMPNextTime)
                {
                    if (NeedStoneHeal(ref StoneMPActive, MP, Stats[Stat.MP], Settings.StoneMPTriggerPercent))
                    {
                        if (item.CurrentDura == 0) { RemoveStone(item); return; }

                        manaRegen += Math.Max(1, item.Info.Durability * Settings.StoneMPGainPercent / 100);

                        // 离线挂机免损耗：回蓝照给，电量不扣
                        if (!AfkFreeConsumableDura && ConsumeStoneDura(item, Settings.StoneMPDuraCost)) { RemoveStone(item); return; }

                        StoneMPNextTime = now + Math.Max(1, Settings.StoneMPDelay) * 1000;
                    }
                    else StoneMPNextTime = now + 1000;
                }
            }
            else { StoneMPActive = false; StoneMPNextTime = 0; }
        }

        /// <summary>扣一次持久；返回 true 表示电量已归零（调用方应移除石头）。</summary>
        private static bool ConsumeStoneDura(UserItem item, int cost)
        {
            if (cost <= 0) return false;
            item.CurrentDura = (ushort)Math.Max(0, item.CurrentDura - cost);
            item.DuraChanged = true;
            return item.CurrentDura == 0;
        }

        /// <summary>
        /// 生命魔法石的「补到满」状态机：
        /// 满 → 停止并复位；掉到「触发阈值」以下 → 启动；启动后只要没满就一直保持。
        /// </summary>
        private static bool NeedStoneHeal(ref bool active, int current, int max, int triggerPercent)
        {
            if (max <= 0) { active = false; return false; }
            if (current >= max) { active = false; return false; }                 // 已满：停止
            int threshold = max * Math.Max(0, triggerPercent) / 100;
            if (!active && current < threshold) active = true;                    // 掉到阈值以下：启动
            return active;                                                        // 启动后一直补到满
        }

        protected void RemoveStone(UserItem item)
        {
            StoneHPActive = false;
            StoneMPActive = false;

            // 石头可能在装备槽，也可能在背包里（「允许背包内使用」模式），按引用找到实际位置清除
            if (ReferenceEquals(Info.Equipment[(int)EquipmentSlot.守护石], item))
            {
                Info.Equipment[(int)EquipmentSlot.守护石] = null;
            }
            else
            {
                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    if (!ReferenceEquals(Info.Inventory[i], item)) continue;
                    Info.Inventory[i] = null;
                    break;
                }
            }

            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
            RefreshStats();
            ReceiveChat(string.Format("{0} 的能量已经耗尽", item.Info.Name), ChatType.Hint);
        }

        private void ProcessRegen()
        {
            if (Dead) return;

            int healthRegen = 0, manaRegen = 0;

            if (CanRegen)
            {
                RegenTime = Envir.Time + RegenDelay;

                if (HP < Stats[Stat.HP])
                {
                    healthRegen += (int)(Stats[Stat.HP] * 0.03F) + 1;
                    healthRegen += (int)(healthRegen * ((double)Stats[Stat.生命恢复] / Settings.HealthRegenWeight));
                }

                if (MP < Stats[Stat.MP])
                {
                    manaRegen += (int)(Stats[Stat.MP] * 0.03F) + 1;
                    manaRegen += (int)(manaRegen * ((double)Stats[Stat.法力恢复] / Settings.ManaRegenWeight));
                }
            }

            if (Envir.Time > PotTime)
            {
                //PotTime = Envir.Time + Math.Max(50,Math.Min(PotDelay, 600 - (Level * 10)));
                PotTime = Envir.Time + PotDelay;
                int PerTickRegen = 5 + (Level / 10);

                if (PotHealthAmount > PerTickRegen)
                {
                    healthRegen += PerTickRegen;
                    PotHealthAmount -= (ushort)PerTickRegen;
                }
                else
                {
                    healthRegen += PotHealthAmount;
                    PotHealthAmount = 0;
                }

                if (PotManaAmount > PerTickRegen)
                {
                    manaRegen += PerTickRegen;
                    PotManaAmount -= (ushort)PerTickRegen;
                }
                else
                {
                    manaRegen += PotManaAmount;
                    PotManaAmount = 0;
                }
            }

            if (Envir.Time > HealTime)
            {
                HealTime = Envir.Time + HealDelay;

                int incHeal = (Level / 10) + (HealAmount / 10);
                if (HealAmount > (5 + incHeal))
                {
                    healthRegen += (5 + incHeal);
                    HealAmount -= (ushort)Math.Min(HealAmount, 5 + incHeal);
                }
                else
                {
                    healthRegen += HealAmount;
                    HealAmount = 0;
                }
            }

            if (Envir.Time > VampTime)
            {
                VampTime = Envir.Time + VampDelay;

                if (VampAmount > 10)
                {
                    healthRegen += 10;
                    VampAmount -= 10;
                }
                else
                {
                    healthRegen += VampAmount;
                    VampAmount = 0;
                }
            }

            // 气血石 / 魔血石：装备在「石头」格时自动生效。
            // 这里只做 1 秒节拍，HP / MP 各自的回复间隔、触发阈值、增量、耗电都在 Settings（守护石选项卡）里配置，
            // 由 ProcessStones 内部分别计时。
            if (Envir.Time > StoneTime)
            {
                StoneTime = Envir.Time + 1000;
                ProcessStones(ref healthRegen, ref manaRegen);
            }

            if (healthRegen > 0)
            {
                ChangeHP(healthRegen);
                BroadcastDamageIndicator(DamageType.HpRegen, healthRegen);
            }

            if (HP == Stats[Stat.HP])
            {
                PotHealthAmount = 0;
                HealAmount = 0;
            }

            if (manaRegen > 0) ChangeMP(manaRegen);
            if (MP == Stats[Stat.MP]) PotManaAmount = 0;
        }
        private void ProcessPoison()
        {
            PoisonType type = PoisonType.None;
            ArmourRate = 1F;
            DamageRate = 1F;

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (Dead) return;

                Poison poison = PoisonList[i];

                if (poison.Owner != null && poison.Owner.Node == null)
                {
                    PoisonList.RemoveAt(i);
                    continue;
                }

                if (Envir.Time > poison.TickTime)
                {
                    poison.Time++;
                    poison.TickTime = Envir.Time + poison.TickSpeed;

                    if (poison.Time >= poison.Duration)
                    {
                        PoisonList.RemoveAt(i);
                    }

                    if (poison.PType == PoisonType.Green || poison.PType == PoisonType.Bleeding)
                    {
                        LastHitter = poison.Owner;
                        LastHitTime = Envir.Time + 10000;

                        if (poison.PType == PoisonType.Bleeding)
                        {
                            Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Bleeding, EffectType = 0 });
                        }

                        PoisonDamage(-poison.Value, poison.Owner);
                        BroadcastDamageIndicator(DamageType.Poisoning, -poison.Value);

                        if (Dead) break;
                        RegenTime = Envir.Time + RegenDelay;
                    }

                    if (poison.PType == PoisonType.DelayedExplosion)
                    {
                        if (Envir.Time > ExplosionInflictedTime) ExplosionInflictedStage++;

                        if (!ProcessDelayedExplosion(poison))
                        {
                            if (Dead) break;

                            ExplosionInflictedStage = 0;
                            ExplosionInflictedTime = 0;

                            PoisonList.RemoveAt(i);
                            continue;
                        }
                    }
                }

                switch (poison.PType)
                {
                    case PoisonType.Red:
                        ArmourRate -= 0.10F;
                        break;
                    case PoisonType.Stun:
                        DamageRate += 0.20F;
                        break;
                    case PoisonType.Blindness:
                        break;
                }

                type |= poison.PType;
            }

            if (type == CurrentPoison) return;

            Enqueue(new S.Poisoned { Poison = type });
            Broadcast(new S.ObjectPoisoned { ObjectID = ObjectID, Poison = type });

            CurrentPoison = type;
        }
        private bool ProcessDelayedExplosion(Poison poison)
        {
            if (Dead) return false;

            if (ExplosionInflictedStage == 0)
            {
                Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 0 });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 0 });
                return true;
            }
            if (ExplosionInflictedStage == 1)
            {
                if (Envir.Time > ExplosionInflictedTime)
                    ExplosionInflictedTime = poison.TickTime + 3000;
                Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 1 });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 1 });
                return true;
            }
            if (ExplosionInflictedStage == 2)
            {
                Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 2 });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion, EffectType = 2 });
                if (poison.Owner != null)
                {
                    switch (poison.Owner.Race)
                    {
                        case ObjectType.Player:
                            PlayerObject caster = (PlayerObject)poison.Owner;
                            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time, poison.Owner, caster.GetMagic(Spell.DelayedExplosion), poison.Value, this.CurrentLocation);
                            CurrentMap.ActionList.Add(action);
                            break;
                        case ObjectType.Monster://this is in place so it could be used by mobs if one day someone chooses to
                            Attacked((MonsterObject)poison.Owner, poison.Value, DefenceType.MAC);
                            break;
                    }

                    LastHitter = poison.Owner;
                }
                return false;
            }
            return false;
        }
        private void ProcessItems()
        {
            for (var i = 0; i < Info.Inventory.Length; i++)
            {
                var item = Info.Inventory[i];

                if (item?.ExpireInfo?.ExpiryDate <= Envir.Now)
                {
                    ReceiveChat($"{item.Info.FriendlyName} 已经过期", ChatType.Hint);
                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    Info.Inventory[i] = null;
                    continue;
                }

                if (item?.RentalInformation?.RentalLocked == true && item?.RentalInformation?.ExpiryDate <= Envir.Now)
                {
                    ReceiveChat($"租赁绑定解除 {item.Info.FriendlyName}.", ChatType.Hint);
                    item.RentalInformation = null;
                }
            }

            for (var i = 0; i < Info.Equipment.Length; i++)
            {
                var item = Info.Equipment[i];

                if (item?.ExpireInfo?.ExpiryDate <= Envir.Now)
                {
                    ReceiveChat($"{item.Info.FriendlyName} 物品已经过期", ChatType.Hint);
                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    Info.Equipment[i] = null;
                    continue;
                }

                if (item?.RentalInformation?.RentalLocked == true && item?.RentalInformation?.ExpiryDate <= Envir.Now)
                {
                    ReceiveChat($"解除租赁绑定 {item.Info.FriendlyName}.", ChatType.Hint);
                    item.RentalInformation = null;
                }
            }

            if (Info.AccountInfo == null) return;

            for (int i = 0; i < Info.AccountInfo.Storage.Length; i++)
            {
                var item = Info.AccountInfo.Storage[i];
                if (item?.ExpireInfo?.ExpiryDate <= Envir.Now)
                {
                    ReceiveChat($"仓库中{item.Info.FriendlyName} 已经过期", ChatType.Hint);
                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    Info.AccountInfo.Storage[i] = null;
                    continue;
                }
            }
        }
        public override void Process(DelayedAction action)
        {
        }
        protected void UpdateGMBuff()
        {
            if (!IsGM) return;

            GMOptions options = GMOptions.None;

            if (GMGameMaster) options |= GMOptions.GameMaster;
            if (GMNeverDie) options |= GMOptions.Superman;
            if (Observer) options |= GMOptions.Observer;

            AddBuff(BuffType.游戏管理, this, 0, null, false, values: (byte)options);
        }
        public virtual void LevelUp()
        {
            RefreshStats();
            SetHP(Stats[Stat.HP]);
            SetMP(Stats[Stat.MP]);
            
            Broadcast(new S.ObjectLeveled { ObjectID = ObjectID });          
        }
        public virtual Color GetNameColour(HumanObject human)
        {
            return NameColour;
        }
        public virtual void RefreshNameColour() { }
        protected void SetHP(int amount)
        {
            if (HP == amount) return;

            HP = amount <= Stats[Stat.HP] ? amount : Stats[Stat.HP];
            HP = GMNeverDie ? Stats[Stat.HP] : HP;

            if (!Dead && HP == 0) Die();

            //HealthChanged = true;
            SendHealthChanged();
        }
        protected virtual void SendHealthChanged()
        {
            Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        protected void SetMP(int amount)
        {
            if (MP == amount) return;
            //was info.MP
            MP = amount <= Stats[Stat.MP] ? amount : Stats[Stat.MP];
            MP = GMNeverDie ? Stats[Stat.MP] : MP;

            // HealthChanged = true;
            SendHealthChanged();
        }
        public void ChangeHP(int amount)
        {
            if (SpecialMode.HasFlag(SpecialItemMode.Protection) && MP > 0 && amount < 0)
            {
                ChangeMP(amount);
                return;
            }

            if (HP + amount > Stats[Stat.HP])
                amount = Stats[Stat.HP] - HP;

            if (amount == 0) return;

            HP += amount;
            HP = GMNeverDie ? Stats[Stat.HP] : HP;

            if (HP < 0) HP = 0;

            if (!Dead && HP == 0) Die();

            // HealthChanged = true;
            SendHealthChanged();
        }
        public void PoisonDamage(int amount, MapObject Attacker)
        {
            ChangeHP(amount);
        }
        public void ChangeMP(int amount)
        {
            if (MP + amount > Stats[Stat.MP])
                amount = Stats[Stat.MP] - MP;

            if (amount == 0) return;

            MP += amount;
            MP = GMNeverDie ? Stats[Stat.MP] : MP;

            if (MP < 0) MP = 0;

            // HealthChanged = true;
            SendHealthChanged();
        }
        public void GetMinePayout(MineSet Mine)
        {
            if ((Mine.Drops == null) || (Mine.Drops.Count == 0)) return;
            if (FreeSpace(Info.Inventory) == 0) return;
            byte Slot = (byte)Envir.Random.Next(Mine.TotalSlots);
            for (int i = 0; i < Mine.Drops.Count; i++)
            {
                MineDrop Drop = Mine.Drops[i];
                if ((Drop.MinSlot <= Slot) && (Drop.MaxSlot >= Slot) && (Drop.Item != null))
                {
                    var info = Envir.GetItemInfo(Drop.Item.Index);

                    UserItem item = Envir.CreateDropItem(info);
                    if (item.Info.Type == ItemType.矿石)
                    {
                        item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, (Drop.MinDura + Envir.Random.Next(Math.Max(0, Drop.MaxDura - Drop.MinDura))) * 1000);
                        if ((Drop.BonusChance > 0) && (Envir.Random.Next(100) <= Drop.BonusChance))
                            item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, item.CurrentDura + (Envir.Random.Next(Drop.MaxBonusDura) * 1000));
                    }

                    if (CheckGroupQuestItem(item)) continue;

                    if (CanGainItem(item))
                    {
                        GainItem(item);
                        Report.ItemChanged(item, item.Count, 2);
                    }
                    return;
                }
            }
        }
        public virtual bool CheckGroupQuestItem(UserItem item, bool gainItem = true)
        {
            return false;
        }
        /// <summary>祝福油吃幸运。普通版(luckCap=7)：成功率 1/(10×当前幸运)、1/20 诅咒；
        /// 超级版(superOil=true，超级祝福油 Shape=22)：必成、不诅咒、上限由 luckCap 决定（默认 10）。</summary>
        protected bool TryLuckWeapon(int luckCap = 7, bool superOil = false)
        {
            var item = Info.Equipment[(int)EquipmentSlot.武器];

            if (item == null || item.AddedStats[Stat.幸运] >= luckCap)
                return false;

            if (item.Info.Bind.HasFlag(BindMode.DontUpgrade))
                return false;

            if (item.RentalInformation != null && item.RentalInformation.BindingFlags.HasFlag(BindMode.DontUpgrade))
                return false;

            string message = String.Empty;
            ChatType chatType;

            if (!superOil && item.AddedStats[Stat.幸运] > (Settings.MaxLuck * -1) && Envir.Random.Next(20) == 0)
            {
                Stats[Stat.幸运]--;
                item.AddedStats[Stat.幸运]--;
                Enqueue(new S.RefreshItem { Item = item });

                message = GameLanguage.WeaponCurse;
                chatType = ChatType.System;

            }
            else if (superOil || item.AddedStats[Stat.幸运] <= 0 || Envir.Random.Next(10 * item.GetTotal(Stat.幸运)) == 0)
            {
                Stats[Stat.幸运]++;
                item.AddedStats[Stat.幸运]++;
                Enqueue(new S.RefreshItem { Item = item });

                message = GameLanguage.WeaponLuck;
                chatType = ChatType.Hint;
            }
            else
            {
                message = GameLanguage.WeaponNoEffect;
                chatType = ChatType.Hint;
            }

            if (this is HeroObject hero)
            {
                if (message == GameLanguage.WeaponCurse ||
                    message == GameLanguage.WeaponLuck)
                {
                    hero.Owner.Enqueue(new S.RefreshItem { Item = item });
                }

                hero.Owner.ReceiveChat($"[英雄: {hero.Name}] {message}", chatType);
            }
            else
            {
                ReceiveChat(message, chatType);
            }

            return true;
        }
        protected bool CanUseItem(UserItem item)
        {
            if (item == null) return false;

            switch (Gender)
            {
                case MirGender.男性:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.男性))
                    {
                        ReceiveChat(GameLanguage.NotFemale, ChatType.System);
                        return false;
                    }
                    break;
                case MirGender.女性:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.女性))
                    {
                        ReceiveChat(GameLanguage.NotMale, ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (Class)
            {
                case MirClass.战士:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.战士))
                    {
                        ReceiveChat("战士禁用物品", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.法师:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.法师))
                    {
                        ReceiveChat("法师禁用物品", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.道士:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.道士))
                    {
                        ReceiveChat("道士禁用物品", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.弓箭:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.弓箭))
                    {
                        ReceiveChat("弓箭禁用物品", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.刺客:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.刺客))
                    {
                        ReceiveChat("刺客禁用物品", ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (item.Info.RequiredType)
            {
                case RequiredType.Level:
                    if (Level < item.Info.RequiredAmount)
                    {
                        ReceiveChat(GameLanguage.LowLevel, ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MaxAC:
                    if (Stats[Stat.MaxAC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("防御力不足", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MaxMAC:
                    if (Stats[Stat.MaxMAC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("魔法防御力不足", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MaxDC:
                    if (Stats[Stat.MaxDC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat(GameLanguage.LowDC, ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MaxMC:
                    if (Stats[Stat.MaxMC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat(GameLanguage.LowMC, ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MaxSC:
                    if (Stats[Stat.MaxSC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat(GameLanguage.LowSC, ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MaxLevel:
                    if (Level > item.Info.RequiredAmount)
                    {
                        ReceiveChat("超过要求的最大等级", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MinAC:
                    if (Stats[Stat.MinAC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("防御力不足", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MinMAC:
                    if (Stats[Stat.MinMAC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("魔法防御力不足", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MinDC:
                    if (Stats[Stat.MinDC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("攻击力不足", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MinMC:
                    if (Stats[Stat.MinMC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("魔法力不足", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MinSC:
                    if (Stats[Stat.MinSC] < item.Info.RequiredAmount)
                    {
                        ReceiveChat("道术力不足", ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (item.Info.Type)
            {
                case ItemType.卷轴:
                    switch (item.Info.Shape)
                    {
                        case 0:
                            if (CurrentMap.Info.NoEscape)
                            {
                                ReceiveChat(GameLanguage.CanNotDungeon, ChatType.System);
                                return false;
                            }
                            break;
                        case 1:
                            if (CurrentMap.Info.NoTownTeleport)
                            {
                                ReceiveChat(GameLanguage.NoTownTeleport, ChatType.System);
                                return false;
                            }
                            break;
                        case 2:
                            if (CurrentMap.Info.NoRandom)
                            {
                                ReceiveChat(GameLanguage.CanNotRandom, ChatType.System);
                                return false;
                            }
                            break;
                        case 20: // 回城石
                            if (CurrentMap.Info.NoTownTeleport)
                            {
                                ReceiveChat(GameLanguage.NoTownTeleport, ChatType.System);
                                return false;
                            }
                            break;
                        case 21: // 随机石
                            if (CurrentMap.Info.NoRandom)
                            {
                                ReceiveChat(GameLanguage.CanNotRandom, ChatType.System);
                                return false;
                            }
                            break;
                        case 6:
                            if (!Dead)
                            {
                                ReceiveChat(GameLanguage.CannotResurrection, ChatType.Hint);
                                return false;
                            }
                            break;
                        case 10:
                            {
                                int skillId = item.Info.Effect;

                                if (MyGuild == null)
                                {
                                    ReceiveChat("必须在行会中才能使用此技能", ChatType.Hint);
                                    return false;
                                }
                                if (MyGuildRank != MyGuild.Ranks[0])
                                {
                                    ReceiveChat("必须是会长才能使用此技能", ChatType.Hint);
                                    return false;
                                }
                                GuildBuffInfo buffInfo = Envir.FindGuildBuffInfo(skillId);

                                if (buffInfo == null) return false;

                                if (MyGuild.BuffList.Any(e => e.Info.Id == skillId))
                                {
                                    ReceiveChat("所在行会已有了这个技能", ChatType.Hint);
                                    return false;
                                }
                            }
                            break;
                    }
                    break;
                case ItemType.药水:
                    if (CurrentMap.Info.NoDrug)
                    {
                        ReceiveChat("药水禁用", ChatType.System);
                        return false;
                    }
                    if (BufffNoDrug)
                    {
                        ReceiveChat("不能使用药水", ChatType.System);
                        return false;
                    }
                    break;
                case ItemType.技能书:
                    if (Info.Magics.Any(t => t.Spell == (Spell)item.Info.Shape))
                    {
                        return false;
                    }
                    break;
                case ItemType.马鞍:
                case ItemType.蝴蝶结:
                case ItemType.铃铛:
                case ItemType.面甲:
                case ItemType.缰绳:
                    if (Info.Equipment[(int)EquipmentSlot.坐骑] == null)
                    {
                        ReceiveChat("与坐骑一起时使用", ChatType.System);
                        return false;
                    }
                    break;
                case ItemType.鱼钩:
                case ItemType.鱼漂:
                case ItemType.鱼饵:
                case ItemType.探鱼器:
                case ItemType.摇轮:
                    if (Info.Equipment[(int)EquipmentSlot.武器] == null || !Info.Equipment[(int)EquipmentSlot.武器].Info.IsFishingRod)
                    {
                        ReceiveChat("与鱼竿一起使用", ChatType.System);
                        return false;
                    }
                    break;
                case ItemType.镶嵌宝石:
                    break;
                case ItemType.灵物:
                    switch (item.Info.Shape)
                    {
                        case 20://mirror rename creature
                            if (Info.IntelligentCreatures.Count == 0) return false;
                            break;
                        case 21://creature stone
                            break;
                        case 22://nuts maintain food levels
                            if (!CreatureSummoned)
                            {
                                ReceiveChat("唤出灵物后使用", ChatType.System);
                                return false;
                            }
                            break;
                        case 23://basic creature food
                            if (!CreatureSummoned)
                            {
                                ReceiveChat("与灵物一起时才能使用", ChatType.System);
                                return false;
                            }
                            else
                            {
                                for (int i = 0; i < Pets.Count; i++)
                                {
                                    if (Pets[i].Race != ObjectType.Creature) continue;

                                    var pet = (IntelligentCreatureObject)Pets[i];
                                    if (pet.PetType != SummonedCreatureType) continue;
                                    if (pet.Fullness > 9900)
                                    {
                                        ReceiveChat(pet.Name + " 不饿", ChatType.System);
                                        return false;
                                    }
                                    return true;
                                }
                                return false;
                            }
                        case 24://wonderpill vitalize creature
                            if (!CreatureSummoned)
                            {
                                ReceiveChat("只能与灵物一起时使用", ChatType.System);
                                return false;
                            }
                            else
                            {
                                for (int i = 0; i < Pets.Count; i++)
                                {
                                    if (Pets[i].Race != ObjectType.Creature) continue;

                                    var pet = (IntelligentCreatureObject)Pets[i];
                                    if (pet.PetType != SummonedCreatureType) continue;
                                    if (pet.Fullness > 0)
                                    {
                                        ReceiveChat(pet.Name + " 无需唤醒", ChatType.System);
                                        return false;
                                    }
                                    return true;
                                }
                                return false;
                            }
                        case 25://Strongbox
                            break;
                        case 26://Wonderdrugs
                            break;
                        case 27://Fortunecookies
                            break;
                    }
                    break;
            }

            if (RidingMount && item.Info.Type != ItemType.卷轴 && item.Info.Type != ItemType.药水)
            {
                return false;
            }

            return true;
        }
        public virtual void UseItem(ulong id) { }
        protected void ConsumeItem(UserItem item, byte cost)
        {
            item.Count -= cost;
            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = cost });

            if (item.Count != 0) return;

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                if (Info.Equipment[i] != null && Info.Equipment[i].Slots.Length > 0)
                {
                    for (int j = 0; j < Info.Equipment[i].Slots.Length; j++)
                    {
                        if (Info.Equipment[i].Slots[j] != item) continue;
                        Info.Equipment[i].Slots[j] = null;
                        return;
                    }
                }

                if (Info.Equipment[i] != item) continue;
                Info.Equipment[i] = null;

                return;
            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != item) continue;
                Info.Inventory[i] = null;
                return;
            }

            //Item not found
        }
        protected bool DropItem(UserItem item, int range = 1, bool DeathDrop = false)
        {
            ItemObject ob = new ItemObject(this, item, DeathDrop);

            if (!ob.Drop(range)) return false;

            if (item.Info.Type == ItemType.肉)
                item.CurrentDura = (ushort)Math.Max(0, item.CurrentDura - 2000);

            return true;
        }
        protected void DeathDrop(MapObject killer)
        {
            //假人不爆装：AI 玩家没有真人那种「回来捡尸体」的能力，一旦爆光装备就会变成
            //光着身子的怪人，严重影响观感。它们本来也不产出任何收益，禁掉不影响平衡。
            if (this is FakePlayerObject) return;

            //★离线挂机不爆装（10-26）：挂机时人不在电脑前，死了掉的装备/背包物品根本捡不回来，
            //  等于「挂机在消耗物品」。地图配置 NoDropPlayer 之类照旧在前面判断，这里只是额外兜住挂机角色。
            PlayerObject afkPlayer = this as PlayerObject;
            if (afkPlayer != null && afkPlayer.OfflineAFK) return;

            HeroObject afkHero = this as HeroObject;
            if (afkHero != null && afkHero.Owner != null && afkHero.Owner.OfflineAFK) return;

            var pkbodydrop = true;

            if (CurrentMap.Info.NoDropPlayer && (Race == ObjectType.Player || Race == ObjectType.Hero))
                return;

            if ((killer == null) || ((pkbodydrop) || (killer.Race != ObjectType.Player) || killer.Race != ObjectType.Hero))
            {
                for (var i = 0; i < Info.Equipment.Length; i++)
                {
                    var item = Info.Equipment[i];

                    if (item == null)
                        continue;

                    if (item.Info.Bind.HasFlag(BindMode.DontDeathdrop))
                        continue;

                    // TODO: Check this.
                    if (item.WeddingRing != -1 && Info.Equipment[(int)EquipmentSlot.左戒指].UniqueID == item.UniqueID)
                        continue;

                    if (item.SealedInfo != null && item.SealedInfo.ExpiryDate > Envir.Now)
                        continue;

                    if (((killer == null) || ((killer != null) && ((killer.Race != ObjectType.Player) || (killer.Race != ObjectType.Hero)))))
                    {
                        if (item.Info.Bind.HasFlag(BindMode.BreakOnDeath))
                        {
                            Info.Equipment[i] = null;
                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                            ReceiveChat($"物品： {item.FriendlyName} 因死亡而消失", ChatType.System2);
                            Report?.ItemChanged(item, item.Count, 1);
                        }
                    }
                    if (ItemSets.Any(set => set.Set == ItemSet.祈祷套装 && !set.SetComplete))
                    {
                        if (item.Info.Set == ItemSet.祈祷套装)
                        {
                            Info.Equipment[i] = null;
                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                            Report?.ItemChanged(item, item.Count, 1);
                        }
                    }

                    if (item.Count > 1)
                    {
                        var percent = Envir.RandomomRange(10, 8);
                        var count = (ushort)Math.Ceiling(item.Count / 10F * percent);

                        if (count > item.Count)
                            throw new ArgumentOutOfRangeException();

                        var temp2 = Envir.CreateFreshItem(item.Info);
                        temp2.Count = count;

                        if (!DropItem(temp2, Settings.DropRange, true))
                            continue;

                        if (count == item.Count)
                            Info.Equipment[i] = null;

                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = count });
                        item.Count -= count;

                        Report?.ItemChanged(item, count, 1);
                    }
                    else if (Envir.Random.Next(30) == 0)
                    {
                        if (Envir.ReturnRentalItem(item, item.RentalInformation?.OwnerName, Info))
                        {
                            Info.Equipment[i] = null;
                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                            ReceiveChat($"角色死亡 {item.Info.FriendlyName} 已经归还给了它的主人", ChatType.Hint);
                            Report?.ItemMailed(item, 1, 1);

                            continue;
                        }

                        if (!DropItem(item, Settings.DropRange, true))
                        {
                            continue;
                        }

                        if (item.Info.GlobalDropNotify)
                        {
                            foreach (var player in Envir.Players)
                            {
                                player.ReceiveChat($"{Name} 掉落了 {item.FriendlyName}.", ChatType.System2);
                            }
                        }

                        Info.Equipment[i] = null;
                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                        Report?.ItemChanged(item, item.Count, 1);
                    }
                }

            }

            for (var i = 0; i < Info.Inventory.Length; i++)
            {
                var item = Info.Inventory[i];

                if (item == null)
                    continue;

                if (item.Info.Bind.HasFlag(BindMode.DontDeathdrop))
                    continue;

                if (item.WeddingRing != -1)
                    continue;

                if (item.SealedInfo != null && item.SealedInfo.ExpiryDate > Envir.Now)
                    continue;

                if (item.Count > 1)
                {
                    var percent = Envir.RandomomRange(10, 8);

                    if (percent == 0)
                        continue;

                    var count = (ushort)Math.Ceiling(item.Count / 10F * percent);

                    if (count > item.Count)
                        throw new ArgumentOutOfRangeException();

                    var temp2 = Envir.CreateFreshItem(item.Info);
                    temp2.Count = count;

                    if (!DropItem(temp2, Settings.DropRange, true))
                        continue;

                    if (count == item.Count)
                        Info.Inventory[i] = null;

                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = count });
                    item.Count -= count;

                    Report?.ItemChanged(item, count, 1);
                }
                else if (Envir.Random.Next(10) == 0)
                {
                    if (Envir.ReturnRentalItem(item, item.RentalInformation?.OwnerName, Info))
                    {
                        Info.Inventory[i] = null;
                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                        ReceiveChat($"角色死亡 {item.Info.FriendlyName} 已归还给物主", ChatType.Hint);
                        Report?.ItemMailed(item, 1, 1);

                        continue;
                    }

                    if (!DropItem(item, Settings.DropRange, true))
                        continue;

                    if (item.Info.GlobalDropNotify)
                        foreach (var player in Envir.Players)
                        {
                            player.ReceiveChat($"{Name} 掉落了 {item.FriendlyName}", ChatType.System2);
                        }

                    Info.Inventory[i] = null;
                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                    Report?.ItemChanged(item, item.Count, 1);
                }
            }

            RefreshStats();
        }
        protected static int FreeSpace(IList<UserItem> array)
        {
            int count = 0;

            for (int i = 0; i < array.Count; i++)
                if (array[i] == null) count++;

            return count;
        }
        protected void AddItem(UserItem item)
        {
            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem temp = Info.Inventory[i];
                    if (temp == null || item.Info != temp.Info || temp.Count >= temp.Info.StackSize) continue;

                    if (item.Count + temp.Count <= temp.Info.StackSize)
                    {
                        temp.Count += item.Count;
                        return;
                    }
                    item.Count -= (ushort)(temp.Info.StackSize - temp.Count);
                    temp.Count = temp.Info.StackSize;
                }
            }

            if (item.Info.Type == ItemType.药水 || item.Info.Type == ItemType.卷轴 || (item.Info.Type == ItemType.特殊消耗品 && item.Info.Effect == 1))
            {
                for (int i = PotionBeltMinimum; i < PotionBeltMaximum; i++)
                {
                    if (Info.Inventory[i] != null) continue;
                    Info.Inventory[i] = item;
                    return;
                }
            }
            else if (item.Info.Type == ItemType.护身符)
            {
                for (int i = AmuletBeltMinimum; i < AmuletBeltMaximum; i++)
                {
                    if (Info.Inventory[i] != null) continue;
                    Info.Inventory[i] = item;
                    return;
                }
            }
            else
            {
                for (int i = BeltSize; i < Info.Inventory.Length; i++)
                {
                    if (Info.Inventory[i] != null) continue;
                    Info.Inventory[i] = item;
                    return;
                }
            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != null) continue;
                Info.Inventory[i] = item;
                return;
            }
        }
        protected bool CorrectStartItem(ItemInfo info)
        {
            switch (Class)
            {
                case MirClass.战士:
                    if (!info.RequiredClass.HasFlag(RequiredClass.战士)) return false;
                    break;
                case MirClass.法师:
                    if (!info.RequiredClass.HasFlag(RequiredClass.法师)) return false;
                    break;
                case MirClass.道士:
                    if (!info.RequiredClass.HasFlag(RequiredClass.道士)) return false;
                    break;
                case MirClass.刺客:
                    if (!info.RequiredClass.HasFlag(RequiredClass.刺客)) return false;
                    break;
                case MirClass.弓箭:
                    if (!info.RequiredClass.HasFlag(RequiredClass.弓箭)) return false;
                    break;
                default:
                    return false;
            }

            switch (Gender)
            {
                case MirGender.男性:
                    if (!info.RequiredGender.HasFlag(RequiredGender.男性)) return false;
                    break;
                case MirGender.女性:
                    if (!info.RequiredGender.HasFlag(RequiredGender.女性)) return false;
                    break;
                default:
                    return false;
            }

            return true;
        }
        public virtual void CheckItemInfo(ItemInfo info, bool dontLoop = false)
        {
            if (Connection == null) return;   // 英雄无连接；玩家断线瞬间也不再炸
            Connection.CheckItemInfo(info, dontLoop);
        }
        public virtual void CheckItem(UserItem item)
        {
            if (Connection == null) return;   // 同上：HeroObject 在子类里转发给主人
            Connection.CheckItem(item);
        }
        public void SetLevelEffects()
        {
            LevelEffects = LevelEffects.None;

            if (Info.Flags[990]) LevelEffects |= LevelEffects.Mist;
            if (Info.Flags[991]) LevelEffects |= LevelEffects.RedDragon;
            if (Info.Flags[992]) LevelEffects |= LevelEffects.BlueDragon;
            if (Info.Flags[993]) LevelEffects |= LevelEffects.Rebirth1;
            if (Info.Flags[994]) LevelEffects |= LevelEffects.Rebirth2;
            if (Info.Flags[995]) LevelEffects |= LevelEffects.Rebirth3;
            if (Info.Flags[996]) LevelEffects |= LevelEffects.NewBlue;
            if (Info.Flags[997]) LevelEffects |= LevelEffects.YellowDragon;
            if (Info.Flags[998]) LevelEffects |= LevelEffects.Phoenix;
        }
        public virtual void Revive(int hp, bool effect)
        {
            if (!Dead) return;

            Dead = false;
            SetHP(hp);

            CurrentMap.RemoveObject(this);
            Broadcast(new S.ObjectRemove { ObjectID = ObjectID });

            CurrentMap = CurrentMap;
            CurrentLocation = CurrentLocation;

            CurrentMap.AddObject(this);

            Enqueue(new S.MapChanged
            {
                MapIndex = CurrentMap.Info.Index,
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                Weather = CurrentMap.Info.WeatherParticles,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight,
                Music = CurrentMap.Info.Music
            });

            Enqueue(new S.Revived());
            Broadcast(new S.ObjectRevived { ObjectID = ObjectID, Effect = effect });
        }

        protected virtual void SendBaseStats()
        {
            Enqueue(new S.BaseStatsInfo { Stats = Settings.ClassBaseStats[(byte)Class] });
        }

        #region Refresh Stats
        public void RefreshStats()
        {
            if (HasUpdatedBaseStats == false)
            {
                SendBaseStats();                
                HasUpdatedBaseStats = true;
            }

            Stats.Clear();

            RefreshLevelStats();
            RefreshBagWeight();
            RefreshEquipmentStats();
            RefreshItemSetStats();
            RefreshMirSetStats();
            RefreshSkills();
            RefreshBuffs();
            RefreshGuildBuffs();

            //Add any rate percent changes

            Stats[Stat.HP] += (Stats[Stat.HP] * Stats[Stat.生命值数率]) / 100;
            Stats[Stat.MP] += (Stats[Stat.MP] * Stats[Stat.法力值数率]) / 100;
            Stats[Stat.MaxAC] += (Stats[Stat.MaxAC] * Stats[Stat.最大防御数率]) / 100;
            Stats[Stat.MaxMAC] += (Stats[Stat.MaxMAC] * Stats[Stat.最大魔御数率]) / 100;

            Stats[Stat.MaxDC] += (Stats[Stat.MaxDC] * Stats[Stat.最大物理攻击数率]) / 100;
            Stats[Stat.MaxMC] += (Stats[Stat.MaxMC] * Stats[Stat.最大魔法攻击数率]) / 100;
            Stats[Stat.MaxSC] += (Stats[Stat.MaxSC] * Stats[Stat.最大道术攻击数率]) / 100;
            Stats[Stat.攻击速度] += (Stats[Stat.攻击速度] * Stats[Stat.攻击速度数率]) / 100;

            RefreshStatCaps();

            if (HP > Stats[Stat.HP]) SetHP(Stats[Stat.HP]);
            if (MP > Stats[Stat.MP]) SetMP(Stats[Stat.MP]);

            AttackSpeed = 1400 - ((Stats[Stat.攻击速度] * 60) + Math.Min(370, (Level * 14)));

            if (AttackSpeed < 550) AttackSpeed = 550;
        }
        public virtual void RefreshGuildBuffs() { }
        protected void RefreshLevelStats()
        {
            MaxExperience = Level < Settings.ExperienceList.Count ? Settings.ExperienceList[Level - 1] : 0;

            foreach (var stat in Settings.ClassBaseStats[(byte)Class].Stats)
            {
                Stats[stat.Type] = stat.Calculate(Class, Level);
            }
        }
        public void RefreshBagWeight()
        {
            CurrentBagWeight = 0;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null)
                {
                    CurrentBagWeight += item.Weight;
                }
            }
        }
        private void RefreshEquipmentStats()
        {
            short OldLooks_Weapon = Looks_Weapon;
            short OldLooks_WeaponEffect = Looks_WeaponEffect;
            short OldLooks_Armour = Looks_Armour;
            short Old_MountType = Mount.MountType;
            byte OldLooks_Wings = Looks_Wings;
            byte OldLight = Light;

            Looks_Armour = 0;
            Looks_Weapon = -1;
            Looks_WeaponEffect = 0;
            Looks_Wings = 0;
            Light = 0;
            CurrentWearWeight = 0;
            CurrentHandWeight = 0;
            Mount.MountType = -1;

            SpecialMode = SpecialItemMode.None;

            FastRun = false;

            Stats[Stat.技能熟练度倍率] = 1;

            var skillsToAdd = new List<string>();
            var skillsToRemove = new List<string> { Settings.HealRing, Settings.FireRing, Settings.BlinkSkill };

            ItemSets.Clear();
            MirSet.Clear();

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem temp = Info.Equipment[i];
                if (temp == null) continue;
                ItemInfo realItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);

                if (realItem.Type == ItemType.武器 || realItem.Type == ItemType.照明物)
                    CurrentHandWeight = (int)Math.Min(int.MaxValue, CurrentHandWeight + temp.Weight);
                else
                    CurrentWearWeight = (int)Math.Min(int.MaxValue, CurrentWearWeight + temp.Weight);

                if (temp.CurrentDura == 0 && temp.Info.Durability > 0) continue;

                if (realItem.Type == ItemType.盔甲)
                {
                    Looks_Armour = realItem.Shape;
                    Looks_Wings = realItem.Effect;
                }

                if (realItem.Type == ItemType.武器)
                {
                    Looks_Weapon = realItem.Shape;
                    Looks_WeaponEffect = realItem.Effect;
                }

                if (realItem.Type == ItemType.坐骑)
                {
                    Mount.MountType = realItem.Shape;
                    //RealItem.Effect;
                }

                if (temp.Info.IsFishingRod) continue;

                // 气血石 / 魔血石 的属性栏填的是「每跳回复量」，不是人物属性加成，不能并入 Stats
                if (realItem.Type != ItemType.气血石 && realItem.Type != ItemType.魔血石)
                {
                    Stats.Add(realItem.Stats);
                    Stats.Add(temp.AddedStats);
                }

                Stats[Stat.MinAC] += temp.Awake.GetAC();
                Stats[Stat.MaxAC] += temp.Awake.GetAC();
                Stats[Stat.MinMAC] += temp.Awake.GetMAC();
                Stats[Stat.MaxMAC] += temp.Awake.GetMAC();

                Stats[Stat.MinDC] += temp.Awake.GetDC();
                Stats[Stat.MaxDC] += temp.Awake.GetDC();
                Stats[Stat.MinMC] += temp.Awake.GetMC();
                Stats[Stat.MaxMC] += temp.Awake.GetMC();
                Stats[Stat.MinSC] += temp.Awake.GetSC();
                Stats[Stat.MaxSC] += temp.Awake.GetSC();

                Stats[Stat.HP] += temp.Awake.GetHPMP();
                Stats[Stat.MP] += temp.Awake.GetHPMP();

                if (realItem.Light > Light) Light = realItem.Light;
                if (realItem.Unique != SpecialItemMode.None)
                {
                    SpecialMode |= realItem.Unique;

                    if (realItem.Unique.HasFlag(SpecialItemMode.Flame)) skillsToAdd.Add(Settings.FireRing);
                    if (realItem.Unique.HasFlag(SpecialItemMode.Healing)) skillsToAdd.Add(Settings.HealRing);
                    if (realItem.Unique.HasFlag(SpecialItemMode.Blink)) skillsToAdd.Add(Settings.BlinkSkill);
                }

                if (realItem.CanFastRun)
                {
                    FastRun = true;
                }

                RefreshSocketStats(temp, skillsToAdd);

                if (realItem.Set == ItemSet.非套装) continue;

                ItemSets itemSet = ItemSets.Where(set => set.Set == realItem.Set && !set.Type.Contains(realItem.Type) && !set.SetComplete).FirstOrDefault();

                if (itemSet != null)
                {
                    itemSet.Type.Add(realItem.Type);
                    itemSet.Count++;
                }
                else
                {
                    ItemSets.Add(new ItemSets { Count = 1, Set = realItem.Set, Type = new List<ItemType> { realItem.Type } });
                }

                //Mir Set
                if (realItem.Set == ItemSet.天龙套装)
                {
                    if (!MirSet.Contains((EquipmentSlot)i))
                    {
                        MirSet.Add((EquipmentSlot)i);
                    }
                }
            }

            AddTempSkills(skillsToAdd);
            RemoveTempSkills(skillsToRemove.Except(skillsToAdd));

            if (SpecialMode.HasFlag(SpecialItemMode.Muscle))
            {
                Stats[Stat.背包负重] = Stats[Stat.背包负重] * 2;
                Stats[Stat.装备负重] = Stats[Stat.装备负重] * 2;
                Stats[Stat.腕力负重] = Stats[Stat.腕力负重] * 2;
            }

            if ((OldLooks_Armour != Looks_Armour) || (OldLooks_Weapon != Looks_Weapon) || (OldLooks_WeaponEffect != Looks_WeaponEffect) || (OldLooks_Wings != Looks_Wings) || (OldLight != Light))
            {
                UpdateLooks(OldLooks_Weapon);                
            }

            if (Old_MountType != Mount.MountType)
            {
                RefreshMount(false);
            }
        }
        private void RefreshSocketStats(UserItem equipItem, List<string> skillsToAdd)
        {
            if (equipItem == null) return;

            if (equipItem.Info.Type == ItemType.武器 && equipItem.Info.IsFishingRod)
            {
                return;
            }

            if (equipItem.Info.Type == ItemType.坐骑 && !RidingMount)
            {
                return;
            }

            for (int j = 0; j < equipItem.Slots.Length; j++)
            {
                UserItem temp = equipItem.Slots[j];
                if (temp == null) continue;

                ItemInfo RealItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);

                if (RealItem.Type == ItemType.武器 || RealItem.Type == ItemType.照明物)
                    CurrentHandWeight = (int)Math.Min(int.MaxValue, CurrentHandWeight + temp.Weight);
                else
                    CurrentWearWeight = (int)Math.Min(int.MaxValue, CurrentWearWeight + temp.Weight);

                if (temp.CurrentDura == 0 && temp.Info.Durability > 0) continue;

                Stats.Add(RealItem.Stats);
                Stats.Add(temp.AddedStats);

                if (RealItem.Light > Light) Light = RealItem.Light;
                if (RealItem.Unique != SpecialItemMode.None)
                {
                    SpecialMode |= RealItem.Unique;

                    if (RealItem.Unique.HasFlag(SpecialItemMode.Skill)) Stats[Stat.技能熟练度倍率] = 3;

                    if (RealItem.Unique.HasFlag(SpecialItemMode.Flame)) skillsToAdd.Add(Settings.FireRing);
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Healing)) skillsToAdd.Add(Settings.HealRing);
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Blink)) skillsToAdd.Add(Settings.BlinkSkill);
                }
            }

            //TODO - Add Socket bonuses
        }
        private void RefreshItemSetStats()
        {
            foreach (var s in ItemSets)
            {
                if (s.Set == ItemSet.破碎套装)
                {
                    if (s.Type.Contains(ItemType.项链) && s.Type.Contains(ItemType.戒指) && s.Type.Contains(ItemType.手镯))
                    {
                        Stats[Stat.MinDC] += 1;
                        Stats[Stat.MaxDC] += 3;
                    }
                    if (s.Type.Contains(ItemType.戒指) && s.Type.Contains(ItemType.手镯))
                    {
                        Stats[Stat.攻击速度] += 2;
                        return;
                    }
                }

                if ((s.Set == ItemSet.灵玉套装) && (s.Type.Contains(ItemType.戒指)) && (s.Type.Contains(ItemType.手镯)))
                {
                    Stats[Stat.神圣] += 3;
                }

                if ((s.Set == ItemSet.幻魔石套) && (s.Type.Contains(ItemType.戒指)) && (s.Type.Contains(ItemType.手镯)))
                {
                    Stats[Stat.装备负重] += 5;
                    Stats[Stat.背包负重] += 20;
                }

                if ((s.Set == ItemSet.鏃未套装) && (s.Type.Contains(ItemType.项链)) && (s.Type.Contains(ItemType.手镯)))
                {
                    Stats[Stat.HP] += 25;
                }

                if (s.Set == ItemSet.圣龙套装)
                {
                    if (Info.Equipment[(int)EquipmentSlot.左戒指] != null && Info.Equipment[(int)EquipmentSlot.右戒指] != null)
                    {
                        bool activateRing = false;

                        if (Info.Equipment[(int)EquipmentSlot.左戒指].Info.Name.StartsWith("双花") &&
                            Info.Equipment[(int)EquipmentSlot.左戒指].Info.Set == ItemSet.圣龙套装 &&
                            Info.Equipment[(int)EquipmentSlot.右戒指].Info.Name.StartsWith("双绿") &&
                            Info.Equipment[(int)EquipmentSlot.右戒指].Info.Set == ItemSet.圣龙套装)
                        {
                            activateRing = true;
                        }

                        if (Info.Equipment[(int)EquipmentSlot.右戒指].Info.Name.StartsWith("双花") &&
                            Info.Equipment[(int)EquipmentSlot.右戒指].Info.Set == ItemSet.圣龙套装 &&
                            Info.Equipment[(int)EquipmentSlot.左戒指].Info.Name.StartsWith("双绿") &&
                            Info.Equipment[(int)EquipmentSlot.左戒指].Info.Set == ItemSet.圣龙套装)
                        {
                            activateRing = true;
                        }

                        if (activateRing)
                        {
                            Stats[Stat.MaxDC] += 5;
                            Stats[Stat.MaxMC] += 5;
                            Stats[Stat.MaxSC] += 5;
                            return;
                        }
                    }
                }

                if (s.Set == ItemSet.神龙套装)
                {
                    if (s.Type.Contains(ItemType.戒指) && s.Type.Contains(ItemType.项链))
                    {
                        Stats[Stat.MaxDC] += 8;
                        Stats[Stat.MaxMC] += 8;
                        Stats[Stat.MaxSC] += 8;
                    }
                    if (s.Type.Contains(ItemType.盔甲) && s.Type.Contains(ItemType.戒指) && s.Type.Contains(ItemType.手镯) && s.Type.Contains(ItemType.项链))
                    {
                        Stats[Stat.最大防御数率] += 20;
                    }
                }

                if (!s.SetComplete) continue;

                switch (s.Set)
                {
                    case ItemSet.世轮套装:
                        Stats[Stat.HP] += 50;
                        break;
                    case ItemSet.绿翠套装:
                        Stats[Stat.MP] += 50;
                        break;
                    case ItemSet.道护套装:
                        Stats[Stat.HP] += 30;
                        Stats[Stat.MP] += 30;
                        break;
                    case ItemSet.赤兰套装:
                        Stats[Stat.准确] += 2;
                        Stats[Stat.吸血数率] += 10;
                        break;
                    case ItemSet.密火套装:
                        Stats[Stat.HP] += 50;
                        Stats[Stat.MP] -= 50;
                        break;
                    case ItemSet.幻魔石套:
                        Stats[Stat.MinMC] += 1;
                        Stats[Stat.MaxMC] += 2;
                        break;
                    case ItemSet.灵玉套装:
                        Stats[Stat.MinSC] += 1;
                        Stats[Stat.MaxSC] += 2;
                        break;
                    case ItemSet.五玄套装:
                        //Stats[Stat.HP] += (int)(((double)Stats[Stat.HP] / 100) * 30);
                        Stats[Stat.生命值数率] += 30;
                        Stats[Stat.MinAC] += 2;
                        Stats[Stat.MaxAC] += 2;
                        break;
                    case ItemSet.祈祷套装:
                        Stats[Stat.MinDC] += 2;
                        Stats[Stat.MaxDC] += 5;
                        Stats[Stat.攻击速度] += 2;
                        break;
                    case ItemSet.白骨套装:
                        Stats[Stat.MaxAC] += 2;
                        Stats[Stat.MaxMC] += 1;
                        Stats[Stat.MaxSC] += 1;
                        break;
                    case ItemSet.虫血套装:
                        Stats[Stat.MaxDC] += 1;
                        Stats[Stat.MaxMC] += 1;
                        Stats[Stat.MaxSC] += 1;
                        Stats[Stat.MaxMAC] += 1;
                        Stats[Stat.毒物躲避] += 1;
                        break;
                    case ItemSet.白金套装:
                        Stats[Stat.MaxDC] += 2;
                        Stats[Stat.MaxAC] += 2;
                        break;
                    case ItemSet.强白金套:
                        Stats[Stat.MaxDC] += 3;
                        Stats[Stat.HP] += 30;
                        Stats[Stat.攻击速度] += 2;
                        break;
                    case ItemSet.红玉套装:
                        Stats[Stat.MaxMC] += 2;
                        Stats[Stat.MaxMAC] += 2;
                        break;
                    case ItemSet.强红玉套:
                        Stats[Stat.MaxMC] += 2;
                        Stats[Stat.MP] += 40;
                        Stats[Stat.敏捷] += 2;
                        break;
                    case ItemSet.软玉套装:
                        Stats[Stat.MaxSC] += 2;
                        Stats[Stat.MaxAC] += 1;
                        Stats[Stat.MaxMAC] += 1;
                        break;
                    case ItemSet.强软玉套:
                        Stats[Stat.MaxSC] += 2;
                        Stats[Stat.HP] += 15;
                        Stats[Stat.MP] += 20;
                        Stats[Stat.神圣] += 1;
                        Stats[Stat.准确] += 1;
                        break;
                    case ItemSet.贵人战套:
                        Stats[Stat.MaxDC] += 1;
                        Stats[Stat.背包负重] += 25;
                        break;
                    case ItemSet.贵人法套:
                        Stats[Stat.MaxMC] += 1;
                        Stats[Stat.背包负重] += 17;
                        break;
                    case ItemSet.贵人道套:
                        Stats[Stat.MaxSC] += 1;
                        Stats[Stat.背包负重] += 17;
                        break;
                    case ItemSet.贵人刺套:
                        Stats[Stat.MaxDC] += 1;
                        Stats[Stat.背包负重] += 20;
                        break;
                    case ItemSet.贵人弓套:
                        Stats[Stat.MaxDC] += 1;
                        Stats[Stat.背包负重] += 17;
                        break;
                    case ItemSet.龙血套装:
                        Stats[Stat.MaxSC] += 2;
                        Stats[Stat.HP] += 15;
                        Stats[Stat.MP] += 20;
                        Stats[Stat.神圣] += 1;
                        Stats[Stat.准确] += 1;
                        break;
                    case ItemSet.监视套装:
                        Stats[Stat.魔法躲避] += 1;
                        Stats[Stat.毒物躲避] += 1;
                        break;
                    case ItemSet.暴压套装:
                        Stats[Stat.MaxAC] += 1;
                        Stats[Stat.敏捷] += 1;
                        break;
                    case ItemSet.青玉套装:
                        Stats[Stat.MinDC] += 1;
                        Stats[Stat.MaxDC] += 1;
                        Stats[Stat.MinMC] += 1;
                        Stats[Stat.MaxMC] += 1;
                        Stats[Stat.腕力负重] += 1;
                        Stats[Stat.装备负重] += 2;
                        break;
                    case ItemSet.强青玉套:
                        Stats[Stat.MinDC] += 1;
                        Stats[Stat.MaxDC] += 2;
                        Stats[Stat.MaxMC] += 2;
                        Stats[Stat.准确] += 1;
                        Stats[Stat.HP] += 50;
                        break;
                    case ItemSet.鏃未套装:
                        Stats[Stat.MP] += 25;
                        Stats[Stat.攻击速度] += 2;
                        break;
                }
            }
        }
        private void RefreshMirSetStats()
        {
            if (MirSet.Contains(EquipmentSlot.武器) && MirSet.Contains(EquipmentSlot.盔甲))
            {
                Stats[Stat.攻击增伤] += 15;
            }
            if (MirSet.Contains(EquipmentSlot.头盔) && MirSet.Contains(EquipmentSlot.靴子) && MirSet.Contains(EquipmentSlot.腰带))
            {
                Stats[Stat.MaxDC] += 3;
                Stats[Stat.MaxMC] += 3;
                Stats[Stat.MaxSC] += 3;
                Stats[Stat.腕力负重] += 20;
            }
            if (MirSet.Contains(EquipmentSlot.项链) &&
               (MirSet.Contains(EquipmentSlot.左手镯) || MirSet.Contains(EquipmentSlot.右手镯)) &&
               (MirSet.Contains(EquipmentSlot.左戒指) || MirSet.Contains(EquipmentSlot.右戒指)))
            {
                Stats[Stat.MinDC] += 2;
                Stats[Stat.MaxDC] += 6;
                Stats[Stat.MinMC] += 2;
                Stats[Stat.MaxMC] += 6;
                Stats[Stat.MinSC] += 2;
                Stats[Stat.MaxSC] += 6;
                Stats[Stat.攻击速度] += 2;
                Stats[Stat.背包负重] += 60;
                Stats[Stat.装备负重] += 30;
                Stats[Stat.腕力负重] += 30;
            }
            if (MirSet.Contains(EquipmentSlot.盔甲) &&
                MirSet.Contains(EquipmentSlot.武器) &&
                MirSet.Contains(EquipmentSlot.头盔) &&
                MirSet.Contains(EquipmentSlot.靴子) &&
                MirSet.Contains(EquipmentSlot.腰带) &&
                MirSet.Contains(EquipmentSlot.项链) &&
               (MirSet.Contains(EquipmentSlot.左手镯) || MirSet.Contains(EquipmentSlot.右手镯)) &&
               (MirSet.Contains(EquipmentSlot.左戒指) || MirSet.Contains(EquipmentSlot.右戒指)))
            {
                Stats[Stat.MinAC] += 2;
                Stats[Stat.MaxAC] += 6;
                Stats[Stat.MinMAC] += 1;
                Stats[Stat.MaxMAC] += 4;
                Stats[Stat.幸运] += 2;
                Stats[Stat.HP] += 100;
                Stats[Stat.MP] += 100;
                Stats[Stat.中毒恢复] += 2;
            }
        }
        public void RefreshStatCaps()
        {
            foreach (var cap in Settings.ClassBaseStats[(byte)Class].Caps.Values)
            {
                Stats[cap.Key] = Math.Min(cap.Value, Stats[cap.Key]);
            }

            Stats[Stat.HP] = Math.Max(0, Stats[Stat.HP]);
            Stats[Stat.MP] = Math.Max(0, Stats[Stat.MP]);

            Stats[Stat.MinAC] = Math.Max(0, Stats[Stat.MinAC]);
            Stats[Stat.MaxAC] = Math.Max(0, Stats[Stat.MaxAC]);
            Stats[Stat.MinMAC] = Math.Max(0, Stats[Stat.MinMAC]);
            Stats[Stat.MaxMAC] = Math.Max(0, Stats[Stat.MaxMAC]);
            Stats[Stat.MinDC] = Math.Max(0, Stats[Stat.MinDC]);
            Stats[Stat.MaxDC] = Math.Max(0, Stats[Stat.MaxDC]);
            Stats[Stat.MinMC] = Math.Max(0, Stats[Stat.MinMC]);
            Stats[Stat.MaxMC] = Math.Max(0, Stats[Stat.MaxMC]);
            Stats[Stat.MinSC] = Math.Max(0, Stats[Stat.MinSC]);
            Stats[Stat.MaxSC] = Math.Max(0, Stats[Stat.MaxSC]);

            Stats[Stat.MinDC] = Math.Min(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            Stats[Stat.MinMC] = Math.Min(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
            Stats[Stat.MinSC] = Math.Min(Stats[Stat.MinSC], Stats[Stat.MaxSC]);
        }
        #endregion

        private void AddTempSkills(IEnumerable<string> skillsToAdd)
        {
            foreach (var skill in skillsToAdd)
            {
                Spell spelltype;
                bool hasSkill = false;

                if (!Enum.TryParse(skill, out spelltype)) return;

                for (var i = Info.Magics.Count - 1; i >= 0; i--)
                    if (Info.Magics[i].Spell == spelltype) hasSkill = true;

                if (hasSkill) continue;

                var magic = new UserMagic(spelltype) { IsTempSpell = true };
                Info.Magics.Add(magic);
                SendMagicInfo(magic);                
            }
        }
        public virtual void SendMagicInfo(UserMagic magic)
        {
            Enqueue(magic.GetInfo(false));
        }

        /// <summary>
        /// 新增技能的联动授予：
        ///   学会「万效符」        → 自动学会「灵魂火符(万效)」
        ///   学会「万效符-秘籍」    → 自动学会「灵魂火符(万效-秘)」
        ///   学会「落雷杀-秘笈」    → 自动学会「灭杀」
        /// </summary>
        public void GrantLinkedMagics(Spell learned)
        {
            switch (learned)
            {
                case Spell.GreatTalisman:
                    GrantMagic(Spell.SoulFireBallPlus);
                    break;
                case Spell.GreatTalismanRare:
                    GrantMagic(Spell.SoulFireBallPlusRare);
                    break;
                case Spell.SkyThunderShotRare:
                    GrantMagic(Spell.DeathBlow);
                    break;
            }
        }

        public void GrantMagic(Spell spell)
        {
            for (int i = 0; i < Info.Magics.Count; i++)
                if (Info.Magics[i].Spell == spell) return;

            UserMagic magic = new UserMagic(spell);
            if (magic.Info == null) return;

            Info.Magics.Add(magic);
            SendMagicInfo(magic);
        }
        private void RemoveTempSkills(IEnumerable<string> skillsToRemove)
        {
            foreach (var skill in skillsToRemove)
            {
                if (!Enum.TryParse(skill, out Spell spelltype)) return;

                for (var i = Info.Magics.Count - 1; i >= 0; i--)
                {
                    if (!Info.Magics[i].IsTempSpell || Info.Magics[i].Spell != spelltype) continue;

                    Info.Magics.RemoveAt(i);
                    Enqueue(new S.RemoveMagic { PlaceId = i });
                }
            }
        }
        private void RefreshSkills()
        {
            int[] spiritSwordLvPlus = { 0, 3, 5, 8 };
            int[] slayingLvPlus = { 5, 6, 7, 8 };
            for (int i = 0; i < Info.Magics.Count; i++)
            {
                UserMagic magic = Info.Magics[i];
                switch (magic.Spell)
                {
                    case Spell.Fencing:
                        Stats[Stat.准确] += magic.Level * 3;
                        // Stats[Stat.MaxAC] += (magic.Level + 1) * 3;
                        break;
                    // case Spell.FatalSword:
                    case Spell.Slaying:
                        Stats[Stat.准确] += magic.Level;
                        Stats[Stat.MaxDC] += slayingLvPlus[magic.Level];
                        break;
                    case Spell.SpiritSword:
                        Stats[Stat.准确] += spiritSwordLvPlus[magic.Level];
                        // Stats[Stat.准确] += magic.Level;
                        // Stats[Stat.MaxDC] += (int)(Stats[Stat.MaxSC] * (magic.Level + 1) * 0.1F);
                        break;
                }
            }
        }
        private void RefreshBuffs()
        {
            short Old_TransformType = TransformType;

            TransformType = -1;

            for (int i = 0; i < Buffs.Count; i++)
            {
                Buff buff = Buffs[i];

                if (buff.Paused) continue;

                Stats.Add(buff.Stats);

                if (buff.Values != null && buff.Values.Length > 0)
                {
                    switch (buff.Type)
                    {
                        case BuffType.变形效果:
                            TransformType = (short)buff.Values[0];
                            FastRun = true;
                            break;
                    }
                }
            }

            if (Old_TransformType != TransformType)
            {
                Broadcast(new S.TransformUpdate { ObjectID = ObjectID, TransformType = TransformType });
            }
        }
        public void BroadcastColourChange()
        {
            if (CurrentMap == null) return;

            for (int i = CurrentMap.Players.Count - 1; i >= 0; i--)
            {
                PlayerObject player = CurrentMap.Players[i];
                if (player == this) continue;

                if (Functions.InRange(CurrentLocation, player.CurrentLocation, Globals.DataRange))
                    player.Enqueue(new S.ObjectColourChanged { ObjectID = ObjectID, NameColour = player.GetNameColour(this) });
            }
        }
        public virtual void GainExp(uint amount) { }
        public int ReduceExp(uint amount, uint targetLevel)
        {
            int expPoint;

            if (Level < targetLevel + 10 || !Settings.ExpMobLevelDifference)
            {
                expPoint = (int)amount;
            }
            else
            {
                expPoint = (int)amount - (int)Math.Round(Math.Max(amount / 15, 1) * ((double)Level - (targetLevel + 10)));
            }

            if (expPoint <= 0) expPoint = 1;

            return expPoint;
        }
        public override void BroadcastInfo()
        {
            Packet p;
            if (CurrentMap == null) return;

            for (int i = CurrentMap.Players.Count - 1; i >= 0; i--)
            {
                PlayerObject player = CurrentMap.Players[i];
                if (player == this) continue;

                if (Functions.InRange(CurrentLocation, player.CurrentLocation, Globals.DataRange))
                {
                    p = GetInfoEx(player);
                    if (p != null)
                        player.Enqueue(p);
                }
            }
        }
        public virtual bool CheckMovement(Point location)
        {
            return false;
        }

        /// <summary>
        /// 「穿人」的细粒度判定：按对象类型分别查《服务器设置 → 其他选项》里的 4 个开关。
        /// 玩家/英雄 → 穿人；怪物 → 穿怪（但城门、城墙这类 BlockWalkThrough 对象归「穿城墙」那一档）；
        /// NPC/商人 → 穿NPC。
        /// 只有开关开着才允许穿过去，否则照常被挡住。
        /// </summary>
        private static bool CanWalkThrough(MapObject ob)
        {
            switch (ob.Race)
            {
                case ObjectType.Player:
                case ObjectType.Hero:
                    return Settings.EnableWalkThrough;
                case ObjectType.Monster:
                    return ob.BlockWalkThrough ? Settings.EnableWalkThroughWall : Settings.EnableWalkThroughMonster;
                case ObjectType.Merchant:
                    return Settings.EnableWalkThroughNPC;
                default:
                    return false;
            }
        }
        public bool Walk(MirDirection dir)
        {
            //★防御（10-25）：假人脑在对象刚被回收/尚未落图时仍可能跑一次移动，
            //CurrentMap 为空会在 ValidPoint 处 NRE（用户报障）。返回 false 让调用方自然放弃。
            if (CurrentMap == null || Stats == null) return false;

            if (!CanMove || !CanWalk)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return false;
            }

            Point location = Functions.PointMove(CurrentLocation, dir, 1);

            if (!CurrentMap.ValidPoint(location))
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return false;
            }

            if (!CurrentMap.CheckDoorOpen(location))
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return false;
            }


            Cell cell = CurrentMap.GetCell(location);
            if (cell.Objects != null)
            {
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];

                    // 穿人：开启后忽略玩家/英雄/怪物/NPC 的阻挡。
                    // 具体放行哪几类由《服务器设置 → 其他选项》里的 4 个开关分别决定（见 CanWalkThrough）：
                    //   穿人 = EnableWalkThrough、穿怪 = EnableWalkThroughMonster、
                    //   穿NPC = EnableWalkThroughNPC、穿城墙 = EnableWalkThroughWall。
                    // 地形、普通门、障碍物始终有效。
                    if (WalkThrough && CanWalkThrough(ob)) continue;

                    if (ob.Race == ObjectType.Merchant && Race == ObjectType.Player)
                    {
                        NPCObject NPC = (NPCObject)ob;
                        if (!NPC.Visible || !NPC.IsVisibleTo(Info.Index)) continue;
                    }
                    else
                        if (!ob.Blocking || (CheckCellTime && ob.CellTime >= Envir.Time)) continue;

                    Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                    return false;
                }
            }

            if (HasBuff(BuffType.气流术, out Buff concentration))
            {
                if (!concentration.Get<bool>("Moveable"))      //气流术-秘籍：移动中不中断气功凝聚
                {
                    concentration.Set("InterruptTime", Envir.Time + (Settings.Second * 3));

                    if (!concentration.Get<bool>("Interrupted"))
                    {
                        concentration.Set("Interrupted", true);
                        UpdateConcentration(true, true);
                    }
                }
            }

            if (Hidden)
            {
                RemoveBuff(BuffType.隐身术);
            }

            Direction = dir;
            if (CheckMovement(location)) return false;

            CurrentMap.GetCell(CurrentLocation).Remove(this);
            RemoveObjects(dir, 1);

            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, 1);

            _stepCounter++;

            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                SetBindSafeZone(szi);
                InSafeZone = true;
            }
            else
                InSafeZone = false;

            if (RidingMount) DecreaseMountLoyalty(1);
            Moved();

            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + GetDelayTime(MoveDelay);          
            
            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectWalk { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            GetPlayerLocation();

            cell = CurrentMap.GetCell(CurrentLocation);

            //★防御（10-25）：空格子 Objects 可能为 null（与上方 2716 行的判空保持一致），
            //假人/英雄快速移动踩到刚清空的格子会 NRE（用户报障：假人 Walk 栈）。
            if (cell.Objects != null)
            {
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    if (cell.Objects[i].Race != ObjectType.Spell) continue;
                    SpellObject ob = (SpellObject)cell.Objects[i];

                    ob.ProcessSpell(this);
                    //break;
                }
            }

            return true;
        }
        public bool Run(MirDirection dir)
        {
            //★防御（10-25）：同 Walk——对象未落图/已回收时 CurrentMap 或 Stats 可能为空，
            //直接返回 false，不抛 NRE（假人 StepTo / 英雄 MoveTo 都会走到这里）。
            if (CurrentMap == null || Stats == null) return false;

            // 超负重：背包超重时不再被强制降级为走路
            if (!IgnoreWeight && CurrentBagWeight > Stats[Stat.背包负重])
            {
                Walk(dir);
            }

            var steps = RidingMount || ActiveSwiftFeet && !Sneaking ? 3 : 2;

            if (!CanMove || !CanWalk || !CanRun)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return false;
            }

            if (HasBuff(BuffType.气流术, out Buff concentration))
            {
                if (!concentration.Get<bool>("Moveable"))      //气流术-秘籍：移动中不中断气功凝聚
                {
                    concentration.Set("InterruptTime", Envir.Time + (Settings.Second * 3));

                    if (!concentration.Get<bool>("Interrupted"))
                    {
                        concentration.Set("Interrupted", true);
                        UpdateConcentration(true, true);
                    }
                }
            }            

            if (Hidden && !Sneaking)
            {
                RemoveBuff(BuffType.隐身术);
                RemoveBuff(BuffType.月影术);
                RemoveBuff(BuffType.烈火身);
            }

            Moved();

            Direction = dir;
            Point location = Functions.PointMove(CurrentLocation, dir, 1);
            for (int j = 1; j <= steps; j++)
            {
                location = Functions.PointMove(CurrentLocation, dir, j);
                if (!CurrentMap.ValidPoint(location))
                {
                    Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                    return false;
                }
                if (!CurrentMap.CheckDoorOpen(location))
                {
                    Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                    return false;
                }
                Cell cell = CurrentMap.GetCell(location);

                if (cell.Objects != null)
                {
                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];

                        // 穿人：奔跑路径上不穿 NPC（保持原有行为），其余按 4 档开关判定
                        if (WalkThrough && ob.Race != ObjectType.Merchant && CanWalkThrough(ob)) continue;

                        if (ob.Race == ObjectType.Merchant && Race == ObjectType.Player)
                        {
                            NPCObject NPC = (NPCObject)ob;
                            if (!NPC.Visible || !NPC.IsVisibleTo(Info.Index)) continue;
                        }
                        else
                            if (!ob.Blocking || (CheckCellTime && ob.CellTime >= Envir.Time)) continue;

                        Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                        return false;
                    }
                }
                if (CheckMovement(location)) return false;

            }
            if (RidingMount && !Sneaking)
            {
                DecreaseMountLoyalty(2);
            }

            Direction = dir;

            CurrentMap.GetCell(CurrentLocation).Remove(this);
            RemoveObjects(dir, steps);

            Point OldLocation = CurrentLocation;
            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, steps);


            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                SetBindSafeZone(szi);
                InSafeZone = true;
            }
            else
                InSafeZone = false;            

            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + GetDelayTime(MoveDelay);

            if (!RidingMount)
                _runCounter++;

            if (_runCounter > 10)
            {
                _runCounter -= 8;
                ChangeHP(-1);
            }

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectRun { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            GetPlayerLocation();

            for (int j = 1; j <= steps; j++)
            {
                location = Functions.PointMove(OldLocation, dir, j);
                Cell cell = CurrentMap.GetCell(location);
                if (cell.Objects == null) continue;
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    if (cell.Objects[i].Race != ObjectType.Spell) continue;
                    SpellObject ob = (SpellObject)cell.Objects[i];

                    ob.ProcessSpell(this);
                    //break;
                }
            }

            return true;
        }
        protected virtual void Moved()
        {
        }
        public override int Pushed(MapObject pusher, MirDirection dir, int distance)
        {
            int result = 0;
            MirDirection reverse = Functions.ReverseDirection(dir);
            Cell cell;
            for (int i = 0; i < distance; i++)
            {
                Point location = Functions.PointMove(CurrentLocation, dir, 1);

                if (!CurrentMap.ValidPoint(location)) return result;

                cell = CurrentMap.GetCell(location);

                bool stop = false;
                if (cell.Objects != null)
                    for (int c = 0; c < cell.Objects.Count; c++)
                    {
                        MapObject ob = cell.Objects[c];
                        if (!ob.Blocking) continue;
                        stop = true;
                    }
                if (stop) break;

                CurrentMap.GetCell(CurrentLocation).Remove(this);

                Direction = reverse;
                RemoveObjects(dir, 1);
                CurrentLocation = location;
                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(dir, 1);

                Moved();

                Enqueue(new S.Pushed { Direction = Direction, Location = CurrentLocation });
                Broadcast(new S.ObjectPushed { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
                GetPlayerLocation();
                result++;
            }

            if (result > 0)
            {
                if (HasBuff(BuffType.气流术, out Buff concentration))
                {
                    if (!concentration.Get<bool>("Moveable"))      //气流术-秘籍：移动中不中断气功凝聚
                    {
                        concentration.Set("InterruptTime", Envir.Time + (Settings.Second * 3));

                        if (!concentration.Get<bool>("Interrupted"))
                        {
                            concentration.Set("Interrupted", true);
                            UpdateConcentration(true, true);
                        }
                    }
                }

                cell = CurrentMap.GetCell(CurrentLocation);

                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    if (cell.Objects[i].Race != ObjectType.Spell) continue;
                    SpellObject ob = (SpellObject)cell.Objects[i];

                    ob.ProcessSpell(this);
                    //break;
                }

                SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

                if (szi != null)
                {
                    SetBindSafeZone(szi);
                    InSafeZone = true;
                }
                else
                    InSafeZone = false;
            }

            ActionTime = Envir.Time + 500;
            return result;
        }

        public void GetPlayerLocation()
        {
            if (GroupMembers == null) return;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject member = GroupMembers[i];
                
                if (member.CurrentMap.Info.BigMap <= 0) continue;
                  
                member.Enqueue(new S.SendMemberLocation { MemberName = Name, MemberLocation = CurrentLocation });
                Enqueue(new S.SendMemberLocation { MemberName = member.Name, MemberLocation = member.CurrentLocation });
            }
            Enqueue(new S.SendMemberLocation { MemberName = Name, MemberLocation = CurrentLocation });
        }



        public void RangeAttack(MirDirection dir, Point location, uint targetID)
        {
            LogTime = Envir.Time + Globals.LogDelay;

            if (Info.Equipment[(int)EquipmentSlot.武器] == null) return;
            ItemInfo RealItem = Functions.GetRealItem(Info.Equipment[(int)EquipmentSlot.武器].Info, Info.Level, Info.Class, Envir.ItemInfoList);

            if ((RealItem.Shape / Globals.ClassWeaponCount) != 2) return;
            if (Functions.InRange(CurrentLocation, location, Globals.MaxAttackRange) == false) return;

            MapObject target = null;

            if (targetID == ObjectID)
                target = this;
            else if (targetID > 0)
                target = FindObject(targetID, 10);

            if (target != null && target.Dead) return;

            if (target != null && target.Race != ObjectType.Monster && target.Race != ObjectType.Player && target.Race != ObjectType.Hero) return;

            if (target != null && !target.Dead && target.IsAttackTarget(this) && !target.IsFriendlyTarget(this))
            {
                // 记下「我在打哪个玩家」——组队的假人靠这个判断该帮队长打谁
                if (target.Race == ObjectType.Player)
                {
                    RecentPvPTarget = target;
                    RecentPvPTime = Envir.Time + 15000;
                }

                if (this is PlayerObject player &&
                   player.PMode == PetMode.FocusMasterTarget)
                {
                    foreach (MonsterObject pet in player.Pets)
                    {
                        if (pet.Race != ObjectType.Creature)
                        {
                            pet.Target = target;
                        }
                    }

                    if (player.HeroSpawned &&
                        !player.Hero.Dead)
                    {
                        player.Hero.Target = target;
                    }
                }
            }

            Direction = dir;

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });

            UserMagic magic;
            Spell spell = Spell.None;
            bool focus = false;

            if (target != null && !CanFly(target.CurrentLocation) && (Info.MentalState != 1))
            {
                target = null;
                targetID = 0;
            }

            if (target != null)
            {
                magic = GetMagic(Spell.Focus);

                if (magic != null && Envir.Random.Next(5) <= magic.Level)
                {
                    focus = true;
                    LevelMagic(magic);
                    spell = Spell.Focus;
                }

                int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);

                int damage = GetRangeAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC], distance);

                damage = ApplyArcherState(damage);

                int chanceToHit = (100 + Settings.RangeAccuracyBonus - ((100 / Globals.MaxAttackRange) * distance)) * (focus ? 2 : 1);

                if (chanceToHit < 0) chanceToHit = 0;

                int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500 + 50; //50 MS per Step

                if (Envir.Random.Next(100) < chanceToHit)
                {
                    if (target.CurrentLocation != location)
                        location = target.CurrentLocation;

                    DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + delay, target, damage, DefenceType.ACAgility, true);
                    ActionList.Add(action);
                }
                else
                {
                    DelayedAction action = new DelayedAction(DelayedType.DamageIndicator, Envir.Time + delay, target, DamageType.Miss);
                    ActionList.Add(action);
                }
            }
            else
                targetID = 0;

            Enqueue(new S.RangeAttack { TargetID = targetID, Target = location, Spell = spell });
            Broadcast(new S.ObjectRangeAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, TargetID = targetID, Target = location, Spell = spell });

            AttackTime = Envir.Time + AttackSpeed;
            ActionTime = Envir.Time + 550;
            RegenTime = Envir.Time + RegenDelay;
        }
        public void Attack(MirDirection dir, Spell spell)
        {
            LogTime = Envir.Time + Globals.LogDelay;

            bool Mined = false;
            bool MoonLightAttack = false;
            bool DarkBodyAttack = false;

            if (!CanAttack)
            {
                switch (spell)
                {
                    case Spell.Slaying:
                        Slaying = false;
                        break;
                }

                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            if (Hidden)
            {
                for (int i = 0; i < Buffs.Count; i++)
                {
                    switch (Buffs[i].Type)
                    {
                        case BuffType.月影术:
                            MoonLightAttack = true;
                            break;
                        case BuffType.烈火身:
                            DarkBodyAttack = true;
                            break;
                    }
                }

                RemoveBuff(BuffType.月影术);
                RemoveBuff(BuffType.烈火身);
            }

            byte level = 0;
            UserMagic magic;

            if (RidingMount)
            {
                spell = Spell.None;
            }

            switch (spell)
            {
                case Spell.Slaying:
                    if (!Slaying)
                        spell = Spell.None;
                    else
                    {
                        magic = GetMagic(Spell.Slaying);
                        level = magic.Level;
                    }

                    Slaying = false;
                    break;
                case Spell.DoubleSlash:
                    magic = GetMagic(spell);
                    if (magic == null || magic.Info.BaseCost + (magic.Level * magic.Info.LevelCost) > MP)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    ChangeMP(-(magic.Info.BaseCost + magic.Level * magic.Info.LevelCost));
                    break;
                case Spell.Thrusting:
                case Spell.FlamingSword:
                    magic = GetMagic(spell);
                    if ((magic == null) || (!FlamingSword && (spell == Spell.FlamingSword)))
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    break;
                case Spell.HalfMoon:
                case Spell.CrossHalfMoon:
                    magic = GetMagic(spell);
                    if (magic == null || magic.Info.BaseCost + (magic.Level * magic.Info.LevelCost) > MP)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    ChangeMP(-(magic.Info.BaseCost + magic.Level * magic.Info.LevelCost));
                    break;
                case Spell.TwinDrakeBlade:
                    magic = GetMagic(spell);
                    if (!TwinDrakeBlade || magic == null || magic.Info.BaseCost + magic.Level * magic.Info.LevelCost > MP)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    ChangeMP(-(magic.Info.BaseCost + magic.Level * magic.Info.LevelCost));
                    break;
                default:
                    spell = Spell.None;
                    break;
            }


            if (!Slaying)
            {
                magic = GetMagic(Spell.Slaying);

                if (magic != null && Envir.Random.Next(12) <= magic.Level)
                {
                    Slaying = true;
                    Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.Slaying, CanUse = Slaying });
                }
            }

            Direction = dir;

            if (RidingMount) DecreaseMountLoyalty(3);

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = spell, Level = level });

            AttackTime = Envir.Time + AttackSpeed;
            ActionTime = Envir.Time + 550;
            RegenTime = Envir.Time + RegenDelay;

            Point target = Functions.PointMove(CurrentLocation, dir, 1);

            //damabeBase = the original damage from your gear (+ bonus from moonlight and darkbody)
            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            //damageFinal = the damage you're gonna do with skills added
            int damageFinal;

            if (MoonLightAttack || DarkBodyAttack)
            {
                magic = MoonLightAttack ? GetMagic(Spell.MoonLight) : GetMagic(Spell.DarkBody);

                if (magic != null)
                {
                    damageBase += magic.GetPower();
                }
            }

            if (!CurrentMap.ValidPoint(target))
            {
                switch (spell)
                {
                    case Spell.Thrusting:
                        goto Thrusting;
                    case Spell.HalfMoon:
                        goto HalfMoon;
                    case Spell.CrossHalfMoon:
                        goto CrossHalfMoon;
                    case Spell.None:
                        Mined = true;
                        goto Mining;
                }
                return;
            }

            Cell cell = CurrentMap.GetCell(target);

            if (cell.Objects == null)
            {
                switch (spell)
                {
                    case Spell.Thrusting:
                        goto Thrusting;
                    case Spell.HalfMoon:
                        goto HalfMoon;
                    case Spell.CrossHalfMoon:
                        goto CrossHalfMoon;
                }
                return;
            }

            damageFinal = damageBase;//incase we're not using skills
            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) continue;
                if (!ob.IsAttackTarget(this)) continue;

                if (ob != null && !ob.Dead && ob.IsAttackTarget(this) && !ob.IsFriendlyTarget(this))
                {
                    // 记下「我在打哪个玩家」——组队的假人靠这个判断该帮队长打谁（打怪不记，没这个需求）
                    if (ob.Race == ObjectType.Player)
                    {
                        RecentPvPTarget = ob;
                        RecentPvPTime = Envir.Time + 15000;
                    }

                    if (this is PlayerObject player &&
                   player.PMode == PetMode.FocusMasterTarget)
                    {
                        foreach (MonsterObject pet in player.Pets)
                        {
                            if (pet.Race != ObjectType.Creature)
                            {
                                pet.Target = ob;
                            }
                        }

                        if (player.HeroSpawned &&
                            !player.Hero.Dead)
                        {
                            player.Hero.Target = ob;
                        }
                    }
                }

                //Only undead targets
                if (ob.Undead)
                {
                    damageBase = Math.Min(int.MaxValue, damageBase + Stats[Stat.神圣]);
                    damageFinal = damageBase;//incase we're not using skills
                }

                #region FatalSword
                magic = GetMagic(Spell.FatalSword);

                DefenceType defence = DefenceType.ACAgility;

                if (magic != null)
                {
                    if (!FatalSword && Envir.Random.Next(10) == 0)
                        FatalSword = true;

                    if (FatalSword)
                        damageBase = magic.GetDamage(damageBase);
                }
                #endregion

                #region MPEater
                magic = GetMagic(Spell.MPEater);

                if (magic != null)
                {
                    int baseCount = 1 + Stats[Stat.准确] / 2;
                    int maxCount = baseCount + magic.Level * 5;
                    MPEaterCount += Envir.Random.Next(baseCount, maxCount);
                    if (MPEater)
                    {
                        LevelMagic(magic);
                        damageFinal = magic.GetDamage(damageBase);
                        defence = DefenceType.ACAgility;

                        S.ObjectEffect p = new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.MPEater, EffectType = ObjectID };
                        CurrentMap.Broadcast(p, ob.CurrentLocation);

                        int addMp = 5 * (magic.Level + Stats[Stat.准确] / 4);

                        if (ob.Race == ObjectType.Player)
                        {
                            ((PlayerObject)ob).ChangeMP(-addMp);
                        }

                        ChangeMP(addMp);
                        MPEaterCount = 0;
                        MPEater = false;
                    }
                    else if (!MPEater && 100 <= MPEaterCount) MPEater = true;
                }
                #endregion

                #region Hemorrhage
                magic = GetMagic(Spell.Hemorrhage);

                if (magic != null)
                {
                    HemorrhageAttackCount += Envir.Random.Next(1, 1 + magic.Level * 2);
                    if (Hemorrhage)
                    {
                        damageFinal = magic.GetDamage(damageBase);
                        LevelMagic(magic);
                        S.ObjectEffect ef = new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.Hemorrhage };

                        CurrentMap.Broadcast(ef, ob.CurrentLocation);

                        if (ob == null || ob.Node == null) continue;

                        long calcDuration = magic.Level * 2 + Stats[Stat.幸运] / 6;

                        ob.ApplyPoison(new Poison
                        {
                            Duration = (calcDuration <= 0) ? 1 : calcDuration,
                            Owner = this,
                            PType = PoisonType.Bleeding,
                            TickSpeed = 1000,
                            Value = Stats[Stat.MaxDC] + 1
                        }, this);

                        ob.OperateTime = 0;
                        HemorrhageAttackCount = 0;
                        Hemorrhage = false;
                    }
                    else if (!Hemorrhage && 55 <= HemorrhageAttackCount) Hemorrhage = true;
                }
                #endregion

                DelayedAction action;
                switch (spell)
                {
                    case Spell.Slaying:
                        magic = GetMagic(Spell.Slaying);
                        damageFinal = magic.GetDamage(damageBase);
                        LevelMagic(magic);
                        break;
                    case Spell.DoubleSlash:
                        magic = GetMagic(Spell.DoubleSlash);
                        damageFinal = magic.GetDamage(damageBase);

                        if (defence == DefenceType.ACAgility) defence = DefenceType.MACAgility;

                        action = new DelayedAction(DelayedType.Damage, Envir.Time + 400, ob, damageFinal, DefenceType.Agility, false);
                        ActionList.Add(action);
                        LevelMagic(magic);
                        break;
                    case Spell.Thrusting:
                        magic = GetMagic(Spell.Thrusting);
                        LevelMagic(magic);
                        break;
                    case Spell.HalfMoon:
                        magic = GetMagic(Spell.HalfMoon);
                        LevelMagic(magic);
                        break;
                    case Spell.CrossHalfMoon:
                        magic = GetMagic(Spell.CrossHalfMoon);
                        LevelMagic(magic);
                        break;
                    case Spell.TwinDrakeBlade:
                        magic = GetMagic(Spell.TwinDrakeBlade);
                        damageFinal = magic.GetDamage(damageBase);
                        TwinDrakeBlade = false;
                        action = new DelayedAction(DelayedType.Damage, Envir.Time + 400,
                            ob,                     //Object (Target)
                            damageFinal,            //Damage
                            DefenceType.Agility,    //Defence to target
                            false,                  //Damage Weapon
                            magic,                  //Magic
                            true);                  //Final hit
                        ActionList.Add(action);
                        LevelMagic(magic);
                        break;
                    case Spell.FlamingSword:
                        magic = GetMagic(Spell.FlamingSword);
                        damageFinal = magic.GetDamage(damageBase);
                        FlamingSword = false;
                        defence = DefenceType.AC;
                        //action = new DelayedAction(DelayedType.Damage, Envir.Time + 400, ob, damage, DefenceType.Agility, true);
                        //ActionList.Add(action);
                        LevelMagic(magic);
                        break;
                }

                //if (ob.Attacked(this, damage, defence) <= 0) break;
                action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, ob, damageFinal, defence, true);
                ActionList.Add(action);
                break;
            }

        Thrusting:
            if (spell == Spell.Thrusting)
            {
                target = Functions.PointMove(target, dir, 1);

                if (!CurrentMap.ValidPoint(target)) return;

                cell = CurrentMap.GetCell(target);

                if (cell.Objects == null) return;

                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                    if (!ob.IsAttackTarget(this)) continue;

                    magic = GetMagic(spell);
                    damageFinal = magic.GetDamage(damageBase);
                    ob.Attacked(this, damageFinal, DefenceType.Agility, false);
                    break;
                }


            }
        HalfMoon:
            if (spell == Spell.HalfMoon)
            {
                dir = Functions.PreviousDir(dir);

                magic = GetMagic(spell);
                damageFinal = magic.GetDamage(damageBase);
                for (int i = 0; i < 4; i++)
                {
                    target = Functions.PointMove(CurrentLocation, dir, 1);
                    dir = Functions.NextDir(dir);
                    if (target == Front) continue;

                    if (!CurrentMap.ValidPoint(target)) continue;

                    cell = CurrentMap.GetCell(target);

                    if (cell.Objects == null) continue;

                    for (int o = 0; o < cell.Objects.Count; o++)
                    {
                        MapObject ob = cell.Objects[o];
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                        if (!ob.IsAttackTarget(this)) continue;

                        ob.Attacked(this, damageFinal, DefenceType.Agility, false);
                        break;
                    }
                }
            }

        CrossHalfMoon:
            if (spell == Spell.CrossHalfMoon)
            {
                magic = GetMagic(spell);
                damageFinal = magic.GetDamage(damageBase);
                for (int i = 0; i < 8; i++)
                {
                    target = Functions.PointMove(CurrentLocation, dir, 1);
                    dir = Functions.NextDir(dir);
                    if (target == Front) continue;

                    if (!CurrentMap.ValidPoint(target)) continue;

                    cell = CurrentMap.GetCell(target);

                    if (cell.Objects == null) continue;

                    for (int o = 0; o < cell.Objects.Count; o++)
                    {
                        MapObject ob = cell.Objects[o];
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                        if (!ob.IsAttackTarget(this)) continue;

                        ob.Attacked(this, damageFinal, DefenceType.Agility, false);
                        break;
                    }
                }
            }

        Mining:
            if (Mined)
            {
                if (Info.Equipment[(int)EquipmentSlot.武器] == null) return;
                if (!Info.Equipment[(int)EquipmentSlot.武器].Info.CanMine) return;
                if (Info.Equipment[(int)EquipmentSlot.武器].CurrentDura <= 0)//Stop dura 0 working. use below if you wish to break the item.
                    /*{
                        Enqueue(new S.DeleteItem { UniqueID = Info.Equipment[(int)EquipmentSlot.Weapon].UniqueID, Count = Info.Equipment[(int)EquipmentSlot.Weapon].Count });
                        Info.Equipment[(int)EquipmentSlot.Weapon] = null;
                        RefreshStats();*/
                    return;
                /*}*/
                if (CurrentMap.Mine == null) return;
                MineSpot Mine = CurrentMap.Mine[target.X, target.Y];
                if ((Mine == null) || (Mine.Mine == null)) return;
                if (Mine.StonesLeft > 0)
                {
                    Mine.StonesLeft--;
                    if (Envir.Random.Next(100) < (Mine.Mine.HitRate + (Info.Equipment[(int)EquipmentSlot.武器].GetTotal(Stat.准确)) * 10))
                    {
                        //create some rubble on the floor (or increase whats there)
                        SpellObject Rubble = null;
                        Cell minecell = CurrentMap.GetCell(CurrentLocation);
                        for (int i = 0; i < minecell.Objects.Count; i++)
                        {
                            if (minecell.Objects[i].Race != ObjectType.Spell) continue;
                            SpellObject ob = (SpellObject)minecell.Objects[i];

                            if (ob.Spell != Spell.Rubble) continue;
                            Rubble = ob;
                            Rubble.ExpireTime = Envir.Time + (5 * 60 * 1000);
                            break;
                        }
                        if (Rubble == null)
                        {
                            Rubble = new SpellObject
                            {
                                Spell = Spell.Rubble,
                                Value = 1,
                                ExpireTime = Envir.Time + (5 * 60 * 1000),
                                TickSpeed = 2000,
                                Caster = null,
                                CurrentLocation = CurrentLocation,
                                CurrentMap = this.CurrentMap,
                                Direction = MirDirection.Up
                            };
                            CurrentMap.AddObject(Rubble);
                            Rubble.Spawned();
                        }
                        if (Rubble != null)
                        {
                            ActionList.Add(new DelayedAction(DelayedType.Mine, Envir.Time + 400, Rubble));
                        }

                        //check if we get a payout
                        if (Envir.Random.Next(100) < (Mine.Mine.DropRate + Stats[Stat.采矿出矿数率]))
                        {
                            GetMinePayout(Mine.Mine);
                        }

                        DamageItem(Info.Equipment[(int)EquipmentSlot.武器], 5 + Envir.Random.Next(15));
                    }
                }
                else
                {
                    if (Envir.Time > Mine.LastRegenTick)
                    {
                        Mine.LastRegenTick = Envir.Time + Mine.Mine.SpotRegenRate * 60 * 1000;
                        Mine.StonesLeft = (byte)Envir.Random.Next(Mine.Mine.MaxStones);
                    }
                }
            }
        }
        public virtual bool TryMagic()
        {
            return !Dead && Envir.Time >= ActionTime || Envir.Time >= SpellTime;
        }
        public virtual void BeginMagic(Spell spell, MirDirection dir, uint targetID, Point location, Boolean spellTargetLock = false)
        {
            Magic(spell, dir, targetID, location, spellTargetLock);
        }

        public int MagicCost(UserMagic magic)
        {
            int cost = magic.Info.BaseCost + magic.Info.LevelCost * magic.Level;
            Spell spell = magic.Spell;
            if (spell == Spell.Teleport || spell == Spell.Blink || spell == Spell.StormEscape || spell == Spell.StormEscapeRare)
            {
                if (Stats[Stat.传送技法力消耗数率] > 0)
                {
                    cost += (cost * Stats[Stat.传送技法力消耗数率]) / 100;
                }
            }

            if (Stats[Stat.法力值消耗数率] > 0)
            {
                cost += (cost * Stats[Stat.法力值消耗数率]) / 100;
            }

            if (spell == Spell.Plague)
            {
                cost = Stats[Stat.MaxSC] + Stats[Stat.MinSC];
            }

            return cost;
        }
        public virtual MapObject DefaultMagicTarget => this;
        public void Magic(Spell spell, MirDirection dir, uint targetID, Point location, bool spellTargetLock = false)
        {
            if (!CanCast)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            UserMagic magic = GetMagic(spell);
            int castRange = magic != null ? magic.Info.Range : 0;

            //需求：万效符 / 万效符-秘籍 施放后另外挂一个「万效灵魂火符」/「万效秘灵魂火符」BUFF（5 / 10 分钟），
            //这段时间内「灵魂火符」升格为「万效灵魂火符」/「万效秘灵魂火符」；BUFF 到时自动消失即还原成普通灵魂火符。
            //★万效符技能本身的原有功能（幽灵盾/神圣战甲术/无极真气/先天气功、自身减伤、秘籍净化）完全不动，
            //  升格效果走的是独立 BUFF，互不影响。
            //注意：等级、MP 消耗、冷却都继续按「灵魂火符」自身的数据结算（magic 取 SoulFireBall）。
            if (spell == Spell.SoulFireBall || spell == Spell.SoulFireBallPlus || spell == Spell.SoulFireBallPlusRare)
            {
                bool talismanUpgraded = false;

                if (HasBuff(BuffType.万效秘灵魂火符))
                {
                    spell = Spell.SoulFireBallPlusRare;
                    talismanUpgraded = true;
                }
                else if (HasBuff(BuffType.万效灵魂火符))
                {
                    spell = Spell.SoulFireBallPlus;
                    talismanUpgraded = true;
                }

                //升格期间统一用「灵魂火符」的魔法数据；顺带兜住「老角色没学过万效版」的情况，
                //避免客户端升格后发包、服务端却报「尚未学习该技能」
                if (talismanUpgraded)
                {
                    magic = GetMagic(Spell.SoulFireBall) ?? magic;

                    //射程按「升格后的技能」放行：结算数据继续用普通版，但锁定距离两者取大，
                    //否则把 灵魂火符(万效) 的 Range 调大也不会生效（这里曾被换回普通版而失效）
                    UserMagic upgradedMagic = GetMagic(spell);
                    if (upgradedMagic != null && upgradedMagic.Info.Range > castRange)
                        castRange = upgradedMagic.Info.Range;
                }
            }

            if (magic == null)
            {
                //原来是静默失败（只回一发 UserLocation），玩家会完全看不到反应，这里补一条提示便于自查
                ReceiveChat("尚未学习该技能，无法施展", ChatType.Hint);
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            if ((location.X != 0) && (location.Y != 0) && castRange != 0 && Functions.InRange(CurrentLocation, location, castRange) == false)
            {
                ReceiveChat("目标距离太远，无法施展", ChatType.Hint);
                return;
            }

            if (Hidden)
            {
                RemoveBuff(BuffType.月影术);
                RemoveBuff(BuffType.烈火身);
            }

            AttackTime = Envir.Time + MoveDelay;
            SpellTime = Envir.Time + 1800; //Spell Delay

            if (spell != Spell.ShoulderDash)
            {
                ActionTime = Envir.Time + MoveDelay;
            }

            LogTime = Envir.Time + Globals.LogDelay;

            long delay = magic.GetDelay();

            if (magic != null && Envir.Time < (magic.CastTime + delay))
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            int cost = MagicCost(magic);

            if (cost > MP)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            RegenTime = Envir.Time + RegenDelay;
            ChangeMP(-cost);

            if (spell == Spell.ShoulderDash && Race == ObjectType.Hero)
                dir = Direction;

            Direction = dir;
            if (spell != Spell.ShoulderDash && spell != Spell.BackStep && spell != Spell.FlashDash)
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });

            MapObject target = null;

            if (targetID == ObjectID)
            {
                target = this;
            }
            else if (targetID > 0)
            {
                //搜索半径必须覆盖技能射程：原写死 10 格，射程调到 13 后，
                //11~13 格的目标会解析成 null，表现出来就是「明明点到了怪却打不出去」。
                target = FindObject(targetID, Math.Max(10, castRange));
            }

            if (target != null && target.Race != ObjectType.Monster && target.Race != ObjectType.Player && target.Race != ObjectType.Hero)
            {
                target = null;
            }

            if (target != null && !target.Dead && target.IsAttackTarget(this) && !target.IsFriendlyTarget(this))
            {
                // 记下「我在打哪个玩家」——组队的假人靠这个判断该帮队长打谁
                if (target.Race == ObjectType.Player)
                {
                    RecentPvPTarget = target;
                    RecentPvPTime = Envir.Time + 15000;
                }

                if (this is PlayerObject player &&
                   player.PMode == PetMode.FocusMasterTarget)
                {
                    foreach (MonsterObject pet in player.Pets)
                    {
                        if (pet.Race != ObjectType.Creature)
                        {
                            pet.Target = target;
                        }
                    }

                    if (player.HeroSpawned &&
                        !player.Hero.Dead)
                    {
                        player.Hero.Target = target;
                    }
                }
            }

            bool cast = true;
            byte level = magic.Level;
            switch (spell)
            {
                case Spell.FireBall:
                case Spell.GreatFireBall:
                case Spell.FrostCrunch:
                    if (!Fireball(target, magic)) targetID = 0;
                    break;
                case Spell.Healing:
                    if (target == null)
                    {
                        target = DefaultMagicTarget;
                        targetID = ObjectID;
                    }
                    Healing(target, magic);
                    break;
                case Spell.HealingRare:
                    if (target == null)
                    {
                        target = this;
                        targetID = ObjectID;
                    }
                    HealingRare(target, magic);
                    break;
                case Spell.Repulsion:
                case Spell.EnergyRepulsor:
                case Spell.FireBurst:
                    Repulsion(magic);
                    break;
                case Spell.ElectricShock:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target as MonsterObject));
                    break;
                case Spell.Poisoning:
                    if (!Poisoning(target, magic)) cast = false;
                    break;
                case Spell.HellFire:
                    HellFire(magic);
                    break;
                case Spell.ThunderBolt:
                    ThunderBolt(target, magic);
                    break;
                case Spell.SoulFireBall:
                    if (!SoulFireball(target, magic, out cast)) targetID = 0;
                    break;
                case Spell.SummonSkeleton:
                    SummonSkeleton(magic);
                    break;
                case Spell.Teleport:
                case Spell.Blink:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 200, magic, location));
                    break;
                case Spell.Hiding:
                    Hiding(magic);
                    break;
                case Spell.Haste:
                case Spell.LightBody:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));
                    break;
                case Spell.Fury:
                    FurySpell(magic, out cast);
                    break;
                case Spell.ImmortalSkin:
                    ImmortalSkin(magic, out cast);
                    break;
                case Spell.ImmortalSkinRare:
                    ImmortalSkinRare(magic, out cast);
                    break;
                case Spell.HeavenlySecrets:
                    HeavenlySecrets(magic, out cast);
                    break;
                case Spell.FireBang:
                case Spell.IceStorm:
                    FireBang(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location);
                    break;
                case Spell.MassHiding:
                    MassHiding(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.SoulShield:
                case Spell.BlessedArmour:
                    SoulShield(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.FireWall:
                    FireWall(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location);
                    break;
                case Spell.Lightning:
                    Lightning(magic);
                    break;
                case Spell.HeavenlySword:
                    HeavenlySword(magic);
                    break;
                case Spell.MassHealing:
                    MassHealing(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location);
                    break;
                case Spell.ShoulderDash:
                    ShoulderDash(magic);
                    return;
                case Spell.ThunderStorm:
                case Spell.FlameField:
                case Spell.StormEscape:
                    ThunderStorm(magic);
                    if (spell == Spell.FlameField)
                        SpellTime = Envir.Time + 2500; //Spell Delay
                    if (spell == Spell.StormEscape)
                        //Start teleport.
                        ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 750, magic, location));
                    break;
                case Spell.StormEscapeRare:
                    ThunderStorm(magic);
                    if (spell == Spell.FlameField)
                        SpellTime = Envir.Time + 2500;
                    if (spell == Spell.StormEscapeRare)
                        ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 750, magic, location));
                    break;
                case Spell.MagicShield:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, magic.GetPower(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]) + 15)));
                    break;
                case Spell.GreatFireBallRare:
                    GreatFireBallRare(target, magic, out cast);
                    break;
                case Spell.FlameDisruptor:
                    FlameDisruptor(target, magic);
                    break;
                case Spell.TurnUndead:
                    TurnUndead(target, magic);
                    break;
                case Spell.MagicBooster:
                    MagicBooster(magic);
                    break;
                case Spell.Vampirism:
                    Vampirism(target, magic);
                    break;
                case Spell.SummonShinsu:
                    SummonShinsu(magic);
                    break;
                case Spell.Purification:
                    if (target == null)
                    {
                        target = DefaultMagicTarget;
                        targetID = ObjectID;
                    }
                    Purification(target, magic);
                    break;
                case Spell.LionRoar:
                case Spell.LionRoarRare:
                case Spell.BattleCry:
                    CurrentMap.ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, CurrentLocation));
                    break;
                case Spell.Revelation:
                    Revelation(target, magic);
                    break;
                case Spell.PoisonCloud:
                    PoisonCloud(magic, location, out cast);
                    break;
                case Spell.Entrapment:
                    Entrapment(target, magic);
                    break;
                case Spell.EntrapmentRare:
                    EntrapmentRare(target, magic);
                    break;
                case Spell.BladeAvalanche:
                    BladeAvalanche(magic);
                    break;
                case Spell.SlashingBurst:
                    SlashingBurst(magic, out cast);
                    break;
                case Spell.DimensionalSword:
                    DimensionalSword(target, magic, out cast);
                    break;
                case Spell.DimensionalSwordRare:
                    DimensionalSwordRare(target, magic, out cast);
                    break;
                case Spell.Rage:
                    Rage(magic);
                    break;
                case Spell.Mirroring:
                    Mirroring(magic);
                    break;
                case Spell.Blizzard:
                    Blizzard(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.MeteorStrike:
                    MeteorStrike(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.HealingcircleRare:
                    HealingcircleRare(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.IceThrust:
                    IceThrust(magic);
                    break;
                case Spell.ProtectionField:
                    ProtectionField(magic);
                    break;
                case Spell.PetEnhancer:
                    PetEnhancer(target, magic, out cast);
                    break;
                case Spell.TrapHexagon:
                    TrapHexagon(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.Reincarnation:
                    if (target == null)
                    {
                        for (int i = CurrentMap.Players.Count - 1; i >= 0; i--)
                        {
                            if (CurrentMap.Players[i].Dead)
                            {
                                target = CurrentMap.Players[i];
                                break;
                            }
                        }
                    }
                    Reincarnation(magic, target == null ? null : target as PlayerObject, out cast);
                    break;
                case Spell.Curse:
                    Curse(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.SummonHolyDeva:
                    SummonHolyDeva(magic);
                    break;
                case Spell.Hallucination:
                    Hallucination(target, magic);
                    break;
                case Spell.EnergyShield:
                    EnergyShield(target, magic, out cast);
                    break;
                case Spell.UltimateEnhancer:
                    UltimateEnhancer(target, magic, out cast);
                    break;
                case Spell.Plague:
                    Plague(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.SwiftFeet:
                    SwiftFeet(magic, out cast);
                    break;
                case Spell.MoonLight:
                    MoonLight(magic);
                    break;
                case Spell.Trap:
                    Trap(magic, target, out cast);
                    break;
                case Spell.CatTongue:
                    CatTongue(target, magic);
                    break;
                case Spell.PoisonSword:
                    PoisonSword(magic);
                    break;
                case Spell.DarkBody:
                    DarkBody(target, magic);
                    break;
                case Spell.FlashDash:
                    FlashDash(magic);
                    return;
                case Spell.CrescentSlash:
                    CrescentSlash(magic);
                    break;
                case Spell.StraightShot:
                    if (!StraightShot(target, magic)) targetID = 0;
                    break;
                case Spell.DoubleShot:
                    if (!DoubleShot(target, magic)) targetID = 0;
                    break;
                case Spell.BackStep:
                    BackStep(magic);
                    return;
                case Spell.ExplosiveTrap:
                    ExplosiveTrap(magic, Front);
                    break;
                case Spell.DelayedExplosion:
                    if (!DelayedExplosion(target, magic)) targetID = 0;
                    break;
                case Spell.Concentration:
                    Concentration(magic);
                    break;
                case Spell.ElementalShot:
                    if (!ElementalShot(target, magic)) targetID = 0;
                    break;
                case Spell.ElementalBarrier:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, magic.GetPower(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]))));
                    break;
                case Spell.BindingShot:
                    BindingShot(magic, target, out cast);
                    break;
                case Spell.SummonVampire:
                case Spell.SummonToad:
                case Spell.SummonSnakes:
                    ArcherSummon(magic, target, location);
                    break;
                case Spell.Stonetrap:
                    ArcherSummonStone(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, out cast);
                    break;
                case Spell.VampireShot:
                case Spell.PoisonShot:
                case Spell.CrippleShot:
                    SpecialArrowShot(target, magic);
                    break;
                case Spell.NapalmShot:
                    NapalmShot(target, magic);
                    break;
                case Spell.OneWithNature:
                    OneWithNature(target, magic);
                    break;
                case Spell.MoonMist:
                    MoonMist(magic);
                    break;
                case Spell.HealingCircle:
                    HealingCircle(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location);
                    break;

                //Custom Spells
                case Spell.Portal:
                    Portal(magic, location, out cast);
                    break;
                case Spell.MeteorShower:
                    if (!MeteorShower(target, magic)) targetID = 0;
                    return;
                case Spell.FireBounce:
                    if (!FireBounce(target, magic, this)) targetID = 0;
                    break;

                //—— 新增技能（对照官网 mir2db）——

                case Spell.ThunderBoltRare: //强击-秘籍：自动对 5x5 范围内敌人各放一次雷电术
                    if (!ThunderBoltRare(target, magic)) targetID = 0;
                    break;
                case Spell.AbsorbFlame: //吸魔炎风
                case Spell.AbsorbFlameRare: //吸魔炎风-秘籍
                    AbsorbFlame(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, spell == Spell.AbsorbFlameRare, out cast);
                    break;
                case Spell.GreatTalisman: //万效符
                case Spell.GreatTalismanRare: //万效符-秘籍
                    GreatTalisman(magic, spell == Spell.GreatTalismanRare, out cast);
                    break;
                case Spell.SoulFireBallPlus: //灵魂火符(万效)
                case Spell.SoulFireBallPlusRare: //灵魂火符(万效-秘)
                    if (!SoulFireballPlus(target, magic, spell == Spell.SoulFireBallPlusRare ? 6 : 3)) targetID = 0;
                    break;
                case Spell.PetEnhancerRare: //血龙兽-秘籍
                    PetEnhancerRare(magic, out cast);
                    break;
                case Spell.CrescentSlashRare: //月华乱舞-秘籍
                    CrescentSlashRare(magic);
                    break;
                case Spell.ShadowCombo: //影华连击
                case Spell.ShadowComboRare: //影华连击-秘笈
                    if (!ShadowCombo(target, magic, spell == Spell.ShadowComboRare)) targetID = 0;
                    break;
                case Spell.SkyThunderShot: //落雷杀
                case Spell.SkyThunderShotRare: //落雷杀-秘笈
                    SkyThunderShot(magic, spellTargetLock ? (target != null ? target.CurrentLocation : location) : location, spell == Spell.SkyThunderShotRare, out cast);
                    break;
                case Spell.DeathBlow: //灭杀
                    if (!DeathBlow(target, magic)) targetID = 0;
                    break;
                case Spell.FlashDashRare: //拔刀术-秘籍（野蛮冲撞 + 拔刀术 合体连招）
                    FlashDashRare(magic);
                    return;
                case Spell.MoonMistRare: //月影雾-秘籍
                    MoonMistRare(magic);
                    break;
                case Spell.DelayedExplosionRare: //爆闪-秘籍
                    if (!DelayedExplosion(target, magic)) targetID = 0;
                    break;
                case Spell.ConcentrationRare: //气流术-秘籍
                    ConcentrationRare(magic);
                    break;

                default:
                    cast = false;
                    break;
            }

            if (cast)
            {
                magic.CastTime = Envir.Time;
            }

            //灵魂火符(万效)等多目标技能：附带副目标列表，客户端据此为每个目标画一张飞行符
            //注意：绝大多数技能不设置副目标，这里必须取快照并保证非 null，否则 S.Magic 写包会 NRE
            List<uint> secondaryTargetIDs = _secondaryTargetIDs ?? new List<uint>();
            _secondaryTargetIDs = null;

            Enqueue(new S.Magic { Spell = spell, TargetID = targetID, Target = location, Cast = cast, Level = level, SecondaryTargetIDs = secondaryTargetIDs });
            Broadcast(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = spell, TargetID = targetID, Target = location, Cast = cast, Level = level, SecondaryTargetIDs = secondaryTargetIDs });
        }

        #region Elemental System
        private void Concentration(UserMagic magic)
        {
            int duration = 45 + (15 * magic.Level);

            var buff = AddBuff(BuffType.气流术, this, Settings.Second * duration, new Stats());

            buff.Set("InterruptTime", (long)0);
            buff.Set("Interrupted", false);

            LevelMagic(magic);
            OperateTime = 0;

            UpdateConcentration(true, false);
        }
        public void UpdateConcentration(bool concentrating, bool interrupted)
        {
            Enqueue(new S.SetConcentration { ObjectID = ObjectID, Enabled = concentrating, Interrupted = interrupted });
            Broadcast(new S.SetConcentration { ObjectID = ObjectID, Enabled = concentrating, Interrupted = interrupted });
        }
        private bool ElementalShot(MapObject target, UserMagic magic)
        {
            if (HasElemental)
            {
                if (target == null || !target.IsAttackTarget(this)) return false;
                if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;

                int orbPower = GetElementalOrbPower(false);//base power + orbpower

                int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]) + orbPower);
                int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

                DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);
                ActionList.Add(action);
            }
            else
            {
                ObtainElement(true);//gather orb through casting
                LevelMagic(magic);
                return false;
            }
            return true;
        }
        public void GatherElement()
        {
            UserMagic magic = GetMagic(Spell.Meditation);

            if (magic == null) return;

            int meditationLvl = magic.Level;
            int concentrateChance = 0;

            if (HasBuff(BuffType.气流术, out Buff concentration) && !concentration.Get<bool>("Interrupted"))
            {
                magic = GetMagic(Spell.Concentration);

                if (magic != null)
                {
                    concentrateChance = magic.Level + 1;
                }
            }

            if (meditationLvl >= 0)
            {
                int rnd = Envir.Random.Next(10);
                if (rnd >= (8 - meditationLvl - concentrateChance))
                {
                    ObtainElement(false);
                    LevelMagic(GetMagic(Spell.Meditation));
                }
            }
        }
        public void ObtainElement(bool cast)
        {
            int orbType = 0;
            int meditateLevel = 0;

            UserMagic spell = GetMagic(Spell.Meditation);

            if (spell == null)
            {
                ReceiveChat("技能需要冥想", ChatType.System);
                return;
            }

            meditateLevel = spell.Level;

            int maxOrbs = (int)Settings.OrbsExpList[Settings.OrbsExpList.Count - 1];

            if (cast)
            {
                ElementsLevel = (int)Settings.OrbsExpList[0];
                orbType = 1;
                if (Settings.GatherOrbsPerLevel)//Meditation Orbs per level
                    if (meditateLevel == 3)
                    {
                        Enqueue(new S.SetElemental { ObjectID = ObjectID, Enabled = true, Value = (uint)Settings.OrbsExpList[0], ElementType = 1, ExpLast = (uint)maxOrbs });
                        Broadcast(new S.SetElemental { ObjectID = ObjectID, Enabled = true, Casted = true, Value = (uint)Settings.OrbsExpList[0], ElementType = 1, ExpLast = (uint)maxOrbs });
                        ElementsLevel = (int)Settings.OrbsExpList[1];
                        orbType = 2;
                    }

                HasElemental = true;
            }
            else
            {
                HasElemental = false;
                ElementsLevel++;

                if (Settings.GatherOrbsPerLevel)//Meditation Orbs per level
                    if (ElementsLevel > Settings.OrbsExpList[GetMagic(Spell.Meditation).Level])
                    {
                        HasElemental = true;
                        ElementsLevel = (int)Settings.OrbsExpList[GetMagic(Spell.Meditation).Level];
                        return;
                    }

                if (ElementsLevel >= Settings.OrbsExpList[0]) HasElemental = true;
                for (int i = 0; i <= Settings.OrbsExpList.Count - 1; i++)
                {
                    if (Settings.OrbsExpList[i] != ElementsLevel) continue;
                    orbType = i + 1;
                    break;
                }
            }

            Enqueue(new S.SetElemental { ObjectID = ObjectID, Enabled = HasElemental, Value = (uint)ElementsLevel, ElementType = (uint)orbType, ExpLast = (uint)maxOrbs });
            Broadcast(new S.SetElemental { ObjectID = ObjectID, Enabled = HasElemental, Casted = cast, Value = (uint)ElementsLevel, ElementType = (uint)orbType, ExpLast = (uint)maxOrbs });
        }
        public int GetElementalOrbCount()
        {
            int OrbCount = 0;
            for (int i = Settings.OrbsExpList.Count - 1; i >= 0; i--)
            {
                if (ElementsLevel >= Settings.OrbsExpList[i])
                {
                    OrbCount = i + 1;
                    break;
                }
            }
            return OrbCount;
        }
        public int GetElementalOrbPower(bool defensive)
        {
            if (!HasElemental) return 0;

            if (defensive)
                return (int)Settings.OrbsDefList[GetElementalOrbCount() - 1];

            if (!defensive)
                return (int)Settings.OrbsDmgList[GetElementalOrbCount() - 1];

            return 0;
        }
        #endregion

        #region Wizard Skills
        private bool Fireball(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);

            return true;
        }
        private void Repulsion(UserMagic magic)
        {
            bool result = false;
            for (int d = 0; d <= 1; d++)
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

                        for (int i = 0; cell.Objects != null && i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player) continue;

                            if (!ob.IsAttackTarget(this) || ob.Level >= Level) continue;

                            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + Level - ob.Level) continue;

                            int distance = 1 + Math.Max(0, magic.Level - 1) + Envir.Random.Next(2);
                            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, ob.CurrentLocation);

                            if (ob.Pushed(this, dir, distance) == 0) continue;

                            if (ob.Race == ObjectType.Player)
                            {
                                SafeZoneInfo szi = CurrentMap.GetSafeZone(ob.CurrentLocation);

                                if (szi != null)
                                {
                                    ((PlayerObject)ob).BindLocation = szi.Location;
                                    ((PlayerObject)ob).BindMapIndex = CurrentMapIndex;
                                    ob.InSafeZone = true;
                                }
                                else
                                    ob.InSafeZone = false;

                                ob.Attacked(this, magic.GetDamage(0), DefenceType.None, false);
                            }
                            result = true;
                        }
                    }
                }
            }

            if (result) LevelMagic(magic);
        }
        private void ElectricShock(MonsterObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            if (Envir.Random.Next(4 - magic.Level) > 0)
            {
                if (Envir.Random.Next(2) == 0) LevelMagic(magic);
                return;
            }

            LevelMagic(magic);

            if (target.Master == this)
            {
                target.ShockTime = Envir.Time + (magic.Level * 5 + 10) * 1000;
                target.Target = null;
                return;
            }

            if (Envir.Random.Next(2) > 0)
            {
                target.ShockTime = Envir.Time + (magic.Level * 5 + 10) * 1000;
                target.Target = null;
                return;
            }

            if (target.Level > Level + 2 || !target.Info.CanTame) return;

            if (Envir.Random.Next(Level + 20 + magic.Level * 5) <= target.Level + 10)
            {
                if (Envir.Random.Next(5) > 0 && target.Master == null)
                {
                    target.RageTime = Envir.Time + (Envir.Random.Next(20) + 10) * 1000;
                    target.Target = null;
                }
                return;
            }

            var petBonus = Globals.MaxPets - 3;

            if (Pets.Count(t => !t.Dead && t.Race != ObjectType.Creature) >= magic.Level + petBonus) return;

            int rate = (int)(target.Stats[Stat.HP] / 100);
            if (rate <= 2) rate = 2;
            else rate *= 2;

            if (Envir.Random.Next(rate) != 0) return;
            //else if (Envir.Random.Next(20) == 0) target.Die();

            if (target.Master != null)
            {
                target.SetHP(target.Stats[Stat.HP] / 10);
                target.Master.Pets.Remove(target);
            }
            else if (target.Respawn != null)
            {
                target.Respawn.Count--;
                Envir.MonsterCount--;
                CurrentMap.MonsterCount--;
                target.Respawn = null;
            }

            target.Master = this;
            //target.HealthChanged = true;
            target.BroadcastHealthChange();
            Pets.Add(target);
            target.Target = null;
            target.RageTime = 0;
            target.ShockTime = 0;
            target.OperateTime = 0;
            target.MaxPetLevel = (byte)(1 + magic.Level * 2);

            if (!Settings.PetSave)
            {
                target.TameTime = Envir.Time + (Settings.Minute * 60);
            }

            target.Broadcast(new S.ObjectName { ObjectID = target.ObjectID, Name = target.Name });
        }
        private void HellFire(UserMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction, 4);
            CurrentMap.ActionList.Add(action);

            if (magic.Level != 3) return;

            MirDirection dir = (MirDirection)(((int)Direction + 1) % 8);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, dir, 4);
            CurrentMap.ActionList.Add(action);

            dir = (MirDirection)(((int)Direction - 1 + 8) % 8);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, dir, 4);
            CurrentMap.ActionList.Add(action);
        }
        private void ThunderBolt(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            if (target.Undead) damage = (int)(damage * 1.5F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);
        }
        private void Vampirism(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }

        //==================================================================
        //  新增技能实现（数值/图标对照官网 mir2db 技能列表 49~53）
        //==================================================================

        /// <summary>收集以 centre 为中心、边长为 size 的方形内全部敌对目标（怪物/玩家/英雄）。</summary>
        private List<MapObject> GetHostileInSquare(Point centre, int size)
        {
            var list = new List<MapObject>();
            int r = (size - 1) / 2;

            for (int x = centre.X - r; x <= centre.X + r; x++)
            {
                for (int y = centre.Y - r; y <= centre.Y + r; y++)
                {
                    Point p = new Point(x, y);
                    if (!CurrentMap.ValidPoint(p)) continue;

                    Cell cell = CurrentMap.GetCell(p);
                    if (cell == null || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (ob == null || ob.Dead) continue;
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) continue;
                        if (!ob.IsAttackTarget(this)) continue;
                        if (list.Contains(ob)) continue;
                        list.Add(ob);
                    }
                }
            }
            return list;
        }

        //强击-秘籍（法师）：自动对 5x5 范围内的敌人各施放一次雷电术
        private bool ThunderBoltRare(MapObject target, UserMagic magic)
        {
            //需求：不再刷「请先选择一个可攻击的目标」——没有目标/目标已死时静默失败，
            //      与普通单体法术（灵魂火符等）保持一致。玩家连放或目标刚被抢时会一直刷屏。
            if (target == null || !target.IsAttackTarget(this))
                return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            //官网：0级基准 5 格范围；3级"攻击范围增加"（本服最高 3 级，取 7x7 近似）
            int size = magic.Level >= 3 ? 7 : 5;

            var hit = GetHostileInSquare(target.CurrentLocation, size);

            if (hit.Count == 0) return false;

            //官网：2 级起有 25% 几率落雷数量翻倍
            int times = (magic.Level >= 2 && Envir.Random.Next(4) == 0) ? 2 : 1;

            for (int t = 0; t < times; t++)
            {
                for (int i = 0; i < hit.Count; i++)
                {
                    MapObject ob = hit[i];
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500 + (t * 300), magic, damage, ob, ob.CurrentLocation));
                }
            }
            return true;
        }

        //吸魔炎风（法师）：在指定位置卷起火焰旋风，把普通怪物卷进来并持续造成伤害（秘籍版持续 6 秒）
        private void AbsorbFlame(UserMagic magic, Point location, bool rare, out bool cast)
        {
            cast = false;
            if (!CurrentMap.ValidPoint(location))
            {
                ReceiveChat("该位置无法施展吸魔炎风", ChatType.Hint);
                return;
            }

            int duration = rare ? 6 : 3;
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));
            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500;

            CurrentMap.ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, damage, location, duration));
            cast = true;
        }

        //万效符（道士）：给自身与范围内队友挂上幽灵盾/神圣战甲术/无极真气/先天气功，自身额外减伤
        /// <summary>万效符施放后「灵魂火符 → 万效灵魂火符」的升格 BUFF 时长（秒）</summary>
        private const int GreatTalismanUpgradeDuration = 300;        // 5 分钟
        /// <summary>万效符-秘籍施放后「灵魂火符 → 万效秘灵魂火符」的升格 BUFF 时长（秒）</summary>
        private const int GreatTalismanUpgradeRareDuration = 600;    // 10 分钟

        private void GreatTalisman(UserMagic magic, bool rare, out bool cast)
        {
            cast = false;

            UserItem item = GetAmulet(1);
            if (item == null)
            {
                ReceiveChat("必须装备护身符才能施展万效符", ChatType.Hint);
                return;
            }
            cast = true;

            int duration = 30 + (magic.Level + 1) * 20;                    //秒
            int power = magic.GetPower(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));
            //官网：万效符自身减伤 10%；万效符-秘籍 0级减伤 20%，之后每级再 +5%（25% / 35%）
            int reduction = rare ? (20 + magic.Level * 5) : 10;             //减伤 %
            int scBonus = Math.Max(1, Stats[Stat.MaxSC] / 5);

            var targets = new List<HumanObject> { this };

            if (GroupMembers != null)
            {
                for (int i = 0; i < GroupMembers.Count; i++)
                {
                    HumanObject member = GroupMembers[i];
                    if (member == null || member.Node == null || member.Dead) continue;
                    if (member.CurrentMap != CurrentMap) continue;
                    if (Functions.MaxDistance(CurrentLocation, member.CurrentLocation) > 5) continue;
                    if (targets.Contains(member)) continue;
                    targets.Add(member);
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                HumanObject ob = targets[i];

                ob.AddBuff(BuffType.幽灵盾, this, Settings.Second * duration, new Stats { [Stat.MaxMAC] = ob.Level / 7 + 4 });
                ob.AddBuff(BuffType.神圣战甲术, this, Settings.Second * duration, new Stats { [Stat.MaxAC] = ob.Level / 7 + 4 });
                ob.AddBuff(BuffType.无极真气, this, Settings.Second * duration, new Stats { [Stat.MaxSC] = scBonus });
                ob.AddBuff(BuffType.先天气功, this, Settings.Second * duration, new Stats
                {
                    [Stat.气功盾恢复数率] = 10,
                    [Stat.气功盾恢复生命值] = power
                });

                //万效符-秘籍：额外净化 1 次
                if (rare)
                {
                    for (int k = 0; k < ob.Buffs.Count; k++)
                    {
                        var buff = ob.Buffs[k];
                        if (!buff.Properties.HasFlag(BuffProperty.Debuff)) continue;
                        ob.RemoveBuff(buff.Type);
                    }

                    if (ob.PoisonList.Any(x => x.PType == PoisonType.DelayedExplosion))
                    {
                        ob.ExplosionInflictedTime = 0;
                        ob.ExplosionInflictedStage = 0;
                    }

                    ob.PoisonList.Clear();
                }

                ob.OperateTime = 0;
            }

            AddBuff(rare ? BuffType.万效符秘笈 : BuffType.万效符, this, Settings.Second * duration, new Stats
            {
                [Stat.伤害降低数率] = reduction
            });

            //★需求：万效符 / 万效符-秘籍 施放后，额外挂一个「灵魂火符升格」BUFF（普通 5 分钟 / 秘籍 10 分钟）。
            //  该 BUFF 只负责把「灵魂火符」升格为「万效灵魂火符」/「万效秘灵魂火符」，到时自动消失即还原成普通灵魂火符。
            //  ★万效符原有功能（护盾、减伤、净化）与时长完全不动，这里是纯新增的独立 BUFF。
            AddBuff(rare ? BuffType.万效秘灵魂火符 : BuffType.万效灵魂火符, this,
                Settings.Second * (rare ? GreatTalismanUpgradeRareDuration : GreatTalismanUpgradeDuration), new Stats());

            //确保客户端已学会对应的「万效版灵魂火符」，否则客户端升格后发来的施法包在本地取不到技能数据
            GrantMagic(rare ? Spell.SoulFireBallPlusRare : Spell.SoulFireBallPlus);

            OperateTime = 0;

            ConsumeItem(item, 1);
            LevelMagic(magic);
        }

        //灵魂火符(万效)（道士）：向范围内的敌人连续施放 N 张灵魂火符（秘籍版 4 张）
        private List<uint> _secondaryTargetIDs;   //灵魂火符(万效)等多目标技能的副目标，随 S.Magic/S.ObjectMagic 下发

        //灵魂火符(万效) / (万效-秘) 的副目标搜索范围：13×13 格（半径 6 格）。
        //原来是 5×5（半径 2 格），以主目标为圆心，怪稍微散开就补不到副目标，表现为只飞一张符。
        private const int SoulFireBallPlusSpreadSize = 13;

        //灵魂火符(万效)：一次飞出 count 张符，同时攻击 count 个目标（秘籍 6 张，普通 3 张）
        private bool SoulFireballPlus(MapObject target, UserMagic magic, int count)
        {
            UserItem item = GetAmulet(1);
            if (item == null)
            {
                ReceiveChat("必须装备护身符才能施展灵魂火符(万效)", ChatType.Hint);
                return false;
            }

            //没有可攻击目标时静默失败：和普通「灵魂火符」保持一致，
            //不再刷「请先选择一个可攻击的目标」（玩家连放时这句会一直刷屏）
            if (target == null || !target.IsAttackTarget(this))
                return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            //主目标优先，其余从「施法者为圆心」的范围内补充，最多 count 个目标、一符一目标。
            //原先是以主目标为圆心取 5×5（半径只有 2 格）：怪一旦不是挤在目标身边，
            //就一个副目标都补不进来，表现出来就是「明明周围有几只怪却只飞出一张符」。
            //改为施法者为圆心、半径 6 格（13×13，比原半径 2 格多 4 格），并按离自身远近排序。
            var targets = new List<MapObject> { target };
            var candidates = GetHostileInSquare(CurrentLocation, SoulFireBallPlusSpreadSize);
            candidates.Sort((a, b) =>
            {
                int da = Functions.MaxDistance(CurrentLocation, a.CurrentLocation);
                int db = Functions.MaxDistance(CurrentLocation, b.CurrentLocation);
                return da != db ? da.CompareTo(db) : a.ObjectID.CompareTo(b.ObjectID);
            });

            foreach (var ob in candidates)
            {
                if (targets.Count >= count) break;
                if (ob == target || !ob.IsAttackTarget(this)) continue;
                targets.Add(ob);
            }

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int baseDelay = distance * 50 + 500;

            _secondaryTargetIDs = new List<uint>();
            for (int i = 1; i < targets.Count; i++)
                _secondaryTargetIDs.Add(targets[i].ObjectID);

            foreach (var ob in targets)
                ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + baseDelay, magic, damage, ob, ob.CurrentLocation));

            ConsumeItem(item, 1);
            return true;
        }

        //血龙兽-秘籍（道士）：血龙水的增强版。
        //
        //需求（2026-10-10 澄清）：
        //  一次性把「身上所有召唤技能」全部施放一遍（召唤骷髅 / 召唤神兽 / 精魂召唤术），
        //  把能召唤的召唤物一次全叫出来，然后给身上所有召唤物统一挂上「血龙水」（宠物身上出特效），
        //  持续时间 = 普通血龙水的 2 倍。
        //  注意：不再是「把已有召唤物召回 + 强化」，也不是「只强化已有的那几只」。
        private void PetEnhancerRare(UserMagic magic, out bool cast)
        {
            cast = false;

            //本服道士的三个召唤技能；没学会的直接跳过（GetMagic 返回 null）。
            //各自沿用原有规则：已有同名召唤物则召回；缺护身符材料 / 达数量上限则该项跳过。
            Spell[] summonSpells = { Spell.SummonSkeleton, Spell.SummonShinsu, Spell.SummonHolyDeva };
            string[] summonPetNames = { Settings.SkeletonName, Settings.ShinsuName, Settings.AngelName };
            int[] summonAmuletCosts = { 1, 5, 2 };

            //先做一次护身符预算检查（只用于提示）：材料不够时召唤会静默跳过，
            //玩家会以为「秘籍没生效」，所以提前算一遍并给出原因。
            bool amuletShort = false;

            int amuletLeft = 0;
            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem equipped = Info.Equipment[i];
                if (equipped == null) continue;
                if (equipped.Info.Type != ItemType.护身符 || equipped.Info.Shape != 0) continue;

                amuletLeft += equipped.Count;
            }

            for (int i = 0; i < summonSpells.Length; i++)
            {
                if (GetMagic(summonSpells[i]) == null) continue;

                //身上已有同名召唤物 → 走「召回」流程，不消耗护身符
                bool exists = false;
                for (int p = 0; p < Pets.Count; p++)
                {
                    MonsterObject check = Pets[p];
                    if (check == null || check.Dead || check.Node == null) continue;
                    if (check.Info.Name != summonPetNames[i]) continue;

                    exists = true;
                    break;
                }

                if (exists) continue;

                if (amuletLeft < summonAmuletCosts[i])
                {
                    amuletShort = true;
                    continue;
                }

                amuletLeft -= summonAmuletCosts[i];
            }

            bool hasSummonSpell = false;

            for (int i = 0; i < summonSpells.Length; i++)
            {
                UserMagic summonMagic = GetMagic(summonSpells[i]);
                if (summonMagic == null) continue;

                hasSummonSpell = true;

                switch (summonSpells[i])
                {
                    case Spell.SummonSkeleton:
                        SummonSkeleton(summonMagic);
                        break;
                    case Spell.SummonShinsu:
                        SummonShinsu(summonMagic);
                        break;
                    case Spell.SummonHolyDeva:
                        SummonHolyDeva(summonMagic);
                        break;
                }
            }

            if (amuletShort)
                ReceiveChat("护身符不足，部分召唤物未能召唤", ChatType.Hint);

            bool hasPets = false;
            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject pet = Pets[i];
                if (pet == null || pet.Dead || pet.Node == null) continue;

                hasPets = true;
                break;
            }

            //没学会任何召唤技能、身上也没宠物 —— 这本秘籍就没有任何作用，给出提示
            if (!hasSummonSpell && !hasPets)
            {
                ReceiveChat("尚未学会任何召唤技能，身上也没有召唤物", ChatType.Hint);
                return;
            }

            //血龙水持续时间 =（道术攻击力 + 技能强度）× 2（普通血龙水的 2 倍）
            int duration = (GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) + magic.GetPower()) * 2;

            //召唤是延迟落地的（召唤骷髅 / 召唤神兽 500ms、精魂召唤术 1500ms），
            //所以往后推一段时间，等全部召唤物都进 Pets 了再统一挂血龙水，
            //否则刚召唤出来的那几只会被漏掉。
            ActionList.Add(new DelayedAction(DelayedType.PetEnhancerRareApply, Envir.Time + PetEnhancerRareApplyDelay, duration));

            cast = true;
            LevelMagic(magic);
        }

        //「一次性召唤」全部落地所需等待时间（大于精魂召唤术的 1500ms）
        private const int PetEnhancerRareApplyDelay = 2000;

        //血龙兽-秘籍：等召唤全部落地后，统一给身上所有召唤物挂上血龙水（时长已按 2 倍算好）
        protected void CompletePetEnhancerRareApply(IList<object> data)
        {
            if (Dead) return;

            int duration = (int)data[0];

            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject pet = Pets[i];
                if (pet == null || pet.Dead || pet.Node == null) continue;

                int dcInc = 2 + pet.Level * 2;
                int acInc = 4 + pet.Level;

                //血龙水本体效果（DC / AC 提升），客户端会按 BuffType.血龙水 播放宠物身上的血龙水特效
                pet.AddBuff(BuffType.血龙水, this, Settings.Second * duration, new Stats
                {
                    [Stat.MinDC] = dcInc,
                    [Stat.MaxDC] = dcInc,
                    [Stat.MinAC] = acInc,
                    [Stat.MaxAC] = acInc
                });
            }
        }

        //月华乱舞-秘籍（刺客）：默认攻击范围 4 个方向各 3 格，2 段伤害并眩晕
        private void CrescentSlashRare(UserMagic magic)
        {
            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (Envir.Random.Next(0, 100) <= Stats[Stat.准确])
                damageBase += damageBase;
            int damageFinal = magic.GetDamage(damageBase);

            //默认攻击范围：4 个方向（朝向 -1 / 朝向 / 朝向 +1 / 朝向 +2），每个方向 3 格
            const int slashDirs = 4;
            const int slashReach = 3;

            var hitTargets = new List<MapObject>();

            for (int d = 0; d < slashDirs; d++)
            {
                MirDirection dir = (MirDirection)(((int)Direction + d - 1 + 8) % 8);

                for (int step = 1; step <= slashReach; step++)
                {
                    Point hitPoint = Functions.PointMove(CurrentLocation, dir, step);
                    if (!CurrentMap.ValidPoint(hitPoint)) break;

                    Cell cell = CurrentMap.GetCell(hitPoint);
                    if (cell == null || cell.Objects == null) continue;

                    for (int j = 0; j < cell.Objects.Count; j++)
                    {
                        MapObject target = cell.Objects[j];
                        if (target.Race != ObjectType.Monster && target.Race != ObjectType.Player) continue;
                        if (!target.IsAttackTarget(this)) continue;
                        if (hitTargets.Contains(target)) continue;

                        hitTargets.Add(target);
                    }
                }
            }

            for (int i = 0; i < hitTargets.Count; i++)
            {
                MapObject target = hitTargets[i];

                //2 段伤害
                ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + AttackSpeed, target, damageFinal, DefenceType.AC, true));
                ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + AttackSpeed + 150, target, damageFinal, DefenceType.AC, true));

                //眩晕（官网：0级基准眩晕5秒，每级+1秒）
                target.ApplyPoison(new Poison
                {
                    Owner = this,
                    PType = PoisonType.Stun,
                    Duration = 5 + magic.Level,
                    TickSpeed = 1000
                }, this);

                //眩晕期间降低 (1 + 等级) 点准确（官网：0级降1点，每级+1点）
                target.AddBuff(BuffType.月华乱舞减准, this, Settings.Second * (5 + magic.Level), new Stats
                {
                    [Stat.准确] = -(1 + magic.Level)
                });

                LevelMagic(magic);
            }
        }

        //影华连击（刺客）：化成虚影（隐藏 + 免疫伤害），位移至正前方 2 格（秘笈 3 格）后对目标连续攻击
        private bool ShadowCombo(MapObject target, UserMagic magic, bool rare)
        {
            //★类型必须先判：ItemObject 的 IsAttackTarget 是直接 throw 的，
            //  FindObject(targetID) 有可能返回玩家脚边的掉落物，不能无脑调用。
            //★需求：不再刷「请先选择一个可攻击的目标」，没有合法目标时静默失败。
            if (target == null || (!(target is HumanObject) && !(target is MonsterObject)) || !target.IsAttackTarget(this))
                return false;
            if (Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 1)
            {
                ReceiveChat("影华连击必须贴身（1 格内）才能施展", ChatType.Hint);
                return false;
            }
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]));

            //官网：影华连击-秘籍 3 级时解除自身所有状态异常（状态异常中也可使用）
            if (rare && magic.Level >= 3)
            {
                for (int k = 0; k < Buffs.Count; k++)
                {
                    var buff = Buffs[k];
                    if (!buff.Properties.HasFlag(BuffProperty.Debuff)) continue;
                    RemoveBuff(buff.Type);
                }

                PoisonList.Clear();
            }

            //化虚影：人物隐藏（变成影子）+ 化虚影期间免疫伤害
            //Hidden 的 setter 只广播给别人、不含自己，所以要单独 Enqueue 一份
            //隐藏时长与客户端特效同步：只播当前朝向那一个方向的 20 帧
            int duration = rare ? ShadowComboRareDuration : ShadowComboDuration;

            Hidden = true;
            Enqueue(new S.ObjectHidden { ObjectID = ObjectID, Hidden = true });
            //隐藏比位移多持续 ShadowComboStealthExtra，确保位移先生效、人物再在新位置显形
            AddBuff(BuffType.影化连击, this, duration + ShadowComboStealthExtra, new Stats { [Stat.伤害降低数率] = 100 });

            //需求：特效播完后人物才位移，落点为正前方 2 格；被地形挡住就停在能走到的最远处。
            //★这里**绝不能把生物当阻挡**：影华连击要求贴身（目标就在正前方 1 格），
            //  一旦用生物阻挡，第一格就被占住、destination 始终等于 CurrentLocation，
            //  下面那句 if (destination != CurrentLocation) 永远为 false → 延迟位移根本不会入队，
            //  表现就是「释放完技能人物没有位移」（这就是之前那个 bug 的根因）。
            //  改用 CanFly（只看地图边界 + 地形是否可走，**忽略生物**）实现「化虚影穿透」。
            int jump = 2;
            Point destination = CurrentLocation;

            for (int i = 0; i < jump; i++)
            {
                Point next = Functions.PointMove(destination, Direction, 1);
                if (!CanFly(next)) break;
                destination = next;
            }

            if (destination != CurrentLocation)
            {
                ActionList.Add(new DelayedAction(DelayedType.ShadowComboDash, Envir.Time + duration, destination, Direction));
            }

            //★★ 群体连击：影华连击/秘籍是群攻技能，打的是**自身周围 3×3**。
            //  每个方向的挥砍动作各算一次攻击 → 整段特效连续造成若干次伤害（普通 6 次 / 秘籍 10 次），
            //  每一次伤害都同时结算 3×3 范围内的**所有**可攻击目标。
            List<MapObject> targets = new List<MapObject>();

            for (int x = CurrentLocation.X - 1; x <= CurrentLocation.X + 1; x++)
            {
                for (int y = CurrentLocation.Y - 1; y <= CurrentLocation.Y + 1; y++)
                {
                    Point p = new Point(x, y);
                    if (!CurrentMap.ValidPoint(p)) continue;

                    Cell cell = CurrentMap.GetCell(p);
                    if (cell == null || cell.Objects == null) continue;

                    for (int k = 0; k < cell.Objects.Count; k++)
                    {
                        MapObject ob = cell.Objects[k];
                        if (ob == this) continue;

                        //★必须先按**类型**过滤再问 IsAttackTarget：
                        //  ItemObject（地上的掉落物）的 IsAttackTarget 是直接 throw NotSupportedException 的，
                        //  不分青红皂白地调用会把内循环线程打断（曾经就是这里报 NotSupportedException）。
                        //  能被打的只有 HumanObject（玩家/英雄）和 MonsterObject（怪物/宠物）。
                        if (!(ob is HumanObject) && !(ob is MonsterObject)) continue;

                        if (ob.Dead) continue;
                        if (!ob.IsAttackTarget(this)) continue;
                        if (targets.Contains(ob)) continue;

                        targets.Add(ob);
                    }
                }
            }

            //兜底：3×3 扫描没命中任何目标时（例如目标刚好在被地形判定挡住的格上），
            //至少把玩家选定的那个目标打进连击，避免「放了技能却没伤害」。
            if (targets.Count == 0) targets.Add(target);

            //连击伤害：按动画节奏均匀分布到**挥砍主体动画**（ShadowComboDuration）里
            //（动画期间人物隐藏，伤害不能挤在最前面打完，否则动画还没播完怪就已经倒地）
            //秘籍的收招爆炸段（2000-2600ms）留给下面那记「最后一击」，不与连击段混在一起。
            int hits = rare ? ShadowComboRareHits : ShadowComboHits;
            int step = (ShadowComboDuration - 250) / hits;
            for (int i = 0; i < hits; i++)
            {
                for (int t = 0; t < targets.Count; t++)
                {
                    MapObject ob = targets[t];
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 250 + (i * step), magic, damage, ob, ob.CurrentLocation));
                }
            }

            //秘籍专属：收招「最后一击」再补一次满伤害（本体 20 帧播完、收招爆炸段的正中间），
            //伤害按**最大物理攻击**结算（取 MaxDC，不再随机下限），让收招那一下明显比其他连击重。
            if (rare)
            {
                int finalDamage = magic.GetDamage(Stats[Stat.MaxDC]);

                for (int t = 0; t < targets.Count; t++)
                {
                    MapObject ob = targets[t];
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + ShadowComboRareFinalTime, magic, finalDamage, ob, ob.CurrentLocation));
                }
            }

            return true;
        }

        //影华连击连击段数：整段特效连续造成多少次伤害（每个方向的挥砍动作算一次攻击）
        private const int ShadowComboHits = 6;
        private const int ShadowComboRareHits = 10;

        //秘籍收招「最后一击」的触发时刻：主体 20 帧（2000ms）播完、收招爆炸 6 帧（2000-2600ms）的正中间，
        //正好对上客户端那一段爆炸特效的最亮时刻，也确保它排在 10 次连击（最晚 1825ms）之后
        private const int ShadowComboRareFinalTime = 2300;

        //影华连击动画总时长（客户端 Magic_32bit 1390-1559 中「当前朝向」那一个方向的 20 帧，每帧 100ms = 2000ms），
        //隐藏时长与延迟位移都以它为准，必须与客户端 ShadowComboDuration 保持一致
        private const int ShadowComboDuration = 2000;

        //影华连击-秘籍：20 帧本体 + 收招最后一击 6 帧 = 26 帧，必须与客户端 ShadowComboRareDuration 一致
        private const int ShadowComboRareDuration = 2600;

        //「影化连击」buff 的额外容差：让隐藏比位移多持续一点，
        //保证「位移包先到、人物再显形」，否则人物会在原地闪一帧再跳过去。
        private const int ShadowComboStealthExtra = 300;

        //影华连击：特效播完后的延迟位移（把人物移到施法时算好的落点）
        protected void CompleteShadowComboDash(IList<object> data)
        {
            if (Dead) return;

            Point destination = (Point)data[0];
            MirDirection direction = (MirDirection)data[1];

            if (destination == CurrentLocation) return;

            //只校验落点地形是否合法。
            //★不再因为「落点有生物」就取消本次位移：影华连击本来就是化虚影穿透，
            //  如果因为 2 秒后落点恰好站了别的怪就整段取消，玩家会完全看不到位移。
            Cell cell = CurrentMap.GetCell(destination);
            if (cell == null || !cell.Valid) return;

            int distance = Functions.MaxDistance(CurrentLocation, destination);
            CurrentMap.GetCell(CurrentLocation).Remove(this);
            RemoveObjects(direction, 1);
            CurrentLocation = destination;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(direction, 1);
            Enqueue(new S.UserDashAttack { Direction = direction, Location = destination });
            Broadcast(new S.ObjectDashAttack { ObjectID = ObjectID, Direction = direction, Location = destination, Distance = distance });
        }

        //落雷杀（弓手）：在指定位置持续引发群体落雷；普通 4 格、秘笈 8 格（范围在 Map.CompleteMagic 里展开）
        private void SkyThunderShot(UserMagic magic, Point location, bool rare, out bool cast)
        {
            cast = false;
            if (!CurrentMap.ValidPoint(location))
            {
                ReceiveChat("该位置无法施展落雷杀", ChatType.Hint);
                return;
            }

            //落雷杀系列必须装备「气功石」（护身符类、Shape=4）；仅作为施法前置，不消耗
            if (GetAmulet(1, 4) == null)
            {
                ReceiveChat("必须装备【气功石】才能施展落雷杀", ChatType.Hint);
                return;
            }

            int damage = magic.GetDamage(GetRangeAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC], 3));
            damage = ApplyArcherState(damage);

            //持续时间固定：气功术为被动技能，玩家无法主动凝聚气功，因此不再消耗/依赖气功层数
            int duration = rare ? 6 : 4;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500;

            CurrentMap.ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, damage, location, duration));

            if (rare)
                AddBuff(BuffType.灭杀窗口, this, Settings.Second * 10, new Stats());

            cast = true;
        }

        //灭杀（弓手）：落雷杀-秘笈生效后的 10 秒内，对 1 格范围内的单个敌人打出更强的落雷
        private bool DeathBlow(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;

            if (!HasBuff(BuffType.灭杀窗口, out _))
            {
                ReceiveChat("需要先施放「落雷杀-秘笈」", ChatType.Hint);
                return false;
            }

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            if (distance > 1 && Info.MentalState != 1)
            {
                ReceiveChat("灭杀必须贴身（1 格内）才能施展", ChatType.Hint);
                return false;
            }

            int damage = magic.GetDamage(GetRangeAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC], distance));
            damage = ApplyArcherState(damage);

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 400, magic, damage, target, target.CurrentLocation));
            return true;
        }

        //月影雾-秘籍（刺客）：沿用月影雾的特效与音效，隐身持续时长增加 2 倍（攻击范围在 Map.CompleteMagic 里同步扩大 2 倍）
        private void MoonMistRare(UserMagic magic)
        {
            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].Type == BuffType.月影术) return;
            }

            var time = GetAttackPower(Stats[Stat.MinAC], Stats[Stat.MaxAC]);

            //月影雾时长 (time + (等级+1)*5) * 500 → 秘籍翻倍
            AddBuff(BuffType.月影术, this, (time + (magic.Level + 1) * 5) * 1000, new Stats());

            CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MoonMist }, CurrentLocation);

            //攻击力翻倍（攻击范围扩大到 4 格由 Map.CompleteMagic 的 MoonMistRare 分支处理）
            //注意 data[2] 必须是伤害值：这里原来误传了 time，会被当成伤害结算
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC])) * 2;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);

            LevelMagic(magic);
        }

        //气流术-秘籍（弓手）：可在移动时凝聚气功，并立即获得 (等级+1) 层气功
        private void ConcentrationRare(UserMagic magic)
        {
            int duration = 60 + (20 * magic.Level);

            var buff = AddBuff(BuffType.气流术, this, Settings.Second * duration, new Stats());

            buff.Set("InterruptTime", (long)0);
            buff.Set("Interrupted", false);
            buff.Set("Moveable", true);                 //官网：可以在移动时获得气功

            //官网：升级 1~4 级时，使用时立即获得 1/2/3/4 层气功（0 级不额外获得）
            if (GetMagic(Spell.Meditation) != null)
            {
                for (int i = 0; i < magic.Level; i++)
                {
                    ObtainElement(false);
                }
            }

            LevelMagic(magic);
            OperateTime = 0;

            UpdateConcentration(true, false);
        }
        private void FireBang(UserMagic magic, Point location)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        private void FireWall(UserMagic magic, Point location)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        private void Lightning(UserMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
        }
        private void TurnUndead(MapObject target, UserMagic magic)
        {
            if(target != null &&
               target.Race == ObjectType.Monster &&
               target.Undead &&
               target.IsAttackTarget(this))
            {
                // undead pet logic
                if (target.Master is PlayerObject master)
                {
                    if (master.PKPoints < 200 &&
                        (master.BrownTime == 0 &&
                        !master.AtWar(this)))
                    {
                            BrownTime = Envir.Time + Settings.Minute;
                    }   
                }

                if (Envir.Random.Next(2) + Level - 1 <= target.Level)
                {
                    target.Target = this;
                    return;
                }

                int dif = Level - target.Level + 15;

                if (Envir.Random.Next(100) >= (magic.Level + 1 << 3) + dif)
                {
                    target.Target = this;
                    return;
                }

                DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target);
                ActionList.Add(action);
            }
        }
        private void FlameDisruptor(MapObject target, UserMagic magic)
        {
            if (target == null || (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            if (!target.Undead) damage = (int)(damage * 1.5F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);
        }
        private void GreatFireBallRare(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) || !target.IsAttackTarget(this)) return;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            if (!target.Undead) damage = (int)(damage * 3F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 5000, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);
            cast = true;
        }
        private void ThunderStorm(UserMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        private void Mirroring(UserMagic magic)
        {
            MonsterObject monster;
            DelayedAction action;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.CloneName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front, true);
                CurrentMap.ActionList.Add(action);
                return;
            }

            MonsterInfo info = Envir.GetMonsterInfo(Settings.CloneName);
            if (info == null) return;

            LevelMagic(magic);

            monster = MonsterObject.GetMonster(info);
            monster.Master = this;
            monster.ActionTime = Envir.Time + 1000;
            monster.RefreshNameColour(false);

            Pets.Add(monster);

            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front, false);
            CurrentMap.ActionList.Add(action);
        }
        private void Blizzard(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            ActiveBlizzard = true;
            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private void MeteorStrike(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            ActiveBlizzard = true;
            CurrentMap.ActionList.Add(action);
            cast = true;
        }

        private void IceThrust(UserMagic magic)
        {
            int damageBase = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
            if (Envir.Random.Next(100) < (1 + Stats[Stat.幸运]))
                damageBase += damageBase;
            int damageFinish = magic.GetDamage(damageBase);

            Point location = Functions.PointMove(CurrentLocation, Direction, 1);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 1500, this, magic, location, Direction, damageFinish, (int)(damageFinish * 0.6));

            CurrentMap.ActionList.Add(action);
        }

        private void MagicBooster(UserMagic magic)
        {
            int bonus = 6 + magic.Level * 6;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, bonus));
        }

        private void HeavenlySecrets(UserMagic magic, out bool cast)
        {
            cast = true;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));

        }

        #endregion

        #region Taoist Skills
        private void Healing(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;

            int health = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) * 2) + Level;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, health, target);

            ActionList.Add(action);
        }
        private bool Poisoning(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;

            UserItem item = GetPoison(1);
            if (item == null) return false;

            int power = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, power, target, item);
            ActionList.Add(action);
            ConsumeItem(item, 1);
            return true;
        }
        private bool SoulFireball(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return false;
            cast = true;

            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);
            ConsumeItem(item, 1);

            return true;
        }
        private void SummonSkeleton(UserMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.SkeletonName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            // 道士可以同时养「变异骷髅 + 神兽 + 月灵」三只（原上限 2）
            if (Pets.Count(x => x.Race == ObjectType.Monster) >= 3) return;

            UserItem item = GetAmulet(1);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.SkeletonName);
            if (info == null) return;

            LevelMagic(magic);
            ConsumeItem(item, 1);

            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(4 + magic.Level);
            monster.ActionTime = Envir.Time + 1000;
            monster.RefreshNameColour(false);

            //Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Purification(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target);

            ActionList.Add(action);
        }
        private void SummonShinsu(UserMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.ShinsuName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            // 道士可以同时养「变异骷髅 + 神兽 + 月灵」三只（原上限 2）
            if (Pets.Count(x => x.Race == ObjectType.Monster) >= 3) return;

            UserItem item = GetAmulet(5);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.ShinsuName);
            if (info == null) return;

            LevelMagic(magic);
            ConsumeItem(item, 5);

            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 1000;

            //Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Hiding(UserMagic magic)
        {
            UserItem item = GetAmulet(1);
            if (item == null) return;

            ConsumeItem(item, 1);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) + (magic.Level + 1) * 5);
            ActionList.Add(action);

        }
        private void MassHiding(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) / 2 + (magic.Level + 1) * 2, location);
            CurrentMap.ActionList.Add(action);
        }
        private void SoulShield(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) * 4 + (magic.Level + 1) * 50, location);
            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 1);
        }
        private void MassHealing(UserMagic magic, Point location)
        {
            int value = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location);
            CurrentMap.ActionList.Add(action);
        }
        private void Revelation(MapObject target, UserMagic magic)
        {
            if (target == null) return;

            int value = GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, value, target);

            ActionList.Add(action);
        }
        private void PoisonCloud(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            //需求：毒雾 = 灵魂火符式投掷 + 群毒，携带「灰色毒粉」或「黄色毒粉」都能放，
            //效果由用掉的毒粉决定（与施毒术完全一致）：
            //  · 灰色毒粉(Shape=1) → 绿毒(PoisonType.Green) → 持续掉血；
            //  · 黄色毒粉(Shape=2) → 红毒(PoisonType.Red)   → 降低防御。
            //基础伤害（对点 Value）两种毒都一样，只有中毒种类不同。
            //
            //注意一个坑，会让玩家觉得「按了完全没反应」：
            //  「护身符」和「毒粉」共用同一个装备槽（EquipmentSlot.护身符，见 CanEquip 里
            //  被注释掉的 `|| item.Info.Shape == 0`）：同一个槽不可能同时装着 5 张符和 5 个毒粉，
            //  所以不能要求「5 符 + 5 毒粉」的双重条件，否则毒雾永远放不出来。
            //  这里只校验「5 个毒粉」，灰/黄皆可（GetPoison(5) 的 shape=0 正是取 1 或 2 的任一）。
            UserItem poison = GetPoison(5);
            if (poison == null)
            {
                ReceiveChat("必须装备 5 个毒粉才能施展毒雾（灰色毒粉=掉血 / 黄色毒粉=掉防）", ChatType.Hint);
                return;
            }

            byte poisonShape = (byte)poison.Info.Shape;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            //data[5] = 用掉的毒粉形态，带到 Map.CompleteMagic → SpellObject，决定这一片云挂绿毒还是红毒
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, damage, location, (byte)Envir.Random.Next(Stats[Stat.毒素伤害]), poisonShape);

            //只消耗 5 个毒粉（护身符与毒粉共用一个槽，无法同时要求两样）
            ConsumeItem(poison, 5);

            CurrentMap.ActionList.Add(action);
            cast = true;
        }

        private void MoonMist(UserMagic magic)
        {
            for (int i = 0; i < Buffs.Count; i++)
                if (Buffs[i].Type == BuffType.月影术) return;

            var time = GetAttackPower(Stats[Stat.MinAC], Stats[Stat.MaxAC]);

            AddBuff(BuffType.月影术, this, (time + (magic.Level + 1) * 5) * 500, new Stats());

            CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MoonMist }, CurrentLocation);
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]));
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
            LevelMagic(magic);

        }
        private bool CatTongue(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]));

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);

            ActionList.Add(action);

            return true;
        }

        private void TrapHexagon(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            bool anyTargetsFound = false;
            for (int x = location.X - 1; x <= location.X + 1; x++)
            {
                if (x < 0 || x >= CurrentMap.Width) continue;
                for (int y = location.Y - 1; y < location.Y + 1; y++)
                {
                    if (y < 0 || y >= CurrentMap.Height) continue;
                    if (!CurrentMap.ValidPoint(x, y)) continue;
                    var cell = CurrentMap.GetCell(x, y);
                    if (cell == null ||
                        cell.Objects == null ||
                        cell.Objects.Count <= 0) continue;
                    foreach (var target in cell.Objects)
                    {
                        switch (target.Race)
                        {
                            case ObjectType.Monster:
                                if (!target.IsAttackTarget(this)) continue;
                                if (target.Level > Level + 2) continue;
                                anyTargetsFound = true;
                                break;
                        }
                    }
                }
            }
            if (!anyTargetsFound)
                return;

            UserItem item = GetAmulet(1);
            //Point location = target.CurrentLocation;

            if (item == null) return;

            LevelMagic(magic);
            uint duration = (uint)((magic.Level * 5 + 10) * 1000);
            int value = (int)duration;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location);
            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 1);
            cast = true;
        }
        private void Reincarnation(UserMagic magic, PlayerObject target, out bool cast)
        {
            cast = true;
            if (CurrentMap.Info.NoReincarnation)
            {
                ReceiveChat("不能在此地图使用复活技能", ChatType.System);
                return;
            }
            if (target == null)
            {
                ReceiveChat("无效的复活目标", ChatType.System);
                return;
            }
            if (target.Race != ObjectType.Player)
            {
                ReceiveChat("只能复活玩家", ChatType.System);
                return;
            }
            if (!target.Dead)
            {
                ReceiveChat("目标不需要复活", ChatType.System);
                return;
            }
            UserItem item = GetAmulet(1, 3);
            if (item == null)
            {
                ReceiveChat("缺少复活使用的苏生符籍", ChatType.System);
                return;
            }
            if (target.ReincarnationHost != null)
                {
                ReceiveChat("目标不再接受复活", ChatType.System);
                return;
            }
            if (Envir.Time >= ReincarnationExpireTime)
            {
                ReincarnationReady = false;
                ActiveReincarnation = false;
                ReincarnationTarget = null;
                if (Envir.Time >= ReincarnationExpireTime)
                {
                    ReceiveChat("复活技能已准备就绪", ChatType.System);
                }
            }
            else
            {
                ReceiveChat("复活技能恢复时间不足", ChatType.System);
                return;
            }
            if (!ActiveReincarnation && !ReincarnationReady)
            {
                cast = false;
                if (Envir.Random.Next(30) > (1 + magic.Level) * 10)
                {
                    ReceiveChat("技能发动失败", ChatType.System);
                    return;
                }
                int CastTime = Math.Abs(9000 - ((magic.Level + 1) * 1000));
                ExpireTime = Envir.Time + CastTime;
                ReincarnationTarget = target;
                ReincarnationExpireTime = ExpireTime + 5000;
                target.ReincarnationHost = this;

                SpellObject ob = new SpellObject
                {
                    Spell = Spell.Reincarnation,
                    ExpireTime = ExpireTime,
                    TickSpeed = 1000,
                    Caster = this,
                    CurrentLocation = target.CurrentLocation,
                    CastLocation = target.CurrentLocation,
                    Show = true,
                    CurrentMap = CurrentMap,
                };

                if (target != null && DefaultMagicTarget != null)
                {
                    Packet p = new S.Chat
                    {
                        Message = string.Format("{0} 正在复活 {1}", DefaultMagicTarget.Name, target.Name),
                        Type = ChatType.System
                    };

                    if (Functions.InRange(CurrentLocation, target.CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                    }
                }

                CurrentMap.AddObject(ob);
                ob.Spawned();
                ConsumeItem(item, 1);
                
                DelayedAction action = new DelayedAction(DelayedType.Magic, ExpireTime, magic, target);
                ActionList.Add(action);

                ActiveReincarnation = true;
                ReincarnationReady = true;
                return;
            }
        }
        private void SummonHolyDeva(UserMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.AngelName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            // 道士可以同时养「变异骷髅 + 神兽 + 月灵」三只（原上限 2）
            if (Pets.Count(x => x.Race == ObjectType.Monster) >= 3) return;

            UserItem item = GetAmulet(2);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.AngelName);
            if (info == null) return;

            LevelMagic(magic);
            ConsumeItem(item, 2);

            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 1000;

            //Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 1500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Hallucination(MapObject target, UserMagic magic)
        {
            if (target == null || target.Race != ObjectType.Monster || !target.IsAttackTarget(this)) return;

            int damage = 0;
            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, delay, magic, damage, target);

            ActionList.Add(action);
        }
        private void EnergyShield(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || target.Node == null || !target.IsFriendlyTarget(this)) target = this; //offical is only party target

            int duration = 30 + 50 * magic.Level;
            int power = magic.GetPower(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            int chance = (10 - (Stats[Stat.幸运] / 3 + magic.Level + 1));

            if (chance < 2) chance = 2;

            var stats = new Stats
            {
                [Stat.气功盾恢复数率] = (int)Math.Round((1 / (decimal)chance) * 100),
                [Stat.气功盾恢复生命值] = power
            };

            switch (target.Race)
            {
                case ObjectType.Player:
                    //Only targets
                    if (target.IsFriendlyTarget(this))
                    {
                        target.AddBuff(BuffType.先天气功, this, (Settings.Second * duration), stats);
                        target.OperateTime = 0;
                        LevelMagic(magic);
                        cast = true;
                    }
                    break;
            }
        }
        private void UltimateEnhancer(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || target.Node == null || !target.IsFriendlyTarget(this)) return;
            UserItem item = GetAmulet(1);
            if (item == null) return;

            int expiretime = GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) * 4 + (magic.Level + 1) * 50;
            int value = Stats[Stat.MaxSC] >= 5 ? Math.Min(8, Stats[Stat.MaxSC] / 5) : 1;

            switch (target.Race)
            {
                case ObjectType.Monster:
                case ObjectType.Player:
                case ObjectType.Hero:
                    //Only targets
                    if (target.IsFriendlyTarget(this))
                    {
                        var stats = new Stats();

                        if (target.Race == ObjectType.Monster || ((HumanObject)target).Class == MirClass.战士 || ((HumanObject)target).Class == MirClass.刺客)
                        {
                            stats[Stat.MaxDC] = value;
                        }
                        else if (((HumanObject)target).Class == MirClass.法师 || ((HumanObject)target).Class == MirClass.弓箭)
                        {
                            stats[Stat.MaxMC] = value;
                        }
                        else if (((HumanObject)target).Class == MirClass.道士)
                        {
                            stats[Stat.MaxSC] = value;
                        }

                        target.AddBuff(BuffType.无极真气, this, Settings.Second * expiretime, stats);
                        target.OperateTime = 0;
                        LevelMagic(magic);
                        ConsumeItem(item, 1);
                        cast = true;
                    }
                    break;
            }
        }
        private void Plague(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step


            PoisonType pType = PoisonType.None;

            UserItem itemp = GetPoison(1, 1);

            if (itemp != null)
                pType = PoisonType.Green;
            else
            {
                itemp = GetPoison(1, 2);

                if (itemp != null)
                    pType = PoisonType.Red;
            }

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC])), location, pType);
            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 1);
            if (itemp != null) ConsumeItem(itemp, 1);
        }
        private void Curse(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            ConsumeItem(item, 1);

            if (Envir.Random.Next(10 - ((magic.Level + 1) * 2)) > 2) return;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC])), location, 1 + ((magic.Level + 1) * 2));
            CurrentMap.ActionList.Add(action);

        }


        private void PetEnhancer(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;

            if (target == null || target.Race != ObjectType.Monster || !target.IsFriendlyTarget(this)) return;

            int duration = GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) + magic.GetPower();

            cast = true;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, duration, target);

            ActionList.Add(action);
        }
        private void HealingRare(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;

            int health = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) * 10) + Level;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, health, target);

            ActionList.Add(action);
        }
        private void HealingcircleRare(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(3);
            if (item == null) return;
            cast = true;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            CurrentMap.ActionList.Add(action);

            ConsumeItem(item, 3);
        }
        #endregion

        #region Warrior Skills
        private void Entrapment(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = 0;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void EntrapmentRare(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = 0;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void BladeAvalanche(UserMagic magic)
        {
            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (Envir.Random.Next(0, 100) <= (1 + Stats[Stat.幸运]))
                damageBase += damageBase;//crit should do something like double dmg, not double max dc dmg!
            int damageFinal = magic.GetDamage(damageBase);

            int col = 3;
            int row = 3;

            Point[] loc = new Point[col]; //0 = left 1 = center 2 = right
            loc[0] = Functions.PointMove(CurrentLocation, Functions.PreviousDir(Direction), 1);
            loc[1] = Functions.PointMove(CurrentLocation, Direction, 1);
            loc[2] = Functions.PointMove(CurrentLocation, Functions.NextDir(Direction), 1);
            bool train = false;
            for (int i = 0; i < col; i++)
            {
                Point startPoint = loc[i];
                for (int j = 0; j < row; j++)
                {
                    Point hitPoint = Functions.PointMove(startPoint, Direction, j);

                    if (!CurrentMap.ValidPoint(hitPoint)) continue;

                    Cell cell = CurrentMap.GetCell(hitPoint);

                    if (cell.Objects == null) continue;

                    for (int k = 0; k < cell.Objects.Count; k++)
                    {
                        MapObject target = cell.Objects[k];
                        switch (target.Race)
                        {
                            case ObjectType.Monster:
                            case ObjectType.Player:
                                //Only targets
                                if (target.IsAttackTarget(this))
                                {
                                    if (target.Attacked(this, j <= 1 ? damageFinal : (int)(damageFinal * 0.6), DefenceType.MAC, false) > 0)
                                        train = true;
                                }
                                break;
                        }
                    }
                }
            }
            if (train)
                LevelMagic(magic);
        }
        private void ProtectionField(UserMagic magic)
        {
            int duration = 45 + (15 * magic.Level);
            int addValue = (int)Math.Round(Stats[Stat.MaxAC] * (0.2 + (0.03 * magic.Level)));

            AddBuff(BuffType.护身气幕, this, Settings.Second * duration, new Stats { [Stat.MaxAC] = addValue, [Stat.MinAC] = addValue });
            OperateTime = 0;
            LevelMagic(magic);
        }
        private void Rage(UserMagic magic)
        {

            int duration = 18 + (6 * magic.Level);
            int addValue = (int)Math.Round(Stats[Stat.MaxDC] * (0.12 + (0.03 * magic.Level)));

            AddBuff(BuffType.剑气爆, this, Settings.Second * duration, new Stats { [Stat.MaxDC] = addValue, [Stat.MinDC] = addValue });
            OperateTime = 0;
            LevelMagic(magic);
        }

        private void ShoulderDash(UserMagic magic)
        {
            ShoulderDash(magic, true);
        }

        /// <summary>
        /// 野蛮冲撞的位移与推挤逻辑。
        /// finalise = true（普通野蛮冲撞）：由本函数负责结算技能等级并下发施法包。
        /// finalise = false（拔刀术-秘籍连招的「野蛮」阶段）：只做位移与推挤，结算交给连招函数统一处理。
        /// 返回本次实际突进的格数。
        /// </summary>
        private int ShoulderDash(UserMagic magic, bool finalise)
        {
            if (InTrapRock || !CanWalk)
            {
                return 0;
            }

            Point _nextLocation;
            MapObject _target = null;

            bool _blocking = false;
            bool _canDash = false;

            int _cellsTravelled = 0;
            int dist = Envir.Random.Next(2) + magic.Level + 2;

            ActionTime = Envir.Time + MoveDelay;

            for (int i = 0; i < dist; i++)
            {
                if (_blocking)
                {
                    break;
                }

                _nextLocation = Functions.PointMove(CurrentLocation, Direction, 1);

                if (!CurrentMap.ValidPoint(_nextLocation) || CurrentMap.GetSafeZone(_nextLocation) != null)
                {
                    break;
                }

                // acquire target
                if (i == 0)
                {
                    Cell targetCell = CurrentMap.GetCell(_nextLocation);

                    if (targetCell.Objects != null)
                    {
                        int cellCnt = targetCell.Objects.Count;

                        for (int j = 0; j < cellCnt; j++)
                        {
                            MapObject ob = targetCell.Objects[j];

                            if ((ob.Race == ObjectType.Player ||
                                ob.Race == ObjectType.Monster ||
                                ob.Race == ObjectType.Hero) &&
                                ob.IsAttackTarget(this) &&
                                ob.Level < Level)
                            {
                                _target = ob;
                                break;
                            }

                            if(ob.Blocking)
                            {
                                _blocking = true;
                                break;
                            }
                        }
                    }
                    
                    if (_blocking)
                    {
                        break;
                    }
                }

                // try to dash
                Cell dashCell = CurrentMap.GetCell(_nextLocation);
                _canDash = false;

                if (_target == null)
                {
                    if (dashCell.Objects != null)
                    {
                        int cellCnt = dashCell.Objects.Count;

                        for (int k = 0; k < cellCnt; k++)
                        {
                            MapObject ob = dashCell.Objects[k];

                            if (ob.Blocking)
                            {
                                _blocking = true;
                                break;
                            }
                        }

                        if(!_blocking)
                        {
                            _canDash = true;
                        }
                    }
                    else
                    {
                        _canDash = true;
                    }
                }
                else
                {
                    // try to push
                    if (_target.Pushed(this, Direction, 1) == 0)
                    {
                        _blocking = true;
                    }
                    else
                    {
                        _canDash = true;
                    }
                }

                if (_canDash)
                {
                    CurrentMap.GetCell(CurrentLocation).Remove(this);
                    RemoveObjects(Direction, 1);

                    Enqueue(new S.UserDash { Direction = Direction, Location = _nextLocation });
                    Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = _nextLocation });

                    CurrentMap.GetCell(_nextLocation).Add(this);
                    AddObjects(Direction, 1);

                    // dash interrupt
                    Cell cell = CurrentMap.GetCell(_nextLocation);
                    for (int l = 0; l < cell.Objects.Count; l++)
                    {
                        if (cell.Objects[l].Race == ObjectType.Spell)
                        {
                            SpellObject ob = (SpellObject)cell.Objects[l];

                            if (IsAttackTarget(ob.Caster))
                            {
                                switch(ob.Spell)
                                {
                                    case Spell.FireWall:
                                        Attacked((PlayerObject)ob.Caster, ob.Value, DefenceType.MAC, false);
                                        _blocking = true;
                                        break;
                                }
                            }
                        }
                    }

                    CurrentLocation = _nextLocation;
                    _cellsTravelled++;
                }
            }

            if (_cellsTravelled == 0)
            {
                Enqueue(new S.UserDashFail { Direction = Direction, Location = CurrentLocation });
                Broadcast(new S.ObjectDashFail { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

                if (InSafeZone)
                {
                    ReceiveChat("安全区内技能无效", ChatType.System);
                }
                else
                {
                    ReceiveChat("冲撞力不足", ChatType.System);
                }
            }
            else
            {
                _target?.Attacked(this, magic.GetDamage(0), DefenceType.None, false);
                if (finalise) LevelMagic(magic);

                // Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = Front });
            }

            long now = Envir.Time;

            magic.CastTime = now;
            if (finalise) Enqueue(new S.MagicCast { Spell = magic.Spell });

            CellTime = now + 500;
            _stepCounter = 0;

            return _cellsTravelled;
        }

        private void SlashingBurst(UserMagic magic, out bool cast)
        {
            cast = true;

            // damage
            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            int damageFinal = magic.GetDamage(damageBase);

            // objects = this, magic, damage, currentlocation, direction, attackRange
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damageFinal, CurrentLocation, Direction, 1);
            CurrentMap.ActionList.Add(action);

            // telpo location
            Point location = Functions.PointMove(CurrentLocation, Direction, 2);

            if (!CurrentMap.ValidPoint(location)) return;

            Cell cInfo = CurrentMap.GetCell(location);

            bool blocked = false;
            if (cInfo.Objects != null)
            {
                for (int c = 0; c < cInfo.Objects.Count; c++)
                {
                    MapObject ob = cInfo.Objects[c];
                    if (!ob.Blocking) continue;
                    blocked = true;
                    if ((cInfo.Objects == null) || blocked) break;
                }
            }

            // blocked telpo cancel
            if (blocked) return;

            Teleport(CurrentMap, location, false);

            //// move character
            //CurrentMap.GetCell(CurrentLocation).Remove(this);
            //RemoveObjects(Direction, 1);

            //CurrentLocation = location;

            //CurrentMap.GetCell(CurrentLocation).Add(this);
            //AddObjects(Direction, 1);

            //Enqueue(new S.UserAttackMove { Direction = Direction, Location = location });
        }
        private void DimensionalSword(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;

            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            int damageFinal = magic.GetDamage(damageBase);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damageFinal, CurrentLocation, Direction, 1);
            CurrentMap.ActionList.Add(action);

            if (target == null || !target.IsAttackTarget(this)) return;
            if (Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 2) return;
            if (target.CurrentMap != CurrentMap) return;

            Point backLocation = Functions.PointMove(target.CurrentLocation, Functions.ReverseDirection(target.Direction), 1);

            if (CurrentMap.ValidPoint(backLocation))
            {
                Teleport(CurrentMap, backLocation, false);

                Direction = Functions.DirectionFromPoint(CurrentLocation, target.CurrentLocation);
                Enqueue(new S.UserAttackMove { Direction = Direction, Location = CurrentLocation });

                target.Attacked(this, damageFinal, DefenceType.AC, false);
                cast = true;
            }
        }
        private void DimensionalSwordRare(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;

            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            int damageFinal = magic.GetDamage(damageBase);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damageFinal, CurrentLocation, Direction, 1);
            CurrentMap.ActionList.Add(action);

            if (target == null || !target.IsAttackTarget(this)) return;
            if (Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 3) return;
            if (target.CurrentMap != CurrentMap) return;

            Point backLocation = Functions.PointMove(target.CurrentLocation, Functions.ReverseDirection(target.Direction), 1);

            if (CurrentMap.ValidPoint(backLocation))
            {
                CurrentMap.GetCell(CurrentLocation).Remove(this);
                RemoveObjects(Direction, 1);

                CurrentLocation = backLocation;
                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(Direction, 1);

                Direction = Functions.DirectionFromPoint(CurrentLocation, target.CurrentLocation);
                Enqueue(new S.UserAttackMove { Direction = Direction, Location = CurrentLocation });

                target.Attacked(this, damageFinal, DefenceType.AC, false);
                cast = true;

                //★★ 最后一击：客户端 Magic_32bit 1300-1379（8 方向 × 每方向 6 帧有效）那套劈砍动作
                //   播完之后，再补一记重击。伤害按**最大物理攻击**结算（取 MaxDC，不再随机下限）。
                //   时机 = 施法起 600ms（客户端该特效是 6 帧 × FrameInterval≈100ms）。
                int finalDamage = magic.GetDamage(Stats[Stat.MaxDC]);

                ActionList.Add(new DelayedAction(DelayedType.DimensionalSwordFinal, Envir.Time + DimensionalSwordRareFinalDelay, magic, target, finalDamage));
            }
        }

        //时空剑-秘籍「最后一击」的触发时刻：等**整段技能特效播完**再补这一击。
        //客户端那一套特效是 起手 300ms / 命中 1000ms / 收招爆炸 500+500ms，最长 1000ms；
        //取 1100ms（1000ms + 100ms 间隔），确保这一击与技能本体效果不重叠。
        //★★ 必须与客户端 PlayerObject.DimensionalSwordFinalDelay 完全一致。
        private const int DimensionalSwordRareFinalDelay = 1100;

        //时空剑-秘籍：特效播完后的「最后一击」——对同一目标再结算一次最大伤害
        protected void CompleteDimensionalSwordFinal(IList<object> data)
        {
            if (Dead) return;

            UserMagic magic = (UserMagic)data[0];
            MapObject target = (MapObject)data[1];
            int damage = (int)data[2];

            if (magic == null || target == null || target.Dead) return;
            if (target.CurrentMap != CurrentMap || target.Node == null) return;
            if (!target.IsAttackTarget(this)) return;

            //瞬移后应贴身，给 2 格容差；目标跑远就放弃这一击（不跨屏追打）
            if (Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 2) return;

            if (target.Attacked(this, damage, DefenceType.AC, false) > 0) LevelMagic(magic);
        }
        private void FurySpell(UserMagic magic, out bool cast)
        {
            cast = true;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));
        }
        private void ImmortalSkin(UserMagic magic, out bool cast)
        {
            cast = true;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));

        }
        private void ImmortalSkinRare(UserMagic magic, out bool cast)
        {
            cast = true;

            ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));

        }
        private void CounterAttackCast(UserMagic magic, MapObject target)
        {
            if (target == null || magic == null) return;

            if (CounterAttack == false) return;

            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (Envir.Random.Next(0, 100) <= Stats[Stat.准确])
                damageBase += damageBase;//crit should do something like double dmg, not double max dc dmg!
            int damageFinal = magic.GetDamage(damageBase);


            MirDirection dir = Functions.ReverseDirection(target.Direction);
            Direction = dir;

            if (Functions.InRange(CurrentLocation, target.CurrentLocation, 1) == false) return;
            if (Envir.Random.Next(10) > magic.Level + 6) return;
            Enqueue(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = Spell.CounterAttack, TargetID = target.ObjectID, Target = target.CurrentLocation, Cast = true, Level = GetMagic(Spell.CounterAttack).Level, SelfBroadcast = true });
            DelayedAction action = new DelayedAction(DelayedType.Damage, AttackTime, target, damageFinal, DefenceType.AC, true);
            ActionList.Add(action);
            LevelMagic(magic);
            CounterAttack = false;
        }
        #endregion

        #region Assassin Skills

        private void HeavenlySword(UserMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
        }
        private void SwiftFeet(UserMagic magic, out bool cast)
        {
            cast = true;

            ActiveSwiftFeet = true;

            AddBuff(BuffType.轻身步, this, (Settings.Second * 25) + (magic.Level * 5000), new Stats(), true);

            LevelMagic(magic);
        }
        private void MoonLight(UserMagic magic)
        {
            var time = Settings.Second * 15; //var time = GetAttackPower(Stats[Stat.MinAC], Stats[Stat.MaxAC]);

            AddBuff(BuffType.月影术, this, (time + (magic.Level * 5000)), new Stats()); //AddBuff(BuffType.月影术, this, (time + (magic.Level + 1) * 5) * 500, new Stats());

            LevelMagic(magic);
        }
        private void Trap(UserMagic magic, MapObject target, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsAttackTarget(this) || !(target is MonsterObject)) return;
            if (target.Level >= Level + 2) return;

            LevelMagic(magic);
            uint duration = 60000;
            int value = (int)duration;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, target);
            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private bool PoisonSword(UserMagic magic)
        {
            UserItem item = GetPoison(1);
            if (item == null) return false;

            Point hitPoint;
            Cell cell;
            MirDirection dir = Functions.PreviousDir(Direction);
            int power = magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]));

            for (int i = 0; i < 5; i++)
            {
                hitPoint = Functions.PointMove(CurrentLocation, dir, 1);
                dir = Functions.NextDir(dir);

                if (!CurrentMap.ValidPoint(hitPoint)) continue;
                cell = CurrentMap.GetCell(hitPoint);

                if (cell.Objects == null) continue;

                for (int o = 0; o < cell.Objects.Count; o++)
                {
                    MapObject target = cell.Objects[o];
                    if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) continue;
                    if (target == null || !target.IsAttackTarget(this) || target.Node == null) continue;

                    target.ApplyPoison(new Poison
                    {
                        Duration = 3 + power / 10 + magic.Level * 3,
                        Owner = this,
                        PType = PoisonType.Green,
                        TickSpeed = 1000,
                        Value = power / 10 + magic.Level + 1 + Envir.Random.Next(Stats[Stat.毒素伤害])
                    }, this);

                    target.OperateTime = 0;
                    break;
                }
            }

            LevelMagic(magic);
            ConsumeItem(item, 1);
            return true;
        }
        private void DarkBody(MapObject target, UserMagic magic)
        {
            if (target == null) return;

            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.AssassinCloneName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.Die();
                return;
            }

            MonsterInfo info = Envir.GetMonsterInfo(Settings.AssassinCloneName);
            if (info == null) return;

            monster = MonsterObject.GetMonster(info);
            monster.Master = this;
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 500;
            monster.RefreshNameColour(false);
            monster.Target = target;
            Pets.Add(monster);

            monster.Spawn(CurrentMap, CurrentLocation);

            if (!HasBuff(BuffType.烈火身, out _))
            {
                LevelMagic(magic);
            }

            var duration = (GetAttackPower(Stats[Stat.MinAC], Stats[Stat.MaxAC]) + (magic.Level + 1) * 5) * 500;

            AddBuff(BuffType.烈火身, this, duration, new Stats());
        }
        private void CrescentSlash(UserMagic magic)
        {
            int damageBase = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (Envir.Random.Next(0, 100) <= Stats[Stat.准确])
                damageBase += damageBase;//crit should do something like double dmg, not double max dc dmg!
            int damageFinal = magic.GetDamage(damageBase);

            MirDirection backDir = Functions.ReverseDirection(Direction);
            MirDirection preBackDir = Functions.PreviousDir(backDir);
            MirDirection nextBackDir = Functions.NextDir(backDir);

            for (int i = 0; i < 8; i++)
            {
                MirDirection dir = (MirDirection)i;
                Point hitPoint = Functions.PointMove(CurrentLocation, dir, 1);

                if (dir != backDir && dir != preBackDir && dir != nextBackDir)
                {

                    if (!CurrentMap.ValidPoint(hitPoint)) continue;

                    Cell cell = CurrentMap.GetCell(hitPoint);

                    if (cell.Objects == null) continue;


                    for (int j = 0; j < cell.Objects.Count; j++)
                    {
                        MapObject target = cell.Objects[j];
                        switch (target.Race)
                        {
                            case ObjectType.Monster:
                            case ObjectType.Player:
                                //Only targets
                                if (target.IsAttackTarget(this))
                                {
                                    DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + AttackSpeed, target, damageFinal, DefenceType.AC, true);
                                    ActionList.Add(action);
                                }
                                break;
                        }
                    }
                    LevelMagic(magic);
                }
            }
        }

        private void FlashDash(UserMagic magic)
        {
            FlashDash(magic, 0, false);
        }

        /// <summary>
        /// 拔刀术的冲刺与斩杀逻辑。
        /// minJump：强制的最小冲刺格数。拔刀术-秘籍连招时传 1，保证「推怪」之后一定还会再向前冲一格，
        ///          客户端才会播放「刺客冲击（拔刀）」动作。
        /// partOfCombo：true 表示这是连招的一段，本函数不结算技能等级、也不重复下发施法包，由连招函数统一处理。
        /// attackDistance：挥刀的攻击距离（格）。普通拔刀术 1 格，拔刀术-秘籍 2 格。
        /// 返回是否命中目标。
        /// </summary>
        private bool FlashDash(UserMagic magic, int minJump, bool partOfCombo, int attackDistance = 1)
        {
            bool success = false;
            ActionTime = Envir.Time;

            int travel = 0;
            bool blocked = false;
            int jumpDistance = (magic.Level <= 1) ? 0 : 1;//3 max
            if (jumpDistance < minJump) jumpDistance = minJump;
            Point location = CurrentLocation;
            for (int i = 0; i < jumpDistance; i++)
            {
                location = Functions.PointMove(location, Direction, 1);
                if (!CurrentMap.ValidPoint(location)) break;

                Cell cInfo = CurrentMap.GetCell(location);
                if (cInfo.Objects != null)
                {
                    for (int c = 0; c < cInfo.Objects.Count; c++)
                    {
                        MapObject ob = cInfo.Objects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.Objects == null) || blocked) break;
                    }
                }
                if (blocked) break;
                travel++;
            }

            jumpDistance = travel;

            if (jumpDistance > 0)
            {
                location = Functions.PointMove(CurrentLocation, Direction, jumpDistance);
                CurrentMap.GetCell(CurrentLocation).Remove(this);
                RemoveObjects(Direction, 1);
                CurrentLocation = location;
                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(Direction, 1);
                Enqueue(new S.UserDashAttack { Direction = Direction, Location = location });
                Broadcast(new S.ObjectDashAttack { ObjectID = ObjectID, Direction = Direction, Location = location, Distance = jumpDistance });
            }
            else
            {
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            }

            if (travel == 0) location = CurrentLocation;

            int attackDelay = (AttackSpeed - 120) <= 300 ? 300 : (AttackSpeed - 120);
            AttackTime = Envir.Time + attackDelay;
            SpellTime = Envir.Time + 300;

            //攻击判定：从冲刺落点起，沿面朝方向最多检查 attackDistance 格，命中第一个目标即停
            Point attackLocation = location;
            for (int d = 0; d < attackDistance; d++)
            {
                attackLocation = Functions.PointMove(attackLocation, Direction, 1);
                if (!CurrentMap.ValidPoint(attackLocation)) break;

                Cell cInfo = CurrentMap.GetCell(attackLocation);
                if (cInfo.Objects == null) continue;

                for (int c = 0; c < cInfo.Objects.Count; c++)
                {
                    MapObject ob = cInfo.Objects[c];
                    switch (ob.Race)
                    {
                        case ObjectType.Monster:
                        case ObjectType.Player:
                            //Only targets
                            if (ob.IsAttackTarget(this))
                            {
                                DelayedAction action = new DelayedAction(DelayedType.Damage, AttackTime, ob, magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC])), DefenceType.AC, true);
                                ActionList.Add(action);
                                success = true;
                                if ((((ob.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonAttackWeight) >= ob.Stats[Stat.毒物躲避])) && (Envir.Random.Next(15) <= magic.Level + 1))
                                {
                                    DelayedAction pa = new DelayedAction(DelayedType.Poison, AttackTime, ob, PoisonType.Stun, SpellEffect.TwinDrakeBlade, magic.Level + 1, 1000);
                                    ActionList.Add(pa);
                                }
                            }
                            break;
                    }
                }

                if (success) break;
            }
            if (success && !partOfCombo) //technicaly this makes flashdash lvl when it casts rather then when it hits (it wont lvl if it's not hitting!)
                LevelMagic(magic);

            magic.CastTime = Envir.Time;
            if (!partOfCombo) Enqueue(new S.MagicCast { Spell = magic.Spell });

            return success;
        }

        //拔刀术-秘籍：原地连砍的刀数
        private const int FlashDashRareHits = 4;

        //拔刀术-秘籍：每刀之间的间隔（ms），必须与客户端排队播放的节奏大体一致
        private const int FlashDashRareHitDelay = 220;

        //拔刀术-秘籍：收招推怪的搜索距离（身前面朝方向几格内的怪）
        private const int FlashDashRarePushRange = 4;

        //拔刀术-秘籍：收招把怪推开的格数
        private const int FlashDashRarePushDistance = 4;

        /// <summary>
        /// 拔刀术-秘籍：不再有野蛮冲撞的位移与推挤。
        /// 人物原地连续挥砍 FlashDashRareHits 刀（每刀独立结算一次伤害），
        /// 收招时把身前面朝方向 FlashDashRarePushRange 格内的怪沿该方向推开 FlashDashRarePushDistance 格。
        /// </summary>
        private void FlashDashRare(UserMagic magic)
        {
            if (InTrapRock || !CanWalk)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            ActionTime = Envir.Time;

            int attackDelay = (AttackSpeed - 120) <= 300 ? 300 : (AttackSpeed - 120);
            AttackTime = Envir.Time + attackDelay;
            SpellTime = Envir.Time + 300;

            bool hit = false;

            for (int i = 0; i < FlashDashRareHits; i++)
            {
                //每一刀都重新找一次身前的第一个可攻击目标（前面几刀打死了，后面的刀自然落空）
                MapObject target = FindFlashDashRareTarget(2);

                //挥砍动作：本机由客户端自己排队播放，这里广播给其他玩家
                //（客户端会把连续收到的动作排进 ActionFeed 依次播完，自然形成连砍）
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

                if (target == null) continue;

                long hitTime = Envir.Time + (i * FlashDashRareHitDelay);

                ActionList.Add(new DelayedAction(DelayedType.Damage,
                    hitTime,
                    target,
                    magic.GetDamage(GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC])),
                    DefenceType.AC, true));
                hit = true;

                //每一刀都独立判定一次麻痹/眩晕，判定条件与普通拔刀术完全一致：
                //先过毒物躲避，再过 Random(15) <= 等级+1（0/1/2 级约 13% / 20% / 27%），
                //命中则上 PoisonType.Stun（麻痹·眩晕），时长 = 等级+1 秒，并广播 TwinDrakeBlade 命中特效。
                //4 刀各判一次 → 概率叠加，整体「至少中一次」的概率明显高于普通拔刀术。
                if ((((target.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonAttackWeight) >= target.Stats[Stat.毒物躲避])) && (Envir.Random.Next(15) <= magic.Level + 1))
                {
                    ActionList.Add(new DelayedAction(DelayedType.Poison,
                        hitTime,
                        target, PoisonType.Stun, SpellEffect.TwinDrakeBlade, magic.Level + 1, 1000));
                }
            }

            //收招：等最后一刀打完，把身前一排怪推开 4 格
            ActionList.Add(new DelayedAction(DelayedType.FlashDashRarePush,
                Envir.Time + (FlashDashRareHits * FlashDashRareHitDelay),
                Direction, FlashDashRarePushRange, FlashDashRarePushDistance));

            //整段连砍命中就结算一次修炼值
            if (hit) LevelMagic(magic);

            //连招结束：只下发一次施法包，冷却从连招结束时算起
            magic.CastTime = Envir.Time;
            Enqueue(new S.MagicCast { Spell = magic.Spell });
        }

        /// <summary>沿面朝方向最多 maxDistance 格，找第一个可攻击的目标（怪或玩家）。</summary>
        private MapObject FindFlashDashRareTarget(int maxDistance)
        {
            Point scan = CurrentLocation;

            for (int d = 0; d < maxDistance; d++)
            {
                scan = Functions.PointMove(scan, Direction, 1);
                if (!CurrentMap.ValidPoint(scan)) break;

                Cell cell = CurrentMap.GetCell(scan);
                if (cell == null || cell.Objects == null) continue;

                for (int c = 0; c < cell.Objects.Count; c++)
                {
                    MapObject ob = cell.Objects[c];
                    if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player) continue;
                    if (ob.Dead || ob.Node == null || !ob.IsAttackTarget(this)) continue;
                    return ob;
                }
            }

            return null;
        }

        /// <summary>拔刀术-秘籍的延迟收招：把身前面朝方向上指定范围内的怪沿该方向推开。</summary>
        protected void CompleteFlashDashRarePush(IList<object> data)
        {
            if (Dead) return;

            MirDirection direction = (MirDirection)data[0];
            int searchCells = (int)data[1];
            int pushCells = (int)data[2];

            PushMonstersInFront(direction, searchCells, pushCells);
        }

        /// <summary>把身前面朝方向 searchCells 格内的怪物沿该方向推开 pushCells 格，返回被推开的目标数。</summary>
        private int PushMonstersInFront(MirDirection direction, int searchCells, int pushCells)
        {
            int pushed = 0;
            Point scan = CurrentLocation;

            for (int i = 0; i < searchCells; i++)
            {
                scan = Functions.PointMove(scan, direction, 1);
                if (!CurrentMap.ValidPoint(scan)) break;

                Cell cell = CurrentMap.GetCell(scan);
                if (cell == null || cell.Objects == null) continue;

                //先做快照：Pushed 会把对象移出格子，Cell.Remove 可能把 Objects 置 null
                MapObject[] snapshot = new MapObject[cell.Objects.Count];
                cell.Objects.CopyTo(snapshot);

                for (int k = 0; k < snapshot.Length; k++)
                {
                    MapObject ob = snapshot[k];
                    if (ob == null || ob.Node == null || ob.Dead) continue;
                    if (ob.Race != ObjectType.Monster) continue;

                    pushed += ob.Pushed(this, direction, pushCells);
                }
            }

            return pushed;
        }
        #endregion

        #region Archer Skills
        private int ApplyArcherState(int damage)
        {
            UserMagic magic = GetMagic(Spell.MentalState);

            if (magic != null)
            {
                LevelMagic(magic);
            }

            int dmgpenalty = 100;
            switch (Info.MentalState)
            {
                case 1: //trickshot
                    dmgpenalty = 55 + (Info.MentalStateLvl * 5);
                    break;
                case 2: //group attack
                    dmgpenalty = 80;
                    break;
            }
            return (damage * dmgpenalty) / 100;
        }

        private bool StraightShot(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetRangeAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC], distance));
            damage = ApplyArcherState(damage);

            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);

            return true;
        }
        private bool DoubleShot(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return false;

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetRangeAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC], distance));
            damage = ApplyArcherState(damage);

            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);

            action = new DelayedAction(DelayedType.Magic, Envir.Time + delay + 50, magic, damage, target, target.CurrentLocation);

            ActionList.Add(action);

            return true;
        }
        private void BackStep(UserMagic magic)
        {
            ActionTime = Envir.Time;
            if (!CanWalk) return;

            int travel = 0;
            bool blocked = false;
            int jumpDistance = (magic.Level == 0) ? 1 : magic.Level;//3 max
            MirDirection jumpDir = Functions.ReverseDirection(Direction);
            Point location = CurrentLocation;
            for (int i = 0; i < jumpDistance; i++)
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!CurrentMap.ValidPoint(location)) break;

                Cell cInfo = CurrentMap.GetCell(location);
                if (cInfo.Objects != null)
                    for (int c = 0; c < cInfo.Objects.Count; c++)
                    {
                        MapObject ob = cInfo.Objects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.Objects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }

            jumpDistance = travel;
            if (jumpDistance > 0)
            {
                for (int i = 0; i < jumpDistance; i++)
                {
                    location = Functions.PointMove(CurrentLocation, jumpDir, 1);
                    CurrentMap.GetCell(CurrentLocation).Remove(this);
                    RemoveObjects(jumpDir, 1);
                    CurrentLocation = location;
                    CurrentMap.GetCell(CurrentLocation).Add(this);
                    AddObjects(jumpDir, 1);
                }
                Enqueue(new S.UserBackStep { Direction = Direction, Location = location });
                Broadcast(new S.ObjectBackStep { ObjectID = ObjectID, Direction = Direction, Location = location, Distance = jumpDistance });
                LevelMagic(magic);
            }
            else
            {
                Broadcast(new S.ObjectBackStep { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Distance = jumpDistance });
                ReceiveChat("跳跃力不足", ChatType.System);
            }

            magic.CastTime = Envir.Time;
            Enqueue(new S.MagicCast { Spell = magic.Spell });

            CellTime = Envir.Time + 500;
        }
        private bool DelayedExplosion(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int power = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));
            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, power, target, target.CurrentLocation);
            ActionList.Add(action);
            return true;
        }
        private void ExplosiveTrap(UserMagic magic, Point location)
        {
            int freeTrapSpot = -1;

            var trapIDs = CurrentMap.GetSpellObjects(Spell.ExplosiveTrap, this).Select(x => x.ExplosiveTrapID).Distinct();

            var max = magic.Level + 1;

            if (trapIDs.Count() >= max) return;

            for (int i = 0; i < max; i++)
            {
                if (!trapIDs.Contains(i))
                {
                    freeTrapSpot = i;
                    break;
                }
            }

            if (freeTrapSpot == -1) return;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location, freeTrapSpot);
            CurrentMap.ActionList.Add(action);
        }

        public void DoKnockback(MapObject target, UserMagic magic)//ElementalShot - knockback
        {
            Cell cell = CurrentMap.GetCell(target.CurrentLocation);
            if (!cell.Valid || cell.Objects == null) return;

            if (target.CurrentLocation.Y < 0 || target.CurrentLocation.Y >= CurrentMap.Height || target.CurrentLocation.X < 0 || target.CurrentLocation.X >= CurrentMap.Height) return;

            if (target.Race != ObjectType.Monster && target.Race != ObjectType.Player) return;
            if (!target.IsAttackTarget(this) || target.Level >= Level) return;

            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + ElementsLevel + Level - target.Level) return;
            int distance = 1 + Math.Max(0, magic.Level - 1) + Envir.Random.Next(2);
            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, target.CurrentLocation);

            target.Pushed(this, dir, distance);
        }
        public void BindingShot(UserMagic magic, MapObject target, out bool cast)
        {
            cast = false;

            if (target == null || !target.IsAttackTarget(this) || !(target is MonsterObject)) return;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return;
            if (target.Level > Level + 2) return;
            if (((MonsterObject)target).ShockTime >= Envir.Time) return;//Already shocked


            uint duration = (uint)((magic.Level * 5 + 10) * 1000);
            int value = (int)duration;
            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, value, target, target.CurrentLocation);
            ActionList.Add(action);

            cast = true;
        }
        public void SpecialArrowShot(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return;

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetRangeAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC], distance));
            damage = ApplyArcherState(damage);

            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);
            ActionList.Add(action);
        }
        public void NapalmShot(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;
            if ((Info.MentalState != 1) && !CanFly(target.CurrentLocation)) return;

            int distance = Functions.MaxDistance(CurrentLocation, target.CurrentLocation);
            int damage = magic.GetDamage(GetRangeAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC], distance));
            damage = ApplyArcherState(damage);

            int delay = distance * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, damage, target.CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        public void ArcherSummon(UserMagic magic, MapObject target, Point location)
        {
            if (target != null && target.IsAttackTarget(this))
                location = target.CurrentLocation;
            if (!CanFly(location)) return;

            uint duration = (uint)((magic.Level * 5 + 10) * 1000);
            int value = (int)duration;
            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, value, location, target);
            ActionList.Add(action);
        }

        public void ArcherSummonStone(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            if (!CurrentMap.ValidPoint(location) ||
                !CanFly(location))
            {
                return;
            }

            if (Pets.Exists(x => x.Info.GameName == Settings.StoneName))
            {
                MonsterObject st = Pets.First(x => x.Info.GameName == Settings.StoneName);
                if (!st.Dead)
                {
                    ReceiveChat($"只能召唤一个活动状态的 {Settings.StoneName}", ChatType.Hint);
                    return;
                }
            }

            int duration = (((magic.Level * 5) + 10) * 1000);
            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step
                                                                                     //
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, duration, location);
            ActionList.Add(action);

            cast = true;
        }

        public void OneWithNature(MapObject target, UserMagic magic)
        {
            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        public void HealingCircle(UserMagic magic, Point location)
        {
            var damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]));
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500 + 1200, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        #endregion

        #region Custom
        private void Portal(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            if (!CurrentMap.ValidPoint(location)) return;

            var portalCount = Envir.Spells.Count(x => x.Spell == Spell.Portal && x.Caster == this);

            if (portalCount == 2) return;

            if (!CanFly(location)) return;

            int duration = 30 + (magic.Level * 30);
            int value = duration;
            int passthroughCount = (magic.Level * 2) - 1;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location, passthroughCount);
            CurrentMap.ActionList.Add(action);
            cast = true;
        }

        private bool FireBounce(MapObject target, UserMagic magic, MapObject source, int bounce = -1)
        {
            if (target == null || !target.IsAttackTarget(this) || !source.CanFly(target.CurrentLocation) || bounce == 0) return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            int delay = Functions.MaxDistance(source.CurrentLocation, target.CurrentLocation) * 50; //50 MS per Step

            if (bounce == -1)
            {
                bounce = magic.Level + 2;
                delay += 500;
            }
            else
            {
                CurrentMap.Broadcast(new S.ObjectProjectile { Spell = magic.Info.Spell, Source = source.ObjectID, Destination = target.ObjectID }, source.CurrentLocation);
            }

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation, bounce);
            ActionList.Add(action);

            return true;
        }

        private bool MeteorShower(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            var targetIDs = new List<uint>();

            if (target.Race == ObjectType.Monster)
            {
                List<MapObject> targets = ((MonsterObject)target).FindAllNearby(4, target.CurrentLocation);

                int secondaryTargetCount = targets.Count > 3 ? 3 : targets.Count;

                for (int i = 0; i < secondaryTargetCount; i++)
                {
                    if (targets[i] == target || !targets[i].IsAttackTarget(this)) continue;

                    DelayedAction action2 = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage / 2, targets[i], targets[i].CurrentLocation);
                    ActionList.Add(action2);

                    targetIDs.Add(targets[i].ObjectID);
                }
            }

            Broadcast(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = magic.Info.Spell, TargetID = target.ObjectID, Target = target.CurrentLocation, Cast = true, Level = magic.Level, SecondaryTargetIDs = targetIDs });
            Enqueue(new S.Magic { Spell = Spell.MeteorShower, TargetID = target.ObjectID, Target = target.CurrentLocation, Cast = true, Level = magic.Level, SecondaryTargetIDs = targetIDs });

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target, target.CurrentLocation);
            ActionList.Add(action);

            return true;
        }

        #endregion

        private void CheckSneakRadius()
        {
            if (!Sneaking) return;

            for (int y = CurrentLocation.Y - 3; y <= CurrentLocation.Y + 3; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = CurrentLocation.X - 3; x <= CurrentLocation.X + 3; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;

                    Cell cell = CurrentMap.GetCell(x, y);
                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; cell.Objects != null && i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if ((ob.Race != ObjectType.Player) || ob == this) continue;

                        SneakingActive = false;
                        return;
                    }
                }
            }

            SneakingActive = true;
        }
        protected void CompleteMagic(IList<object> data)
        {
            UserMagic magic = (UserMagic)data[0];
            int value;
            MapObject target;
            Point targetLocation;
            Point location;
            MonsterObject monster;

            switch (magic.Spell)
            {
                #region FireBall, GreatFireBall, GreatFireBallRare, ThunderBolt, SoulFireBall, FlameDisruptor, StraightShot, DoubleShot, MeteorShower

                case Spell.FireBall:
                case Spell.GreatFireBall:
                case Spell.GreatFireBallRare:
                case Spell.ThunderBolt:
                case Spell.SoulFireBall:
                case Spell.FlameDisruptor:
                case Spell.StraightShot:
                case Spell.DoubleShot:
                case Spell.MeteorShower:
                //新增：强击-秘籍 / 灵魂火符(万效) / 灭杀 —— 与单体法术同一结算方式
                case Spell.ThunderBoltRare:
                case Spell.SoulFireBallPlus:
                case Spell.SoulFireBallPlusRare:
                case Spell.DeathBlow:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;

                    //强击-秘籍 / 灭杀：每个被命中的目标身上各落一道雷电（多个目标就是「多根雷电」）
                    //Broadcast 不含自己，所以施法者要单独补一份
                    if (magic.Spell == Spell.ThunderBoltRare || magic.Spell == Spell.DeathBlow)
                    {
                        Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.ThunderBoltStrike });
                        Enqueue(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.ThunderBoltStrike });
                    }

                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0) LevelMagic(magic);
                    break;

                #endregion

                #region ShadowCombo, ShadowComboRare（新增：影华连击）

                case Spell.ShadowCombo:
                case Spell.ShadowComboRare:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;
                    if (target.Attacked(this, value, DefenceType.AC, false) > 0) LevelMagic(magic);
                    break;

                #endregion

                #region FireBounce

                case Spell.FireBounce:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];
                    int bounce = (int)data[4];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0) LevelMagic(magic);

                    if (target.Race == ObjectType.Monster)
                    {
                        var targets = ((MonsterObject)target).FindAllNearby(3, target.CurrentLocation).Where(x => x != target && x.IsAttackTarget(this) && target.CanFly(x.CurrentLocation)).ToList();

                        if (targets.Count > 0)
                        {
                            var nextTarget = targets[Envir.Random.Next(targets.Count)];

                            this.FireBounce(nextTarget, magic, target, --bounce);
                        }
                    }

                    break;

                #endregion

                #region FrostCrunch
                case Spell.FrostCrunch:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0)
                    {
                        if (Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 20) <= magic.Level)
                        {
                            target.ApplyPoison(new Poison
                            {
                                Owner = this,
                                Duration = target.Race == ObjectType.Player ? 4 : 5 + Envir.Random.Next(5),
                                PType = PoisonType.Slow,
                                TickSpeed = 1000,
                            }, this);
                            target.OperateTime = 0;
                        }

                        if (Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 40) <= magic.Level)
                        {
                            target.ApplyPoison(new Poison
                            {
                                Owner = this,
                                Duration = target.Race == ObjectType.Player ? 2 : 5 + Envir.Random.Next(Stats[Stat.冰冻伤害]),
                                PType = PoisonType.Frozen,
                                TickSpeed = 1000,
                            }, this);
                            target.OperateTime = 0;
                        }

                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region Vampirism

                case Spell.Vampirism:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    value = target.Attacked(this, value, DefenceType.MAC, false);
                    if (value == 0) return;
                    LevelMagic(magic);
                    if (VampAmount == 0) VampTime = Envir.Time + 1000;
                    VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                    break;

                #endregion

                #region Healing

                case Spell.Healing:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Health >= target.MaxHealth) return;
                    target.HealAmount = (ushort)Math.Min(ushort.MaxValue, target.HealAmount + value);
                    target.OperateTime = 0;
                    LevelMagic(magic);
                    break;

                #endregion

                #region HealingRare

                case Spell.HealingRare:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Health >= target.MaxHealth) return;
                    target.HealAmount = (ushort)Math.Min(ushort.MaxValue, target.HealAmount + value);
                    target.OperateTime = 0;
                    LevelMagic(magic);
                    break;

                #endregion

                #region ElectricShock

                case Spell.ElectricShock:
                    monster = (MonsterObject)data[1];
                    if (monster == null || !monster.IsAttackTarget(this) || monster.CurrentMap != CurrentMap || monster.Node == null) return;
                    ElectricShock(monster, magic);
                    break;

                #endregion

                #region Poisoning

                case Spell.Poisoning:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    UserItem item = (UserItem)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

                    switch (item.Info.Shape)
                    {
                        case 1:
                            target.ApplyPoison(new Poison
                            {
                                Duration = (value * 2) + ((magic.Level + 1) * 7),
                                Owner = this,
                                PType = PoisonType.Green,
                                TickSpeed = 2000,
                                Value = value / 15 + magic.Level + 1 + Envir.Random.Next(Stats[Stat.毒素伤害])
                            }, this);
                            break;
                        case 2:
                            target.ApplyPoison(new Poison
                            {
                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                Owner = this,
                                PType = PoisonType.Red,
                                TickSpeed = 2000,
                            }, this);
                            break;
                    }
                    target.OperateTime = 0;

                    LevelMagic(magic);
                    break;

                #endregion

                #region StormEscape
                case Spell.StormEscape:
                    location = (Point)data[1];
                    if (CurrentMap.Info.NoTeleport)
                    {
                        ReceiveChat(("地图禁止传送"), ChatType.System);
                        return;
                    }
                    if (!CurrentMap.ValidPoint(location) || Envir.Random.Next(4) >= magic.Level + 1 || !Teleport(CurrentMap, location, false)) return;
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.StormEscape }, CurrentLocation);

                    AddBuff(BuffType.时间之殇, this, Settings.Second * 30, new Stats { [Stat.传送技法力消耗数率] = 30 });
                    LevelMagic(magic);
                    break;
                #endregion

                #region StormEscapeRare
                case Spell.StormEscapeRare:
                    location = (Point)data[1];
                    if (CurrentMap.Info.NoTeleport)
                    {
                        ReceiveChat(("地图传送禁止"), ChatType.System);
                        return;
                    }
                    if (!CurrentMap.ValidPoint(location) || Envir.Random.Next(4) >= magic.Level + 1 || !Teleport(CurrentMap, location, false)) return;
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.StormEscapeRare }, CurrentLocation);

                    AddBuff(BuffType.时间之殇, this, Settings.Second * 30, new Stats { [Stat.传送技法力消耗数率] = 30 });
                    LevelMagic(magic);
                    break;
                #endregion

                #region Teleport
                case Spell.Teleport:                                 
                    if (CurrentMap.Info.NoTeleport)
                    {
                        ReceiveChat(("此地图禁止传送"), ChatType.System);
                        return;
                    }

                    if (!MagicTeleport(magic))
                        return;                    

                    AddBuff(BuffType.时间之殇, this, Settings.Second * 30, new Stats { [Stat.传送技法力消耗数率] = 30 });
                    LevelMagic(magic);

                    break;
                #endregion

                #region Blink

                case Spell.Blink:
                    {
                        location = (Point)data[1];
                        if (CurrentMap.Info.NoTeleport)
                        {
                            ReceiveChat(("此地图传送禁止"), ChatType.System);
                            return;
                        }
                        if (Functions.InRange(CurrentLocation, location, magic.Info.Range) == false) return;
                        if (!CurrentMap.ValidPoint(location) || Envir.Random.Next(4) >= magic.Level + 1 || !Teleport(CurrentMap, location, false)) return;
                        CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Teleport }, CurrentLocation);
                        LevelMagic(magic);

                        AddBuff(BuffType.时间之殇, this, Settings.Second * 30, new Stats { [Stat.传送技法力消耗数率] = 30 });
                    }
                    break;

                #endregion

                #region Hiding

                case Spell.Hiding:
                    {
                        value = (int)data[1];

                        AddBuff(BuffType.隐身术, this, Settings.Second * value, new Stats());

                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region Haste

                case Spell.Haste:
                    {
                        AddBuff(BuffType.体迅风, this, (Settings.Second * 25) + (Settings.Second * magic.Level * 15), new Stats { [Stat.攻击速度] = (magic.Level * 2) + 2 });
						LevelMagic(magic);
                    }
                    break;

                #endregion

                #region Fury

                case Spell.Fury:
                    {
                        AddBuff(BuffType.血龙剑法, this, (Settings.Second * 60) + (magic.Level * 10000), new Stats { [Stat.攻击速度] = 4 });
                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region ImmortalSkin

                case Spell.ImmortalSkin:
                    {
                        var stats = new Stats
                        {
                            [Stat.MaxDC] = (int)Math.Round(Stats[Stat.MaxDC] * (0.05 + (0.01 * magic.Level))) * -1,
                            [Stat.MaxAC] = (int)Math.Round(Stats[Stat.MaxAC] * (0.10 + (0.07 * magic.Level)))
                        };

                        AddBuff(BuffType.金刚不坏, this, (Settings.Second * 60) + (magic.Level * 1000), stats);
                        LevelMagic(magic);
                    }
                    break;
                #endregion

                #region ImmortalSkinRare

                case Spell.ImmortalSkinRare:
                    {
                        var stats = new Stats
                        {
                            [Stat.MaxDC] = (int)Math.Round(Stats[Stat.MaxDC] * (0.05 + (0.01 * magic.Level))) * -1,
                            [Stat.MaxAC] = (int)Math.Round(Stats[Stat.MaxAC] * (0.10 + (0.07 * magic.Level)))
                        };

                        AddBuff(BuffType.金刚不坏秘籍, this, (Settings.Second * 60) + (magic.Level * 1000), stats);
                        LevelMagic(magic);
                    }
                    break;
                #endregion

                #region HeavenlySecrets

                case Spell.HeavenlySecrets:
                    {
                        var stats = new Stats
                        {
                            [Stat.法力值消耗数率] = 17 + magic.Level
                        };

                        AddBuff(BuffType.天上秘术, this, (Settings.Second * 30) + (magic.Level * 10000), stats, true);
                        LevelMagic(magic);
                    }
                    break;
                #endregion

                #region LightBody

                case Spell.LightBody:
                    {
                        AddBuff(BuffType.风身术, this, (magic.Level + 1) * (Settings.Second * 30), new Stats { [Stat.敏捷] = (magic.Level + 1) * 2 });
                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region MagicShield

                case Spell.MagicShield:
                    {
                        if (HasBuff(BuffType.魔法盾, out _)) return;

                        LevelMagic(magic);
                        AddBuff(BuffType.魔法盾, this, Settings.Second * (int)data[1], new Stats { [Stat.伤害降低数率] = (magic.Level + 2) * 10 });
                    }
                    break;

                #endregion

                #region TurnUndead

                case Spell.TurnUndead:
                    {
                        monster = (MonsterObject)data[1];
                        if (monster == null || !monster.IsAttackTarget(this) || monster.CurrentMap != CurrentMap || monster.Node == null) return;

                        if (this is HeroObject hero)
                        {
                            monster.LastHitter = monster.EXPOwner = hero.Owner;
                        }
                        else
                        {
                            monster.LastHitter = monster.EXPOwner = this;
                        }

                        monster.LastHitTime = Envir.Time + 5000;
                        monster.EXPOwner = this;
                        monster.EXPOwnerTime = Envir.Time + 5000;
                        monster.Die();
                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region MagicBooster

                case Spell.MagicBooster:
                    {
                        var stats = new Stats
                        {
                            [Stat.MinMC] = (int)data[1],
                            [Stat.MaxMC] = (int)data[1],
                            [Stat.法力值消耗数率] = 6 + magic.Level
                        };

                        AddBuff(BuffType.深延术, this, Settings.Second * 60, stats, true);
                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region Purification

                case Spell.Purification:
                    target = (MapObject)data[1];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (Envir.Random.Next(4) > magic.Level) return;

                    for (int i = 0; i < target.Buffs.Count; i++)
                    {
                        var buff = target.Buffs[i];

                        if (!buff.Properties.HasFlag(BuffProperty.Debuff)) continue;

                        target.RemoveBuff(buff.Type);
                    }

                    if (target.PoisonList.Any(x => x.PType == PoisonType.DelayedExplosion))
                    {
                        target.ExplosionInflictedTime = 0;
                        target.ExplosionInflictedStage = 0;

                        if (target.ObjectID == ObjectID)
                        {
                            Enqueue(new S.RemoveDelayedExplosion { ObjectID = target.ObjectID });
                        }

                        target.Broadcast(new S.RemoveDelayedExplosion { ObjectID = target.ObjectID });
                    }

                    target.PoisonList.Clear();
                    target.OperateTime = 0;

                    LevelMagic(magic);
                    break;

                #endregion

                #region Revelation

                case Spell.Revelation:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    if (target == null || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) return;
                    if (Envir.Random.Next(4) > magic.Level || Envir.Time < target.RevTime) return;

                    target.RevTime = Envir.Time + value * 1000;
                    target.OperateTime = 0;
                    target.BroadcastHealthChange();

                    LevelMagic(magic);
                    break;

                #endregion

                #region Reincarnation

                case Spell.Reincarnation:

                    if (ReincarnationReady)
                    {
                        ReceiveChat("激活目标复活", ChatType.System);
                        ReincarnationTarget.Enqueue(new S.RequestReincarnation { });
                    }
                    LevelMagic(magic);
                    break;

                #endregion

                #region Entrapment

                case Spell.Entrapment:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || target.Race != ObjectType.Monster ||
                        Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 7 || target.Level >= Level + 5 + Envir.Random.Next(8)) return;

                    MirDirection pulldirection = (MirDirection)((byte)(Direction - 4) % 8);
                    int pulldistance = 0;
                    if ((byte)pulldirection % 2 > 0)
                        pulldistance = Math.Max(0, Math.Min(Math.Abs(CurrentLocation.X - target.CurrentLocation.X), Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y)));
                    else
                        pulldistance = pulldirection == MirDirection.Up || pulldirection == MirDirection.Down ? Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y) - 2 : Math.Abs(CurrentLocation.X - target.CurrentLocation.X) - 2;

                    int levelgap = target.Race == ObjectType.Player ? Level - target.Level + 4 : Level - target.Level + 9;
                    if (Envir.Random.Next(30) >= ((magic.Level + 1) * 3) + levelgap) return;

                    int duration = target.Race == ObjectType.Player ? (int)Math.Round((magic.Level + 1) * 1.6) : (int)Math.Round((magic.Level + 1) * 0.8);
                    if (duration > 0) target.ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = duration, TickSpeed = 1000 }, this);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.Entrapment }, target.CurrentLocation);
                    if (target.Pushed(this, pulldirection, pulldistance) > 0) LevelMagic(magic);
                    break;

                #endregion

                #region EntrapmentRare

                case Spell.EntrapmentRare:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || target.Race != ObjectType.Monster ||
                        Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 7 || target.Level >= Level + 5 + Envir.Random.Next(8)) return;

                    MirDirection rarepulldirection = (MirDirection)((byte)(Direction - 4) % 8);
                    int rarepulldistance = 0;
                    if ((byte)rarepulldirection % 2 > 0)
                        rarepulldistance = Math.Max(0, Math.Min(Math.Abs(CurrentLocation.X - target.CurrentLocation.X), Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y)));
                    else
                        rarepulldistance = rarepulldirection == MirDirection.Up || rarepulldirection == MirDirection.Down ? Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y) - 2 : Math.Abs(CurrentLocation.X - target.CurrentLocation.X) - 2;

                    int levelgapRare = target.Race == ObjectType.Player ? Level - target.Level + 4 : Level - target.Level + 9;
                    if (Envir.Random.Next(30) >= ((magic.Level + 1) * 3) + levelgapRare) return;

                    int durationRare = target.Race == ObjectType.Player ? (int)Math.Round((magic.Level + 1) * 1.6) : (int)Math.Round((magic.Level + 1) * 0.8);
                    if (durationRare > 0) target.ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = durationRare, TickSpeed = 1000 }, this);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.EntrapmentRare }, target.CurrentLocation);
                    if (target.Pushed(this, rarepulldirection, rarepulldistance) > 0) LevelMagic(magic);
                    break;

                #endregion

                #region Hallucination

                case Spell.Hallucination:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null ||
                        Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 7 || Envir.Random.Next(Level + 20 + magic.Level * 5) <= target.Level + 10) return;
                    item = GetAmulet(1);
                    if (item == null) return;

                    ((MonsterObject)target).HallucinationTime = Envir.Time + (Envir.Random.Next(20) + 10) * 1000;
                    target.Target = null;

                    ConsumeItem(item, 1);

                    LevelMagic(magic);
                    break;

                #endregion

                #region PetEnhancer

                case Spell.PetEnhancer:
                    {
                        value = (int)data[1];
                        target = (MonsterObject)data[2];

                        if (target.Node == null) return;

                        int dcInc = 2 + target.Level * 2;
                        int acInc = 4 + target.Level;

                        var stats = new Stats
                        {
                            [Stat.MinDC] = dcInc,
                            [Stat.MaxDC] = dcInc,
                            [Stat.MinAC] = acInc,
                            [Stat.MaxAC] = acInc
                        };

                        target.AddBuff(BuffType.血龙水, this, (Settings.Second * value), stats);
                        LevelMagic(magic);
                    }
                    break;

                #endregion

                #region CatTongue 
                case Spell.CatTongue:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.AC, false) > 0)
                    {
                        int rnd = Envir.Random.Next(10);
                        if (rnd >= 8)
                        {
                            if (target.Race == ObjectType.Player && Settings.PvpCanFreeze)
                                target.ApplyPoison(new Poison { PType = PoisonType.Frozen, Duration = (magic.Level + 1) * 3, TickSpeed = 1000 }, this);
                            else
                                rnd -= 4;
                        }

                        if (rnd <= 4)
                            target.ApplyPoison(new Poison { PType = PoisonType.Stun, Duration = (magic.Level + 1) * 3, TickSpeed = 1000 }, this);
                        else if (rnd <= 7)
                            target.ApplyPoison(new Poison { PType = PoisonType.Slow, Duration = (magic.Level + 1) * 3, TickSpeed = 1000 }, this);

                        LevelMagic(magic);
                    }
                    break;
                #endregion

                #region ElementalBarrier, ElementalShot

                case Spell.ElementalBarrier:
                    {
                        if (HasBuff(BuffType.金刚术, out _)) return;

                        if (!HasElemental)
                        {
                            ObtainElement(true);
                            LevelMagic(magic);
                            return;
                        }

                        int barrierPower = GetElementalOrbPower(true);//defensive orbpower
                                                                      //destroy orbs
                        ElementsLevel = 0;
                        ObtainElement(false);
                        LevelMagic(magic);

                        AddBuff(BuffType.金刚术, this, Settings.Second * ((int)data[1] + barrierPower), new Stats { [Stat.伤害降低数率] = (magic.Level + 1) * 10 });
                        CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.ElementalBarrierUp }, CurrentLocation);
                    }
                    break;

                case Spell.ElementalShot:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2))
                    {
                        //destroy orbs
                        ElementsLevel = 0;
                        ObtainElement(false);//update and send to client
                        return;
                    }
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0)
                        LevelMagic(magic);
                    DoKnockback(target, magic);//ElementalShot - Knockback

                    //destroy orbs
                    ElementsLevel = 0;
                    ObtainElement(false);//update and send to client
                    break;

                #endregion

                #region DelayedExplosion

                case Spell.DelayedExplosion:
                case Spell.DelayedExplosionRare: //新增：爆闪-秘籍
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0) LevelMagic(magic);

                    target.ApplyPoison(new Poison
                    {
                        Duration = (value * 2) + (magic.Level + 1) * 7,
                        Owner = this,
                        PType = PoisonType.DelayedExplosion,
                        TickSpeed = 2000,
                        Value = value
                    }, this);

                    target.OperateTime = 0;
                    LevelMagic(magic);
                    break;

                #endregion

                #region BindingShot

                case Spell.BindingShot:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;
                    if (((MonsterObject)target).ShockTime >= Envir.Time) return;//Already shocked

                    Point place = target.CurrentLocation;
                    MonsterObject centerTarget = null;

                    for (int y = place.Y - 1; y <= place.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= CurrentMap.Height) break;

                        for (int x = place.X - 1; x <= place.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= CurrentMap.Width) break;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject targetob = cell.Objects[i];

                                if (y == place.Y && x == place.X && targetob.Race == ObjectType.Monster)
                                {
                                    centerTarget = (MonsterObject)targetob;
                                }

                                switch (targetob.Race)
                                {
                                    case ObjectType.Monster:
                                        if (targetob == null || !targetob.IsAttackTarget(this) || targetob.Node == null || targetob.Level > this.Level + 2) continue;

                                        MonsterObject mobTarget = (MonsterObject)targetob;

                                        if (centerTarget == null) centerTarget = mobTarget;

                                        mobTarget.ShockTime = Envir.Time + value;
                                        mobTarget.Target = null;
                                        break;
                                }
                            }
                        }
                    }

                    if (centerTarget == null) return;

                    //only the centertarget holds the effect
                    centerTarget.BindingShotCenter = true;
                    centerTarget.Broadcast(new S.SetBindingShot { ObjectID = centerTarget.ObjectID, Enabled = true, Value = value });

                    LevelMagic(magic);
                    break;

                #endregion

                #region VampireShot, PoisonShot, CrippleShot
                case Spell.VampireShot:
                case Spell.PoisonShot:
                case Spell.CrippleShot:
                    value = (int)data[1];
                    target = (MapObject)data[2];
                    targetLocation = (Point)data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null || !Functions.InRange(target.CurrentLocation, targetLocation, 2)) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) == 0) return;

                    int buffTime = 5 + (5 * magic.Level);

                    bool hasVampBuff = HasBuff(BuffType.吸血地闪, out _);
                    bool hasPoisonBuff = HasBuff(BuffType.毒魔闪, out _);

                    bool doVamp = false, doPoison = false;
                    if (magic.Spell == Spell.VampireShot)
                    {
                        doVamp = true;
                        if (!hasVampBuff && !hasPoisonBuff && (Envir.Random.Next(20) >= 8))//40% chance
                        {
                            AddBuff(BuffType.吸血地闪, this, Settings.Second * buffTime, new Stats());
                            BroadcastInfo();
                        }
                    }
                    if (magic.Spell == Spell.PoisonShot)
                    {
                        doPoison = true;
                        if (!hasPoisonBuff && !hasVampBuff && (Envir.Random.Next(20) >= 8))//40% chance
                        {
                            AddBuff(BuffType.毒魔闪, this, Settings.Second * buffTime, new Stats());
                            BroadcastInfo();
                        }
                    }
                    if (magic.Spell == Spell.CrippleShot)
                    {
                        if (hasVampBuff || hasPoisonBuff)
                        {
                            place = target.CurrentLocation;
                            for (int y = place.Y - 1; y <= place.Y + 1; y++)
                            {
                                if (y < 0) continue;
                                if (y >= CurrentMap.Height) break;
                                for (int x = place.X - 1; x <= place.X + 1; x++)
                                {
                                    if (x < 0) continue;
                                    if (x >= CurrentMap.Width) break;
                                    Cell cell = CurrentMap.GetCell(x, y);
                                    if (!cell.Valid || cell.Objects == null) continue;
                                    for (int i = 0; i < cell.Objects.Count; i++)
                                    {
                                        MapObject targetob = cell.Objects[i];
                                        if (targetob.Race != ObjectType.Monster && targetob.Race != ObjectType.Player) continue;
                                        if (targetob == null || !targetob.IsAttackTarget(this) || targetob.Node == null) continue;
                                        if (targetob.Dead) continue;

                                        if (hasVampBuff)//Vampire Effect
                                        {
                                            //cancel out buff
                                            AddBuff(BuffType.吸血地闪, this, 0, new Stats());

                                            target.Attacked(this, value, DefenceType.MAC, false);
                                            if (VampAmount == 0) VampTime = Envir.Time + Settings.Second;
                                            VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                                        }
                                        if (hasPoisonBuff)//Poison Effect
                                        {
                                            //cancel out buff
                                            AddBuff(BuffType.毒魔闪, this, 0, new Stats());

                                            targetob.ApplyPoison(new Poison
                                            {
                                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                                Owner = this,
                                                PType = PoisonType.Green,
                                                TickSpeed = 2000,
                                                Value = value / 25 + magic.Level + 1 + Envir.Random.Next(Stats[Stat.毒素伤害])
                                            }, this);
                                            targetob.OperateTime = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (doVamp)//Vampire Effect
                        {
                            if (VampAmount == 0) VampTime = Envir.Time + 1000;
                            VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                        }
                        if (doPoison)//Poison Effect
                        {
                            target.ApplyPoison(new Poison
                            {
                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                Owner = this,
                                PType = PoisonType.Green,
                                TickSpeed = 2000,
                                Value = value / 25 + magic.Level + 1 + Envir.Random.Next(Stats[Stat.毒素伤害])
                            }, this);
                            target.OperateTime = 0;
                        }
                    }

                    LevelMagic(magic);
                    break;
                #endregion

                #region ArcherSummons
                case Spell.SummonVampire:
                case Spell.SummonToad:
                case Spell.SummonSnakes:
                    value = (int)data[1];
                    location = (Point)data[2];
                    target = (MapObject)data[3];

                    int SummonType = 0;
                    switch (magic.Spell)
                    {
                        case Spell.SummonVampire:
                            SummonType = 1;
                            break;
                        case Spell.SummonToad:
                            SummonType = 2;
                            break;
                        case Spell.SummonSnakes:
                            SummonType = 3;
                            break;
                    }
                    if (SummonType == 0) return;

                    for (int i = 0; i < Pets.Count; i++)
                    {
                        monster = Pets[i];
                        if ((monster.Info.Name != (SummonType == 1 ? Settings.VampireName : (SummonType == 2 ? Settings.ToadName : Settings.SnakeTotemName))) || monster.Dead) continue;
                        if (monster.Node == null) continue;
                        monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500, target));
                        monster.Target = target;
                        return;
                    }

                    if (Pets.Count(x => x.Race == ObjectType.Monster) >= 2) return;

                    //left it in for future summon amulets
                    //UserItem item = GetAmulet(5);
                    //if (item == null) return;

                    MonsterInfo info = Envir.GetMonsterInfo((SummonType == 1 ? Settings.VampireName : (SummonType == 2 ? Settings.ToadName : Settings.SnakeTotemName)));
                    if (info == null) return;

                    LevelMagic(magic);
                    //ConsumeItem(item, 5);

                    monster = MonsterObject.GetMonster(info);
                    monster.PetLevel = magic.Level;
                    monster.Master = this;
                    monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
                    monster.Direction = Direction;
                    monster.ActionTime = Envir.Time + 1000;
                    monster.Target = target;

                    if (SummonType == 1)
                        ((Monsters.VampireSpider)monster).AliveTime = Envir.Time + ((magic.Level * 1500) + 15000);
                    if (SummonType == 2)
                        ((Monsters.SpittingToad)monster).AliveTime = Envir.Time + ((magic.Level * 2000) + 25000);
                    if (SummonType == 3)
                        ((Monsters.SnakeTotem)monster).AliveTime = Envir.Time + ((magic.Level * 1500) + 20000);

                    //Pets.Add(monster);

                    DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, location);
                    CurrentMap.ActionList.Add(action);
                    break;
                case Spell.Stonetrap:
                    {
                        duration = (int)data[1];
                        location = (Point)data[2];

                        if (Pets.Where(x => x.Race == ObjectType.Monster).Count() >= magic.Level + 1) return;

                        MonsterInfo mInfo = Envir.GetMonsterInfo(Settings.StoneName);
                        if (mInfo == null) return;

                        LevelMagic(magic);

                        monster = MonsterObject.GetMonster(mInfo);

                        monster.Master = this;
                        monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
                        monster.Direction = Direction;
                        monster.ActionTime = Envir.Time + 1000;

                        StoneTrap st = monster as StoneTrap;
                        st.DieTime = Envir.Time + duration;

                        DelayedAction act = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, location);
                        CurrentMap.ActionList.Add(act);
                        break;
                    }
                    #endregion

            }
        }
        protected void CompleteMine(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            if (target == null) return;
            target.Broadcast(new S.MapEffect { Effect = SpellEffect.Mine, Location = target.CurrentLocation, Value = (byte)Direction });
            //target.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.Mine });
            if ((byte)target.Direction < 6)
                target.Direction++;
            target.Broadcast(target.GetInfo());
        }
        protected void CompleteAttack(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            int damage = (int)data[1];
            DefenceType defence = (DefenceType)data[2];
            bool damageWeapon = (bool)data[3];
            UserMagic userMagic = null;
            bool finalHit = false;
            if (data.Count >= 5)
                userMagic = (UserMagic)data[4];
            if (data.Count >= 6)
                finalHit = (bool)data[5];
            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            if (FatalSword)
                defence = DefenceType.Agility;

            if (target.Attacked(this, damage, defence, damageWeapon) <= 0) return;
            if (FatalSword)
            {
                S.ObjectEffect p = new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.FatalSword };
                CurrentMap.Broadcast(p, target.CurrentLocation);
                FatalSword = false;
                var magic = GetMagic(Spell.FatalSword);
                if (magic != null) LevelMagic(magic);
            }
            if (userMagic != null && finalHit)
            {
                if (userMagic.Spell == Spell.TwinDrakeBlade)
                {
                    if ((((target.Race != ObjectType.Player) || Settings.PvpCanResistPoison) &&
                        (Envir.Random.Next(Settings.PoisonAttackWeight) >= target.Stats[Stat.毒物躲避])) &&
                        (target.Level < Level + 10 && Envir.Random.Next(target.Race == ObjectType.Player ? 40 : 20) <= userMagic.Level + 1))
                    {
                        target.ApplyPoison(new Poison { PType = PoisonType.Stun, Duration = target.Race == ObjectType.Player ? 2 : 2 + userMagic.Level, TickSpeed = 1000 }, this);
                        target.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.TwinDrakeBlade });
                    }
                }
            }

            //Level Fencing / SpiritSword
            foreach (UserMagic magic in Info.Magics)
            {
                switch (magic.Spell)
                {
                    case Spell.Fencing:
                    case Spell.SpiritSword:
                        LevelMagic(magic);
                        break;
                }
            }
        }
        protected void CompleteDamageIndicator(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            DamageType type = (DamageType)data[1];

            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            target.BroadcastDamageIndicator(type);
        }
        protected void CompleteSpellEffect(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            SpellEffect effect = (SpellEffect)data[1];

            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            S.ObjectEffect p = new S.ObjectEffect { ObjectID = target.ObjectID, Effect = effect };
            CurrentMap.Broadcast(p, target.CurrentLocation);
        }
        protected void CompletePoison(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            PoisonType pt = (PoisonType)data[1];
            SpellEffect sp = (SpellEffect)data[2];
            int duration = (int)data[3];
            int tickSpeed = (int)data[4];

            if (target == null) return;

            target.ApplyPoison(new Poison { PType = pt, Duration = duration, TickSpeed = tickSpeed }, this);
            target.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = sp });
        }
        protected UserItem GetAmulet(int count, int shape = 0)
        {
            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem item = Info.Equipment[i];
                if (item != null && item.Info.Type == ItemType.护身符 && item.Info.Shape == shape && item.Count >= count)
                    return item;
            }

            return null;
        }
        protected UserItem GetPoison(int count, byte shape = 0)
        {
            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem item = Info.Equipment[i];
                if (item != null && item.Info.Type == ItemType.护身符 && item.Count >= count)
                {
                    if (shape == 0)
                    {
                        if (item.Info.Shape == 1 || item.Info.Shape == 2)
                            return item;
                    }
                    else
                    {
                        if (item.Info.Shape == shape)
                            return item;
                    }
                }
            }

            return null;
        }
        public UserMagic GetMagic(Spell spell)
        {
            for (int i = 0; i < Info.Magics.Count; i++)
            {
                UserMagic magic = Info.Magics[i];
                if (magic.Spell != spell) continue;
                return magic;
            }

            return null;
        }
        public void LevelMagic(UserMagic magic)
        {
            byte exp = (byte)(Envir.Random.Next(3) + 1);

            if (Settings.MentorSkillBoost && Info.Mentor != 0 && Info.IsMentor)
            {
                if (HasBuff(BuffType.衣钵相传, out _))
                {
                    CharacterInfo mentor = Envir.GetCharacterInfo(Info.Mentor);
                    PlayerObject player = Envir.GetPlayer(mentor.Name);
                    if (player.CurrentMap == CurrentMap && Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                    {
                        if (Stats[Stat.技能熟练度倍率] == 1)
                        {
                            if (GroupMembers != null && GroupMembers.Contains(player))
                                exp *= 2;
                        }
                    }
                }
            }

            exp *= (byte)Math.Min(byte.MaxValue, Stats[Stat.技能熟练度倍率]);

            if (Level == ushort.MaxValue) exp = byte.MaxValue;

            int oldLevel = magic.Level;

            switch (magic.Level)
            {
                case 0:
                    if (Level < magic.Info.Level1)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need1)
                    {
                        magic.Level++;
                        magic.Experience = (ushort)(magic.Experience - magic.Info.Need1);
                        RefreshStats();
                    }
                    break;
                case 1:
                    if (Level < magic.Info.Level2)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need2)
                    {
                        magic.Level++;
                        magic.Experience = (ushort)(magic.Experience - magic.Info.Need2);
                        RefreshStats();
                    }
                    break;
                case 2:
                    if (Level < magic.Info.Level3)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need3)
                    {
                        magic.Level++;
                        magic.Experience = 0;
                        RefreshStats();
                    }
                    break;
                default:
                    return;
            }

            if (oldLevel != magic.Level)
            {
                long delay = magic.GetDelay();
                Enqueue(new S.MagicDelay { ObjectID = ObjectID, Spell = magic.Spell, Delay = delay });
            }

            Enqueue(new S.MagicLeveled { ObjectID = ObjectID, Spell = magic.Spell, Level = magic.Level, Experience = magic.Experience });

        }
        public virtual bool MagicTeleport(UserMagic magic)
        {
            return false;
        }
        public override bool Teleport(Map temp, Point location, bool effects = true, byte effectnumber = 0)
        {
            Map oldMap = CurrentMap;
            Point oldLocation = CurrentLocation;

            bool mapChanged = temp != oldMap;

            if (!base.Teleport(temp, location, effects)) return false;

            Enqueue(new S.MapChanged
            {
                MapIndex = CurrentMap.Info.Index,
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                Weather = CurrentMap.Info.WeatherParticles,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight,
                Music = CurrentMap.Info.Music
            });

            if (effects) Enqueue(new S.ObjectTeleportIn { ObjectID = ObjectID, Type = effectnumber });

            if (RidingMount) RefreshMount();
            if (ActiveBlizzard) ActiveBlizzard = false;

            if (CheckStacked())
            {
                StackingTime = Envir.Time + 1000;
                Stacking = true;
            }

            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                SetBindSafeZone(szi);
                InSafeZone = true;
            }
            else
                InSafeZone = false;            

            return true;
        }
        protected virtual void SetBindSafeZone(SafeZoneInfo szi) { }
        public virtual bool AtWar(HumanObject attacker)
        {
            return false;
        }
        private Packet GetMountInfo()
        {
            return new S.MountUpdate
            {
                ObjectID = ObjectID,
                RidingMount = RidingMount,
                MountType = Mount.MountType
            };
        }
        protected Packet GetUpdateInfo()
        {
            return new S.PlayerUpdate
            {
                ObjectID = ObjectID,
                Weapon = Looks_Weapon,
                WeaponEffect = Looks_WeaponEffect,
                Armour = Looks_Armour,

                // 换装备后品质也跟着广播，否则别人看你换了神物装备还得等下次重新进视野才发光
                ArmourGrade = Info.Equipment[(int)EquipmentSlot.盔甲] != null ? (byte)Info.Equipment[(int)EquipmentSlot.盔甲].Info.Grade : (byte)0,
                WeaponGrade = Info.Equipment[(int)EquipmentSlot.武器] != null ? (byte)Info.Equipment[(int)EquipmentSlot.武器].Info.Grade : (byte)0,

                Light = Light,
                WingEffect = Looks_Wings
            };
        }
        protected virtual void UpdateLooks(short OldLooks_Weapon)
        {
            Broadcast(GetUpdateInfo());
        }
        public Packet GetInfoEx(HumanObject player)
        {
            var p = (S.ObjectPlayer)GetInfo();

            if (p != null)
            {
                p.NameColour = player.GetNameColour(this);
            }

            return p;
        }
        public override bool IsAttackTarget(HumanObject attacker)
        {
            return true;
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {            
            return true;
        }

        public override bool IsFriendlyTarget(HumanObject ally)
        {
            return true;
        }
        public override bool IsFriendlyTarget(MonsterObject ally)
        {            
            return true;
        }
        public override void ReceiveChat(string text, ChatType type) { }
        public override Packet GetInfo() { return null; }
        public override int Attacked(HumanObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            if (attacker.Race == ObjectType.Hero)
            {
                HeroObject heroAttacker = (HeroObject)attacker;
                attacker = heroAttacker.Owner;
                heroAttacker.Target = this;
            }

            var armour = GetArmour(type, attacker, out bool hit);

            if (!hit)
            {
                return 0;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            if (damageWeapon)
                attacker.DamageWeapon();

            damage += attacker.Stats[Stat.攻击增伤];

            if (Envir.Random.Next(100) < Stats[Stat.反弹伤害])
            {
                if (attacker.IsAttackTarget(this))
                {
                    attacker.Attacked(this, damage, type, false);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect }, CurrentLocation);
                }
                return 0;
            }

            //MagicShield, ElementalBarrier
            if (Stats[Stat.伤害降低数率] > 0)
            {
                damage -= (damage * Stats[Stat.伤害降低数率]) / 100;
            }

            if (armour >= damage)
            {
                BroadcastDamageIndicator(DamageType.Miss);
                return 0;
            }

            if (Hidden)
            {
                RemoveBuff(BuffType.月影术);
                RemoveBuff(BuffType.烈火身);
            }

            //EnergyShield
            if (Stats[Stat.气功盾恢复数率] > 0)
            {
                if (Envir.Random.Next(100) < Stats[Stat.气功盾恢复数率])
                {
                    if (HP + (Stats[Stat.气功盾恢复生命值]) >= Stats[Stat.HP])
                        SetHP(Stats[Stat.HP]);
                    else
                        ChangeHP(Stats[Stat.气功盾恢复生命值]);
                }
            }

            if (Envir.Random.Next(100) < (attacker.Stats[Stat.暴击倍率] * Settings.CriticalRateWeight))
            {
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Critical }, CurrentLocation);
                damage = Math.Min(int.MaxValue, damage + (int)Math.Floor(damage * (((double)attacker.Stats[Stat.暴击伤害] / (double)Settings.CriticalDamageWeight) * 10)));
                BroadcastDamageIndicator(DamageType.Critical);
            }

            if (HasBuff(BuffType.魔法盾, out Buff magicShield))
            {
                var duration = (int)Math.Min(int.MaxValue, magicShield.ExpireTime - ((damage - armour) * 60));
                AddBuff(BuffType.魔法盾, this, duration, null);
            }

            if (HasBuff(BuffType.金刚术, out Buff elementalBarrier))
            {
                var duration = (int)Math.Min(int.MaxValue, elementalBarrier.ExpireTime - ((damage - armour) * 60));
                AddBuff(BuffType.金刚术, this, duration, null);
            }

            if (attacker.Stats[Stat.吸血数率] > 0 && damageWeapon)
            {
                attacker.HpDrain += Math.Max(0, ((float)(damage - armour) / 100) * attacker.Stats[Stat.吸血数率]);
                if (attacker.HpDrain > 2)
                {
                    int HpGain = (int)Math.Floor(attacker.HpDrain);
                    attacker.ChangeHP(HpGain);
                    attacker.HpDrain -= HpGain;
                }
            }

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (PoisonList[i].PType != PoisonType.LRParalysis) continue;

                PoisonList.RemoveAt(i);
                OperateTime = 0;
            }

            LastHitter = attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            if (Envir.Time > BrownTime && PKPoints < 200 && !AtWar(attacker))
                attacker.BrownTime = Envir.Time + Settings.Minute;

            ushort LevelOffset = (byte)(Level > attacker.Level ? 0 : Math.Min(10, attacker.Level - Level));

            ApplyNegativeEffects(attacker, type, LevelOffset);

            attacker.GatherElement();

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;

            CounterAttackCast(GetMagic(Spell.CounterAttack), LastHitter);

            Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });

            BroadcastDamageIndicator(DamageType.Hit, armour - damage);

            ChangeHP(armour - damage);
            return damage - armour;
        }
        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            var armour = GetArmour(type, attacker, out bool hit);

            if (!hit)
            {
                return 0;
            }

            if (Envir.Random.Next(100) < Stats[Stat.反弹伤害])
            {
                if (attacker.IsAttackTarget(this))
                {
                    attacker.Attacked(this, damage, type, false);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect }, CurrentLocation);
                }
                return 0;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            //MagicShield, ElementalBarrier
            if (Stats[Stat.伤害降低数率] != 0)
            {
                damage -= (damage * Stats[Stat.伤害降低数率]) / 100;
            }

            if (armour >= damage)
            {
                BroadcastDamageIndicator(DamageType.Miss);
                return 0;
            }

            if (Hidden)
            {
                RemoveBuff(BuffType.月影术);
                RemoveBuff(BuffType.烈火身);
            }

            if (Stats[Stat.气功盾恢复数率] > 0)
            {
                if (Envir.Random.Next(100) < Stats[Stat.气功盾恢复数率])
                {
                    if (HP + (Stats[Stat.气功盾恢复生命值]) >= Stats[Stat.HP])
                        SetHP(Stats[Stat.HP]);
                    else
                        ChangeHP(Stats[Stat.气功盾恢复生命值]);
                }
            }

            if (HasBuff(BuffType.魔法盾, out Buff magicShield))
            {
                var duration = (int)Math.Min(int.MaxValue, magicShield.ExpireTime - ((damage - armour) * 60));
                AddBuff(BuffType.魔法盾, this, duration, null);
            }

            if (HasBuff(BuffType.金刚术, out Buff elementalBarrier))
            {
                var duration = (int)Math.Min(int.MaxValue, elementalBarrier.ExpireTime - ((damage - armour) * 60));
                AddBuff(BuffType.金刚术, this, duration, null);
            }

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                if (PoisonList[i].PType != PoisonType.LRParalysis) continue;

                PoisonList.RemoveAt(i);
                OperateTime = 0;
            }

            LastHitter = attacker.Master ?? attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;

            CounterAttackCast(GetMagic(Spell.CounterAttack), LastHitter);

            if (StruckTime < Envir.Time)
            {
                Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
                Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });
                StruckTime = Envir.Time + 500;
            }

            BroadcastDamageIndicator(DamageType.Hit, armour - damage);

            ChangeHP(armour - damage);
            return damage - armour;
        }
        public override int Struck(int damage, DefenceType type = DefenceType.ACAgility)
        {
            int armour = 0;

            if (Hidden)
            {
                RemoveBuff(BuffType.月影术);
                RemoveBuff(BuffType.烈火身);
            }

            switch (type)
            {
                case DefenceType.ACAgility:
                    armour = GetAttackPower(Stats[Stat.MinAC], Stats[Stat.MaxAC]);
                    break;
                case DefenceType.AC:
                    armour = GetAttackPower(Stats[Stat.MinAC], Stats[Stat.MaxAC]);
                    break;
                case DefenceType.MACAgility:
                    armour = GetAttackPower(Stats[Stat.MinMAC], Stats[Stat.MaxMAC]);
                    break;
                case DefenceType.MAC:
                    armour = GetAttackPower(Stats[Stat.MinMAC], Stats[Stat.MaxMAC]);
                    break;
                case DefenceType.Agility:
                    break;
            }

            armour = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(armour * ArmourRate))));
            damage = (int)Math.Max(int.MinValue, (Math.Min(int.MaxValue, (decimal)(damage * DamageRate))));

            //MagicShield, ElementalBarrier
            if (Stats[Stat.伤害降低数率] != 0)
            {
                damage -= (damage * Stats[Stat.伤害降低数率]) / 100;
            }

            if (armour >= damage) return 0;

            if (HasBuff(BuffType.魔法盾, out Buff magicShield))
            {
                var duration = (int)Math.Min(int.MaxValue, magicShield.ExpireTime - ((damage - armour) * 60));
                AddBuff(BuffType.魔法盾, this, duration, null);
            }

            if (HasBuff(BuffType.金刚术, out Buff elementalBarrier))
            {
                var duration = (int)Math.Min(int.MaxValue, elementalBarrier.ExpireTime - ((damage - armour) * 60));
                AddBuff(BuffType.金刚术, this, duration, null);
            }

            RegenTime = Envir.Time + RegenDelay;
            LogTime = Envir.Time + Globals.LogDelay;

            DamageDura();
            ActiveBlizzard = false;
            ActiveReincarnation = false;
            Enqueue(new S.Struck { AttackerID = 0 });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = 0, Direction = Direction, Location = CurrentLocation });

            ChangeHP(armour - damage);
            return damage - armour;
        }

        public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false, bool ignoreDefence = true)
        {
            if (Caster != null && !NoResist)
            {
                if (((Caster.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonResistWeight) < Stats[Stat.毒物躲避]))
                {
                    return;
                }
            }

            if (!ignoreDefence && (p.PType == PoisonType.Green))
            {
                int armour = GetAttackPower(Stats[Stat.MinMAC], Stats[Stat.MaxMAC]);

                if (p.Value < armour)
                    p.PType = PoisonType.None;
                else
                    p.Value -= armour;
            }

            if (p.Owner != null && p.Owner is PlayerObject player && Envir.Time > BrownTime && PKPoints < 200)
            {
                bool ownerBrowns = true;
                if (player.MyGuild != null && MyGuild != null && MyGuild.IsAtWar() && MyGuild.IsEnemy(player.MyGuild))
                    ownerBrowns = false;

                if (ownerBrowns && !player.WarZone)
                        p.Owner.BrownTime = Envir.Time + Settings.Minute;
            }

            if ((p.PType == PoisonType.Green) || (p.PType == PoisonType.Red)) p.Duration = Math.Max(0, p.Duration - Stats[Stat.中毒恢复]);
            if (p.Duration == 0) return;
            if (p.PType == PoisonType.None) return;

            for (int i = 0; i < PoisonList.Count; i++)
            {
                if (PoisonList[i].PType != p.PType) continue;
                if ((PoisonList[i].PType == PoisonType.Green) && (PoisonList[i].Value > p.Value)) return;//cant cast weak poison to cancel out strong poison
                if ((PoisonList[i].PType != PoisonType.Green) && ((PoisonList[i].Duration - PoisonList[i].Time) > p.Duration)) return;//cant cast 1 second poison to make a 1minute poison go away!
                if ((PoisonList[i].PType == PoisonType.Frozen) || (PoisonList[i].PType == PoisonType.Slow) || (PoisonList[i].PType == PoisonType.Paralysis) || (PoisonList[i].PType == PoisonType.LRParalysis)) return;//prevents mobs from being perma frozen/slowed
                if (p.PType == PoisonType.DelayedExplosion) return;

                ReceiveChat(GameLanguage.BeenPoisoned, ChatType.System2);
                PoisonList[i] = p;
                return;
            }

            switch (p.PType)
            {
                case PoisonType.DelayedExplosion:
                    {
                        ExplosionInflictedTime = Envir.Time + 4000;
                        Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion });
                        Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.DelayedExplosion });
                        ReceiveChat("行走触发爆炸", ChatType.System);
                    }
                    break;
                case PoisonType.Dazed:
                    {
                        Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Stunned, Time = (uint)(p.Duration * p.TickSpeed) });
                        Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Stunned, Time = (uint)(p.Duration * p.TickSpeed) });
                        ReceiveChat(GameLanguage.BeenPoisoned, ChatType.System2);
                    }
                    break;
                case PoisonType.Blindness:
                    {
                        AddBuff(BuffType.失明状态, Caster, (int)(p.Duration * p.TickSpeed), new Stats { [Stat.准确] = p.Value * -1 });
                        ReceiveChat(GameLanguage.BeenPoisoned, ChatType.System2);
                    }
                    break;
                default:
                    {
                        ReceiveChat(GameLanguage.BeenPoisoned, ChatType.System2);
                    }
                    break;
            }

            PoisonList.Add(p);
        }

        public override Buff AddBuff(BuffType type, MapObject owner, int duration, Stats stats, bool refreshStats = true, bool updateOnly = false, params int[] values)
        {
            //★ 配置属性注入要在 base 之前判「本来就有没有」，base 之后 buff 已经进列表了。
            bool existed = HasBuff(type, out Buff _);

            Buff b = base.AddBuff(type, owner, duration, stats, refreshStats, updateOnly, values);

            ApplyBuffConfigStats(b, owner, existed, stats);

            FinishAddBuff(b, refreshStats);

            return b;
        }

        /// <summary>
        /// 重放一条「已经存在于列表里的」持久化 BUFF（登录、英雄召唤/复活时用）。
        ///
        /// 只把存下来的剩余时长 / 属性 / Values 灌回去，**刻意不走 ApplyBuffConfigStats**：
        /// 配置里那部分属性在第一次挂上时就已经并进 buff.Stats 了，重放时再来一遍会越登录越强
        /// （自定义槽位最明显：它以传进来的 stats 为底再叠一层配置值，登录 N 次就叠 N 层）。
        ///
        /// 时长语义与登录一致：存的是「剩余毫秒」，重放时把 LastTime 拉到当前时刻，
        /// 所以下线/收起英雄的那段时间不掉 BUFF（和药剂写着 5 小时就该给满 5 小时的设计一致）。
        /// </summary>
        public void ReplayBuff(Buff buff)
        {
            if (buff == null || buff.Info == null) return;

            buff.LastTime = Envir.Time;
            buff.ObjectID = ObjectID;

            Buff b = base.AddBuff(buff.Type, null, (int)buff.ExpireTime, buff.Stats, true, true, buff.Values);

            FinishAddBuff(b, true);
        }

        /// <summary>BUFF 落地后的统一收尾：特效广播 + 下发客户端 + 刷属性。AddBuff / ReplayBuff 共用。</summary>
        private void FinishAddBuff(Buff b, bool refreshStats)
        {
            if (b == null) return;

            switch (b.Type)
            {
                case BuffType.魔法盾:
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.MagicShieldUp }, CurrentLocation);
                    break;
                case BuffType.金刚术:
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.ElementalBarrierUp }, CurrentLocation);
                    break;
                case BuffType.轻身步:
                    // 轻身步的「跑 3 格」标志位必须跟着 buff 走。
                    // 原来只在 SwiftFeet() 里置 true、只在 RemoveBuff 里置 false，
                    // 而登录/换图时 PlayerObject.StartGameSuccess() 会重放持久化 buff（走本方法），
                    // 却不经过 SwiftFeet() —— 于是客户端 Sprint=true（跑 3 格）、服务端还是 false（跑 2 格），
                    // 每跑一步都被 S.UserLocation 纠偏，表现为回拉卡顿/错位，重放一次技能才自愈。
                    ActiveSwiftFeet = true;
                    break;
            }

            var packet = new S.AddBuff { Buff = b.ToClientBuff() };

            Enqueue(packet);

            if (b.Info.Visible)
            {
                Broadcast(packet);
            }

            if (refreshStats)
            {
                RefreshStats();
            }
        }

        /// <summary>
        /// 把 Configs\BuffSystem.ini 里配的属性加成挂到 buff 上。
        ///
        /// 两种情况的处理刻意不同：
        ///   · 自定义槽位（Custom1~Custom24）：以「本次调用带进来的属性」为底，再叠加配置属性。
        ///       —— 底值必须保留：脚本的 GIVEBUFF 是带属性调用的（如 MaxDC=12），
        ///          如果直接整体替换，脚本给的那部分会被静默吃掉。
        ///          而「本次调用带进来的」是确定值，重复施法重建出来的结果一致，不会越滚越大。
        ///   · 已有 BUFF（如 [隐身术] 补充一条属性）：只在第一次挂上时追加一次。
        ///      它们大多是 ResetDuration 堆叠，没法从 buff.Stats 里区分「代码给的」和「配置给的」，
        ///      每次重放都追加会越滚越大，所以刻意只加一次。
        /// </summary>
        private void ApplyBuffConfigStats(Buff buff, MapObject owner, bool existed, Stats source)
        {
            if (buff == null) return;

            BuffDef def = BuffConfig.Get(buff.Type);

            if (def == null || def.Stats.Count == 0) return;

            if (!def.IsCustom && existed) return;

            // 等级取施法者等级；没有施法者时取目标自身（GM 给自己加 = 按自己等级算）
            MapObject caster = owner ?? this;
            int level = caster == null ? 0 : caster.Level;

            Stats bonus = def.BuildStats(level);

            if (def.IsCustom)
            {
                Stats merged = new Stats(source ?? new Stats());
                merged.Add(bonus);

                buff.Stats = merged;
            }
            else
            {
                buff.Stats.Add(bonus);
            }
        }

        public override void PauseBuff(Buff b)
        {
            if (b.Paused) return;

            base.PauseBuff(b);

            Enqueue(new S.PauseBuff { Type = b.Type, ObjectID = ObjectID, Paused = true });
        }

        public override void UnpauseBuff(Buff b)
        {
            if (!b.Paused) return;

            base.UnpauseBuff(b);

            Enqueue(new S.PauseBuff { Type = b.Type, ObjectID = ObjectID, Paused = false });
        }
        protected int GetCurrentStatCount(UserItem gem, UserItem item)
        {
            if (gem.GetTotal(Stat.MaxDC) > 0)
                return item.AddedStats[Stat.MaxDC];

            else if (gem.GetTotal(Stat.MaxMC) > 0)
                return item.AddedStats[Stat.MaxMC];

            else if (gem.GetTotal(Stat.MaxSC) > 0)
                return item.AddedStats[Stat.MaxSC];

            else if (gem.GetTotal(Stat.MaxAC) > 0)
                return item.AddedStats[Stat.MaxAC];

            else if (gem.GetTotal(Stat.MaxMAC) > 0)
                return item.AddedStats[Stat.MaxMAC];

            else if ((gem.Info.Durability) > 0)
                return item.Info.Durability > item.MaxDura ? 0 : ((item.MaxDura - item.Info.Durability) / 1000);

            else if (gem.GetTotal(Stat.攻击速度) > 0)
                return item.AddedStats[Stat.攻击速度];

            else if (gem.GetTotal(Stat.敏捷) > 0)
                return item.AddedStats[Stat.敏捷];

            else if (gem.GetTotal(Stat.准确) > 0)
                return item.AddedStats[Stat.准确];

            else if (gem.GetTotal(Stat.毒素伤害) > 0)
                return item.AddedStats[Stat.毒素伤害];

            else if (gem.GetTotal(Stat.冰冻伤害) > 0)
                return item.AddedStats[Stat.冰冻伤害];

            else if (gem.GetTotal(Stat.魔法躲避) > 0)
                return item.AddedStats[Stat.魔法躲避];

            else if (gem.GetTotal(Stat.毒物躲避) > 0)
                return item.AddedStats[Stat.毒物躲避];

            else if (gem.GetTotal(Stat.幸运) > 0)
                return item.AddedStats[Stat.幸运];

            else if (gem.GetTotal(Stat.中毒恢复) > 0)
                return item.AddedStats[Stat.中毒恢复];

            else if (gem.GetTotal(Stat.HP) > 0)
                return item.AddedStats[Stat.HP];

            else if (gem.GetTotal(Stat.MP) > 0)
                return item.AddedStats[Stat.MP];

            else if (gem.GetTotal(Stat.生命恢复) > 0)
                return item.AddedStats[Stat.生命恢复];

            else if (gem.GetTotal(Stat.生命值数率) > 0)
                return item.AddedStats[Stat.生命值数率];

            else if (gem.GetTotal(Stat.法力值数率) > 0)
                return item.AddedStats[Stat.法力值数率];

            else if (gem.GetTotal(Stat.法力恢复) > 0)
                return item.AddedStats[Stat.法力恢复];

            else if (gem.GetTotal(Stat.神圣) > 0)
                return item.AddedStats[Stat.神圣];

            else if (gem.GetTotal(Stat.强度) > 0)
                return item.AddedStats[Stat.强度];
            return 0;
        }
        public bool CanGainItem(UserItem item)
        {
            if (FreeSpace(Info.Inventory) > 0)
            {
                return true;
            }

            if (item.Info.Type == ItemType.护身符)
            {
                ushort count = item.Count;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem bagItem = Info.Inventory[i];

                    if (bagItem == null || bagItem.Info != item.Info) continue;

                    if (bagItem.Count + count <= bagItem.Info.StackSize) return true;

                    count -= (ushort)(bagItem.Info.StackSize - bagItem.Count);
                }

                return false;
            }

            if (item.Info.StackSize > 1)
            {
                ushort count = item.Count;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem bagItem = Info.Inventory[i];

                    if (bagItem.Info != item.Info) continue;

                    if (bagItem.Count + count <= bagItem.Info.StackSize) return true;

                    count -= (ushort)(bagItem.Info.StackSize - bagItem.Count);
                }
            }

            return false;
        }
        public bool CanGainItems(UserItem[] items)
        {
            int itemCount = items.Count(e => e != null);
            ushort stackOffset = 0;

            if (itemCount < 1) return true;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;

                if (items[i].Info.StackSize > 1)
                {
                    ushort count = items[i].Count;

                    for (int u = 0; u < Info.Inventory.Length; u++)
                    {
                        UserItem bagItem = Info.Inventory[u];

                        if (bagItem == null || bagItem.Info != items[i].Info) continue;

                        if (bagItem.Count + count > bagItem.Info.StackSize) stackOffset++;

                        break;
                    }
                }
            }

            if (FreeSpace(Info.Inventory) < itemCount + stackOffset) return false;

            return true;
        }
        public bool CanEquipItem(UserItem item, int slot)
        {
            switch ((EquipmentSlot)slot)
            {
                case EquipmentSlot.武器:
                    if (item.Info.Type != ItemType.武器)
                        return false;
                    break;
                case EquipmentSlot.盔甲:
                    if (item.Info.Type != ItemType.盔甲)
                        return false;
                    break;
                case EquipmentSlot.头盔:
                    if (item.Info.Type != ItemType.头盔)
                        return false;
                    break;
                case EquipmentSlot.照明物:
                    if (item.Info.Type != ItemType.照明物)
                        return false;
                    break;
                case EquipmentSlot.项链:
                    if (item.Info.Type != ItemType.项链)
                        return false;
                    break;
                case EquipmentSlot.左手镯:
                    if (item.Info.Type != ItemType.手镯)
                        return false;
                    break;
                case EquipmentSlot.右手镯:
                    if (item.Info.Type != ItemType.手镯 && item.Info.Type != ItemType.护身符)
                        return false;
                    break;
                case EquipmentSlot.左戒指:
                case EquipmentSlot.右戒指:
                    if (item.Info.Type != ItemType.戒指)
                        return false;
                    break;
                case EquipmentSlot.护身符:
                    if (item.Info.Type != ItemType.护身符)// || item.Info.Shape == 0
                        return false;
                    break;
                case EquipmentSlot.靴子:
                    if (item.Info.Type != ItemType.靴子)
                        return false;
                    break;
                case EquipmentSlot.腰带:
                    if (item.Info.Type != ItemType.腰带)
                        return false;
                    break;
                case EquipmentSlot.守护石:
                    // 石头格：原有的守护石，以及气血石 / 魔血石
                    if (item.Info.Type != ItemType.守护石 && item.Info.Type != ItemType.气血石 && item.Info.Type != ItemType.魔血石)
                        return false;
                    break;
                case EquipmentSlot.坐骑:
                    if (item.Info.Type != ItemType.坐骑)
                        return false;
                    break;
                default:
                    return false;
            }


            switch (Gender)
            {
                case MirGender.男性:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.男性))
                        return false;
                    break;
                case MirGender.女性:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.女性))
                        return false;
                    break;
            }


            switch (Class)
            {
                case MirClass.战士:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.战士))
                        return false;
                    break;
                case MirClass.法师:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.法师))
                        return false;
                    break;
                case MirClass.道士:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.道士))
                        return false;
                    break;
                case MirClass.刺客:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.刺客))
                        return false;
                    break;
            }

            switch (item.Info.RequiredType)
            {
                case RequiredType.Level:
                    if (Level < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MaxAC:
                    if (Stats[Stat.MaxAC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MaxMAC:
                    if (Stats[Stat.MaxMAC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MaxDC:
                    if (Stats[Stat.MaxDC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MaxMC:
                    if (Stats[Stat.MaxMC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MaxSC:
                    if (Stats[Stat.MaxSC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MaxLevel:
                    if (Level > item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MinAC:
                    if (Stats[Stat.MinAC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MinMAC:
                    if (Stats[Stat.MinMAC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MinDC:
                    if (Stats[Stat.MinDC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MinMC:
                    if (Stats[Stat.MinMC] < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MinSC:
                    if (Stats[Stat.MinSC] < item.Info.RequiredAmount)
                        return false;
                    break;
            }

            // 超负重：不再校验腕力负重 / 装备负重
            if (!IgnoreWeight)
            {
                if (item.Info.Type == ItemType.武器 || item.Info.Type == ItemType.照明物)
                {
                    if (item.Weight - (Info.Equipment[slot] != null ? Info.Equipment[slot].Weight : 0) + CurrentHandWeight > Stats[Stat.腕力负重])
                        return false;
                }
                else
                    if (item.Weight - (Info.Equipment[slot] != null ? Info.Equipment[slot].Weight : 0) + CurrentWearWeight > Stats[Stat.装备负重])
                    return false;
            }

            if (RidingMount && item.Info.Type != ItemType.照明物)
            {
                return false;
            }

            return true;
        }
        public void GainItem(UserItem item)
        {
            //CheckItemInfo(item.Info);
            CheckItem(item);

            UserItem clonedItem = item.Clone();

            Enqueue(new S.GainedItem { Item = clonedItem }); //Cloned because we are probably going to change the amount.

            AddItem(item);
            RefreshBagWeight();
        }

        private void DamageDura()
        {
            // 离线挂机免损耗：挂机期间被怪打不掉装备持久
            if (AfkFreeEquipDura) return;

            // 战斗磨损倍率：按概率触发（30 即 30% 的被击次数才掉身上装备持久）
            if (Settings.DuraLossRate < 100 && Envir.Random.Next(100) >= Settings.DuraLossRate) return;

            if (!SpecialMode.HasFlag(SpecialItemMode.NoDuraLoss))
                for (int i = 0; i < Info.Equipment.Length; i++)
                    // 石头格的耐力就是气血石 / 魔血石的电量，由 ProcessStones 按秒扣，战斗不能再扣一次
                    if (i != (int)EquipmentSlot.武器 && i != (int)EquipmentSlot.守护石)
                        DamageItem(Info.Equipment[i], Envir.Random.Next(1) + 1);
        }
        public void DamageWeapon()
        {
            // 离线挂机免损耗：挂机期间不磨损武器
            if (AfkFreeEquipDura) return;

            if (!SpecialMode.HasFlag(SpecialItemMode.NoDuraLoss))
            {
                // 战斗磨损倍率：按概率触发（30 即 30% 的攻击才掉武器持久），1 点的消耗也能按比例放缓
                if (Settings.DuraLossRate < 100 && Envir.Random.Next(100) >= Settings.DuraLossRate) return;
                DamageItem(Info.Equipment[(int)EquipmentSlot.武器], Envir.Random.Next(4) + 1);
            }
        }
        public void DamageItem(UserItem item, int amount, bool isChanged = false)
        {
            if (item == null || item.CurrentDura == 0 || item.Info.Type == ItemType.护身符) return;
            if ((item.WeddingRing == Info.Married) && (Info.Equipment[(int)EquipmentSlot.左戒指].UniqueID == item.UniqueID)) return;
            if (item.GetTotal(Stat.强度) > 0) amount = Math.Max(1, amount - item.GetTotal(Stat.强度));
            item.CurrentDura = (ushort)Math.Max(ushort.MinValue, item.CurrentDura - amount);
            item.DuraChanged = true;

            if (item.CurrentDura > 0 && isChanged != true) return;
            Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });

            item.DuraChanged = false;
            RefreshStats();
        }

        public void RemoveObjects(MirDirection dir, int count)
        {
            switch (dir)
            {
                case MirDirection.Up:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpRight:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Right:
                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownRight:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Down:
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownLeft:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Left:
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpLeft:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
            }
        }
        public void AddObjects(MirDirection dir, int count)
        {
            switch (dir)
            {
                case MirDirection.Up:
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpRight:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Right:
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownRight:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Down:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownLeft:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Left:
                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpLeft:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
            }
        }
        public override void Remove(HumanObject player)
        {
            if (player == this) return;

            base.Remove(player);
            Enqueue(new S.ObjectRemove { ObjectID = player.ObjectID });
        }
        public override void Add(HumanObject player)
        {
            if (player == this) return;

            //base.Add(player);
            Enqueue(player.GetInfoEx(this));
            player.Enqueue(GetInfoEx(player));

            player.SendHealth(this);
            SendHealth(player);
        }
        public override void Remove(MonsterObject monster)
        {
            Enqueue(new S.ObjectRemove { ObjectID = monster.ObjectID });
        }
        public override void Add(MonsterObject monster)
        {
            Enqueue(monster.GetInfo());

            monster.SendHealth(this);
        }
        public override void SendHealth(HumanObject player)
        {
            if (!player.IsMember(this) && Envir.Time > RevTime) return;
            byte time = Math.Min(byte.MaxValue, (byte)Math.Max(5, (RevTime - Envir.Time) / 1000));
            player.Enqueue(new S.ObjectHealth { ObjectID = ObjectID, Percent = PercentHealth, Expire = time, CurrentHP = Health, MaxHP = MaxHealth });
        }
        protected virtual void CleanUp()
        {
            Connection.Player = null;
            Info.Player = null;
            Info.Mount = null;
            Connection.CleanObservers();
            Connection = null;
            Info = null;
        }
        public virtual void Enqueue(Packet p)
        {
            if (Connection == null) return;
            Connection.Enqueue(p);

            //MessageQueue.EnqueueDebugging(((ServerPacketIds)p.Index).ToString());
        }
        public virtual void Enqueue(Packet p, MirConnection c)
        {            
            if (c == null)
            {
                Enqueue(p);
                return;
            }

            c.Enqueue(p);
        }

        public void SpellToggle(Spell spell, SpellToggleState state)
        {
            if (Dead) return;

            UserMagic magic;
            bool use = Convert.ToBoolean(state);

            magic = GetMagic(spell);
            if (magic == null) return;

            int cost;
            switch (spell)
            {
                case Spell.Thrusting:
                    Info.Thrusting = state == SpellToggleState.None ? !Info.Thrusting : use;
                    break;
                case Spell.HalfMoon:
                    Info.HalfMoon = state == SpellToggleState.None ? !Info.HalfMoon : use;
                    break;
                case Spell.CrossHalfMoon:
                    Info.CrossHalfMoon = state == SpellToggleState.None ? !Info.CrossHalfMoon : use;
                    break;
                case Spell.DoubleSlash:
                    Info.DoubleSlash = state == SpellToggleState.None ? !Info.DoubleSlash : use;
                    break;
                case Spell.TwinDrakeBlade:
                    if (TwinDrakeBlade) return;
                    magic = GetMagic(spell);
                    if (magic == null) return;
                    cost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
                    if (cost >= MP) return;

                    TwinDrakeBlade = true;
                    ChangeMP(-cost);

                    Enqueue(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = spell });
                    break;
                case Spell.FlamingSword:
                    if (FlamingSword || Envir.Time < FlamingSwordTime) return;
                    magic = GetMagic(spell);
                    if (magic == null) return;
                    cost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
                    if (cost >= MP) return;

                    FlamingSword = true;
                    FlamingSwordTime = Envir.Time + 10000;
                    Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.FlamingSword, CanUse = true });
                    ChangeMP(-cost);
                    break;
                case Spell.CounterAttack:
                    if (CounterAttack || Envir.Time < CounterAttackTime) return;
                    magic = GetMagic(spell);
                    if (magic == null) return;
                    cost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
                    if (cost >= MP) return;

                    CounterAttack = true;
                    CounterAttackTime = Envir.Time + (Settings.Second * 7);

                    var stats = new Stats
                    {
                        [Stat.MinAC] = 11 + magic.Level * 3,
                        [Stat.MinMAC] = 11 + magic.Level * 3,
                        [Stat.MaxAC] = 11 + magic.Level * 3,
                        [Stat.MaxMAC] = 11 + magic.Level * 3,
                    };

                    AddBuff(BuffType.天务, this, Settings.Second * 7, stats);
                    ChangeMP(-cost);
                    break;
                case Spell.MentalState:
                    Info.MentalState = (byte)((Info.MentalState + 1) % 3);

                    ShowMentalState();
                    break;
            }
        }

        private void ShowMentalState()
        {
            switch (Info.MentalState)
            {
                case 0:
                    ReceiveChat("精神状态: 集中模式", ChatType.Hint);
                    break;
                case 1:
                    ReceiveChat("精神状态: 穿透模式", ChatType.Hint);
                    break;
                case 2:
                    ReceiveChat("精神状态: 组队模式", ChatType.Hint);
                    break;
            }

            AddBuff(BuffType.精神状态, this, 0, new Stats(), false, values: Info.MentalState);
        }

        #region Mounts

        public void RefreshMount(bool refreshStats = true)
        {
            if (RidingMount)
            {
                if (Mount.MountType < 0)
                {
                    RidingMount = false;
                }
                else if (!Mount.CanRide)
                {
                    RidingMount = false;
                    ReceiveChat("乘骑需装配马鞍", ChatType.System);
                }
                else if (!Mount.CanMapRide)
                {
                    RidingMount = false;
                    ReceiveChat("此地图禁止乘骑", ChatType.System);
                }
                else if (!Mount.CanDungeonRide)
                {
                    RidingMount = false;
                    ReceiveChat("此地图乘骑需装配缰绳", ChatType.System);
                }
            }
            else
            {
                RidingMount = false;
            }

            if (refreshStats)
                RefreshStats();

            Broadcast(GetMountInfo());
            Enqueue(GetMountInfo());
        }
        public void IncreaseMountLoyalty(int amount)
        {
            UserItem item = Info.Equipment[(int)EquipmentSlot.坐骑];
            if (item != null && item.CurrentDura < item.MaxDura)
            {
                item.CurrentDura = (ushort)Math.Min(item.MaxDura, item.CurrentDura + amount);
                item.DuraChanged = false;
                Enqueue(new S.ItemRepaired { UniqueID = item.UniqueID, MaxDura = item.MaxDura, CurrentDura = item.CurrentDura });
            }
        }
        public void DecreaseMountLoyalty(int amount)
        {
            // 离线挂机免损耗：坐骑忠诚不掉（坐骑耐久也是装备持久）
            if (AfkFreeEquipDura) return;

            if (Envir.Time > DecreaseLoyaltyTime)
            {
                DecreaseLoyaltyTime = Envir.Time + (Mount.SlowLoyalty ? (LoyaltyDelay * 2) : LoyaltyDelay);
                UserItem item = Info.Equipment[(int)EquipmentSlot.坐骑];
                if (item != null && item.CurrentDura > 0)
                {
                    DamageItem(item, amount);

                    if (item.CurrentDura == 0)
                    {
                        RefreshMount();
                    }
                }
            }
        }

        public void ToggleRide()
        {
            switch (TransformType)
            {
                case 33:
                case 34:
                case 35:
                case 36:
                case 37:
                case 38:
                    ReceiveChat("此类外形不能使用坐骑", ChatType.System);
                    return;
            }

            if (Mount.MountType > -1)
            {
                RidingMount = !RidingMount;
                RefreshMount();
            }
            else
            {
                ReceiveChat("没有坐骑...", ChatType.System);
            }
        }
        #endregion
    }
}
