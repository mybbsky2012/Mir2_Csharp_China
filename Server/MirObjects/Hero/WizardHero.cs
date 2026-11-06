using Server.MirDatabase;

namespace Server.MirObjects
{
    public class WizardHero : HeroObject
    {
        public WizardHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }
        public int NearbyCount;
        public int TargetSurroundedCount;
        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (HasRangedSpell && CanCast)
                return TargetDistance <= ViewRange;

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }
        protected override void ProcessFriend()
        {
            if (!CanCast || InSafeZone) return;

            // 自保增益：魔法盾 / 深延术 / 天上秘术 —— 缺哪个补哪个，长 buff 不重复叠
            if (TryCast(HeroSpellKind.Buff)) return;
        }

        protected override void ProcessAttack()
        {
            if (!CanCast || Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
            NearbyCount = FindAllTargets(2, CurrentLocation).Count;
            TargetSurroundedCount = FindAllTargets(1, Target.CurrentLocation).Count;
            if (!HasRangedSpell) return;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            UserMagic magic;

            if (InAttackRange())
            {
                // ① 贴身推开（只对等级不高于自己的目标，否则推不动白耗蓝）
                if (TargetDistance == 1 && Target.Level < Level)
                {
                    magic = GetMagic(Spell.Repulsion);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                // ② 圣言术：只对不死系（对普通怪放没有任何效果）
                if (Target.Undead && Target.Level < Level)
                {
                    magic = GetMagic(Spell.TurnUndead);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                // ③ 被围 / 目标身边扎堆 → 群攻。按学习等级自动挑最高级的那本：
                //    火龙气焰 > 天霜冰环 > 火流星 > 冰咆哮 > 爆裂火焰 > 火墙 …
                if (NearbyCount > 1 || TargetSurroundedCount > 1)
                {
                    if (TryCast(HeroSpellKind.Aoe)) return;
                }

                // ④ 单体主力：按「学习等级 + 威力」自动挑最高的
                //    天上落焰 / 大火球-秘籍 / 强击-秘籍 / 灭天火 / 嗜血术 … 学了新的自动顶上
                //    rotate：同档（评分差 10% 内）的换着放（火球 ⇄ 雷电 之类），不降输出
                if (TryCast(HeroSpellKind.Single, rotate: true)) return;
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

            if (Target == null || !CanAttack) return;

            // 先施放已排队的法术，再做走位决策（否则队列会被走位分支卡死，法术永远放不出来）
            if (CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            // 风筝走位：目标贴身时立刻后撤拉开距离，而不是站着近战互殴
            if (CanMove && HasRangedSpell && TargetDistance == 1)
            {
                MoveAwayFrom(Target);
                return;
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
