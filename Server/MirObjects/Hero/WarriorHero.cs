using Server.MirDatabase;
using Server.MirEnvir;

namespace Server.MirObjects
{
    public class WarriorHero : HeroObject
    {
        public WarriorHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }
        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (Info.Thrusting)
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
            if (!CanCast) return;

            if (Target != null)
            {
                UserMagic magic = GetMagic(Spell.Rage);
                if (CanUseMagic(magic) && !HasBuff(BuffType.剑气爆))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.ProtectionField);
                if (CanUseMagic(magic) && !HasBuff(BuffType.护身气幕))
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

            if (HasMagic(Spell.FlamingSword) && Envir.Time > FlamingSwordTime)
                SpellToggle(Spell.FlamingSword, SpellToggleState.True);

            int surrounded = SurroundedCount;

            // 主动群攻优先：被围（2 个以上敌人贴身）时先放剑刃雪崩（正面三向横扫）
            if (surrounded >= 2 && CanCast)
            {
                UserMagic aoe = GetMagic(Spell.BladeAvalanche);
                if (CanUseMagic(aoe))
                {
                    Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
                    BeginMagic(aoe.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }
            }

            // 没有主动群攻时自动切换被动群攻姿态：2 个以上贴身用圆月（全月），单个贴身用半月，脱围后关闭
            if (HasMagic(Spell.CrossHalfMoon))
                SpellToggle(Spell.CrossHalfMoon, surrounded >= 2 ? SpellToggleState.True : SpellToggleState.False);
            if (HasMagic(Spell.HalfMoon) && !Info.CrossHalfMoon)
                SpellToggle(Spell.HalfMoon, surrounded == 1 ? SpellToggleState.True : SpellToggleState.False);
        }

        private bool HasAnySurroundedAoE => HasMagic(Spell.BladeAvalanche) || HasMagic(Spell.CrossHalfMoon) || HasMagic(Spell.HalfMoon);

        protected override void ProcessTarget()
        {
            if (CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            // 完全没有群攻手段还被 3 个以上敌人围死：先脱围再打
            if (Target != null && !Target.Dead && CanMove && !HasAnySurroundedAoE && SurroundedCount >= 3)
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
                Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
                var behindEnemyLocation = Functions.PointMove(Target.CurrentLocation, Direction, 1);
                var thrustCell = CurrentMap.GetCell(behindEnemyLocation);
                bool ThrustObject = false;

                if (thrustCell.Objects != null && thrustCell.Objects.Count != 0) ThrustObject = true;

                Spell spell = Spell.None;

                if (Info.Thrusting && ((TargetDistance == 2) || (TargetDistance == 1 && ThrustObject == true)))
                {
                    spell = Spell.Thrusting;
                }

                if (spell == Spell.None && Slaying)
                    spell = Spell.Slaying;

                if (spell == Spell.None)
                {
                    if (Info.HalfMoon)
                        spell = Spell.HalfMoon;

                    if (Info.CrossHalfMoon)
                        spell = Spell.CrossHalfMoon;

                    if (TwinDrakeBlade)
                        spell = Spell.TwinDrakeBlade;

                    if (FlamingSword)
                        spell = Spell.FlamingSword;
                }

                Attack(Direction, spell);
            }
        }
    }
}
