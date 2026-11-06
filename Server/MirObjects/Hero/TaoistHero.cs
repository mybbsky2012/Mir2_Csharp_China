using Server.MirDatabase;

namespace Server.MirObjects
{
    public class TaoistHero : HeroObject
    {
        public TaoistHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }
        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (HasRangedSpell && CanCast)
                return TargetDistance <= ViewRange;

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }
        protected override void ProcessFriend()
        {
            if (!CanCast) return;
            UserMagic magic;
            List<MapObject> TargetList = new List<MapObject>()
            {
                this,
                Owner
            };

            // ① 净化：自己或主人中了减益/毒
            for (int i = 0; i < TargetList.Count; i++)
            {
                MapObject target = TargetList[i];
                if (target == null || target.Dead) continue;

                if (target.Buffs.Any(b => b.Properties.HasFlag(BuffProperty.Debuff)) || target.PoisonList.Count > 0)
                {
                    MirDirection direction = Functions.DirectionFromPoint(CurrentLocation, target.CurrentLocation);
                    magic = GetMagic(Spell.Purification);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, direction, target.ObjectID, target.CurrentLocation);
                        return;
                    }
                }
            }

            // ② 治疗：谁血少先治谁（自己 → 主人）
            if (PercentHealth < 90 && TryCast(HeroSpellKind.Heal)) return;
            if (Owner != null && !Owner.Dead && Owner.PercentHealth < 90 && TryCast(HeroSpellKind.Heal, Owner)) return;

            // ③ 增益：先给主人补，再给自己补。幽灵盾/神圣战甲术/无极真气/先天气功/万效符
            //    自动挑「还没上的、等级最高的」那本，高级 buff 会顶掉低级的
            if (Owner != null && !Owner.Dead && TryCast(HeroSpellKind.Buff, Owner)) return;
            if (TryCast(HeroSpellKind.Buff)) return;
        }

        protected override void ProcessAttack()
        {           
            if (Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
            if (!CanCast) return;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            UserMagic magic;
            UserItem amuletItem = GetAmulet(1);
            UserItem poisonItem = GetPoison(1);

            // 被围（2 个以上敌人贴身）时优先毒云群攻（需要携带毒粉）
            if (SurroundedCount >= 2 && poisonItem != null)
            {
                magic = GetMagic(Spell.PoisonCloud);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }
            }

            if (InAttackRange())
            {
                // 上毒（有对应毒粉、目标还没中这种毒）
                magic = GetMagic(Spell.Poisoning);
                if (CanUseMagic(magic) && poisonItem != null)
                {
                    if ((poisonItem.Info.Shape == 1 && !Target.PoisonList.Any(p => p.PType == PoisonType.Green)) || (poisonItem.Info.Shape == 2 && !Target.PoisonList.Any(p => p.PType == PoisonType.Red)))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                // 心灵启示：让目标一段时间内无法回血
                if (Target.AutoRev == false && Envir.Time > Target.RevTime && amuletItem != null)
                {
                    magic = GetMagic(Spell.Revelation);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                // 单体主力：诅咒术 / 烦脑 / 灵魂火符自动挑最高级的那本；
                // 灵魂火符在「万效符」buff 期间由服务端自动升格成三连符，这里不用特殊处理
                // rotate：同档（评分差 10% 内）的换着放（灵魂火符 ⇄ 火符-秘籍 之类），不降输出
                if (amuletItem != null && TryCast(HeroSpellKind.Single, rotate: true)) return;
            }
        }

        protected override void ProcessTarget()
        {
            if (Target != null && Owner.PMode == PetMode.FocusMasterTarget && !Target.IsAttackTarget(Owner))
            {
                Target = null;
                return;
            }

            if (CanMove && ((!CanCast || NextMagicSpell == Spell.None) && Owner.Info.HeroBehaviour == HeroBehaviour.反击))
            {
                MoveTo(Owner.Back);
                return;
            }

            if (CanCast && NextMagicSpell != Spell.None)
            {
                if (Target != Owner && !CanAttack) return;
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            if (Target == null || !CanAttack) return;

            // 风筝走位：目标贴身时后撤拉开距离，保持远程输出位置；
            // 被围死（≥2 个贴身）切不开口子时不站桩，照常单体反击，切不开到底就瞬移回主人身边
            if (CanMove && HasRangedSpell && TargetDistance == 1)
            {
                if (TryBreakout(Target, 2)) return;
            }

            // 被围且没有毒云可用（没学技能或没毒粉）：脱围后撤；切不开就反击/瞬移
            if (CanMove && SurroundedCount >= 2 && (GetMagic(Spell.PoisonCloud) == null || GetPoison(1) == null))
            {
                if (TryBreakout(Target, 2)) return;
            }

            if (CanAttack && (!HasRangedSpell && InAttackRange() || NextMagicSpell == Spell.None && Owner.Info.HeroBehaviour == HeroBehaviour.攻击 && TargetDistance == 1))
            {
                Attack();

                if (Target != null && Target.Dead)
                {
                    FindTarget();
                }

                return;
            }

            if (CanMove && (!HasRangedSpell || NextMagicSpell == Spell.None && Owner.Info.HeroBehaviour == HeroBehaviour.攻击 && TargetDistance > 1))
                MoveTo(Target.CurrentLocation);
        }

        private bool HasRangedSpell => Info.Magics.Select(x => x.Spell).Intersect(Globals.RangedSpells).Any();
    }
}
