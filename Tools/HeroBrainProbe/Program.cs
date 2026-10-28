using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace HeroBrainProbe;

/// <summary>
/// 用真实数据库的技能表，模拟「学全技能、技能满级」的英雄在各场景会选什么技能。
/// 只读，不改库。用法：在服务端目录（Server.MirDB 所在处）运行。
/// </summary>
internal static class Program
{
    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        Settings.Load();

        Envir envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        // 技能按职业分组（Spell 枚举值区间）
        var classes = new (string Name, int[] Ranges)[]
        {
            ("战士", new[] { 1, 22 }),
            ("法师", new[] { 31, 58 }),
            ("道士", new[] { 61, 88 }),
            ("刺客", new[] { 91, 107 }),
            ("弓手", new[] { 121, 141 }),
            ("新增技能", new[] { 108, 120 }),
        };
        var extras = new[] { 142, 143, 144, 145, 146 };   // 新技能里散落的弓手/刺客技能

        foreach (var cls in classes)
        {
            List<UserMagic> magics = BuildMagics(envir, cls.Ranges, cls.Name == "新增技能" ? extras : null);
            if (magics.Count == 0) continue;

            Console.WriteLine();
            Console.WriteLine($"===== {cls.Name}（已学 {magics.Count} 个技能，全部满级）=====");

            Dump("开强化(先手)", magics, HeroSpellKind.Toggle, 1, 1, null);
            Dump("增益/防护", magics, HeroSpellKind.Buff, 0, 3, null);
            Dump("单体-贴身(1格)", magics, HeroSpellKind.Single, 0, 1, null);
            Dump("单体-中距(5格)", magics, HeroSpellKind.Single, 0, 5, null);
            Dump("群攻-被围3个", magics, HeroSpellKind.Aoe, 3, 1, null);
            Dump("控制", magics, HeroSpellKind.Control, 3, 1, null);
        }

        Console.WriteLine();
        Console.WriteLine("===== 快捷键优先自检（法师：火球设 F1，天上落焰不设键）=====");
        {
            var wiz = BuildMagics(envir, new[] { 31, 58 }, null);
            UserMagic fb = wiz.FirstOrDefault(x => x.Spell == Spell.FireBall);
            if (fb != null) fb.Key = 17;
            Dump("单体-中距(5格)", wiz, HeroSpellKind.Single, 0, 5, null);
        }

        return 0;
    }

    private static List<UserMagic> BuildMagics(Envir envir, int[] range, int[] extras = null)
    {
        var list = new List<UserMagic>();

        foreach (MagicInfo info in envir.MagicInfoList)
        {
            int id = (int)info.Spell;
            bool inRange = id >= range[0] && id <= range[1];
            bool inExtras = extras != null && extras.Contains(id);
            if (!inRange && !inExtras) continue;
            if (!HeroSpellBook.IsKnown(info.Spell)) continue;

            var magic = new UserMagic(info.Spell) { Level = 3, Info = info };
            list.Add(magic);
        }

        return list;
    }

    private static void Dump(string title, List<UserMagic> magics, HeroSpellKind kind, int surround, int distance,
        IEnumerable<BuffType> buffs)
    {
        var buffSet = new HashSet<BuffType>(buffs ?? new BuffType[0]);
        var ranked = HeroSpellBook.Rank(magics, kind, surround, distance,
            b => buffSet.Contains(b), s => false, autoSkill: true);

        string top = ranked.Count == 0
            ? "（没有可用技能）"
            : string.Join(" > ", ranked.Take(4).Select(x => $"{x.Info?.Name ?? x.Spell.ToString()}" +
                $"[L{(int)(x.Info?.Level3 ?? 0)}/{(int)(x.Info?.Range ?? 0)}格/S{HeroSpellBook.Score(x):0}]"));

        Console.WriteLine($"  {title,-16}: {top}");
    }
}
