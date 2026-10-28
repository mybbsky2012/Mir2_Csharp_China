using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 分身术的分身（怪物 AI 985）。
    ///
    /// 原版行为：只会每 1.2 秒朝目标丢一发雷电术，学了再多技能也不会用。
    ///
    /// 现在的行为（和英雄同一套思路）：
    ///   ① 技能来源 = 主人真正学会的技能（master.Info.Magics），不是写死的表；
    ///   ② 分类与强弱排序复用 <see cref="HeroSpellBook"/>：先补自身防护/增益，贴身先推开，
    ///      被围（≥2 个贴身）先群攻，剩下按“学习等级 + 威力”挑最强单体；
    ///   ③ 每个技能各自走 MagicInfo 的冷却（强击/雷仙风这类长冷却不会被无脑刷）；
    ///   ④ 施法按玩家标准扣主人的法力（Setup.ini [Game] CloneSkillMpCost 可调百分比）；
    ///   ⑤ 一个都放不出来时退回原来的雷电术（不扣蓝），保证不会变成哑巴。
    /// </summary>
    public class HumanWizard : MonsterObject
    {
        public long FearTime, DecreaseMPTime;
        public byte AttackRange = 6;
        public bool Summoned;

        /// <summary>每个技能自己的冷却结束时间。</summary>
        private readonly Dictionary<Spell, long> _cooldowns = new Dictionary<Spell, long>();

        /// <summary>上一发放的输出技能：用来避免连着两次刷同一个技能。</summary>
        private Spell _lastSpell = Spell.None;

        protected internal HumanWizard(MonsterInfo info)
            : base(info)
        {
            Direction = MirDirection.Down;
            Summoned = true;
        }

        protected override bool InAttackRange()
        {
            return CurrentMap == Target.CurrentMap && Functions.InRange(CurrentLocation, Target.CurrentLocation, AttackRange);
        }

        /// <summary>兜底攻击：原版雷电术（不扣主人法力）。</summary>
        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Broadcast(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = Spell.ThunderBolt, TargetID = Target.ObjectID, Target = Target.CurrentLocation, Cast = true, Level = 3 });

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;

            int damage = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);
            if (damage == 0) return;

            DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 500, Target, damage, DefenceType.MAC);
            ActionList.Add(action);
        }

        protected override void ProcessAI()
        {
            base.ProcessAI();

            if (Master != null && Master is PlayerObject && Envir.Time > DecreaseMPTime)
            {
                DecreaseMPTime = Envir.Time + 1000;
                if (!Master.Dead) ((PlayerObject)Master).ChangeMP(-10);

                if (((PlayerObject)Master).MP <= 0) Die();
            }
        }

        protected override void ProcessTarget()
        {
            if (Target == null || !CanAttack) return;

            PlayerObject master = Master as PlayerObject;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            int dist = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);

            // 智能放技能：放出去就不再走位
            if (master != null && Settings.CloneSmartSkill && TryCast(master, dist)) return;

            if (Master != null)
                MoveTo(Master.CurrentLocation);

            if (InAttackRange() && (Master != null || Envir.Time < FearTime))
            {
                Attack();
                return;
            }

            FearTime = Envir.Time + 5000;

            if (Envir.Time < ShockTime)
            {
                Target = null;
                return;
            }

            if (dist < AttackRange)
            {
                MirDirection dir = Functions.DirectionFromPoint(Target.CurrentLocation, CurrentLocation);

                if (Walk(dir)) return;

                switch (Envir.Random.Next(2)) //No favour
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
        }

        #region 技能选择

        private bool TryCast(PlayerObject master, int dist)
        {
            if (master == null || master.Dead) return false;

            // 1) 自身防护：魔法盾（没盾先上盾，最便宜也最救命）
            if (TryCastBuff(master, Spell.MagicShield)) return true;

            // 2) 自身增益：深延术（加魔法力，很贵；蓝到八成以上才开，别把主人的蓝吸干）
            if (master.MP * 5 >= Math.Max(1, master.Stats[Stat.MP]) * 4 && TryCastBuff(master, Spell.MagicBooster)) return true;

            int surround = CountNearbyEnemies(1);

            // 3) 控制：被贴身先推开（抗拒火环）
            if (surround >= 1 && dist <= 1 &&
                TryCastKind(master, dist, surround, HeroSpellKind.Control)) return true;

            // 4) 群攻：被两个以上贴身才值得放
            if (surround >= 2 && TryCastKind(master, dist, surround, HeroSpellKind.Aoe)) return true;

            // 5) 单体主力
            return TryCastKind(master, dist, surround, HeroSpellKind.Single);
        }

        private bool TryCastBuff(PlayerObject master, Spell spell)
        {
            UserMagic magic = master.GetMagic(spell);
            if (magic == null || magic.Info == null) return false;
            if (CloneSkillBook.Get(spell) == null) return false;

            BuffType buff = spell == Spell.MagicShield ? BuffType.魔法盾 : BuffType.深延术;
            if (HasBuff(buff)) return false;
            if (!CanApplyBuff(buff)) return false;   // 服务端 buff 配置里没有这条时不要硬加（GetBuffInfo 会抛异常）
            if (IsCoolingDown(spell)) return false;
            if (!CanPay(master, magic)) return false;

            BroadcastMagic(magic, ObjectID, CurrentLocation);

            if (spell == Spell.MagicShield)
            {
                int duration = magic.GetPower(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]) + 15);
                AddBuff(BuffType.魔法盾, this, Settings.Second * duration,
                    new Stats { [Stat.伤害降低数率] = (magic.Level + 2) * 10 });
            }
            else
            {
                int bonus = 6 + magic.Level * 6;
                AddBuff(BuffType.深延术, this, Settings.Second * 60,
                    new Stats { [Stat.MinMC] = bonus, [Stat.MaxMC] = bonus }, true);
            }

            FinishCast(master, magic);
            return true;
        }

        private bool TryCastKind(PlayerObject master, int dist, int surround, HeroSpellKind kind)
        {
            List<UserMagic> candidates = CloneSkillBook.Rank(master.Info.Magics, kind, surround, dist,
                HasBuff, m => CanPay(master, m), IsCoolingDown);

            // 地狱雷光对活人只有 1/10 伤害，目标不是不死系就别浪费这一拍
            var usable = new List<UserMagic>();
            foreach (UserMagic magic in candidates)
            {
                CloneSkillInfo info = CloneSkillBook.Get(magic.Spell);
                if (info == null) continue;
                if (info.UndeadOnly && (Target == null || !Target.Undead)) continue;
                usable.Add(magic);
            }

            // 同档技能轮换，别永远同一个技能刷屏
            UserMagic pick = CloneSkillBook.PickVaried(usable, _lastSpell, Envir.Random.Next(1000));
            if (pick == null) return false;

            CloneSkillInfo picked = CloneSkillBook.Get(pick.Spell);
            if (picked == null) return false;
            if (!Execute(master, pick, picked)) return false;

            _lastSpell = pick.Spell;
            return true;
        }

        #endregion

        #region 施法执行

        private bool Execute(PlayerObject master, UserMagic magic, CloneSkillInfo info)
        {
            Point targetLocation = Target != null ? Target.CurrentLocation : CurrentLocation;
            uint targetID = Target != null ? Target.ObjectID : 0;

            BroadcastMagic(magic, targetID, targetLocation);

            int damage = magic.GetDamage(GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]));

            switch (info.Shape)
            {
                case CloneCastShape.Target:
                    if (Target == null) return false;
                    AddDelayedDamage(Target, damage, 400);
                    break;

                case CloneCastShape.Burst:
                    {
                        List<MapObject> targets = CollectInBurst(targetLocation, info.Radius);
                        foreach (MapObject ob in targets)
                        {
                            int value = magic.Spell == Spell.ThunderStorm && !ob.Undead ? damage / 10 : damage;
                            AddDelayedDamage(ob, value, 400);
                        }
                    }
                    break;

                case CloneCastShape.Line:
                    {
                        List<MapObject> targets = CollectInLine(Direction, info.Radius);
                        foreach (MapObject ob in targets) AddDelayedDamage(ob, damage, 300);
                    }
                    break;

                case CloneCastShape.FrontBurst:
                    {
                        List<MapObject> targets = CollectInFront(Direction, info.Radius);
                        foreach (MapObject ob in targets) AddDelayedDamage(ob, damage, 300);
                    }
                    break;

                case CloneCastShape.PushRing:
                    PushNearbyEnemies(master, magic);
                    break;

                default:
                    return false;
            }

            FinishCast(master, magic);
            return true;
        }

        private void FinishCast(PlayerObject master, UserMagic magic)
        {
            int cost = SkillCost(magic);
            if (cost > 0) master.ChangeMP(-cost);

            _cooldowns[magic.Spell] = Envir.Time + Math.Max(500, magic.GetDelay());

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;
        }

        private void BroadcastMagic(UserMagic magic, uint targetID, Point targetLocation)
        {
            Broadcast(new S.ObjectMagic
            {
                ObjectID = ObjectID,
                Direction = Direction,
                Location = CurrentLocation,
                Spell = magic.Spell,
                TargetID = targetID,
                Target = targetLocation,
                Cast = true,
                Level = (byte)Math.Min((int)3, (int)magic.Level),
            });
        }

        private void AddDelayedDamage(MapObject target, int damage, int delay)
        {
            if (target == null || damage <= 0) return;

            ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + delay, target, damage, DefenceType.MAC));
        }

        #endregion

        #region 范围收集 / 抗拒火环

        private List<MapObject> CollectInBurst(Point center, int radius)
        {
            var list = new List<MapObject>();

            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = center.X - radius; x <= center.X + radius; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;

                    Cell cell = CurrentMap.GetCell(x, y);
                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (!IsDamageable(ob)) continue;
                        list.Add(ob);
                    }
                }
            }

            return list;
        }

        private List<MapObject> CollectInLine(MirDirection dir, int length)
        {
            var list = new List<MapObject>();

            for (int step = 1; step <= length; step++)
            {
                Point location = Functions.PointMove(CurrentLocation, dir, step);
                if (!CurrentMap.ValidPoint(location)) break;

                Cell cell = CurrentMap.GetCell(location);
                if (cell.Objects == null) continue;

                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (!IsDamageable(ob)) continue;
                    list.Add(ob);
                    break; // 一格只打一个，和原版技能一致
                }
            }

            return list;
        }

        private List<MapObject> CollectInFront(MirDirection dir, int depth)
        {
            var list = new List<MapObject>();
            var seen = new HashSet<MapObject>();

            MirDirection[] spread = { Functions.PreviousDir(dir), dir, Functions.NextDir(dir) };

            foreach (MirDirection side in spread)
            {
                Point start = Functions.PointMove(CurrentLocation, side, 1);

                for (int step = 0; step < depth; step++)
                {
                    Point hit = Functions.PointMove(start, dir, step);
                    if (!CurrentMap.ValidPoint(hit)) continue;

                    Cell cell = CurrentMap.GetCell(hit);
                    if (cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (!IsDamageable(ob)) continue;
                        if (!seen.Add(ob)) continue;
                        list.Add(ob);
                    }
                }
            }

            return list;
        }

        private void PushNearbyEnemies(PlayerObject master, UserMagic magic)
        {
            int level = master != null ? master.Level : Level;

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

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (!IsDamageable(ob)) continue;
                            if (ob.Level >= level) continue;

                            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + level - ob.Level) continue;

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
                                {
                                    ob.InSafeZone = false;
                                }

                                AddDelayedDamage(ob, magic.GetDamage(0), 200);
                            }
                        }
                    }
                }
            }
        }

        private bool IsDamageable(MapObject ob)
        {
            if (ob == null || ob == this) return false;
            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player && ob.Race != ObjectType.Hero) return false;
            if (ob.Dead) return false;

            return ob.IsAttackTarget(this);
        }

        #endregion

        #region 法力 / 冷却

        private int CountNearbyEnemies(int range)
        {
            return FindAllTargets(range, CurrentLocation).Count;
        }

        /// <summary>服务端 BuffSystem 里是否登记了这个 buff（没登记时 AddBuff 会抛异常）。</summary>
        private bool CanApplyBuff(BuffType type)
        {
            var list = Envir.BuffInfoList;
            if (list == null) return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type == type) return true;
            }

            return false;
        }

        private bool IsCoolingDown(Spell spell)
        {
            long until;
            return _cooldowns.TryGetValue(spell, out until) && Envir.Time < until;
        }

        private bool CanPay(PlayerObject master, UserMagic magic)
        {
            if (master == null || magic == null || magic.Info == null) return false;
            return master.MP >= SkillCost(magic);
        }

        /// <summary>分身施法的法力消耗：按玩家标准算，再乘 Setup.ini 的百分比。</summary>
        private int SkillCost(UserMagic magic)
        {
            int baseCost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
            if (baseCost <= 0) return 0;

            int percent = Settings.CloneSkillMpCost;
            if (percent <= 0) return 0;
            if (percent > 1000) percent = 1000;

            return (int)Math.Ceiling(baseCost * percent / 100.0);
        }

        #endregion

        public override void Spawned()
        {
            base.Spawned();
            Summoned = false;
        }

        public override void ChangeHP(int amount)
        {
            if (Master != null && Master is PlayerObject)
            {
                ((PlayerObject)Master).ChangeMP(amount);
                return;
            }
            base.ChangeHP(amount);
        }

        public override void Die()
        {
            if (Dead) return;

            HP = 0;
            Dead = true;

            //DeadTime = Envir.Time + DeadDelay;
            DeadTime = 0;

            Broadcast(new S.ObjectDied { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = (byte)(Master != null ? 1 : 0) });

            if (EXPOwner != null && EXPOwner.Node != null && Master == null && EXPOwner.Race == ObjectType.Player) EXPOwner.WinExp(Experience);

            if (Respawn != null)
                Respawn.Count--;

            if (Master == null)
                Drop();

            Master = null;

            PoisonList.Clear();
            _cooldowns.Clear();
            _lastSpell = Spell.None;
            Envir.MonsterCount--;
            if (CurrentMap != null)
            CurrentMap.MonsterCount--;
        }

        public override Packet GetInfo()
        {
            PlayerObject master = null;
            short weapon = -1;
            short armour = 0;
            byte wing = 0;
            short weaponeffects = 0;
            
            if (Master != null && Master is PlayerObject) 
                master = (PlayerObject)Master;

            if (master != null)
            {
                weapon = master.Looks_Weapon;
                armour = master.Looks_Armour;
                wing = master.Looks_Wings;
                weaponeffects = master.Looks_WeaponEffect;
            }

            return new S.ObjectPlayer
            {
                ObjectID = ObjectID,
                Name = master != null ? master.Name : Name,
                NameColour = NameColour,
                Class = master != null ? master.Class : MirClass.法师,
                Gender =  master != null ? master.Gender : MirGender.男性,
                Location = CurrentLocation,
                Direction = Direction,
                Hair = master != null ? master.Hair : (byte)0,
                Weapon = weapon,
                Armour = armour,
                Light = master != null ? master.Light : Light,
                Poison = CurrentPoison,
                Dead = Dead,
                Hidden = Hidden,
                Effect = SpellEffect.None,
                WingEffect = wing,
                WeaponEffect = weaponeffects,
                Extra = Summoned,
                TransformType = -1
            };
        }
    }
}
