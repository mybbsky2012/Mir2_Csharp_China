using Server.MirDatabase;

namespace Server.MirObjects
{
    /// <summary>英雄技能在 AI 里的角色分类。</summary>
    public enum HeroSpellKind
    {
        None = 0,
        /// <summary>自身/主人增益、防护：身上没有对应 buff 才放。</summary>
        Buff,
        /// <summary>开关型强化（烈火、半月、狂风、双龙、天务…）：只开不关。</summary>
        Toggle,
        /// <summary>单体输出主力。</summary>
        Single,
        /// <summary>群体输出：按贴身敌人数（MinSurround）决定是否值得放。</summary>
        Aoe,
        /// <summary>控制/减益（推开、定身、诅咒、麻痹）。</summary>
        Control,
        /// <summary>治疗。</summary>
        Heal,
        /// <summary>召唤（宝宝/分身）。</summary>
        Summon,
    }

    /// <summary>单个技能的 AI 档案。</summary>
    public sealed class HeroSpellProfile
    {
        public HeroSpellKind Kind;
        /// <summary>Buff 类依赖的 buff；None = 不做 buff 判重。</summary>
        public BuffType Buff = BuffType.None;
        /// <summary>Aoe/Control：需要的贴身敌人数下限（0 = 不限）。</summary>
        public int MinSurround;
        /// <summary>施放需要的目标距离下限（-1 = 不限）。</summary>
        public int MinDistance = -1;
        /// <summary>施放需要的目标距离上限（-1 = 用技能自身 Range，Range=0 视为近身 2 格）。</summary>
        public int MaxDistance = -1;
        /// <summary>true = 以自身为目标施放（buff/防护/开关）。</summary>
        public bool SelfCast;
        /// <summary>
        /// 伏击型 buff（烈火身/月影术）：走路、攻击、施法、挨打任意一个动作都会把它消耗掉，
        /// 本质上只能「开战前上一次」。战斗中刷新只会每拍抢走施法机会 → 英雄一直放技能不攻击，
        /// 所以这类 buff 只在没有目标（未交战）时补，并且带冷却。
        /// </summary>
        public bool Ambush;

        public HeroSpellProfile(HeroSpellKind kind, BuffType buff = BuffType.None, bool selfCast = false)
        {
            Kind = kind;
            Buff = buff;
            SelfCast = selfCast;
        }
    }

    /// <summary>
    /// 英雄技能档案库（10-28）。
    ///
    /// 目的：把「英雄会放哪些技能、什么时候放、放给自己还是敌人」从各职业硬编码的
    /// if 链里抽出来，做成一张表；排序交给 <see cref="Rank"/>：
    ///   ① 玩家给英雄设了快捷键（UserMagic.Key 17~24 = 英雄技能栏 1~8）的技能排最前，按 1→8 顺序；
    ///   ② 其余按「学习等级（Level3）为主 + 威力/延迟折算」从高到低 —— 学了更高级的技能自动顶掉低级的。
    /// 没登记在表里的技能一律不主动放（避免放出语义不明的技能）。
    /// </summary>
    public static class HeroSpellBook
    {
        private static readonly Dictionary<Spell, HeroSpellProfile> Profiles = Build();

        public static HeroSpellProfile Get(Spell spell)
        {
            HeroSpellProfile p;
            return Profiles.TryGetValue(spell, out p) ? p : null;
        }

        public static bool IsKnown(Spell spell)
        {
            return Profiles.ContainsKey(spell);
        }

        /// <summary>全部登记过的技能（只给测试/排查用）。</summary>
        public static IEnumerable<Spell> AllSpells => Profiles.Keys;

        /// <summary>快捷键序号：英雄栏 1~8 存成 Key=17~24，这里归一到 1~8；没设键的排最后。</summary>
        public static int KeyRank(UserMagic magic)
        {
            if (magic == null || magic.Key <= 0) return 1000;
            return magic.Key > 16 ? magic.Key - 16 : magic.Key;
        }

        /// <summary>
        /// 综合评分：学习等级（保证高级技能优先）+ 威力/施法延迟折算的 DPS ×2（保证同级里挑更能打的）。
        /// 战士一类 Power 全 0 的技能靠学习等级也能排出正确档次；法师/弓手这类数值差得多的，
        /// DPS 会把「等级高但伤害低」的秘笈版压到后面去。
        /// </summary>
        public static double Score(UserMagic magic)
        {
            if (magic == null || magic.Info == null) return 0;

            MagicInfo info = magic.Info;
            double level = Math.Max(info.Level3, info.Level1);
            double damage = info.PowerBase + info.PowerBonus * magic.Level
                          + info.MPowerBase + info.MPowerBonus * magic.Level;
            double mult = info.MultiplierBase + info.MultiplierBonus * magic.Level;
            if (mult <= 0) mult = 1;
            double delay = Math.Max(info.DelayBase, 800) / 1000.0;

            return level + damage * mult / delay * 2;
        }

        /// <summary>伏击型 buff（烈火身/月影术）的补放冷却：放进去了也会被下一次动作吃掉，补太勤没意义。</summary>
        public const int AmbushCooldownMs = 12000;

        /// <summary>
        /// 伏击型 buff 现在该不该补：
        /// ① 交战中（有活目标）绝不补 —— 补了也是下一秒被走路/攻击消耗掉，只会白占一拍施法；
        /// ② 冷却内不补 —— 防止「补上→被消耗→再补」的空转。
        /// </summary>
        public static bool CanRefreshAmbushBuff(bool inCombat, long now, long nextAllowed)
        {
            return !inCombat && now >= nextAllowed;
        }

        /// <summary>
        /// 按「是否能放 + 该不该放」过滤，再按「快捷键顺序 → 评分」排序。
        /// </summary>
        /// <param name="all">英雄已学技能（Info.Magics）</param>
        /// <param name="kind">要挑哪一类</param>
        /// <param name="surround">当前贴身敌人数</param>
        /// <param name="distance">与目标的距离（Buff 类传 0 即可）</param>
        /// <param name="hasBuff">查询目标身上是否已有某 buff</param>
        /// <param name="toggleActive">查询某个开关技能当前是否已开启</param>
        /// <param name="autoSkill">Setup.ini [Hero] AutoSkill；false 时只放设过快捷键的技能</param>
        /// <param name="ignoreDistance">true = 不按距离过滤（分身有自己的射程表，见 CloneSkillBook）</param>
        public static List<UserMagic> Rank(IEnumerable<UserMagic> all, HeroSpellKind kind, int surround, int distance,
            Func<BuffType, bool> hasBuff, Func<Spell, bool> toggleActive, bool autoSkill, bool ignoreDistance = false)
        {
            var list = new List<UserMagic>();
            if (all == null) return list;

            foreach (UserMagic magic in all)
            {
                if (magic == null || magic.Info == null) continue;

                HeroSpellProfile p = Get(magic.Spell);
                if (p == null || p.Kind != kind) continue;

                // AutoSkill 关闭 = 恢复原版：只放玩家设过快捷键的技能
                if (!autoSkill && magic.Key <= 0) continue;

                switch (kind)
                {
                    case HeroSpellKind.Buff:
                        // 身上已有这个 buff 就不重复上（万效符/金刚不坏这类长 buff 尤其重要）
                        if (p.Buff != BuffType.None && hasBuff != null && hasBuff(p.Buff)) continue;
                        break;

                    case HeroSpellKind.Toggle:
                        // 已经开着的开关型技能绝不再点一次（点了就会关掉）
                        if (toggleActive != null && toggleActive(magic.Spell)) continue;
                        break;

                    default:
                        // 攻击类挂了 debuff 的（诅咒术/施毒），目标已经有了就不再放
                        if (p.Buff != BuffType.None && hasBuff != null && hasBuff(p.Buff)) continue;
                        break;
                }

                if (p.MinSurround > 0 && surround < p.MinSurround) continue;

                if (!ignoreDistance)
                {
                    if (p.MinDistance >= 0 && distance < p.MinDistance) continue;

                    if (p.MaxDistance >= 0)
                    {
                        if (distance > p.MaxDistance) continue;
                    }
                    else
                    {
                        // 统一按技能自身射程过滤（Range=0 的近身/自身型技能按 2 格算）。
                        // Buff/治疗也走这一条：主人跑远了就不该硬放（Magic() 会直接判距离太远失败）
                        int range = magic.Info.Range;
                        int effective = range == 0 ? 2 : range;
                        if (distance > effective) continue;
                    }
                }

                list.Add(magic);
            }

            list.Sort(Compare);
            return list;
        }

        private static int Compare(UserMagic a, UserMagic b)
        {
            int ka = KeyRank(a);
            int kb = KeyRank(b);
            if (ka != kb) return ka.CompareTo(kb);

            double sa = Score(a);
            double sb = Score(b);
            if (Math.Abs(sa - sb) > 0.01) return sb.CompareTo(sa);

            // 同分按 Spell 序号，保证排序稳定可测
            return ((byte)a.Spell).CompareTo((byte)b.Spell);
        }

        private static Dictionary<Spell, HeroSpellProfile> Build()
        {
            var map = new Dictionary<Spell, HeroSpellProfile>();

            void Add(Spell spell, HeroSpellKind kind, BuffType buff = BuffType.None,
                bool selfCast = false, int minSurround = 0, int minDistance = -1, int maxDistance = -1,
                bool ambush = false)
            {
                map[spell] = new HeroSpellProfile(kind, buff, selfCast)
                {
                    MinSurround = minSurround,
                    MinDistance = minDistance,
                    MaxDistance = maxDistance,
                    Ambush = ambush,
                };
            }

            //＝＝＝＝＝＝＝＝＝＝ 战士 ＝＝＝＝＝＝＝＝＝＝
            // 增益/防护
            Add(Spell.Rage, HeroSpellKind.Buff, BuffType.剑气爆, true);
            Add(Spell.ProtectionField, HeroSpellKind.Buff, BuffType.护身气幕, true);
            Add(Spell.Fury, HeroSpellKind.Buff, BuffType.血龙剑法, true);
            Add(Spell.ImmortalSkin, HeroSpellKind.Buff, BuffType.金刚不坏, true);
            Add(Spell.ImmortalSkinRare, HeroSpellKind.Buff, BuffType.金刚不坏秘籍, true);
            // 开关型强化：只开不关
            Add(Spell.Thrusting, HeroSpellKind.Toggle, selfCast: true);
            Add(Spell.HalfMoon, HeroSpellKind.Toggle, selfCast: true);
            Add(Spell.CrossHalfMoon, HeroSpellKind.Toggle, selfCast: true);
            Add(Spell.TwinDrakeBlade, HeroSpellKind.Toggle, selfCast: true);
            Add(Spell.FlamingSword, HeroSpellKind.Toggle, selfCast: true);
            Add(Spell.CounterAttack, HeroSpellKind.Toggle, selfCast: true);
            // 群攻
            Add(Spell.BladeAvalanche, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.SlashingBurst, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.DimensionalSword, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.DimensionalSwordRare, HeroSpellKind.Aoe, minSurround: 2);
            // 控制
            Add(Spell.LionRoar, HeroSpellKind.Control, minSurround: 3);
            Add(Spell.LionRoarRare, HeroSpellKind.Control, minSurround: 3);
            Add(Spell.Entrapment, HeroSpellKind.Control, minDistance: 1);
            Add(Spell.EntrapmentRare, HeroSpellKind.Control, minDistance: 1);

            //＝＝＝＝＝＝＝＝＝＝ 法师 ＝＝＝＝＝＝＝＝＝＝
            Add(Spell.MagicShield, HeroSpellKind.Buff, BuffType.魔法盾, true);
            Add(Spell.MagicBooster, HeroSpellKind.Buff, BuffType.深延术, true);
            Add(Spell.HeavenlySecrets, HeroSpellKind.Buff, BuffType.天上秘术, true);
            Add(Spell.Mirroring, HeroSpellKind.Summon, selfCast: true);
            // 单体
            Add(Spell.FireBall, HeroSpellKind.Single);
            Add(Spell.GreatFireBall, HeroSpellKind.Single);
            Add(Spell.GreatFireBallRare, HeroSpellKind.Single);
            Add(Spell.ThunderBolt, HeroSpellKind.Single);
            Add(Spell.ThunderBoltRare, HeroSpellKind.Single);
            Add(Spell.FrostCrunch, HeroSpellKind.Single);
            Add(Spell.FlameDisruptor, HeroSpellKind.Single);
            Add(Spell.Vampirism, HeroSpellKind.Single);
            Add(Spell.MeteorStrike, HeroSpellKind.Single);
            Add(Spell.IceThrust, HeroSpellKind.Single, maxDistance: 2);   // 冰焰术：近身大招
            // 群攻
            Add(Spell.FireBang, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.FireWall, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.ThunderStorm, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.IceStorm, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.FlameField, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.Blizzard, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.FireBounce, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.MeteorShower, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.HellFire, HeroSpellKind.Aoe, minSurround: 2, maxDistance: 2);
            Add(Spell.Lightning, HeroSpellKind.Aoe, minSurround: 2, maxDistance: 2);
            Add(Spell.AbsorbFlame, HeroSpellKind.Aoe, minSurround: 2, maxDistance: 2);
            Add(Spell.AbsorbFlameRare, HeroSpellKind.Aoe, minSurround: 2, maxDistance: 2);
            // 控制
            Add(Spell.Repulsion, HeroSpellKind.Control, minSurround: 1, maxDistance: 1);

            //＝＝＝＝＝＝＝＝＝＝ 道士 ＝＝＝＝＝＝＝＝＝＝
            Add(Spell.SoulShield, HeroSpellKind.Buff, BuffType.幽灵盾, true);
            Add(Spell.BlessedArmour, HeroSpellKind.Buff, BuffType.神圣战甲术, true);
            Add(Spell.UltimateEnhancer, HeroSpellKind.Buff, BuffType.无极真气, true);
            Add(Spell.EnergyShield, HeroSpellKind.Buff, BuffType.先天气功, true);
            Add(Spell.GreatTalisman, HeroSpellKind.Buff, BuffType.万效符, true, maxDistance: 9);   // 万效符自身 Range=0，但实际是给自身+身边队友上，允许给主人放
            Add(Spell.GreatTalismanRare, HeroSpellKind.Buff, BuffType.万效符秘笈, true, maxDistance: 9);
            Add(Spell.PetEnhancer, HeroSpellKind.Buff, BuffType.血龙水, true);
            // 治疗
            Add(Spell.Healing, HeroSpellKind.Heal, selfCast: true);
            Add(Spell.HealingRare, HeroSpellKind.Heal, selfCast: true);
            Add(Spell.MassHealing, HeroSpellKind.Heal, selfCast: true);
            Add(Spell.HealingCircle, HeroSpellKind.Heal, selfCast: true);
            Add(Spell.HealingcircleRare, HeroSpellKind.Heal, selfCast: true);
            // 输出
            Add(Spell.SoulFireBall, HeroSpellKind.Single);
            Add(Spell.SoulFireBallPlus, HeroSpellKind.Single);
            Add(Spell.SoulFireBallPlusRare, HeroSpellKind.Single);
            Add(Spell.Curse, HeroSpellKind.Single, BuffType.诅咒术);
            Add(Spell.Plague, HeroSpellKind.Aoe, minSurround: 2);   // 烦脑：3x3 范围毒，别当单体外
            Add(Spell.PoisonCloud, HeroSpellKind.Aoe, minSurround: 2);
            // 控制/召唤
            Add(Spell.TrapHexagon, HeroSpellKind.Control);
            Add(Spell.Hallucination, HeroSpellKind.Control);
            Add(Spell.Revelation, HeroSpellKind.Control, minDistance: 1);
            Add(Spell.SummonSkeleton, HeroSpellKind.Summon, selfCast: true);
            Add(Spell.SummonShinsu, HeroSpellKind.Summon, selfCast: true);
            Add(Spell.SummonHolyDeva, HeroSpellKind.Summon, selfCast: true);

            //＝＝＝＝＝＝＝＝＝＝ 刺客 ＝＝＝＝＝＝＝＝＝＝
            Add(Spell.Haste, HeroSpellKind.Buff, BuffType.体迅风, true);
            Add(Spell.LightBody, HeroSpellKind.Buff, BuffType.风身术, true);
            // 烈火身/月影术：走路、攻击、施法、挨打都会把 buff 消耗掉（HumanObject.Walk/Attack/Magic/Struck），
            // 只能当「伏击前上一次」，不能当持续 buff 每拍刷新，否则英雄会一直放它不出手（详见 HeroSpellProfile.Ambush）
            Add(Spell.DarkBody, HeroSpellKind.Buff, BuffType.烈火身, true, ambush: true);
            Add(Spell.MoonLight, HeroSpellKind.Buff, BuffType.月影术, true, ambush: true);
            Add(Spell.DoubleSlash, HeroSpellKind.Toggle, selfCast: true);
            Add(Spell.FireBurst, HeroSpellKind.Single);
            Add(Spell.PoisonSword, HeroSpellKind.Single);
            //★血风击（Hemorrhage）也不登记：它没有独立的施法分支，是「普通近战命中时按概率追加一次伤害」
            //  的被动触发技能（HumanObject.CompleteAttack 里结算）。登记成 Single 会让刺客英雄每 tick 排它、
            //  排了又放不出来，同样卡成「站着不打」。
            Add(Spell.HeavenlySword, HeroSpellKind.Single, maxDistance: 2);
            Add(Spell.CatTongue, HeroSpellKind.Single);
            Add(Spell.ShadowCombo, HeroSpellKind.Single, maxDistance: 2);
            Add(Spell.ShadowComboRare, HeroSpellKind.Single, maxDistance: 2);
            Add(Spell.FlashDash, HeroSpellKind.Single, maxDistance: 3);
            Add(Spell.FlashDashRare, HeroSpellKind.Single, maxDistance: 3);
            Add(Spell.CrescentSlash, HeroSpellKind.Aoe, minSurround: 2);
            Add(Spell.CrescentSlashRare, HeroSpellKind.Aoe, minSurround: 2);

            //＝＝＝＝＝＝＝＝＝＝ 弓手 ＝＝＝＝＝＝＝＝＝＝
            Add(Spell.Concentration, HeroSpellKind.Buff, BuffType.气流术, true);
            Add(Spell.ConcentrationRare, HeroSpellKind.Buff, BuffType.气流术, true);
            Add(Spell.ElementalBarrier, HeroSpellKind.Buff, BuffType.金刚术, true);
            //★精神状态（MentalState）绝不登记：它根本不能通过 Magic() 施放（服务端只在 SpellToggle 里处理，
            //  走 Magic() 会落到 default → cast=false，什么都没发生；于是 AI 每 tick 都以为「buff 还没上」重复排它，
            //  排完 ProcessAI 直接 return → 英雄站在怪物面前一箭不放）。
            //  而且它是「模式切换」：每点一次 0集中→1穿透→2组队，穿透模式伤害只剩 55%~70%。
            //  英雄默认 0 集中模式就是满伤害，所以干脆不碰。
            Add(Spell.StraightShot, HeroSpellKind.Single);
            Add(Spell.DoubleShot, HeroSpellKind.Single);
            Add(Spell.ElementalShot, HeroSpellKind.Single);
            Add(Spell.VampireShot, HeroSpellKind.Single);
            Add(Spell.PoisonShot, HeroSpellKind.Single);
            Add(Spell.CrippleShot, HeroSpellKind.Single);
            Add(Spell.NapalmShot, HeroSpellKind.Aoe, minSurround: 2);
            //★血龙闪秘笈（OneWithNature）不是单体远程箭：服务端 OneWithNature() 把落点固定成
            //  「施法者自己的 CurrentLocation」，Map 里按 5x5 结算。站着 5 格外放等于打空气。
            //  所以按「自身 5x5 群攻」登记：只在贴身有怪（≥1）时才用，别当主力单体外。
            Add(Spell.OneWithNature, HeroSpellKind.Aoe, minSurround: 1);
            // DeathBlow（灭杀）需要「灭杀窗口」buff，由 SkyThunderShotRare 命中后产生，
            // 这里不登记（登记了会在没窗口时白占一拍），由 ArcherHero 显式判断
            Add(Spell.DelayedExplosion, HeroSpellKind.Single);
            Add(Spell.DelayedExplosionRare, HeroSpellKind.Single);
            Add(Spell.SkyThunderShot, HeroSpellKind.Single, maxDistance: 8);  // 落雷杀：Range 字段为 0 但实际是远程定点
            Add(Spell.SkyThunderShotRare, HeroSpellKind.Single);
            Add(Spell.ExplosiveTrap, HeroSpellKind.Aoe, minSurround: 2, maxDistance: 3);
            Add(Spell.BindingShot, HeroSpellKind.Control);
            Add(Spell.Stonetrap, HeroSpellKind.Control);
            Add(Spell.SummonVampire, HeroSpellKind.Summon, selfCast: true);
            Add(Spell.SummonToad, HeroSpellKind.Summon, selfCast: true);
            Add(Spell.SummonSnakes, HeroSpellKind.Summon, selfCast: true);

            return map;
        }
    }
}
