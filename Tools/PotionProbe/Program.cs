using Server;
using Server.MirEnvir;
using Server.MirObjects;
using System.Text;

namespace PotionProbe;

/// <summary>
/// 只读探针：列出名字含"准确"的物品，以及所有 Shape=3（Buff 类药水），
/// 显示持久（=Buff 时长分钟数）和附加属性，用于核对药水时长设置。
/// 用法：放到服务端目录（与 Server.MirDB 同级）运行 PotionProbe.exe。只读不写库。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine("=== 名字含「准确」的物品 ===");
        foreach (var it in envir.ItemInfoList)
        {
            if (it == null || string.IsNullOrEmpty(it.Name)) continue;
            if (!it.Name.Contains("准确")) continue;
            Print(it);
        }

        Console.WriteLine();
        Console.WriteLine("=== Shape=3（Buff 类药水，全部） ===");
        foreach (var it in envir.ItemInfoList)
        {
            if (it == null) continue;
            if ((int)it.Shape != 3) continue;
            if ((byte)it.Type <= 20) continue;
            Print(it);
        }

        return 0;
    }

    private static void Print(ItemInfo it)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[{it.Index}] '{it.Name}' Type={it.Type} Shape={(int)it.Shape}");
        sb.Append($" 持久(Durability)={it.Durability}(Buff时长={it.Durability}分钟)");
        sb.Append($" StackSize={it.StackSize}");

        // 附加属性里有buff效果的键
        var keys = new[] { Stat.准确, Stat.敏捷, Stat.MaxDC, Stat.MaxMC, Stat.MaxSC, Stat.攻击速度, Stat.HP, Stat.MP, Stat.MaxAC, Stat.MaxMAC, Stat.背包负重, Stat.幸运 };
        var parts = new List<string>();
        foreach (var k in keys)
        {
            if (it.Stats != null && it.Stats[k] != 0)
                parts.Add($"{k}={it.Stats[k]}");
        }
        sb.Append(parts.Count > 0 ? " 属性[" + string.Join(",", parts) + "]" : " 属性[无]");
        Console.WriteLine(sb.ToString());
    }
}
