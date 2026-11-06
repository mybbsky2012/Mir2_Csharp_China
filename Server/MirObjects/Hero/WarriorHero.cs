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

            // 自身增益：剑气爆 / 护身气幕 / 血龙剑法 / 金刚不坏 —— 缺哪个补哪个，学了高级的就不放低级的
            if (TryCast(HeroSpellKind.Buff)) return;
        }
        protected override void ProcessAttack()
        {
            if (Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);

            if (CanCast)
            {
                // 开关型强化：只开不关（开着的绝不点第二次），顺序即优先级
                if (ToggleIfOff(Spell.FlamingSword)) return;      // 烈火剑法
                if (ToggleIfOff(Spell.TwinDrakeBlade)) return;    // 双龙斩

                int nearby = SurroundedCount;
                if (nearby >= 2)                                   // 被围切圆月，单怪用半月
                {
                    if (ToggleIfOff(Spell.CrossHalfMoon)) return;
                }
                else
                {
                    if (ToggleIfOff(Spell.HalfMoon)) return;
                }

                if (ToggleIfOff(Spell.Thrusting)) return;         // 刺杀剑术（隔位）

                // 被围先群攻：空破闪 / 日闪 / 时空剑，按学习等级自动挑最高的那个
                // rotate：同档（评分差 10% 内）的换着放，不降输出。
                //   ★战士的单体输出走平砍（Attack() 里按开关型 spell 附带烈火/半月/刺杀），
                //     没有 HeroSpellKind.Single 技能可轮换，所以「主力」这里指的是群攻这一档。
                if (nearby >= 2 && TryCast(HeroSpellKind.Aoe, rotate: true)) return;

                // 被围死再控场：狮子吼 / 捕绳剑
                if (nearby >= 3 && TryCast(HeroSpellKind.Control)) return;
            }
        }

        private bool HasAnySurroundedAoE => HasMagic(Spell.BladeAvalanche) || HasMagic(Spell.CrossHalfMoon) || HasMagic(Spell.HalfMoon);

        protected override void ProcessTarget()
        {
            if (CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;
            }

            // 完全没有群攻手段还被 3 个以上敌人围死：先脱围再打；
            // 脱困失败（切不开口子）不站桩，落下去照常单体攻击，切不开到底就瞬移回主人身边
            if (Target != null && !Target.Dead && CanMove && !HasAnySurroundedAoE && SurroundedCount >= 3)
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
