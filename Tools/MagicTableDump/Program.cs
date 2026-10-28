using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MagicTableDump;

/// <summary>
/// 只读导出技能表（MagicInfoList）全部数值，给「英雄技能评分」做设计依据。
/// 用法：在服务端目录（Server.MirDB 所在处）执行 MagicTableDump.exe [输出csv]
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
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

        string outPath = args.Length > 0 ? args[0] : "magic_table.csv";

        var sb = new StringBuilder();
        sb.AppendLine("Spell,Id,Name,Level1,Level2,Level3,Need1,Need2,Need3,BaseCost,LevelCost,DelayBase,DelayReduction,PowerBase,PowerBonus,MPowerBase,MPowerBonus,MultiplierBase,MultiplierBonus,Range");

        foreach (MagicInfo m in envir.MagicInfoList.OrderBy(x => (int)x.Spell))
        {
            sb.AppendLine(string.Join(",",
                m.Spell, (int)m.Spell, Csv(m.Name),
                m.Level1, m.Level2, m.Level3, m.Need1, m.Need2, m.Need3,
                m.BaseCost, m.LevelCost, m.DelayBase, m.DelayReduction,
                m.PowerBase, m.PowerBonus, m.MPowerBase, m.MPowerBonus,
                m.MultiplierBase.ToString("0.###"), m.MultiplierBonus.ToString("0.###"), m.Range));
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"已导出 {envir.MagicInfoList.Count} 条技能 -> {Path.GetFullPath(outPath)}");
        return 0;
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Contains(',') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
