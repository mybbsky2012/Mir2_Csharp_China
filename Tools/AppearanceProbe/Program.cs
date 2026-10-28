using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace AppearanceProbe;

/// <summary>
/// 只读：查「布衣 / 木剑 / 练功师 / 稻草人」这些外观在数据库里的真实编号。
/// 用法：AppearanceProbe.exe "D:\mir2test\zerodura"
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                     ?? Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine("找不到 " + Envir.DatabasePath);
            return 2;
        }

        Settings.Load();
        Envir envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        Console.WriteLine(string.Format("物品 {0} 个 / 怪物 {1} 个", envir.ItemInfoList.Count, envir.MonsterInfoList.Count));
        Console.WriteLine();

        string[] itemKeys = { "布衣", "木剑", "稻草人" };
        foreach (string key in itemKeys)
        {
            Console.WriteLine("===== 物品含「" + key + "」=====");
            foreach (ItemInfo it in envir.ItemInfoList)
            {
                if (it == null || it.Name == null) continue;
                if (!it.Name.Contains(key)) continue;

                Console.WriteLine(string.Format("  [{0}] {1} | Type={2} Image={3} Shape={4} 需求性别={5} 需求职业={6} 需求等级={7} 持久={8}",
                    it.Index, it.Name, it.Type, it.Image, it.Shape, it.RequiredGender, it.RequiredClass, it.RequiredAmount, it.Durability));
            }
            Console.WriteLine();
        }

        string[] monKeys = { "练功", "卫士", "弓箭手", "稻草人", "稻草" };
        foreach (string key in monKeys)
        {
            Console.WriteLine("===== 怪物含「" + key + "」=====");
            foreach (MonsterInfo mi in envir.MonsterInfoList)
            {
                if (mi == null || mi.Name == null) continue;
                if (!mi.Name.Contains(key) && (mi.GameName == null || !mi.GameName.Contains(key))) continue;

                Console.WriteLine(string.Format("  [{0}] Name={1} GameName={2} Image={3} AI={4} 等级={5}",
                    mi.Index, mi.Name, mi.GameName, mi.Image, mi.AI, mi.Level));
            }
            Console.WriteLine();
        }

        // 全部怪物按 Image 分组，方便确认稻草人是 27
        Console.WriteLine("===== 含「稻草」的全部条目（按 Image）=====");
        foreach (MonsterInfo mi in envir.MonsterInfoList)
        {
            if (mi == null) continue;
            bool hit = (mi.Name != null && mi.Name.Contains("稻草")) || (mi.GameName != null && mi.GameName.Contains("稻草"));
            if (!hit) continue;
            Console.WriteLine(string.Format("  {0} -> Image={1}", mi.Name, mi.Image));
        }

        return 0;
    }
}
