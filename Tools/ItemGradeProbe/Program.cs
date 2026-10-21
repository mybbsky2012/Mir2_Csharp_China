using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Drawing;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using S = ServerPackets;

namespace ItemGradeProbe;

/// <summary>
/// 只读验证：地面物品包里到底有没有带品质（客户端内挂「物品过滤」按品质筛选的依据）。
/// 不写数据库、不启动服务端。
/// 用法：ItemGradeProbe.exe "E:\...\Mir2Server"
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        int fail = 0;

        // 各品质各取一个样本，检查 GetInfo() 出来的包字段
        ItemGrade[] grades = { ItemGrade.普通, ItemGrade.宝物, ItemGrade.圣物, ItemGrade.神物, ItemGrade.英雄, ItemGrade.None };

        foreach (ItemGrade grade in grades)
        {
            ItemInfo info = envir.ItemInfoList.FirstOrDefault(x => x.Grade == grade);
            if (info == null)
            {
                Console.WriteLine($"[--] {grade}：库里没有样本，跳过");
                continue;
            }

            S.ObjectItem packet = BuildPacket(info, out string err);

            bool ok = packet != null && packet.grade == info.Grade;
            if (!ok) fail++;

            Console.WriteLine($"[{(ok ? "OK" : "!!")}] {info.Name} 品质={info.Grade} → 包内 grade={(packet == null ? "包为空" : packet.grade.ToString())}{err}");
        }

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过 ✓ 服务端地面物品包已带品质" : $"失败 {fail} 项 ✗");
        return fail == 0 ? 0 : 1;
    }

    /// <summary>绕过 Map 相关构造函数，直接给 Item 赋值后调 GetInfo()</summary>
    private static S.ObjectItem BuildPacket(ItemInfo info, out string err)
    {
        err = string.Empty;
        try
        {
            ItemObject ob = (ItemObject)RuntimeHelpers.GetUninitializedObject(typeof(ItemObject));
            ob.Item = new UserItem(info) { UniqueID = 1, Count = 1, MaxDura = info.Durability, CurrentDura = info.Durability };
            ob.CurrentLocation = new Point(100, 100);

            return ob.GetInfo() as S.ObjectItem;
        }
        catch (Exception ex)
        {
            err = "  [异常] " + ex.Message;
            return null;
        }
    }
}
