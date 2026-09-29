using System.Text;
using Server;
using Server.MirEnvir;

namespace DbDump;

/// <summary>
/// 只读工具：把当前 Server.MirDB 里的技能表与物品表导出成 CSV，用于核对脚本里写的名字是否真实存在。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处）：
///     DbDump.exe
/// 产出：
///     magic_dump.csv   SpellIndex,SpellName,Name,Level1,Level2,Level3
///     item_dump.csv    Index,Name,Type,Grade,RequiredAmount,RequiredClass,RequiredGender
///
/// 注意：本工具只读，不会写回数据库。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出重定向时可能失败 */ }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}。");
            Console.WriteLine("请把本程序放到服务端目录（与 Server.exe 同级）再运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine($"物品 {envir.ItemInfoList.Count} 个 / 技能 {envir.MagicInfoList.Count} 个");

        string outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".";

        var magic = new StringBuilder();
        magic.AppendLine("SpellIndex,SpellName,Name,Level1,Level2,Level3");
        for (int i = 0; i < envir.MagicInfoList.Count; i++)
        {
            var m = envir.MagicInfoList[i];
            if (m == null) continue;
            magic.AppendLine($"{i},{m.Spell},{m.Name},{m.Level1},{m.Level2},{m.Level3}");
        }
        string mp = Path.Combine(outDir, "magic_dump.csv");
        File.WriteAllText(mp, magic.ToString(), new UTF8Encoding(true));

        var item = new StringBuilder();
        item.AppendLine("Index,Name,Type,Grade,RequiredAmount,RequiredClass,RequiredGender,RequiredType");
        int n = 0;
        foreach (var it in envir.ItemInfoList)
        {
            if (it == null) continue;
            item.AppendLine($"{it.Index},{it.Name},{it.Type},{it.Grade},{it.RequiredAmount},{it.RequiredClass},{it.RequiredGender},{it.RequiredType}");
            n++;
        }
        string ip = Path.Combine(outDir, "item_dump.csv");
        File.WriteAllText(ip, item.ToString(), new UTF8Encoding(true));

        Console.WriteLine($"已写出 {Path.GetFullPath(mp)} （{envir.MagicInfoList.Count} 行）");
        Console.WriteLine($"已写出 {Path.GetFullPath(ip)} （{n} 行）");
        return 0;
    }
}
