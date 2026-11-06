using System.Drawing;
﻿using Server.MirEnvir;
using S = ServerPackets;


namespace Server.MirObjects
{
    public class SpellObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Spell; }
        }

        public override string Name { get; set; }
        public override int CurrentMapIndex { get; set; }
        public override Point CurrentLocation { get; set; }
        public override MirDirection Direction { get; set; }
        public override ushort Level { get; set; }
        public override bool Blocking
        {
            get
            {
                return false;
            }
        }

        public long TickTime, StartTime;
        // ==== 20261103 韩服AI移植：韩服SpellObject带防御类型字段；默认MAC与本服原有硬编码行为一致 ====
        public DefenceType DefenceType = DefenceType.MAC;
        public MapObject Caster;
        public int Value, TickSpeed, BonusDmg, Healingdmg;
        public Spell Spell;
        public Point CastLocation;
        public bool Show, Decoration;

        //PoisonCloud：施放时用掉的毒粉形态（1=灰色毒粉→绿毒掉血、2=黄色毒粉→红毒掉防）
        public byte PoisonShape;

        //ExplosiveTrap
        public int ExplosiveTrapID;
        public int ExplosiveTrapCount;
        public bool DetonatedTrap;

        public override int Health
        {
            get { throw new NotSupportedException(); }
        }
        public override int MaxHealth
        {
            get { throw new NotSupportedException(); }
        }


        public override void Process()
        {
            if (Decoration) return;

            if (CurrentMap == null)
            {
                Despawn();
                return;
            }

            if (Caster != null && Caster.Node == null) Caster = null;

            if (Envir.Time > ExpireTime || ((Spell == Spell.FireWall || Spell == Spell.Portal || Spell == Spell.ExplosiveTrap || Spell == Spell.Reincarnation || Spell == Spell.HealingCircle && Caster == null || Spell == Spell.HealingcircleRare
                || Spell == Spell.AbsorbFlame || Spell == Spell.AbsorbFlameRare || Spell == Spell.SkyThunderShot || Spell == Spell.SkyThunderShotRare) && Caster == null) || (Spell == Spell.TrapHexagon && Target != null) || (Spell == Spell.Trap && Target != null))
            {
                if (Spell == Spell.TrapHexagon && Target != null || Spell == Spell.Trap && Target != null)
                {
                    MonsterObject ob = (MonsterObject)Target;

                    if (Envir.Time < ExpireTime && ob.ShockTime != 0) return;
                }

                if (Spell == Spell.Reincarnation && Caster != null)
                {
                    ((HumanObject)Caster).ReincarnationReady = true;
                    ((HumanObject)Caster).ReincarnationExpireTime = Envir.Time + 6000;
                }

                if ((Spell == Spell.Blizzard || Spell == Spell.MeteorStrike) && Caster != null)
                {
                    ((HumanObject)Caster).ActiveBlizzard = false;
                }

                //落雷杀-秘笈：结束前在落点补一段「最后一击」（素材 = 原灭杀的 Magic_32bit 1810-1824）。
                //必须在 RemoveObject 之前广播 —— 客户端正是靠这个（唯一 Show=true 的）SpellObject 的
                //ObjectID 找到落点位置，晚一步对象在客户端就已经不存在了。
                if (Spell == Spell.SkyThunderShotRare && Show)
                {
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.SkyThunderShotFinal }, CurrentLocation);
                }

                CurrentMap.RemoveObject(this);
                Despawn();
                return;
            }

            if (Spell == Spell.FireWall)
            {
                if (CurrentMap != Caster?.CurrentMap)
                {
                    CurrentMap.RemoveObject(this);
                    Despawn();
                    return;
                }
            }
            if (Spell == Spell.HealingcircleRare)
            {
                if (CurrentMap != null && (CurrentMap != Caster?.CurrentMap || CurrentMap.Info.Index != CurrentMapIndex))
                {
                    CurrentMap.RemoveObject(this);
                    Despawn();
                    return;
                }
            }
            if (Spell == Spell.Reincarnation && !((HumanObject)Caster).ActiveReincarnation)
            {
                CurrentMap.RemoveObject(this);
                Despawn();
                return;
            }

            if (Spell == Spell.ExplosiveTrap && Caster != null && !Functions.InRange(CurrentLocation, Caster.CurrentLocation, 15))
            {
                CurrentMap.RemoveObject(this);
                Despawn();
                return;
            }
            if (Caster != null && Spell == Spell.HealingCircle && FindObject(Caster.ObjectID, 20) == null)
            {
                CurrentMap.RemoveObject(this);
                Despawn();
                return;
            }
            if (Envir.Time < TickTime) return;
            TickTime = Envir.Time + TickSpeed;

            Cell cell = CurrentMap.GetCell(CurrentLocation);
            if (cell != null && cell.Objects != null)
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    ProcessSpell(cell.Objects[i]);
                }

            //吸魔炎风：把周围的普通怪物卷向旋风中心
            //群攻化后范围里每一格都有一个 SpellObject，只有落点那一份负责推挤，
            //否则每格都按自己为中心推，怪物会被来回拉扯
            if ((Spell == Spell.AbsorbFlame || Spell == Spell.AbsorbFlameRare) && Caster != null && CurrentLocation == CastLocation)
            {
                for (int y = CastLocation.Y - 2; y <= CastLocation.Y + 2; y++)
                {
                    for (int x = CastLocation.X - 2; x <= CastLocation.X + 2; x++)
                    {
                        Point p = new Point(x, y);
                        if (p == CastLocation) continue;
                        if (!CurrentMap.ValidPoint(p)) continue;

                        Cell nearby = CurrentMap.GetCell(p);
                        if (nearby == null || nearby.Objects == null) continue;

                        //先做快照再遍历：Pushed 会把对象移出格子，
                        //Cell.Remove 在格子清空时会把 Objects 置 null，
                        //循环条件再次读取 nearby.Objects.Count 会触发 NullReferenceException
                        MapObject[] snapshot = new MapObject[nearby.Objects.Count];
                        nearby.Objects.CopyTo(snapshot);

                        for (int i = 0; i < snapshot.Length; i++)
                        {
                            MapObject ob = snapshot[i];
                            if (ob == null || ob.Node == null || ob.Dead) continue;
                            if (ob.Race != ObjectType.Monster) continue;

                            MirDirection dir = Functions.DirectionFromPoint(ob.CurrentLocation, CastLocation);
                            ob.Pushed(Caster, dir, 1);
                        }
                    }
                }
            }

            if ((Spell == Spell.MapLava) || (Spell == Spell.MapLightning)) Value = 0;
        }
        public void ProcessSpell(MapObject ob)
        {
            if (Envir.Time < StartTime) return;
            switch (Spell)
            {
                case Spell.FireWall:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;

                        if (!ob.IsAttackTarget(Caster)) return;
                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);
                    }
                    break;
                case Spell.Healing: // SafeZone
                    {
                        if (!(ob.Race == ObjectType.Player || ob.Race == ObjectType.Hero ||
                              (ob.Race == ObjectType.Monster && ob.Master != null && ob.Master.Race == ObjectType.Player))) return;

                        if (ob.Dead || ob.HealAmount != 0 || ob.PercentHealth == 100) return;

                        ob.HealAmount += 25;
                        Broadcast(new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.Healing });
                    }
                    break;
                case Spell.PoisonCloud:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;

                        if (!ob.IsAttackTarget(Caster)) return;
                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);
                        if (!ob.Dead)
                        {
                            //需求：中毒时长由施毒者的道术高低决定。
                            //基础 6 秒，道术（MinSC/MaxSC 的平均值）每 10 点 +1 秒，上限 30 秒。
                            //例：道术 50 → 11 秒，道术 100 → 16 秒，道术 200 → 26 秒。
                            int poisonSC = (Caster.Stats[Stat.MinSC] + Caster.Stats[Stat.MaxSC]) / 2;
                            int poisonDuration = 6 + poisonSC / 10;
                            if (poisonDuration > 30) poisonDuration = 30;

                            //需求：毒雾挂哪种毒由「施放时用掉的毒粉」决定，基础对点伤害 Value 不变：
                            //  · 灰色毒粉(Shape=1) → 绿毒：持续掉血（数值由道术 + BonusDmg 决定，与老版本完全一致）；
                            //  · 黄色毒粉(Shape=2) → 红毒：降低防御（与施毒术一致，红毒不需要 Value）。
                            if (PoisonShape == 2)
                            {
                                ob.ApplyPoison(new Poison
                                {
                                    Duration = poisonDuration,
                                    Owner = Caster,
                                    PType = PoisonType.Red,
                                    TickSpeed = TickSpeed,
                                }, Caster, false, false);
                            }
                            else
                            {
                                ob.ApplyPoison(new Poison
                                {
                                    Duration = poisonDuration,
                                    Owner = Caster,
                                    PType = PoisonType.Green,
                                    TickSpeed = TickSpeed,
                                    Value = poisonSC + BonusDmg
                                }, Caster, false, false);
                            }
                        }
                    }
                    break;
                case Spell.Blizzard:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (Caster != null && ((HumanObject)Caster).ActiveBlizzard == false) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);
                        if (!ob.Dead && Envir.Random.Next(8) == 0)
                            ob.ApplyPoison(new Poison
                            {
                                Duration = 5 + Envir.Random.Next(Caster.Stats[Stat.冰冻伤害]),
                                Owner = Caster,
                                PType = PoisonType.Slow,
                                TickSpeed = 2000,
                            }, Caster);
                    }
                    break;
                //新增：吸魔炎风 / 吸魔炎风-秘籍
                case Spell.AbsorbFlame:
                case Spell.AbsorbFlameRare:
                    {
                        if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (Caster == null) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);
                    }
                    break;
                //新增：落雷杀 / 落雷杀-秘笈
                case Spell.SkyThunderShot:
                case Spell.SkyThunderShotRare:
                    {
                        if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (Caster == null) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);

                        if (!ob.Dead && Envir.Random.Next(6) == 0)
                            ob.ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = 2, TickSpeed = 1000 }, Caster);
                    }
                    break;
                case Spell.MeteorStrike:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (Caster != null && ((HumanObject)Caster).ActiveBlizzard == false) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);
                    }
                    break;
                case Spell.HealingcircleRare:
                    {
                        if (ob == null) return;

                        if (ob.Race == ObjectType.Player || ob.Race == ObjectType.Hero || (ob.Race == ObjectType.Monster && ob.Master != null && ob.Master.Race == ObjectType.Player))
                        {
                            if (ob.IsAttackTarget(Caster)) return;
                            if (ob.Dead || ob.HealAmount != 0 || ob.PercentHealth >= 100) return;

                            ob.HealAmount += 25;
                            Broadcast(new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.Healing });
                        }
                        else if (ob.Race == ObjectType.Monster && ob.IsAttackTarget(Caster) && !ob.Dead)
                        {
                            ob.Attacked(Caster as HumanObject, Value, DefenceType.MAC, false);
                            Broadcast(new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.HealingcircleRare });
                            if (Envir.Random.Next(10) >= 2)
                            {
                                Broadcast(new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.HealingcircleRare1 });
                                ob.ApplyPoison(new Poison { PType = PoisonType.Green, Duration = 2, TickSpeed = 1000 }, this);
                            }
                        }
                    }
                    break;
                case Spell.ExplosiveTrap:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (DetonatedTrap) return;
                        DetonateTrapNow();
                        ob.Attacked(((HumanObject)Caster), Value, DefenceType.MAC, false);
                    }
                    break;
                case Spell.MapLava:
                case Spell.MapLightning:
                    {
                        if (ob is PlayerObject player)
                        {
                            if (player.Account.AdminAccount && player.Observer) return;
                            player.Struck(Value, DefenceType.MAC);
                        }
                    }
                    break;
                case Spell.MapQuake1:
                case Spell.MapQuake2:
                    {
                        if (Value == 0) return;
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.GeneralMeowMeowThunder:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.TreeQueenRoot:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.TreeQueenMassRoots:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.TreeQueenGroundRoots:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        ob.Struck(Value, DefenceType.MAC);

                        if (Envir.Random.Next(3) > 0)
                        {
                            ob.ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = 5, TickSpeed = 1000 }, this);
                        }
                    }
                    break;
                case Spell.StoneGolemQuake:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.EarthGolemPile:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.TucsonGeneralRock:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.Portal:
                    {
                        if (ob.Race != ObjectType.Player) return;
                        if (Caster != ob && (Caster == null || (Caster.GroupMembers == null) || (!Caster.GroupMembers.Contains((HumanObject)ob)))) return;

                        var portal = Envir.Spells.SingleOrDefault(ob => ob != this && ob.Node != null
                            && ob.Spell == Spell.Portal
                            && ob.Caster == Caster);

                        if (portal != null)
                        {
                            Point newExit = Functions.PointMove(portal.CurrentLocation, ob.Direction, 1);

                            if (!portal.CurrentMap.ValidPoint(newExit)) return;

                            ob.Teleport(portal.CurrentMap, newExit, false);
                        }

                        Value -= 1;

                        if (Value < 1)
                        {
                            ExpireTime = 0;
                        }
                    }
                    break;
                case Spell.FlyingStatueIceTornado:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        ob.Struck(Value, DefenceType.MAC);

                        if (Envir.Random.Next(8) == 0)
                        {
                            ob.ApplyPoison(new Poison { PType = PoisonType.Slow, Duration = 5, TickSpeed = 1000 }, this);
                        }
                    }
                    break;
                case Spell.DarkOmaKingNuke:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        ob.Struck(Value, DefenceType.AC);
                        ob.ApplyPoison(new Poison { PType = PoisonType.Dazed, Duration = 5, TickSpeed = 1000 }, this);
                    }
                    break;
                case Spell.HornedSorcererDustTornado:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.HornedCommanderRockFall:
                case Spell.HornedCommanderRockSpike:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.HealingCircle:
                    if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;

                    if (Caster == ob || (Caster.GroupMembers != null && Caster.GroupMembers.Contains(ob)) || (ob.Master != null && Caster.GroupMembers != null && Caster.GroupMembers.Contains(ob.Master)))
                    {
                        if (ob.Dead || ob.HealAmount != 0 || ob.PercentHealth == 100) return;

                        ob.HealAmount += 25;
                        Broadcast(new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = SpellEffect.Healing });
                    }
                    else if (ob.IsAttackTarget(Caster))
                    {
                        if (ob.Dead) return;

                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.MAC);

                        ob.ApplyPoison(new Poison
                        {
                            Owner = Caster,
                            Duration = ob.Race == ObjectType.Player ? 4 : 5 + Envir.Random.Next(5),
                            PType = PoisonType.Slow,
                            TickSpeed = 1000,
                        }, Caster);
                        ob.OperateTime = 0;
                    }
                    break;
                case Spell.YangDragonFlame:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.YangDragonIcyBurst:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.ShardGuardianIceBomb:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;

                        if (Value == 0) return;
                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.GroundFissure:
                    {
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.SkeletonBomb:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.FlameExplosion:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.ButcherFlyAxe:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.RiklebitesRollCall:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.RiklebitesBlast:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.SwordFormation:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.AC);
                    }
                    break;
                case Spell.Mon564NWhirlwind:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon570NRupture:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon570NLightningCloud:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon571NFireBomb:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon572NDarkVortex:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon572NFlame:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon573NBigCobweb:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
                case Spell.Mon609NBomb:
                    {
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) return;
                        if (ob.Dead) return;
                        if (ob == Caster) return;
                        if (!ob.IsAttackTarget(Caster)) return;
                        if (Value == 0) return;

                        ob.Struck(Value, DefenceType.MAC);
                    }
                    break;
            }
        }

        public void DetonateTrapNow()
        {
            DetonatedTrap = true;
            Broadcast(GetInfo());
            ExpireTime = Envir.Time + 1000;
        }

        public override void SetOperateTime()
        {
            long time = Envir.Time + 2000;

            if (TickTime < time && TickTime > Envir.Time)
                time = TickTime;

            if (OwnerTime < time && OwnerTime > Envir.Time)
                time = OwnerTime;

            if (ExpireTime < time && ExpireTime > Envir.Time)
                time = ExpireTime;

            if (PKPointTime < time && PKPointTime > Envir.Time)
                time = PKPointTime;

            if (LastHitTime < time && LastHitTime > Envir.Time)
                time = LastHitTime;

            if (EXPOwnerTime < time && EXPOwnerTime > Envir.Time)
                time = EXPOwnerTime;

            if (BrownTime < time && BrownTime > Envir.Time)
                time = BrownTime;

            for (int i = 0; i < ActionList.Count; i++)
            {
                if (ActionList[i].Time >= time && ActionList[i].Time > Envir.Time) continue;
                time = ActionList[i].Time;
            }

            for (int i = 0; i < PoisonList.Count; i++)
            {
                if (PoisonList[i].TickTime >= time && PoisonList[i].TickTime > Envir.Time) continue;
                time = PoisonList[i].TickTime;
            }

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].NextTime >= time && Buffs[i].NextTime > Envir.Time) continue;
                time = Buffs[i].NextTime;
            }

            if (OperateTime <= Envir.Time || time < OperateTime)
                OperateTime = time;
        }

        public override void Process(DelayedAction action)
        {
            throw new NotSupportedException();
        }
        public override bool IsAttackTarget(HumanObject attacker)
        {
            throw new NotSupportedException();
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {
            throw new NotSupportedException();
        }
        public override int Attacked(HumanObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            throw new NotSupportedException();
        }
        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            throw new NotSupportedException();
        }

        public override int Struck(int damage, DefenceType type = DefenceType.ACAgility)
        {
            throw new NotSupportedException();
        }
        public override bool IsFriendlyTarget(HumanObject ally)
        {
            throw new NotSupportedException();
        }
        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            throw new NotSupportedException();
        }
        public override void ReceiveChat(string text, ChatType type)
        {
            throw new NotSupportedException();
        }

        public override Packet GetInfo()
        {
            switch (Spell)
            {
                case Spell.Healing:
                    return null;
                //毒雾：一片云是由多朵云拼出来的（网格点上各一朵），所以每朵必须画在自己的格子上，
                //不能像其它范围技能那样统一用 CastLocation，否则多朵云会全部叠在落点。
                case Spell.PoisonCloud:
                    if (!Show)
                        return null;

                    return new S.ObjectSpell
                    {
                        ObjectID = ObjectID,
                        Location = CurrentLocation,
                        Spell = Spell,
                        Direction = Direction
                    };
                //新增：落雷杀 / 落雷杀-秘笈 —— 范围内只有落点那一份下发特效（Show 过滤）
                case Spell.SkyThunderShot:
                case Spell.SkyThunderShotRare:
                //新增：吸魔炎风 / 吸魔炎风-秘籍 —— 群攻化后同样只显示落点那一份
                case Spell.AbsorbFlame:
                case Spell.AbsorbFlameRare:
                case Spell.Blizzard:
                case Spell.MeteorStrike:
                case Spell.StoneGolemQuake:
                case Spell.EarthGolemPile:
                case Spell.TreeQueenMassRoots:
                case Spell.TreeQueenGroundRoots:
                case Spell.FlyingStatueIceTornado:
                case Spell.DarkOmaKingNuke:
                case Spell.HornedSorcererDustTornado:
                case Spell.HornedCommanderRockFall:
                case Spell.HornedCommanderRockSpike:
                case Spell.HealingCircle:
                case Spell.HealingcircleRare:
                case Spell.YangDragonFlame:
                case Spell.YangDragonIcyBurst:
                case Spell.ShardGuardianIceBomb:
                case Spell.GroundFissure:
                case Spell.SkeletonBomb:
                case Spell.FlameExplosion:
                case Spell.ButcherFlyAxe:
                case Spell.RiklebitesBlast:
                case Spell.SwordFormation:
                case Spell.Mon564NWhirlwind:
                case Spell.Mon570NRupture:
                case Spell.Mon570NLightningCloud:
                case Spell.Mon571NFireBomb:
                case Spell.Mon572NFlame:
                case Spell.Mon572NDarkVortex:
                case Spell.Mon573NBigCobweb:
                case Spell.Mon609NBomb:
                    if (!Show)
                        return null;

                    return new S.ObjectSpell
                    {
                        ObjectID = ObjectID,
                        Location = CastLocation,
                        Spell = Spell,
                        Direction = Direction
                    };
                case Spell.ExplosiveTrap:
                    return new S.ObjectSpell
                    {
                        ObjectID = ObjectID,
                        Location = CurrentLocation,
                        Spell = Spell,
                        Direction = Direction,
                        Param = DetonatedTrap
                    };
                default:
                    return new S.ObjectSpell
                    {
                        ObjectID = ObjectID,
                        Location = CurrentLocation,
                        Spell = Spell,
                        Direction = Direction
                    };
            }

        }

        public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false, bool ignoreDefence = true)
        {
            throw new NotSupportedException();
        }
        public override void Die()
        {
            throw new NotSupportedException();
        }
        public override int Pushed(MapObject pusher, MirDirection dir, int distance)
        {
            throw new NotSupportedException();
        }
        public override void SendHealth(HumanObject player)
        {
            throw new NotSupportedException();
        }

        public override void Spawned()
        {
            base.Spawned();

            Envir.Spells.Add(this);
        }

        public override void Despawn()
        {
            base.Despawn();

            Envir.Spells.Remove(this);

            if (Spell == Spell.Reincarnation && Caster != null && Caster.Node != null)
            {
                ((HumanObject)Caster).ActiveReincarnation = false;
                ((HumanObject)Caster).Enqueue(new S.CancelReincarnation { });
            }

            if (Spell == Spell.ExplosiveTrap && Caster != null)
            {
                var linkedTraps = CurrentMap.GetSpellObjects(Spell.ExplosiveTrap, Owner).Where(x => x.ExplosiveTrapID == ExplosiveTrapID);

                foreach (var trap in linkedTraps)
                {
                    trap.DetonateTrapNow();
                }
            }

            if (Spell == Spell.Portal && Caster != null)
            {
                var portal = Envir.Spells.SingleOrDefault(ob => ob.Node != null && ob != this
                    && ob.Spell == Spell.Portal
                    && ob.Caster == Caster);

                if (portal != null)
                {
                    portal.ExpireTime = 0;
                    portal.Process();
                }
            }
        }

        public override void BroadcastInfo()
        {
            if ((Spell != Spell.ExplosiveTrap) || (Caster == null))
                base.BroadcastInfo();

            Packet p;
            if (CurrentMap == null) return;

            for (int i = CurrentMap.RealPlayers.Count - 1; i >= 0; i--)   // 20261106 只发真人（假人收包即弃）
            {
                PlayerObject player = CurrentMap.RealPlayers[i];
                if (Functions.InRange(CurrentLocation, player.CurrentLocation, Globals.DataRange))
                {
                    if ((Caster == null) || (player == null)) continue;

                    if ((player == Caster) || player.IsFriendlyTarget(Caster))
                    {
                        p = GetInfo();

                        if (p != null)
                            player.Enqueue(p);
                    }
                }
            }
        }
    }
}
