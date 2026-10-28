using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using Server.MirObjects.Monsters;

namespace CloneProbe;

/// <summary>
/// 只读探针：用真实数据库的技能数值，模拟「法师分身学全技能」时在各个场景会怎么选技能。
/// 用法：把本程序放到服务端目录（与 Server.MirDB 同级）运行 CloneProbe.exe
/// 不会修改任何数据。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine("请在服务端目录（与 Server.MirDB 同级）运行。");
            return 2;
        }

        Settings.Load();
        // 服务端是在 StartGame 里把 BuffInfo.Load() 灌进 BuffInfoList 的，探针这里手工补上

        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("!! LoadDB 失败"); return 2; }

        envir.BuffInfoList.Clear();
        envir.BuffInfoList.AddRange(BuffInfo.Load());

        Console.WriteLine("=== 分身怪物定义 ===");
        DumpMonster(envir, Settings.CloneName);

        SmokeTest(envir);

        Console.WriteLine();
        Console.WriteLine("=== 分身要用的 buff 是否已登记 ===");
        foreach (var t in new[] { BuffType.魔法盾, BuffType.深延术 })
        {
            bool found = false;
            if (envir.BuffInfoList != null)
            {
                for (int i = 0; i < envir.BuffInfoList.Count; i++)
                {
                    if (envir.BuffInfoList[i].Type == t) { found = true; break; }
                }
            }
            Console.WriteLine($"  {t} : {(found ? "已登记" : "未登记（分身不会放这条 buff）")}");
        }

        // 学全分身技能表里的技能（等级 3）
        var magics = new List<UserMagic>();
        foreach (Spell spell in CloneSkillBook.Known)
        {
            var magic = new UserMagic(spell) { Level = 3 };
            if (magic.Info == null) continue;
            magics.Add(magic);
        }
        Console.WriteLine();
        Console.WriteLine($"=== 参与模拟的技能 {magics.Count} 个（等级 3）===");
        foreach (var m in magics.OrderBy(x => (byte)x.Spell))
        {
            Console.WriteLine($"  {(byte)m.Spell,3} {m.Info.Name,-8} 耗蓝{SkillCost(m),5} 冷却{m.GetDelay(),6}ms {DescribePower(m)}");
        }

        Console.WriteLine();
        Console.WriteLine("=== 场景模拟（Setup.ini CloneSkillMpCost = " + Settings.CloneSkillMpCost + "%）===");
        foreach (int maxMp in new[] { 300, 800, 2000 })
        {
            Console.WriteLine();
            Console.WriteLine($"-- 主人最大法力 {maxMp} --");

            Simulate(magics, maxMp, "单体5格", surround: 0, dist: 5, hasShield: true, hasBoost: true, undead: false, mpPercent: 100);
            Simulate(magics, maxMp, "单体5格(没盾)", surround: 0, dist: 5, hasShield: false, hasBoost: true, undead: false, mpPercent: 100);
            Simulate(magics, maxMp, "贴身1格", surround: 1, dist: 1, hasShield: true, hasBoost: true, undead: false, mpPercent: 100);
            Simulate(magics, maxMp, "围3个", surround: 3, dist: 3, hasShield: true, hasBoost: true, undead: false, mpPercent: 100);
            Simulate(magics, maxMp, "围3个(不死系)", surround: 3, dist: 3, hasShield: true, hasBoost: true, undead: true, mpPercent: 100);
            Simulate(magics, maxMp, "蓝只剩20%", surround: 0, dist: 5, hasShield: true, hasBoost: true, undead: false, mpPercent: 20);
            Simulate(magics, maxMp, "蓝只剩5%", surround: 0, dist: 5, hasShield: true, hasBoost: true, undead: false, mpPercent: 5);
            Simulate(magics, maxMp, "蓝空", surround: 0, dist: 5, hasShield: true, hasBoost: true, undead: false, mpPercent: 0);

            // 连续 10 次单体：看是不是会在同档技能之间切换
            var rankedSingle = CloneSkillBook.Rank(magics, HeroSpellKind.Single, 0, 5, _ => true, _ => true, _ => false);
            var seq = new List<string>();
            var counter = new Dictionary<string, int>();
            var rng = new Random(12345);
            Spell last = Spell.None;
            for (int i = 0; i < 2000; i++)
            {
                UserMagic pick = CloneSkillBook.PickVaried(rankedSingle, last, rng.Next(1000));
                if (pick == null) break;
                if (i < 10) seq.Add(pick.Info.Name);
                last = pick.Spell;
                counter[pick.Info.Name] = counter.TryGetValue(pick.Info.Name, out int c) ? c + 1 : 1;
            }
            string dist = string.Join(" ", counter.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}{kv.Value * 100 / 2000}%"));
            Console.WriteLine($"  {"连放10次",-14} → {string.Join(" > ", seq)}");
            Console.WriteLine($"  {"2000次分布",-14} → {dist}");
        }

        return 0;
    }

    /// <summary>冒烟：真正把分身怪造出来，验证 AI 映射与 buff 机制不会抛异常。</summary>
    private static void SmokeTest(Envir envir)
    {
        Console.WriteLine();
        Console.WriteLine("=== 冒烟测试（真的 new 一个分身出来）===");

        var mi = envir.GetMonsterInfo(Settings.CloneName);
        if (mi == null) { Console.WriteLine("  !! 找不到分身怪物定义"); return; }

        MonsterObject monster = MonsterObject.GetMonster(mi);
        if (monster == null) { Console.WriteLine("  !! GetMonster 返回 null"); return; }

        Console.WriteLine($"  AI 985 → {monster.GetType().FullName}");
        Console.WriteLine($"  AttackRange={((Server.MirObjects.Monsters.HumanWizard)monster).AttackRange}");

        int beforeMin = monster.Stats[Stat.MinMC];
        int beforeMax = monster.Stats[Stat.MaxMC];

        try
        {
            monster.AddBuff(BuffType.深延术, monster, 60000, new Stats { [Stat.MinMC] = 12, [Stat.MaxMC] = 12 });
            Console.WriteLine($"  深延术生效: MC {beforeMin}-{beforeMax} → {monster.Stats[Stat.MinMC]}-{monster.Stats[Stat.MaxMC]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  !! AddBuff(深延术) 抛异常: {ex.GetType().Name} {ex.Message}");
        }

        try
        {
            monster.AddBuff(BuffType.魔法盾, monster, 60000, new Stats { [Stat.伤害降低数率] = 30 });
            Console.WriteLine($"  魔法盾生效: 伤害降低={monster.Stats[Stat.伤害降低数率]}%");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  !! AddBuff(魔法盾) 抛异常: {ex.GetType().Name} {ex.Message}");
        }
    }

    private static void DumpMonster(Envir envir, string name)
    {
        var mi = envir.GetMonsterInfo(name);
        if (mi == null) { Console.WriteLine($"{name}: 未找到"); return; }

        Console.WriteLine($"{name} (Index {mi.Index}, AI {mi.AI}) Level={mi.Level} HP={mi.Stats[Stat.HP]} MC={mi.Stats[Stat.MinMC]}-{mi.Stats[Stat.MaxMC]} AttackSpeed={mi.AttackSpeed}");
    }

    private static void Simulate(List<UserMagic> magics, int maxMp, string title, int surround, int dist,
        bool hasShield, bool hasBoost, bool undead, int mpPercent)
    {
        int mp = maxMp * mpPercent / 100;

        bool HasBuff(BuffType t)
        {
            if (t == BuffType.魔法盾) return hasShield;
            if (t == BuffType.深延术) return hasBoost;
            return false;
        }

        bool CanPay(UserMagic m) => mp >= SkillCost(m);

        // 与 HumanWizard.TryCast 同序
        if (!hasShield)
        {
            var shield = magics.FirstOrDefault(m => m.Spell == Spell.MagicShield);
            if (shield != null && CanPay(shield)) { Report(title, mp, maxMp, shield); return; }
        }

        if (!hasBoost && mpPercent >= 80)
        {
            var boost = magics.FirstOrDefault(m => m.Spell == Spell.MagicBooster);
            if (boost != null && CanPay(boost)) { Report(title, mp, maxMp, boost); return; }
        }

        if (surround >= 1 && dist <= 1)
        {
            var control = CloneSkillBook.Rank(magics, HeroSpellKind.Control, surround, dist, HasBuff, CanPay, _ => false);
            if (control.Count > 0) { Report(title, mp, maxMp, control[0]); return; }
        }

        if (surround >= 2)
        {
            foreach (var m in CloneSkillBook.Rank(magics, HeroSpellKind.Aoe, surround, dist, HasBuff, CanPay, _ => false))
            {
                CloneSkillInfo info = CloneSkillBook.Get(m.Spell);
                if (info != null && info.UndeadOnly && !undead) continue;
                Report(title, mp, maxMp, m);
                return;
            }
        }

        foreach (var m in CloneSkillBook.Rank(magics, HeroSpellKind.Single, surround, dist, HasBuff, CanPay, _ => false))
        {
            CloneSkillInfo info = CloneSkillBook.Get(m.Spell);
            if (info != null && info.UndeadOnly && !undead) continue;
            Report(title, mp, maxMp, m);
            return;
        }

        Console.WriteLine($"  {title,-14} → 放不出来（雷电术兜底）   [蓝 {mp}/{maxMp}]");
    }

    private static void Report(string title, int mp, int maxMp, UserMagic magic)
    {
        Console.WriteLine($"  {title,-14} → {magic.Info.Name,-8} 耗蓝{SkillCost(magic),5}  [蓝 {mp}/{maxMp}]");
    }

    private static int SkillCost(UserMagic magic)
    {
        int baseCost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
        int percent = Settings.CloneSkillMpCost;
        if (percent <= 0) return 0;
        return (int)Math.Ceiling(baseCost * percent / 100.0);
    }

    private static string DescribePower(UserMagic m)
    {
        MagicInfo info = m.Info;
        return $"pow{info.PowerBase}+{info.PowerBonus} mpow{info.MPowerBase}+{info.MPowerBonus} mult{info.MultiplierBase}";
    }
}
