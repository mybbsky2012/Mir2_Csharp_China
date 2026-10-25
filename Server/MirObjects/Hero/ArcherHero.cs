using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;

namespace Server.MirObjects
{
    public class ArcherHero : HeroObject
    {
        private static readonly Random Random = new Random();
        private static int GetRandomInt(int minValue, int maxValue)
        {
            return Random.Next(minValue, maxValue);
        }

        public ArcherHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }

        private bool HasRangedSpell => Info.Magics.Select(x => x.Spell).Intersect(Globals.RangedSpells).Any();

        public bool HasWeapon => Info.Equipment[(int)EquipmentSlot.武器] != null && Info.Equipment[(int)EquipmentSlot.武器].CurrentDura > 0;

        public bool HasClassWeapon
        {
            get
            {
                var classweapon = Info.Equipment[(int)EquipmentSlot.武器];
                return classweapon != null && classweapon.Info.RequiredClass == RequiredClass.弓箭 && classweapon.CurrentDura > 0;
            }
        }

        private static Point GetRandomPointAround(int distance, Point center)
        {
            int randomDistance = GetRandomInt(1, distance);
            double randomAngle = GetRandomInt(0, 360) * (Math.PI / 180);
            int targetX = (int)(center.X + (1 + randomDistance) * Math.Cos(randomAngle));
            int targetY = (int)(center.Y + (1 + randomDistance) * Math.Sin(randomAngle));
            return new Point(targetX, targetY);
        }

        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (HasClassWeapon && HasRangedSpell)
                return TargetDistance <= ViewRange;

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }

        protected override void ProcessFriend()
        {
            if (InSafeZone) return;

            if (!HasBuff(BuffType.气流术))
            {
                UserMagic magic = GetMagic(Spell.Concentration);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }
            }
        }

        protected override void ProcessAttack()
        {
            if (Target == null || Target.Dead)
            {
                FindTarget();
                if (Target == null || Target.Dead) return;
            }
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);

            if (!CanCast) return;
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            UserMagic magic;

            // 按目标状态选箭，不再随机：
            // 0. 被围（2 个以上敌人贴身）优先火雨箭群攻；1. 元素珠不足先攒珠；
            // 2. 血厚的目标没上毒优先毒箭；3. 强敌（不低于自身等级）用削弱射击；4. 普通怪直射。
            if (SurroundedCount >= 2)
            {
                magic = GetMagic(Spell.NapalmShot);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }
            }

            if (GetElementalOrbCount() < 1)
            {
                magic = GetMagic(Spell.ElementalShot);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }
            }

            if (!Target.PoisonList.Any(p => p.PType == PoisonType.Green) && !HasBuff(BuffType.毒魔闪) && Target.PercentHealth > 30)
            {
                magic = GetMagic(Spell.PoisonShot);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }
            }

            if (Target.Level >= Level)
            {
                magic = GetMagic(Spell.CrippleShot);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }
            }

            magic = GetMagic(Spell.StraightShot);
            if (CanUseMagic(magic))
            {
                BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                return;
            }

            magic = GetMagic(Spell.ElementalShot);
            if (CanUseMagic(magic))
            {
                BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                return;
            }
        }

        protected override void ProcessTarget()
        {
            if (Target == null || !CanAttack) return;

            if (!HasWeapon || (HasWeapon && !HasClassWeapon))
            {
                MeleeInAttackRange();
                return;
            }

            if (NextMagicSpell != Spell.None)
            {
                RemoteSpellInAttackRange();
                return;
            }

            if (HasClassWeapon && TargetDistance < 2)
            {
                MoveAwayFrom(Target); // 定向后撤风筝，不再随机乱跑
                UpdateTimers();
                return;
            }

            RemoteInAttackRange();
            UpdateTimers();
        }

        private void UpdateTimers()
        {
            AttackTime = Envir.Time + AttackSpeed;
            ActionTime = Envir.Time + 1200;
            RegenTime = Envir.Time + RegenDelay;
        }

        private void MeleeInAttackRange()
        {
            if (InAttackRange())
            {
                Attack();
                if (Target != null && Target.Dead)
                {
                    FindTarget();
                }
            }
            else
            {
                MoveTo(Target.CurrentLocation);
            }
        }

        private void RemoteInAttackRange()
        {
            if (Target == null || !CanAttack) return;

            if (HasClassWeapon && CanCast && NextMagicSpell == Spell.None)
            {
                Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
                RangeAttack(Direction, Target.CurrentLocation, Target.ObjectID);

                if (Target != null && Target.Dead && TargetDistance > ViewRange)
                {
                    FindTarget();
                }
                else
                {
                    MoveTo(Target.CurrentLocation);
                }
            }
        }

        private void RemoteSpellInAttackRange()
        {
            if (Target == null || !CanAttack) return;

            if (HasClassWeapon && CanCast && NextMagicSpell != Spell.None)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.None;

                if (Target != null && Target.Dead && TargetDistance > ViewRange)
                {
                    FindTarget();
                }
                else
                {
                    MoveTo(Target.CurrentLocation);
                }
            }
        }
    }
}