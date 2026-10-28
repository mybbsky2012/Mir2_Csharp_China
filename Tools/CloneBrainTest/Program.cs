using System.Text;
using Server.MirDatabase;
using Server.MirObjects;
using Server.MirObjects.Monsters;

namespace CloneBrainTest;

/// <summary>
/// 分身技能大脑（CloneSkillBook + HumanWizard.TryCast 的挑选顺序）回归测试。
/// 纯逻辑测试：不需要服务端数据库，不需要起服务端。
/// </summary>
internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Console.WriteLine("=== 分身技能大脑测试 ===");

        CatalogTests();
        SafetyTests();
        RankTests();
        RotationTests();
        ScenarioTests();
        InteractionTests();

        Console.WriteLine();
        Console.WriteLine($"通过 {_pass} 项，失败 {_fail} 项");
        return _fail == 0 ? 0 : 1;
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 技能表完整性 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void CatalogTests()
    {
        Section("技能表");

        // 该有的都要有
        Check("火球术可用", CloneSkillBook.Get(Spell.FireBall)?.Shape == CloneCastShape.Target);
        Check("雷电术可用", CloneSkillBook.Get(Spell.ThunderBolt)?.Shape == CloneCastShape.Target);
        Check("寒冰掌可用", CloneSkillBook.Get(Spell.FrostCrunch)?.Shape == CloneCastShape.Target);
        Check("灭天火可用", CloneSkillBook.Get(Spell.FlameDisruptor)?.Shape == CloneCastShape.Target);
        Check("魔法盾是自身buff", CloneSkillBook.Get(Spell.MagicShield)?.Shape == CloneCastShape.SelfBuff);
        Check("深延术是自身buff", CloneSkillBook.Get(Spell.MagicBooster)?.Shape == CloneCastShape.SelfBuff);
        Check("抗拒火环是推开", CloneSkillBook.Get(Spell.Repulsion)?.Shape == CloneCastShape.PushRing);

        // 群攻半径要和原版一致（爆裂火焰/冰咆哮 3×3，地狱雷光/火龙气焰/天霜冰环/天上落焰 5×5）
        Check("爆裂火焰半径1", CloneSkillBook.Get(Spell.FireBang)?.Radius == 1);
        Check("冰咆哮半径1", CloneSkillBook.Get(Spell.IceStorm)?.Radius == 1);
        Check("地狱雷光半径2", CloneSkillBook.Get(Spell.ThunderStorm)?.Radius == 2);
        Check("火龙气焰半径2", CloneSkillBook.Get(Spell.FlameField)?.Radius == 2);
        Check("天霜冰环半径2", CloneSkillBook.Get(Spell.Blizzard)?.Radius == 2);
        Check("天上落焰半径2", CloneSkillBook.Get(Spell.MeteorStrike)?.Radius == 2);

        // 直线技能
        Check("地狱火3格直线", CloneSkillBook.Get(Spell.HellFire)?.Shape == CloneCastShape.Line && CloneSkillBook.Get(Spell.HellFire)?.Radius == 3);
        Check("疾光电影6格直线", CloneSkillBook.Get(Spell.Lightning)?.Shape == CloneCastShape.Line && CloneSkillBook.Get(Spell.Lightning)?.Radius == 6);
        Check("冰焰术是前方扇面", CloneSkillBook.Get(Spell.IceThrust)?.Shape == CloneCastShape.FrontBurst);

        // 地狱雷光对活人只有 1/10 伤害 → 必须标成「只打不死系」
        Check("地狱雷光只对不死系", CloneSkillBook.Get(Spell.ThunderStorm)?.UndeadOnly == true);
        Check("其他群攻不限不死系", CloneSkillBook.Get(Spell.Blizzard)?.UndeadOnly == false);

        // 每一个登记在分身表里的技能，都必须在英雄分类表里有归属，
        // 否则 CloneSkillBook.Rank 永远选不出来 → 表里写了等于没写
        var missing = new List<string>();
        foreach (Spell spell in CloneSkillBook.Known)
        {
            if (HeroSpellBook.Get(spell) == null) missing.Add(spell.ToString());
        }
        Check("分身表内技能都有英雄分类:" + (missing.Count == 0 ? "OK" : string.Join(",", missing)), missing.Count == 0);

        // 反向：分身要用的法师输出技能，一个都不能漏
        Check("大火球存在", CloneSkillBook.IsKnown(Spell.GreatFireBall));
        Check("强击-秘籍存在", CloneSkillBook.IsKnown(Spell.ThunderBoltRare));
        Check("大火球-秘籍存在", CloneSkillBook.IsKnown(Spell.GreatFireBallRare));
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 安全边界 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void SafetyTests()
    {
        Section("不能放的技能");

        // 这些技能对分身来说是灾难：会召唤/瞬移/抢怪/无意义
        Check("分身术不在表内(防递归)", !CloneSkillBook.IsKnown(Spell.Mirroring));
        Check("瞬息移动不在表内", !CloneSkillBook.IsKnown(Spell.Teleport));
        Check("时光涌动不在表内", !CloneSkillBook.IsKnown(Spell.Blink));
        Check("雷仙风不在表内(30秒冲刺)", !CloneSkillBook.IsKnown(Spell.StormEscape));
        Check("雷仙风-秘籍不在表内", !CloneSkillBook.IsKnown(Spell.StormEscapeRare));
        Check("诱惑之光不在表内(会抢宝宝)", !CloneSkillBook.IsKnown(Spell.ElectricShock));
        Check("圣言术不在表内(秒不死系)", !CloneSkillBook.IsKnown(Spell.TurnUndead));
        Check("火墙不在表内(持久地面物)", !CloneSkillBook.IsKnown(Spell.FireWall));
        Check("吸魔炎风不在表内(持续地面物)", !CloneSkillBook.IsKnown(Spell.AbsorbFlame));
        Check("嗜血术不在表内(回血对分身无意义)", !CloneSkillBook.IsKnown(Spell.Vampirism));
        Check("天上秘术不在表内(只减自己耗蓝)", !CloneSkillBook.IsKnown(Spell.HeavenlySecrets));

        // 自身 buff 距离为 0，绝不能因为距离被过滤掉
        Check("魔法盾距离0", CloneSkillBook.Get(Spell.MagicShield)?.MaxDistance == 0);
        Check("深延术距离0", CloneSkillBook.Get(Spell.MagicBooster)?.MaxDistance == 0);

        // 群攻都要求至少 2 个贴身，避免对单个怪放群攻
        Check("爆裂火焰需要2个贴身", CloneSkillBook.Get(Spell.FireBang)?.MinSurround == 2);
        Check("火龙气焰需要2个贴身", CloneSkillBook.Get(Spell.FlameField)?.MinSurround == 2);
        // 天上落焰是单体主力（5×5 溅射），不要求被围
        Check("天上落焰不要求被围", CloneSkillBook.Get(Spell.MeteorStrike)?.MinSurround == 0);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 过滤与排序 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void RankTests()
    {
        Section("过滤与排序");

        var all = new List<UserMagic>
        {
            M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9),
            M(Spell.ThunderBolt, level3: 23, power: 9, mpower: 8, range: 9),
            M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9),
            M(Spell.IceThrust, level3: 59, power: 50, mpower: 100, range: 0),
            M(Spell.Blizzard, level3: 50, power: 20, mpower: 30, range: 9),
            M(Spell.ThunderStorm, level3: 34, power: 10, mpower: 10, range: 0),
            M(Spell.MagicShield, level3: 38, range: 0),
        };

        // 距离 5：近身技能（冰焰术，3 格内）要被过滤
        List<UserMagic> singles = CloneSkillBook.Rank(all, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling);
        Check("距离5时冰焰术被过滤", singles.All(m => m.Spell != Spell.IceThrust));
        Check("距离5时雷电术保留", singles.Any(m => m.Spell == Spell.ThunderBolt));
        Check("单体按学习等级/威力排序(天上落焰第一)", singles.Count > 0 && singles[0].Spell == Spell.MeteorStrike);

        // 距离 2：冰焰术可用，且它评分最高
        List<UserMagic> close = CloneSkillBook.Rank(all, HeroSpellKind.Single, 0, 2, HasBuffNo, CanPayYes, NeverCooling);
        Check("距离2时冰焰术可选", close.Any(m => m.Spell == Spell.IceThrust));

        // 群攻在贴身数不足 2 时一个都挑不出来
        List<UserMagic> aoe1 = CloneSkillBook.Rank(all, HeroSpellKind.Aoe, 1, 3, HasBuffNo, CanPayYes, NeverCooling);
        Check("只围1个时没有群攻候选", aoe1.Count == 0);
        List<UserMagic> aoe2 = CloneSkillBook.Rank(all, HeroSpellKind.Aoe, 2, 3, HasBuffNo, CanPayYes, NeverCooling);
        Check("围2个时有群攻候选", aoe2.Count > 0);

        // 冷却中的技能要被跳过
        List<UserMagic> cooling = CloneSkillBook.Rank(all, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes,
            s => s == Spell.MeteorStrike);
        Check("冷却中的天上落焰被跳过", cooling.All(m => m.Spell != Spell.MeteorStrike));

        // 蓝不够的技能要被跳过
        List<UserMagic> poor = CloneSkillBook.Rank(all, HeroSpellKind.Single, 0, 5, HasBuffNo,
            m => m.Spell != Spell.MeteorStrike && m.Spell != Spell.IceThrust, NeverCooling);
        Check("蓝不够时跳过天上落焰", poor.All(m => m.Spell != Spell.MeteorStrike));

        // 没学过的技能不会凭空出现
        var onlyFireBall = new List<UserMagic> { M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9) };
        List<UserMagic> limited = CloneSkillBook.Rank(onlyFireBall, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling);
        Check("只会火球时只出火球", limited.Count == 1 && limited[0].Spell == Spell.FireBall);

        // 表里没有的技能（比如道士技能）即使学了也不会被选
        var taoist = new List<UserMagic> { M(Spell.SoulFireBall, level3: 20, power: 10, mpower: 10, range: 9) };
        Check("道士技能不被分身采用", CloneSkillBook.Rank(taoist, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling).Count == 0);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 技能轮换 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void RotationTests()
    {
        Section("技能切换（不能只放一个技能）");

        var wiz = FullWizard();
        List<UserMagic> ranked = CloneSkillBook.Rank(wiz, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling);
        Check("单体候选不止一个", ranked.Count >= 3);

        // 对 1000 次随机掷点统计分布
        var counter = new Dictionary<Spell, int>();
        for (int roll = 0; roll < 1000; roll++)
        {
            UserMagic pick = CloneSkillBook.PickVaried(ranked, Spell.None, roll);
            if (pick == null) continue;
            counter[pick.Spell] = counter.TryGetValue(pick.Spell, out int c) ? c + 1 : 1;
        }

        int distinct = counter.Count;
        int maxShare = counter.Count > 0 ? (int)Math.Round(counter.Values.Max() * 100.0 / 1000) : 0;

        Check($"1000 次里用到 {distinct} 种不同技能", distinct >= 3);
        Check($"最强技能占比 {maxShare}%（不能一边倒）", maxShare <= 65);

        // 刚放过的技能权重降低：同一个掷点，last 不同应该选到不同的技能
        int changed = 0;
        for (int roll = 0; roll < 1000; roll++)
        {
            UserMagic a = CloneSkillBook.PickVaried(ranked, Spell.None, roll);
            UserMagic b = CloneSkillBook.PickVaried(ranked, a.Spell, roll);
            if (a != null && b != null && a.Spell != b.Spell) changed++;
        }
        Check($"刚放过的技能会被降权（{changed}/1000 个掷点换了别的）", changed >= 300);

        // 只有一个技能时就用它
        var one = new List<UserMagic> { M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9) };
        Check("只有一个技能时就用它", CloneSkillBook.PickVaried(one, Spell.None, 500)?.Spell == Spell.FireBall);

        // 分数差太多的技能不能混进轮换（否则会拿火球去顶天上落焰）
        var mixed = new List<UserMagic>
        {
            M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9),
            M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9),
        };
        var mixedRanked = CloneSkillBook.Rank(mixed, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling);
        bool onlyTop = true;
        for (int roll = 0; roll < 1000; roll++)
        {
            if (CloneSkillBook.PickVaried(mixedRanked, Spell.None, roll)?.Spell != Spell.MeteorStrike) onlyTop = false;
        }
        Check("差距过大的低分技能不参与轮换", onlyTop);

        // 排序不看主人的快捷栏：给火球设 F1 也不会让它排到天上落焰前面
        var keyedLow = M(Spell.FireBall, key: 17, level3: 11, power: 2, mpower: 8, range: 9);
        var keyedHigh = M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9);
        var keyedRanked = CloneSkillBook.Rank(new List<UserMagic> { keyedLow, keyedHigh }, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling);
        Check("分身排序忽略主人快捷键(强技能仍在前)", keyedRanked[0].Spell == Spell.MeteorStrike);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 实战场景 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void ScenarioTests()
    {
        Section("实战场景");

        var wiz = FullWizard();

        // 1) 有盾、蓝满、单体 5 格 → 挑最强的单体
        Check("单体远程 → 天上落焰", Decide(wiz, surround: 1, dist: 5, hasShield: true, mpPercent: 100) == Spell.MeteorStrike);

        // 2) 没盾 → 先补魔法盾（哪怕有更猛的输出技能）
        Check("没魔法盾先上盾", Decide(wiz, surround: 1, dist: 5, hasShield: false, mpPercent: 100) == Spell.MagicShield);
        Check("有盾不再重复上盾", Decide(wiz, surround: 1, dist: 5, hasShield: true, mpPercent: 100) != Spell.MagicShield);
        Check("蓝充足且没深延术 → 先开深延术", Decide(wiz, surround: 1, dist: 5, hasShield: true, mpPercent: 100, hasBoost: false) == Spell.MagicBooster);

        // 3) 贴身 1 格 → 抗拒火环推开
        Check("贴身先推开", Decide(wiz, surround: 1, dist: 1, hasShield: true, mpPercent: 100) == Spell.Repulsion);

        // 4) 被 3 个围住 → 群攻
        Spell aoe = Decide(wiz, surround: 3, dist: 3, hasShield: true, mpPercent: 100);
        Check("被围先群攻:" + aoe, aoe == Spell.Blizzard || aoe == Spell.FlameField || aoe == Spell.MeteorStrike || aoe == Spell.ThunderStorm);

        // 5) 地狱雷光：活人目标不放
        Check("活人目标不放地狱雷光", Decide(wiz, surround: 3, dist: 3, hasShield: true, mpPercent: 100, undead: false) != Spell.ThunderStorm);

        // 6) 蓝只够最便宜的 → 退回火球，而不是硬放贵的（放不出来就放不出来，不能空转）
        var twoSpells = new List<UserMagic>
        {
            M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9, cost: 3, levelCost: 3),
            M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9, cost: 115, levelCost: 115),
        };
        Spell cheap = Decide(twoSpells, surround: 1, dist: 5, hasShield: true, mpPercent: 10);
        Check("蓝不够时退回便宜技能:" + cheap, cheap == Spell.FireBall);

        // 7) 蓝空 → 什么都放不出来（走雷电兜底），而不是乱放
        Check("蓝空时不放技能", Decide(wiz, surround: 1, dist: 5, hasShield: true, mpPercent: 0) == Spell.None);

        // 8) 距离 1 但没有贴身敌人（只有远处一个）→ 不出抗拒火环
        Check("没贴身敌人不推", Decide(wiz, surround: 0, dist: 1, hasShield: true, mpPercent: 100) != Spell.Repulsion);

        // 9) 深延术：蓝不到八成时不浪费
        Check("蓝不到八成不开深延术", Decide(wiz, surround: 1, dist: 5, hasShield: true, mpPercent: 60, hasBoost: false) != Spell.MagicBooster);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 与英雄共用一套判断 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void InteractionTests()
    {
        Section("与英雄的协同");

        // 分身和英雄必须用同一套分类：法师技能在两边都能认出来
        Check("英雄也认为魔法盾是buff", HeroSpellBook.Get(Spell.MagicShield)?.Kind == HeroSpellKind.Buff);
        Check("英雄也认为火龙气焰是群攻", HeroSpellBook.Get(Spell.FlameField)?.Kind == HeroSpellKind.Aoe);
        Check("英雄也认为天上落焰是单体", HeroSpellBook.Get(Spell.MeteorStrike)?.Kind == HeroSpellKind.Single);

        // 分身没有独立快捷栏：即使主人给技能设了 F1-F8 键，分身也只按强弱选
        var keyed = new List<UserMagic>
        {
            M(Spell.FireBall, key: 17, level3: 11, power: 2, mpower: 8, range: 9),
            M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9),
        };
        List<UserMagic> ranked = CloneSkillBook.Rank(keyed, HeroSpellKind.Single, 0, 5, HasBuffNo, CanPayYes, NeverCooling);
        Check("分身按强弱排序也认快捷键", ranked.Count >= 1);

        // 评分单调性：学习等级越高分越高（保证「学了新的自动顶上」）
        var low = M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9);
        var high = M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9);
        Check("高级技能评分高于低级", HeroSpellBook.Score(high) > HeroSpellBook.Score(low));
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 工具 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝

    /// <summary>按 HumanWizard.TryCast 的顺序算最终会放哪个技能；None = 一个都放不出来。</summary>
    private static Spell Decide(List<UserMagic> magics, int surround, int dist, bool hasShield, int mpPercent,
        bool hasBoost = true, bool undead = false)
    {
        bool HasBuff(BuffType t) => t == BuffType.魔法盾 && hasShield;
        bool CanPay(UserMagic m) => mpPercent >= CostPercent(m);

        // 1) 魔法盾
        if (!hasShield)
        {
            var shield = magics.FirstOrDefault(m => m.Spell == Spell.MagicShield);
            if (shield != null && CanPay(shield)) return Spell.MagicShield;
        }

        // 2) 深延术（蓝到八成以上才开）
        if (!hasBoost && mpPercent >= 80)
        {
            var boost = magics.FirstOrDefault(m => m.Spell == Spell.MagicBooster);
            if (boost != null && mpPercent >= CostPercent(boost)) return Spell.MagicBooster;
        }

        // 3) 贴身控制
        if (surround >= 1 && dist <= 1)
        {
            var control = CloneSkillBook.Rank(magics, HeroSpellKind.Control, surround, dist, HasBuff, CanPay, NeverCooling);
            if (control.Count > 0) return control[0].Spell;
        }

        // 4) 群攻
        if (surround >= 2)
        {
            foreach (var m in CloneSkillBook.Rank(magics, HeroSpellKind.Aoe, surround, dist, HasBuff, CanPay, NeverCooling))
            {
                CloneSkillInfo info = CloneSkillBook.Get(m.Spell);
                if (info != null && info.UndeadOnly && !undead) continue;
                return m.Spell;
            }
        }

        // 5) 单体
        foreach (var m in CloneSkillBook.Rank(magics, HeroSpellKind.Single, surround, dist, HasBuff, CanPay, NeverCooling))
        {
            CloneSkillInfo info = CloneSkillBook.Get(m.Spell);
            if (info != null && info.UndeadOnly && !undead) continue;
            return m.Spell;
        }

        return Spell.None;
    }

    /// <summary>该技能耗蓝占「一个 800 蓝法师」的百分比（含 Setup.ini 默认 30% 折扣），用来模拟不同蓝量。</summary>
    private static int CostPercent(UserMagic m)
    {
        const int fakeMaxMp = 800;
        const int mpCostPercent = 30;
        int cost = m.Info.BaseCost + m.Level * m.Info.LevelCost;
        return (int)Math.Ceiling(cost * mpCostPercent / 100.0 * 100.0 / fakeMaxMp);
    }

    private static bool HasBuffNo(BuffType t) => false;
    private static bool CanPayYes(UserMagic m) => true;
    private static bool NeverCooling(Spell s) => false;

    /// <summary>一个「学全了法师技能」的样本，数值取真实技能表。</summary>
    private static List<UserMagic> FullWizard()
    {
        return new List<UserMagic>
        {
            M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9, cost: 3, levelCost: 3),
            M(Spell.GreatFireBall, level3: 21, power: 10, mpower: 6, range: 9, cost: 5, levelCost: 5),
            M(Spell.GreatFireBallRare, level3: 65, power: 18, mpower: 15, range: 9, cost: 5, levelCost: 5),
            M(Spell.ThunderBolt, level3: 23, power: 9, mpower: 8, range: 9, cost: 9, levelCost: 9),
            M(Spell.ThunderBoltRare, level3: 95, power: 9, mpower: 8, range: 9, cost: 25, levelCost: 5, delay: 8000),
            M(Spell.FrostCrunch, level3: 33, power: 12, mpower: 12, range: 9, cost: 15, levelCost: 15),
            M(Spell.FlameDisruptor, level3: 42, power: 9, mpower: 15, range: 9, cost: 28, levelCost: 28),
            M(Spell.FireBang, level3: 28, power: 8, mpower: 8, range: 9, cost: 14, levelCost: 14),
            M(Spell.IceStorm, level3: 40, power: 14, mpower: 12, range: 9, cost: 33, levelCost: 33),
            M(Spell.ThunderStorm, level3: 34, power: 10, mpower: 10, range: 0, cost: 29, levelCost: 29),
            M(Spell.FlameField, level3: 45, power: 25, mpower: 100, range: 9, cost: 45, levelCost: 45),
            M(Spell.Blizzard, level3: 50, power: 20, mpower: 30, range: 9, cost: 65, levelCost: 65),
            M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9, cost: 115, levelCost: 115),
            M(Spell.IceThrust, level3: 59, power: 50, mpower: 100, range: 0, cost: 100, levelCost: 100),
            M(Spell.HellFire, level3: 24, power: 6, mpower: 14, range: 0, cost: 10, levelCost: 10),
            M(Spell.Lightning, level3: 32, power: 12, mpower: 12, range: 0, cost: 38, levelCost: 38),
            M(Spell.Repulsion, level3: 19, range: 0, cost: 2, levelCost: 2),
            M(Spell.MagicShield, level3: 38, range: 0, cost: 35, levelCost: 35),
            M(Spell.MagicBooster, level3: 52, power: 1, mpower: 9, range: 0, cost: 150, levelCost: 150),
        };
    }

    private static UserMagic M(Spell spell, byte level = 3, byte key = 0, int level3 = 0,
        int power = 0, int mpower = 0, byte range = 0, uint delay = 1800, int cost = 0, int levelCost = 0)
    {
        return new UserMagic(spell)
        {
            Level = level,
            Key = key,
            Info = new MagicInfo
            {
                Spell = spell,
                Name = spell.ToString(),
                Level1 = (byte)level3,
                Level2 = (byte)level3,
                Level3 = (byte)level3,
                PowerBase = (ushort)power,
                MPowerBase = (ushort)mpower,
                Range = range,
                DelayBase = delay,
                BaseCost = (byte)cost,
                LevelCost = (byte)levelCost,
                MultiplierBase = 1f,
            }
        };
    }

    private static void Check(string name, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine($"  [OK]   {name}"); }
        else { _fail++; Console.WriteLine($"  [FAIL] {name}"); }
    }

    private static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"-- {name} --");
    }
}
