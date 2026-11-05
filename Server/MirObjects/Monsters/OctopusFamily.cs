using Server.MirDatabase;
using ServerPackets;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 629 八爪巨兽 / 630 触手 / 640 巨型章鱼的触手 —— 三只站桩型触手怪的公共骨架。
    /// 骨头：原地不动、不自然回血、目标进了视野就按距离分档出手（近 / 中 / 远）。
    /// 本体动作复用 Lib 里已有的近距攻击1/2/3，命中表现靠 SpellEffect 挂在目标身上，
    /// 这样即使某个 Lib 缺「远程攻击」动作也不会画不出来。
    /// </summary>
    public abstract class OctopusStationary : MonsterObject
    {
        protected OctopusStationary(MonsterInfo info) : base(info) { }

        protected override bool CanMove => false;

        protected override bool InAttackRange()
        {
            if (Target == null) return false;
            if (Target.CurrentMap != CurrentMap) return false;
            return Functions.InRange(CurrentLocation, Target.CurrentLocation, Info.ViewRange);
        }

        protected override void ProcessTarget()
        {
            if (Target == null || !CanAttack) return;

            if (!InAttackRange())
            {
                // 不会移动，目标跑出视野就放弃，交给 ProcessSearch 下次重找
                Target = null;
                return;
            }

            Attack();

            if (Target != null && Target.Dead) FindTarget();
        }

        /// <summary>发一个攻击动作（Type 0~4 → 客户端近距攻击1~5），并铺好动作/攻击间隔。</summary>
        protected void BroadcastAttack(byte type)
        {
            Broadcast(new S.ObjectAttack
            {
                ObjectID = ObjectID,
                Direction = Direction,
                Location = CurrentLocation,
                Type = type
            });

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;
        }

        /// <summary>在某对象身上播一个特效（客户端 GameScene.ObjectEffect 里按枚举画）。</summary>
        protected void BroadcastEffect(MapObject ob, SpellEffect effect)
        {
            if (ob == null) return;
            Broadcast(new S.ObjectEffect { ObjectID = ob.ObjectID, Effect = effect });
        }

        /// <summary>延迟结算伤害（和基类 Attack 一样走 300~400ms，让动画先打出去）。</summary>
        protected void Hit(MapObject ob, int damage, int delay = 400, DefenceType type = DefenceType.ACAgility)
        {
            if (ob == null || damage <= 0) return;
            ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + delay, ob, damage, type, false));
        }

        protected bool InRange(int distance)
        {
            return Target != null && Functions.InRange(CurrentLocation, Target.CurrentLocation, distance);
        }
    }

    /// <summary>
    /// 629 八爪巨兽（单朝向，Lib 里只有 近距攻击1 / 死亡）。
    /// 近（≤2 格）：触手拍下 → 大团紫墨糊在目标身上
    /// 中（3~5 格）：吐墨球 → 命中处炸一小团
    /// 远（6~7 格）：紫色触手丛从自己脚下铺开，7 格内全打
    /// </summary>
    public class Mon629P : OctopusStationary
    {
        protected internal Mon629P(MonsterInfo info) : base(info) { }

        protected override void Attack()
        {
            if (Target == null || !Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);

            // 629 没有远程动作，三档统一用近攻1，靠目标身上的特效区分
            BroadcastAttack(0);

            if (InRange(2))
            {
                BroadcastEffect(Target, SpellEffect.Mon629InkSplash);
                Hit(Target, damage);
            }
            else if (InRange(5))
            {
                BroadcastEffect(Target, SpellEffect.Mon629InkBall);
                Hit(Target, damage);
            }
            else
            {
                BroadcastEffect(this, SpellEffect.Mon629Tentacles);
                if (damage > 0) FullmoonAttack(damage, 600, DefenceType.ACAgility, -1, 7);
            }
        }
    }

    /// <summary>
    /// 630 触手（水／冰系，8 方向 × 每方向 10 帧）。
    /// 近（≤2 格）：近攻1 → 白水柱 398 + Dir×10，从自己身上窜起
    /// 中（3~4 格）：近攻2 → 白水柱 481 + Dir×10
    /// 远（5~7 格）：近攻1 → 目标处灰烟炸开 + 红棕尖锥（376-395）
    /// 命中后按概率给目标挂冰冻罩（636-641）
    /// </summary>
    public class Mon630P : OctopusStationary
    {
        protected internal Mon630P(MonsterInfo info) : base(info) { }

        /// <summary>远程（5~7 格）命中时挂在目标身上的特效。641 用同一套 AI，只换这一个。</summary>
        protected virtual SpellEffect FarHitEffect => SpellEffect.Mon630SmokeSpike;

        protected override void Attack()
        {
            if (Target == null || !Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);

            if (InRange(2))
            {
                BroadcastAttack(0);
                Hit(Target, damage);
                TryFreeze(15);
            }
            else if (InRange(4))
            {
                BroadcastAttack(1);
                Hit(Target, damage);
                TryFreeze(15);
            }
            else
            {
                BroadcastAttack(0);
                BroadcastEffect(Target, FarHitEffect);
                Hit(Target, damage, 450);
                TryFreeze(30);
            }
        }

        protected void TryFreeze(int chance)
        {
            if (Target == null || Target.Dead) return;
            if (Envir.Random.Next(100) >= chance) return;
            // 641 自己没有冰冻素材，这里统一用 630 的那段（636-641），客户端 case 里固定取 630 图库
            BroadcastEffect(Target, SpellEffect.Mon630Freeze);
        }
    }

    /// <summary>
    /// 641 巨型章鱼的触手2 —— 和 630 完全同一套行为（原地不动、按距离三档、命中附带冰冻）。
    /// 只有两处不同：水柱素材在自己的 641.Lib（376 / 481 起），远程命中是白水柱而不是灰烟+红锥。
    /// </summary>
    public class Mon641S : Mon630P
    {
        protected internal Mon641S(MonsterInfo info) : base(info) { }

        protected override SpellEffect FarHitEffect => SpellEffect.Mon641WaterHit;
    }

    /// <summary>
    /// 640 巨型章鱼的触手（近攻1/2/3 各 10 帧）。
    /// 三档都打「金屑爆」（464-472），近距炸轻、远距炸得更狠一点。
    /// </summary>
    public class Mon640N : OctopusStationary
    {
        protected internal Mon640N(MonsterInfo info) : base(info) { }

        protected override void Attack()
        {
            if (Target == null || !Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            byte type;

            if (InRange(2))
            {
                type = 0;
                BroadcastAttack(type);
                BroadcastEffect(Target, SpellEffect.Mon640ShardBurst);
                Hit(Target, damage);
            }
            else if (InRange(4))
            {
                type = 1;
                BroadcastAttack(type);
                BroadcastEffect(Target, SpellEffect.Mon640ShardBurst);
                Hit(Target, damage, 450);
            }
            else
            {
                type = 2;
                BroadcastAttack(type);
                BroadcastEffect(Target, SpellEffect.Mon640ShardBurst);
                Hit(Target, damage, 450);
            }
        }
    }
}
