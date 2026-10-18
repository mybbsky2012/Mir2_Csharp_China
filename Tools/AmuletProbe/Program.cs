using System.Text;
using Server;
using Server.MirEnvir;

namespace AmuletProbe;

/// <summary>
/// 只读探针：列出物品库里所有「护身符类」物品（护身符 Shape=0 / 毒粉 Shape=1,2），
/// 以及假人装备方案当前写的物品名能否在库里找到。只读，不写库。
/// 用法：在服务端目录（与 Server.MirDB 同级）运行 AmuletProbe.exe
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

        Console.WriteLine($"物品总数 {envir.ItemInfoList.Count}");

        Console.WriteLine();
        Console.WriteLine("=== 护身符类物品（ItemType.护身符） ===");
        Console.WriteLine("Shape=0 → 护身符(符纸)；Shape=1 → 灰色毒粉；Shape=2 → 黄色毒粉");
        Console.WriteLine("Index\tShape\tStack\tWeight\tReqClass\tReqGender\tReq\tReqAmt\tName");

        foreach (var info in envir.ItemInfoList)
        {
            if (info == null || info.Type != ItemType.护身符) continue;
            Console.WriteLine($"{info.Index}\t{info.Shape}\t{info.StackSize}\t{info.Weight}\t{(byte)info.RequiredClass}\t{(byte)info.RequiredGender}\t{info.RequiredType}\t{info.RequiredAmount}\t{info.Name}");
        }

        Console.WriteLine();
        Console.WriteLine("=== 假人装备方案 FakePlayerEquip.txt 里的护身符/毒粉行 ===");

        string path = Settings.FakePlayerEquipFile;
        if (!Path.IsPathRooted(path)) path = Path.Combine(Settings.ConfigPath, path);

        Console.WriteLine("文件: " + path);

        if (!File.Exists(path))
        {
            Console.WriteLine("(文件不存在)");
            return 0;
        }

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;
            if (!line.Contains("护身符") && !line.Contains("毒")) continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string name = line.Substring(eq + 1).Trim();
            var found = envir.GetItemInfo(name);

            Console.WriteLine($"  {line}  →  {(found == null ? "★库里找不到这个物品名" : $"OK  Type={found.Type} Shape={found.Shape} Stack={found.StackSize}")}");
        }

        return 0;
    }
}
