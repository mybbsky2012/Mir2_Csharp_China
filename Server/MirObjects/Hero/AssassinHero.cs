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

            // 战斗中与空闲时都保持自身增益（进图先加速、开身法）
            {
                UserMagic magic = GetMagic(Spell.Haste);
                if (CanUseMagic(magic) && !HasBuff(BuffType.体迅风))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.LightBody);
                if (CanUseMagic(magic) && !HasBuff(BuffType.风身术))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }
            }
        }
        protected override void ProcessAttack()
        {
            if (Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            // 被围（2 个以上敌人贴身）时优先月华乱舞（自身周围360°群攻）
            if (CanCast && SurroundedCount >= 2)
            {
                UserMagic aoe = GetMagic(Spell.CrescentSlash);
                if (CanUseMagic(aoe))
                {
                    BeginMagic(aoe.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }
            }

            UserMagic magic = GetMagic(Spell.HeavenlySword);
            HasHeavenlySword = CanCast && CanUseMagic(magic);
            if (HasHeavenlySword && TargetDistance == 2)
            {
                BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                return;
            }
        }

        protected override void ProcessTarget()
        {
            if (CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            // 没有月华乱舞还被 3 个以上敌人围死：脱围求生
            if (Target != null && !Target.Dead && CanMove && !HasMagic(Spell.CrescentSlash) && SurroundedCount >= 3)
            {
                MoveAwayFrom(Target);
                return;
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
