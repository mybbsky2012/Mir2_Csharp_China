using Server.MirDatabase;
using Server.MirNetwork;

namespace Server.MirObjects
{
    public class AssassinHero : HeroObject
    {
        private bool HasHeavenlySword;
        public AssassinHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }
        protected override void Load(CharacterInfo info, MirConnection connection)
        {
            base.Load(info, connection);

            if (HasMagic(Spell.DoubleSlash))
                Info.DoubleSlash = true;
        }
        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (HasHeavenlySword)
            {
                int x = Math.Abs(Target.CurrentLocation.X - CurrentLocation.X);
                int y = Math.Abs(Target.CurrentLocation.Y - CurrentLocation.Y);

                if (x > 2 || y > 2) return false;

                return (x <= 1 && y <= 1) || (x == y || x % 2 == y % 2);
            }

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }
        protected override void ProcessFriend()
        {
            if (!CanCast || InSafeZone) return;

            // 自身增益：体迅风 / 风身术 / 烈火身 / 月影术 —— 缺哪个补哪个
            if (TryCast(HeroSpellKind.Buff)) return;
        }
        protected override void ProcessAttack()
        {
            if (Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            // 迁移剑是「2 格突刺」：决定 InAttackRange 是否允许 2 格攻击
            HasHeavenlySword = CanCast && CanUseMagic(GetMagic(Spell.HeavenlySword));

            if (!CanCast) return;

            // 开关型：风剑术只开不关（开着的绝不再点第二次）
            if (ToggleIfOff(Spell.DoubleSlash)) return;

            // 被围（2 个以上敌人贴身）→ 月华乱舞/秘籍（360° 群攻），按等级自动挑最高的
            if (SurroundedCount >= 2 && TryCast(HeroSpellKind.Aoe)) return;

            // 单体主力：影华连击 / 拔刀术-秘籍 / 猛毒剑气 / 血风击 / 烈风击 / 迁移剑 … 自动挑最高级
            if (TryCast(HeroSpellKind.Single)) return;
        }

        protected override void ProcessTarget()
        {
            if (CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            // 没有月华乱舞还被 3 个以上敌人围死：脱围求生；
            // 切不开口子不站桩，照常单体反击，切不开到底就瞬移回主人身边
            if (Target != null && !Target.Dead && CanMove && !HasMagic(Spell.CrescentSlash) && SurroundedCount >= 3)
            {
                if (TryBreakout(Target)) return;
            }

            base.ProcessTarget();
        }
        protected override void Attack()
        {
            if (Target != null && Owner.PMode == PetMode.FocusMasterTarget && !Target.IsAttackTarget(Owner))
            {
                Target = null;
                return;
            }

            if (Target != null && CanAttack)
            {
                Spell spell = Spell.None;

                if (Info.DoubleSlash)
                    spell = Spell.DoubleSlash;

                Attack(Direction, spell);
            }
        }
    }
}
