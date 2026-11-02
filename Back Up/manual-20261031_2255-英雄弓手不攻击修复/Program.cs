using System.Text;
using Server.MirDatabase;
using Server.MirObjects;

namespace HeroBrainTest;

/// <summary>
/// 英雄技能大脑（HeroSpellBook + HeroObject.TryCast 的选技能逻辑）回归测试。
/// 纯逻辑测试，不需要服务端数据库、不需要起服务端。
/// </summary>
internal static class Program
{
    private static int _pass;
    private static int _fail;

    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Console.WriteLine("=== 英雄技能大脑测试 ===");

        CatalogTests();
        ImplementationTests();
        RankingTests();
        FilterTests();
        ScenarioTests();

        Console.WriteLine();
        Console.WriteLine($"通过 {_pass} 项，失败 {_fail} 项");
        return _fail == 0 ? 0 : 1;
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 目录完整性 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void CatalogTests()
    {
        Section("技能目录");

        Check("战士-空破闪已登记", HeroSpellBook.Get(Spell.BladeAvalanche)?.Kind == HeroSpellKind.Aoe);
        Check("战士-时空剑已登记", HeroSpellBook.Get(Spell.DimensionalSword)?.Kind == HeroSpellKind.Aoe);
        Check("战士-金刚不坏是 buff", HeroSpellBook.Get(Spell.ImmortalSkin)?.Kind == HeroSpellKind.Buff);
        Check("战士-烈火剑法是开关", HeroSpellBook.Get(Spell.FlamingSword)?.Kind == HeroSpellKind.Toggle);

        Check("法师-天上落焰已登记", HeroSpellBook.Get(Spell.MeteorStrike)?.Kind == HeroSpellKind.Single);
        Check("法师-冰焰术已登记", HeroSpellBook.Get(Spell.IceThrust)?.Kind == HeroSpellKind.Single);
        Check("法师-火龙气焰是群攻", HeroSpellBook.Get(Spell.FlameField)?.Kind == HeroSpellKind.Aoe);
        Check("法师-天霜冰环是群攻", HeroSpellBook.Get(Spell.Blizzard)?.Kind == HeroSpellKind.Aoe);

        Check("道士-万效符是 buff", HeroSpellBook.Get(Spell.GreatTalisman)?.Kind == HeroSpellKind.Buff);
        Check("道士-先天气功是 buff", HeroSpellBook.Get(Spell.EnergyShield)?.Kind == HeroSpellKind.Buff);
        Check("道士-灵魂火符已登记", HeroSpellBook.Get(Spell.SoulFireBall)?.Kind == HeroSpellKind.Single);

        Check("刺客-影华连击已登记", HeroSpellBook.Get(Spell.ShadowCombo)?.Kind == HeroSpellKind.Single);
        Check("刺客-月华乱舞是群攻", HeroSpellBook.Get(Spell.CrescentSlash)?.Kind == HeroSpellKind.Aoe);

        Check("弓手-血龙闪秘笈按自身群攻登记", HeroSpellBook.Get(Spell.OneWithNature)?.Kind == HeroSpellKind.Aoe);
        Check("弓手-落雷杀已登记", HeroSpellBook.Get(Spell.SkyThunderShot)?.Kind == HeroSpellKind.Single);

        // 有条件才能放的技能绝不能进目录，否则英雄会一直空放
        Check("灭杀未登记（需灭杀窗口 buff）", !HeroSpellBook.IsKnown(Spell.DeathBlow));
        Check("圣言术未登记（只对不死系）", !HeroSpellBook.IsKnown(Spell.TurnUndead));
        Check("施毒术未登记（需毒粉，道士显式处理）", !HeroSpellBook.IsKnown(Spell.Poisoning));
        //★10-31 弓手英雄「站着不打」的元凶：精神状态只能走 SpellToggle，走 Magic() 是空转
        Check("精神状态未登记（Magic() 没有这个分支）", !HeroSpellBook.IsKnown(Spell.MentalState));
        Check("血风击未登记（被动触发，不是主动施法）", !HeroSpellBook.IsKnown(Spell.Hemorrhage));

        // 关键 buff 判重映射
        Check("魔法盾 buff 映射", HeroSpellBook.Get(Spell.MagicShield)?.Buff == BuffType.魔法盾);
        Check("幽灵盾 buff 映射", HeroSpellBook.Get(Spell.SoulShield)?.Buff == BuffType.幽灵盾);
        Check("体迅风 buff 映射", HeroSpellBook.Get(Spell.Haste)?.Buff == BuffType.体迅风);
        Check("气流术 buff 映射", HeroSpellBook.Get(Spell.Concentration)?.Buff == BuffType.气流术);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝======== 实现对齐 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝========
    /// <summary>
    /// 排雷：HeroSpellBook 里登记的技能，服务端必须真的有施法分支。
    /// 没有分支的技能会让 AI 每 tick 都重新挑中它 → ProcessAI 一直 return → 英雄站在原地一箭不放。
    /// 这条是静态检查（读 HumanObject.cs 源码），能提前抓到「登记了但忘了实现」。
    /// </summary>
    private static void ImplementationTests()
    {
        Section("技能实现对齐");

        string repo = FindRepoRoot();
        if (repo == null)
        {
            Check("找到源码目录", false);
            return;
        }

        string path = Path.Combine(repo, "Server", "MirObjects", "HumanObject.cs");
        HashSet<string> magicCases = ExtractCases(path, "public void Magic(Spell spell");
        HashSet<string> toggleCases = ExtractCases(path, "public void SpellToggle(Spell spell");

        Check("解析到 Magic() 的 case 分支", magicCases.Count > 100);
        Check("解析到 SpellToggle() 的 case 分支", toggleCases.Count >= 4);

        var magicOnly = new List<string>();
        var toggleOnly = new List<string>();

        foreach (Spell spell in HeroSpellBook.AllSpells)
        {
            HeroSpellProfile p = HeroSpellBook.Get(spell);
            if (p == null) continue;

            string name = spell.ToString();

            if (p.Kind == HeroSpellKind.Toggle)
            {
                if (!toggleCases.Contains(name)) toggleOnly.Add(name);
            }
            else
            {
                if (!magicCases.Contains(name)) magicOnly.Add(name);
            }
        }

        Check("所有攻击/增益技能都有 Magic() 分支" + (magicOnly.Count > 0 ? " 缺:" + string.Join(",", magicOnly) : ""), magicOnly.Count == 0);
        Check("所有开关技能都有 SpellToggle() 分支" + (toggleOnly.Count > 0 ? " 缺:" + string.Join(",", toggleOnly) : ""), toggleOnly.Count == 0);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Server", "MirObjects", "HumanObject.cs"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>从源码里抽出某个方法体内的 case Spell.XXX: 名单（大括号计数找方法结束）。</summary>
    private static HashSet<string> ExtractCases(string path, string signature)
    {
        var set = new HashSet<string>();
        if (!File.Exists(path)) return set;

        string[] lines = File.ReadAllLines(path);
        int start = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(signature)) { start = i; break; }
        }
        if (start < 0) return set;

        int depth = 0;
        bool began = false;

        for (int i = start; i < lines.Length; i++)
        {
            depth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
            if (lines[i].Contains('{')) began = true;

            var m = System.Text.RegularExpressions.Regex.Match(lines[i], @"case Spell\.(\w+):");
            if (m.Success) set.Add(m.Groups[1].Value);

            if (began && depth == 0) break;
        }

        return set;
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 排序规则 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void RankingTests()
    {
        Section("排序规则");

        Check("Key=17 归一化成 1", HeroSpellBook.KeyRank(M(Spell.FireBall, key: 17)) == 1);
        Check("Key=24 归一化成 8", HeroSpellBook.KeyRank(M(Spell.FireBall, key: 24)) == 8);
        Check("没设键排最后", HeroSpellBook.KeyRank(M(Spell.FireBall)) == 1000);

        // 学习等级高的排前面（这就是「学了高级技能自动顶上」）
        var fireBall = M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9);
        var thunder = M(Spell.ThunderBolt, level3: 23, power: 9, mpower: 8, range: 9);
        var fireBallRare = M(Spell.GreatFireBallRare, level3: 65, power: 18, mpower: 15, range: 9);

        var list = Rank(new[] { fireBall, thunder, fireBallRare }, HeroSpellKind.Single, 0, 5);
        Check("高级技能排低级的之前", list.Count == 3 && list[0].Spell == Spell.GreatFireBallRare);
        Check("低级技能排最后", list[^1].Spell == Spell.FireBall);

        // 快捷键优先于一切（哪怕等级低）
        var keyed = M(Spell.FireBall, key: 18, level3: 11, power: 2, mpower: 8, range: 9);
        list = Rank(new[] { fireBallRare, keyed }, HeroSpellKind.Single, 0, 5);
        Check("设了快捷键的低级技能排最前", list.Count == 2 && list[0].Spell == Spell.FireBall);

        // 快捷键按 1→8 顺序
        var k1 = M(Spell.ThunderBolt, key: 20, level3: 23, range: 9);   // 英雄技能栏 4
        var k2 = M(Spell.FireBall, key: 17, level3: 11, range: 9);      // 英雄技能栏 1
        list = Rank(new[] { k1, k2 }, HeroSpellKind.Single, 0, 5);
        Check("快捷键按 1→8 顺序", list.Count == 2 && list[0].Spell == Spell.FireBall);

        // AutoSkill 关闭 = 只放设过键的
        list = HeroSpellBook.Rank(new[] { fireBallRare, keyed }, HeroSpellKind.Single, 0, 5,
            b => false, s => false, autoSkill: false);
        Check("AutoSkill 关闭时只留设过快捷键的", list.Count == 1 && list[0].Spell == Spell.FireBall);

        // 空 Info 不崩、不参与排序
        var broken = new UserMagic(Spell.FireBall);
        broken.Info = null;
        Check("Info 为 null 的技能被忽略", Rank(new[] { broken, fireBall }, HeroSpellKind.Single, 0, 1).Count == 1);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 过滤条件 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void FilterTests()
    {
        Section("过滤条件");

        var shield = M(Spell.MagicShield, level3: 38);

        Check("身上没有魔法盾 → 可以放",
            Rank(new[] { shield }, HeroSpellKind.Buff, 0, 0, buffs: new BuffType[0]).Count == 1);
        Check("身上已有魔法盾 → 不重复放",
            Rank(new[] { shield }, HeroSpellKind.Buff, 0, 0, buffs: new[] { BuffType.魔法盾 }).Count == 0);

        var flaming = M(Spell.FlamingSword, level3: 40);
        Check("烈火没开 → 可以开",
            Rank(new[] { flaming }, HeroSpellKind.Toggle, 0, 1, toggles: new Spell[0]).Count == 1);
        Check("烈火已开 → 绝不再点一次（点了就关）",
            Rank(new[] { flaming }, HeroSpellKind.Toggle, 0, 1, toggles: new[] { Spell.FlamingSword }).Count == 0);

        var wave = M(Spell.BladeAvalanche, level3: 43);
        Check("群攻：贴身 1 个不放", Rank(new[] { wave }, HeroSpellKind.Aoe, 1, 1).Count == 0);
        Check("群攻：贴身 2 个才放", Rank(new[] { wave }, HeroSpellKind.Aoe, 2, 1).Count == 1);

        var meteor = M(Spell.MeteorStrike, level3: 55, range: 9);
        Check("远程技能：9 格内可放", Rank(new[] { meteor }, HeroSpellKind.Single, 0, 9).Count == 1);
        Check("远程技能：超出射程不放", Rank(new[] { meteor }, HeroSpellKind.Single, 0, 12).Count == 0);

        var iceThrust = M(Spell.IceThrust, level3: 59, range: 0);
        Check("近身大招：贴身 2 格内可放", Rank(new[] { iceThrust }, HeroSpellKind.Single, 0, 2).Count == 1);
        Check("近身大招：离远了不放（不会让法师贴脸）", Rank(new[] { iceThrust }, HeroSpellKind.Single, 0, 5).Count == 0);

        // 挂了 debuff 的攻击技能，目标已经有了就不再放
        var curse = M(Spell.Curse, level3: 44, mpower: 20, range: 9);
        Check("诅咒术：目标没中诅咒 → 放",
            Rank(new[] { curse }, HeroSpellKind.Single, 0, 1, buffs: new BuffType[0]).Count == 1);
        Check("诅咒术：目标已中诅咒 → 不放",
            Rank(new[] { curse }, HeroSpellKind.Single, 0, 1, buffs: new[] { BuffType.诅咒术 }).Count == 0);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 实战场景 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static void ScenarioTests()
    {
        Section("实战场景");

        // ① 法师：只学了火球，后来学了雷电、灭天火、天上落焰、冰焰术 —— 远程应该用最强的
        var wiz = new[]
        {
            M(Spell.FireBall, level3: 11, power: 2, mpower: 8, range: 9),
            M(Spell.ThunderBolt, level3: 23, power: 9, mpower: 8, range: 9),
            M(Spell.FlameDisruptor, level3: 42, power: 9, mpower: 15, range: 9),
            M(Spell.MeteorStrike, level3: 55, power: 20, mpower: 40, range: 9),
            M(Spell.IceThrust, level3: 59, power: 50, mpower: 100, range: 0),
        };
        var picked = Rank(wiz, HeroSpellKind.Single, 0, 6);
        Check("法师远程单体不再只会火球", picked.Count > 0 && picked[0].Spell != Spell.FireBall);
        Check("法师远程单体选天上落焰（冰焰术够不着）", picked.Count > 0 && picked[0].Spell == Spell.MeteorStrike);

        picked = Rank(wiz, HeroSpellKind.Single, 0, 2);
        Check("法师贴身时冰焰术（最强近身）顶上", picked.Count > 0 && picked[0].Spell == Spell.IceThrust);

        // ② 法师被围：天霜冰环(50) 比 火龙气焰(45) 等级高，应选天霜冰环
        var aoes = new[]
        {
            M(Spell.FireBang, level3: 28, power: 8, mpower: 8, range: 9),
            M(Spell.FlameField, level3: 45, power: 25, mpower: 100, range: 9),
            M(Spell.Blizzard, level3: 50, power: 20, mpower: 30, range: 9),
        };
        picked = Rank(aoes, HeroSpellKind.Aoe, 3, 3);
        Check("法师被围选伤害最高的群攻（火龙气焰 mpow100）", picked.Count > 0 && picked[0].Spell == Spell.FlameField);

        // ③ 战士被围：时空剑(94) 优先于 空破闪(43)
        var war = new[]
        {
            M(Spell.BladeAvalanche, level3: 43),
            M(Spell.DimensionalSword, level3: 94, range: 2),
        };
        picked = Rank(war, HeroSpellKind.Aoe, 3, 1);
        Check("战士被围优先时空剑", picked.Count > 0 && picked[0].Spell == Spell.DimensionalSword);

        // ④ 道士给主人补 buff：主人已有幽灵盾，不该再上幽灵盾
        var taoistBuffs = new[]
        {
            M(Spell.SoulShield, level3: 26),
            M(Spell.BlessedArmour, level3: 29),
            M(Spell.UltimateEnhancer, level3: 38),
            M(Spell.EnergyShield, level3: 54),
            M(Spell.GreatTalisman, level3: 90),
        };
        picked = Rank(taoistBuffs, HeroSpellKind.Buff, 0, 3, buffs: new[] { BuffType.幽灵盾 });
        Check("主人已有幽灵盾 → 不重复上", picked.All(x => x.Spell != Spell.SoulShield));
        Check("道士优先上最高级的万效符", picked.Count > 0 && picked[0].Spell == Spell.GreatTalisman);

        // ⑤ 道士 buff 全满 → 一个都不放（不会每帧空放 buff）
        picked = Rank(taoistBuffs, HeroSpellKind.Buff, 0, 3, buffs: new[]
        {
            BuffType.幽灵盾, BuffType.神圣战甲术, BuffType.无极真气, BuffType.先天气功, BuffType.万效符,
        });
        Check("buff 全满 → 不再放任何增益", picked.Count == 0);

        // ⑥ 刺客已开风剑术 → Toggle 不再点
        var sin = new[] { M(Spell.DoubleSlash, level3: 19) };
        Check("刺客风剑术已开 → 不重复点",
            Rank(sin, HeroSpellKind.Toggle, 0, 1, toggles: new[] { Spell.DoubleSlash }).Count == 0);

        // ⑦ 弓手：血龙闪秘籍是「自身 5x5」，不能进单体候选（否则站远处放就是打空气）
        var archer = new[]
        {
            M(Spell.NapalmShot, level3: 55, power: 25, mpower: 25, range: 9),
            M(Spell.OneWithNature, level3: 56, power: 30, mpower: 75, range: 9),
        };
        picked = Rank(archer, HeroSpellKind.Single, 0, 5);
        Check("弓手单体候选里没有血龙闪秘籍", picked.All(x => x.Spell != Spell.OneWithNature));

        // 贴身有怪时它才作为群攻出场（自身 5x5 能真正打到人）
        Check("贴身有怪才用血龙闪秘籍", Rank(archer, HeroSpellKind.Aoe, 1, 1).Any(x => x.Spell == Spell.OneWithNature));
        Check("身边没怪不放血龙闪秘籍", Rank(archer, HeroSpellKind.Aoe, 0, 5).All(x => x.Spell != Spell.OneWithNature));

        // ⑧ 剑走偏锋：学了万效三连符的道士不该再惦记普通灵魂火符
        var taoistAttack = new[]
        {
            M(Spell.SoulFireBall, level3: 24, power: 3, mpower: 8, range: 9),
            M(Spell.SoulFireBallPlusRare, level3: 100, power: 4, mpower: 12, range: 9),
        };
        picked = Rank(taoistAttack, HeroSpellKind.Single, 0, 5);
        Check("道士万效灵魂火符顶掉普通火符", picked.Count > 0 && picked[0].Spell == Spell.SoulFireBallPlusRare);
    }

    //＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝ 辅助 ＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝
    private static List<UserMagic> Rank(IEnumerable<UserMagic> magics, HeroSpellKind kind, int surround, int distance,
        IEnumerable<BuffType> buffs = null, IEnumerable<Spell> toggles = null, bool autoSkill = true)
    {
        var buffSet = new HashSet<BuffType>(buffs ?? new BuffType[0]);
        var toggleSet = new HashSet<Spell>(toggles ?? new Spell[0]);

        return HeroSpellBook.Rank(magics, kind, surround, distance,
            b => buffSet.Contains(b), s => toggleSet.Contains(s), autoSkill);
    }

    private static UserMagic M(Spell spell, byte level = 0, byte key = 0, int level3 = 0,
        int power = 0, int mpower = 0, byte range = 0, uint delay = 1800)
    {
        var magic = new UserMagic(spell)
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
                MultiplierBase = 1f,
            }
        };

        return magic;
    }

    private static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"-- {name} --");
    }

    private static void Check(string name, bool ok)
    {
        if (ok)
        {
            _pass++;
            Console.WriteLine($"  [PASS] {name}");
        }
        else
        {
            _fail++;
            Console.WriteLine($"  [FAIL] {name}");
        }
    }
}
