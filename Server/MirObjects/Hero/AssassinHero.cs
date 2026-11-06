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

        /// <summary>
        /// 烈火身（DarkBody）= 召唤刺客分身 + 隐身 buff。已经有活着的分身时再放，
        /// HumanObject.DarkBody 会先把旧分身 Die() 掉然后直接 return —— 分身没了、buff 也没补上，
        /// AI 下一拍又判定「缺 buff」继续放，英雄就站在原地一直烈火身、不攻击。
        /// 所以有活分身时直接跳过这一发。
        /// </summary>
        protected override bool IsBuffWorthCasting(UserMagic magic)
        {
            if (magic != null && magic.Spell == Spell.DarkBody && HasLiveAssassinClone()) return false;
            return base.IsBuffWorthCasting(magic);
        }

        private bool HasLiveAssassinClone()
        {
            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject pet = Pets[i];
                if (pet == null || pet.Dead || pet.Info == null) continue;
                if (pet.Info.Name == Settings.AssassinCloneName) return true;
            }

            return false;
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
            // rotate：同档（评分差 10% 内）的换着放（影华连击 ⇄ 影华连击-秘籍 之类），不降输出
            if (TryCast(HeroSpellKind.Single, rotate: true)) return;
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
