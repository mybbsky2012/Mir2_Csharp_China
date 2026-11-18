using System.Text;
using Server;
using Server.MirEnvir;

namespace DbHumUpShape;

/// <summary>
/// 羽化专属装备 Shape 档位迁移工具（11-10 夜间）。
///
/// 背景：羽化外观最初只用 Shape 区分「是否羽化 + 哪个职业」（1000~1004），
/// 地图上一律画 Up*Armour\01.Lib，同职业三档长得一模一样。
/// 现改为「十位 = 职业、个位 = 档位」：
///     1001 通用 / 1011,1012,1013 战士一~三档 / 1021~1023 法师 /
///     1031~1033 道士 / 1041~1043 刺客 / 1051~1053 弓箭
/// 客户端按个位取 Up*Armour\0N.Lib，三档外观各不相同。
///
/// 本工具只改已存在装备的 Shape，不增删条目、不动属性，因此角色装备引用（按 Index）不受影响。
///
/// 用法（工作目录必须是服务端目录，且**服务端已关闭**，否则退出时会把旧库写回覆盖）：
///   DbHumUpShape.exe            只预览
///   DbHumUpShape.exe --apply    写入（自动备份 Server.MirDB.bak_时间戳）
/// </summary>
internal static class Program
{
    private const string Prefix = "登仙·";
    private const short ShapeBase = 1000;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出重定向时可能失败 */ }

        bool apply = args.Contains("--apply");

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        // 服务端还开着的话，改完退出会被它的内存副本覆盖 —— 直接拒绝，避免白忙
        if (IsServerRunning())
        {
            Console.WriteLine("!! 检测到 Server.exe 正在运行。请先关闭服务端再执行（否则保存会被覆盖）。");
            return 4;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine($"已载入物品 {envir.ItemInfoList.Count} 个");

        int changed = 0, skipped = 0;
        var rows = new List<(string Name, short Old, short New)>();

        for (int i = 0; i < envir.ItemInfoList.Count; i++)
        {
            ItemInfo info = envir.ItemInfoList[i];
            if (info == null) continue;
            if (info.Type != ItemType.盔甲) continue;
            if (info.Name == null || !info.Name.StartsWith(Prefix, StringComparison.Ordinal)) continue;

            short shape = CalcShape(info);
            if (shape == info.Shape)
            {
                skipped++;
                continue;
            }

            rows.Add((info.Name, info.Shape, shape));
            if (apply) info.Shape = shape;
            changed++;
        }

        if (rows.Count == 0)
        {
            Console.WriteLine($"没有需要迁移的装备（已符合新编码 {skipped} 件）。");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("名称                                旧Shape  新Shape");
        foreach (var r in rows)
            Console.WriteLine($"{r.Name,-34} {r.Old,6}  {r.New,6}");

        Console.WriteLine();
        Console.WriteLine($"共 {changed} 件待迁移，{skipped} 件已合规。");

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("【预览模式】未写入。确认无误后加 --apply 执行。");
            return 0;
        }

        string bak = Envir.DatabasePath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(Envir.DatabasePath, bak, true);
        Console.WriteLine($"已备份 → {Path.GetFileName(bak)}");

        envir.SaveDB();
        Console.WriteLine($"已保存，物品 {envir.ItemInfoList.Count} 个。");
        return 0;
    }

    /// <summary>
    /// 十位 = 职业索引（0 通用 / 1 战 / 2 法 / 3 道 / 4 刺 / 5 弓），个位 = 档位。
    /// 档位由需求等级推：60→1、70→2、80→3，其余（通用 90 级这类）按 1 档。
    /// </summary>
    private static short CalcShape(ItemInfo info)
    {
        int clsIdx;
        switch (info.RequiredClass)
        {
            case RequiredClass.战士: clsIdx = 1; break;
            case RequiredClass.法师: clsIdx = 2; break;
            case RequiredClass.道士: clsIdx = 3; break;
            case RequiredClass.刺客: clsIdx = 4; break;
            case RequiredClass.弓箭: clsIdx = 5; break;
            default: clsIdx = 0; break;   // 战法道 / 全职业 等掩码 → 通用
        }

        int tier = info.RequiredAmount switch
        {
            <= 60 => 1,
            <= 70 => 2,
            <= 80 => 3,
            _ => 1,                        // 通用套 90 级只有一档
        };

        return (short)(ShapeBase + clsIdx * 10 + tier);
    }

    private static bool IsServerRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("Server")
                       .Any(p => !p.HasExited);
        }
        catch { return false; }
    }
}
