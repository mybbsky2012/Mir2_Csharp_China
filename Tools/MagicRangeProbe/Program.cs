using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MagicRangeProbe;

/// <summary>
/// 只读工具：导出 Server.MirDB 中每个技能的 Range（施法/锁定距离，切比雪夫距离）、
/// DelayBase、DelayReduction，用来核对「客户端能不能锁到远处的怪」。
///
/// 关键语义：Range = 0 表示不校验距离；否则要求 |dx| &lt;= Range 且 |dy| &lt;= Range。
/// 客户端 ClientMagic.Range 与服务端 MagicInfo.Range 都来自这一格。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处）：
///     MagicRangeProbe [输出目录]
/// 产出：
///     magic_range.csv
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        string outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".";
        var sb = new StringBuilder();
        sb.AppendLine("Index,Spell,Name,Range,DelayBase,DelayReduction");

        for (int i = 0; i < envir.MagicInfoList.Count; i++)
        {
            MagicInfo m = envir.MagicInfoList[i];
            if (m == null) continue;
            sb.AppendLine($"{i},{m.Spell},{m.Name},{m.Range},{m.DelayBase},{m.DelayReduction}");
        }

        string path = Path.Combine(outDir, "magic_range.csv");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));

        Console.WriteLine($"技能 {envir.MagicInfoList.Count} 个 → {Path.GetFullPath(path)}");
        Console.WriteLine($"地图 {envir.MapInfoList.Count} / 物品 {envir.ItemInfoList.Count} / 怪物 {envir.MonsterInfoList.Count} / NPC {envir.NPCInfoList.Count} / 任务 {envir.QuestInfoList.Count} / 商城 {envir.GameShopList.Count} / 配方 {envir.RecipeInfoList.Count}");

        // 万效符系列单独打一份到控制台，方便直接比对
        Spell[] focus =
        {
            Spell.SoulFireBall, Spell.SoulFireBallPlus, Spell.SoulFireBallPlusRare,
            Spell.GreatTalisman, Spell.GreatTalismanRare
        };

        Console.WriteLine("Spell,Name,Range");
        foreach (MagicInfo m in envir.MagicInfoList)
        {
            if (m == null || Array.IndexOf(focus, m.Spell) < 0) continue;
            Console.WriteLine($"{m.Spell},{m.Name},{m.Range}");
        }

        return 0;
    }
}
