using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MagicRangeSet;

/// <summary>
/// 改库工具：设置指定技能的 Range（施法 / 锁定距离）。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处；**必须先停服**）：
///     MagicRangeSet --set SoulFireBall=13 --set SoulFireBallPlus=13 [--apply]
///
/// 不带 --apply 时只打印「当前值 → 目标值」，不落盘（干跑，用来核对）。
/// 带 --apply 才真正 LoadDB → 改 → SaveDB。
///
/// 注意：
///  1. Range = 0 表示不校验距离；否则要求 |dx| &lt;= Range 且 |dy| &lt;= Range（切比雪夫距离）。
///  2. 客户端锁怪用的是「灵魂火符」自己的 Range（升格后也一样），所以要让升格版锁得更远，
///     必须把 SoulFireBall 这一条一起放大，否则客户端先拦下「目标距离太远」。
///  3. 服务端运行期间会整份重写 Server.MirDB，运行中改库会被覆盖，务必停服后再跑。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        bool apply = args.Any(a => a.Equals("--apply", StringComparison.OrdinalIgnoreCase));
        var wanted = new List<(Spell spell, int range)>();

        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--set", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= args.Length) break;

            string[] parts = args[i + 1].Split('=');
            if (parts.Length != 2)
            {
                Console.WriteLine($"参数格式错误：{args[i + 1]}（应为 Spell=Range）");
                return 2;
            }

            if (!Enum.TryParse(parts[0], true, out Spell spell))
            {
                Console.WriteLine($"未知技能枚举：{parts[0]}");
                return 2;
            }

            if (!byte.TryParse(parts[1], out byte range))
            {
                Console.WriteLine($"范围必须是 0~255 的整数：{parts[1]}");
                return 2;
            }

            wanted.Add((spell, range));
        }

        if (wanted.Count == 0)
        {
            Console.WriteLine("没有要改的项。示例：MagicRangeSet --set SoulFireBall=13 --apply");
            return 2;
        }

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

        Console.WriteLine($"技能 {envir.MagicInfoList.Count} 个，目标 {wanted.Count} 项：");

        int changed = 0;
        foreach ((Spell spell, int range) in wanted)
        {
            MagicInfo info = envir.MagicInfoList.FirstOrDefault(m => m != null && m.Spell == spell);
            if (info == null)
            {
                Console.WriteLine($"  - {spell}：库中不存在，跳过");
                continue;
            }

            if (info.Range == range)
            {
                Console.WriteLine($"  = {spell}（{info.Name}）：已经是 {range}，不动");
                continue;
            }

            Console.WriteLine($"  * {spell}（{info.Name}）：{info.Range} → {range}");
            info.Range = (byte)range;
            changed++;
        }

        if (!apply)
        {
            Console.WriteLine($"干跑结束，未落盘。要写入请加 --apply（预计改动 {changed} 条）。");
            return 0;
        }

        if (changed == 0)
        {
            Console.WriteLine("没有变化，不写库。");
            return 0;
        }

        string backup = Envir.DatabasePath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(Envir.DatabasePath, backup, true);
        Console.WriteLine($"已备份原库 → {backup}");

        envir.SaveDB();
        Console.WriteLine($"已写回 {Path.GetFullPath(Envir.DatabasePath)}（改动 {changed} 条）");
        return 0;
    }
}
