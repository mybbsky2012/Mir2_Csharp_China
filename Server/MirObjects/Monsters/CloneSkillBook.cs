using Server.MirDatabase;

namespace Server.MirObjects.Monsters
{
    /// <summary>分身的施法形态（决定伤害怎么落到目标身上）。</summary>
    public enum CloneCastShape
    {
        None = 0,
        /// <summary>单体指向：火球/雷电/寒冰掌/灭天火…</summary>
        Target,
        /// <summary>目标点周围方形范围：爆裂火焰/冰咆哮/火龙气焰/天霜冰环/天上落焰…</summary>
        Burst,
        /// <summary>自身前方 3 列 × N 深的扇面：冰焰术</summary>
        FrontBurst,
        /// <summary>自身朝目标方向穿透：地狱火(3格)/疾光电影(6格)</summary>
        Line,
        /// <summary>自身增益/防护：魔法盾/深延术</summary>
        SelfBuff,
        /// <summary>把贴身敌人推开：抗拒火环</summary>
        PushRing,
    }

    /// <summary>单个技能在分身手里的施法档案。</summary>
    public sealed class CloneSkillInfo
    {
        public CloneCastShape Shape = CloneCastShape.Target;
        /// <summary>Burst = 半径；Line = 穿透格数；FrontBurst = 纵深。</summary>
        public int Radius;
        /// <summary>最远施法距离。</summary>
        public int MaxDistance = 9;
        /// <summary>需要的贴身敌人数下限。</summary>
        public int MinSurround;
        /// <summary>只对不死系有效（地狱雷光对活人只有 1/10 伤害，放了纯浪费）。</summary>
        public bool UndeadOnly;
    }

    /// <summary>
    /// 分身技能表（10-28）。
    ///
    /// 分身的「会放什么技能」来自主人真正学会的技能（master.Info.Magics），
    /// 这张表只回答另外两件事：
    ///   ① 这个技能分身能不能用（表里没有的一律不放，避免放出召唤/瞬移/圣言这类语义不合适的技能）；
    ///   ② 放出去之后伤害落在哪些格子、多远的距离能放。
    ///
    /// 分类和强弱排序直接复用英雄那套 <see cref="HeroSpellBook"/>（同一张分类表：Buff / Control / Aoe / Single），
    /// 这样英雄和分身的判断标准永远一致，改一处两边都生效。
    /// </summary>
    public static class CloneSkillBook
    {
        private static readonly Dictionary<Spell, CloneSkillInfo> Map = Build();

        public static CloneSkillInfo Get(Spell spell)
        {
            CloneSkillInfo info;
            return Map.TryGetValue(spell, out info) ? info : null;
        }

        public static bool IsKnown(Spell spell)
        {
            return Map.ContainsKey(spell);
        }

        public static IEnumerable<Spell> Known
        {
            get { return Map.Keys; }
        }

        /// <summary>
        /// 从「主人已学技能」里挑出这一类可用的技能，按英雄同样的规则排好序
        /// （快捷键 → 学习等级/威力评分）。
        /// </summary>
        /// <param name="magics">主人已学技能（master.Info.Magics）</param>
        /// <param name="kind">要挑哪一类</param>
        /// <param name="surround">贴身敌人数</param>
        /// <param name="distance">与目标距离</param>
        /// <param name="hasBuff">判断自己身上是否已有某 buff</param>
        /// <param name="canPay">判断主人法力够不够放这个技能</param>
        /// <param name="coolingDown">判断这个技能是否还在冷却</param>
        public static List<UserMagic> Rank(IEnumerable<UserMagic> magics, HeroSpellKind kind, int surround, int distance,
            Func<BuffType, bool> hasBuff, Func<UserMagic, bool> canPay, Func<Spell, bool> coolingDown)
        {
            var result = new List<UserMagic>();
            if (magics == null) return result;

            // 距离完全交给下面的分身影档案判断：英雄表里地狱火/疾光电影是按 2 格登记的，
            // 直接沿用它会把 3~6 格的直线技能误杀
            List<UserMagic> ranked = HeroSpellBook.Rank(magics, kind, surround, distance, hasBuff, null, true, ignoreDistance: true);

            foreach (UserMagic magic in ranked)
            {
                CloneSkillInfo info = Get(magic.Spell);
                if (info == null) continue;

                if (info.MinSurround > 0 && surround < info.MinSurround) continue;
                if (info.Shape != CloneCastShape.SelfBuff && distance > info.MaxDistance) continue;
                if (coolingDown != null && coolingDown(magic.Spell)) continue;
                if (canPay != null && !canPay(magic)) continue;

                result.Add(magic);
            }

            // 主人自己的 F1~F8 是给主人用的，分身没有快捷栏：
            // 这里忽略 Key 顺序，一律按「学习等级 + 威力」从强到弱排
            result.Sort(CompareByScore);
            return result;
        }

        private static int CompareByScore(UserMagic a, UserMagic b)
        {
            double sa = HeroSpellBook.Score(a);
            double sb = HeroSpellBook.Score(b);
            if (Math.Abs(sa - sb) > 0.01) return sb.CompareTo(sa);
            return ((byte)a.Spell).CompareTo((byte)b.Spell);
        }

        /// <summary>
        /// 从排好序的候选里挑一个 —— 不是每次都用最强的那个：
        ///   ① 只在「最高分 ≥40%」的前 4 个里挑（太弱的技能不拿来凑数）；
        ///   ② 按评分加权随机（越强越常出，但不会一直同一个）；
        ///   ③ 刚放过的那个权重降到 15%，避免连着两次刷同一个技能。
        /// 这就是「知道多个技能切换使用」的落点。
        /// </summary>
        /// <param name="ranked">已按强弱排序的候选</param>
        /// <param name="lastSpell">上一发放的技能（None = 没有）</param>
        /// <param name="roll">0~999 的随机数（测试时可传固定值）</param>
        public static UserMagic PickVaried(IList<UserMagic> ranked, Spell lastSpell, int roll)
        {
            if (ranked == null || ranked.Count == 0) return null;
            if (ranked.Count == 1) return ranked[0];

            double top = HeroSpellBook.Score(ranked[0]);
            var pool = new List<UserMagic>();

            foreach (UserMagic magic in ranked)
            {
                if (pool.Count >= 4) break;
                if (HeroSpellBook.Score(magic) < top * 0.4) break;   // 已经排序，后面的只会更低
                pool.Add(magic);
            }

            if (pool.Count <= 1) return ranked[0];

            double total = 0;
            var weights = new double[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                double weight = HeroSpellBook.Score(pool[i]);
                if (pool[i].Spell == lastSpell) weight *= 0.15;
                weights[i] = weight;
                total += weight;
            }

            if (total <= 0) return ranked[0];

            double target = (roll % 1000) / 1000.0 * total;
            double acc = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += weights[i];
                if (target < acc) return pool[i];
            }

            return pool[pool.Count - 1];
        }

        private static Dictionary<Spell, CloneSkillInfo> Build()
        {
            var map = new Dictionary<Spell, CloneSkillInfo>();

            void Add(Spell spell, CloneCastShape shape, int radius = 0, int maxDistance = 9, int minSurround = 0, bool undeadOnly = false)
            {
                map[spell] = new CloneSkillInfo
                {
                    Shape = shape,
                    Radius = radius,
                    MaxDistance = maxDistance,
                    MinSurround = minSurround,
                    UndeadOnly = undeadOnly,
                };
            }

            //＝＝＝＝ 自身防护 / 增益 ＝＝＝＝
            Add(Spell.MagicShield, CloneCastShape.SelfBuff, maxDistance: 0);
            Add(Spell.MagicBooster, CloneCastShape.SelfBuff, maxDistance: 0);

            //＝＝＝＝ 控制 ＝＝＝＝
            Add(Spell.Repulsion, CloneCastShape.PushRing, maxDistance: 1, minSurround: 1);

            //＝＝＝＝ 单体主力 ＝＝＝＝
            Add(Spell.FireBall, CloneCastShape.Target, maxDistance: 9);
            Add(Spell.GreatFireBall, CloneCastShape.Target, maxDistance: 9);
            Add(Spell.GreatFireBallRare, CloneCastShape.Target, maxDistance: 9);
            Add(Spell.ThunderBolt, CloneCastShape.Target, maxDistance: 9);
            Add(Spell.ThunderBoltRare, CloneCastShape.Target, maxDistance: 9);
            Add(Spell.FrostCrunch, CloneCastShape.Target, maxDistance: 9);
            Add(Spell.FlameDisruptor, CloneCastShape.Target, maxDistance: 9);

            //＝＝＝＝ 群攻 ＝＝＝＝
            Add(Spell.FireBang, CloneCastShape.Burst, radius: 1, maxDistance: 9, minSurround: 2);
            Add(Spell.IceStorm, CloneCastShape.Burst, radius: 1, maxDistance: 9, minSurround: 2);
            Add(Spell.FlameField, CloneCastShape.Burst, radius: 2, maxDistance: 9, minSurround: 2);
            Add(Spell.Blizzard, CloneCastShape.Burst, radius: 2, maxDistance: 9, minSurround: 2);
            Add(Spell.MeteorStrike, CloneCastShape.Burst, radius: 2, maxDistance: 9, minSurround: 0);
            Add(Spell.ThunderStorm, CloneCastShape.Burst, radius: 2, maxDistance: 9, minSurround: 2, undeadOnly: true);

            //＝＝＝＝ 直线 / 近身 ＝＝＝＝
            Add(Spell.HellFire, CloneCastShape.Line, radius: 3, maxDistance: 3);
            Add(Spell.Lightning, CloneCastShape.Line, radius: 6, maxDistance: 6);
            Add(Spell.IceThrust, CloneCastShape.FrontBurst, radius: 3, maxDistance: 3);

            return map;
        }
    }
}
