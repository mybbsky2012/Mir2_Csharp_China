using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MagicDelayProbe;

/// <summary>
/// 只读工具：把 Server.MirDB 里每个技能的「基准延时 / 等级延时差值」导出，并按运行时的真实算法
/// 算出 0~3 级各自的冷却，用来定位「技能冷却时间 2147340 秒」这类异常值到底出在哪一格。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处）：
///     MagicDelayProbe.exe [输出目录]
/// 产出：
///     magic_delay.csv
///
/// 注意：本工具只读，不会写回数据库。
/// </summary>
internal static class Program
{
    private const long WarnMs = 600000;   // 超过 10 分钟就标出来

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 重定向时可能失败 */ }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}。");
            Console.WriteLine("请把本程序放到服务端目录(与 Server.exe 同级)再运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine($"技能 {envir.MagicInfoList.Count} 个");

        string outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".";

        var sb = new StringBuilder();
        sb.AppendLine("Index,Spell,Name,DelayBase(ms),DelayReduction(ms),Lv0延时,Lv0秒,Lv1延时,Lv1秒,Lv2延时,Lv2秒,Lv3延时,Lv3秒,异常");

        int bad = 0;
        for (int i = 0; i < envir.MagicInfoList.Count; i++)
        {
            MagicInfo m = envir.MagicInfoList[i];
            if (m == null) continue;

            // 运行时算法（Server/MirDatabase/MagicInfo.cs:219 UserMagic.GetDelay）
            //     return Info.DelayBase - (Level * Info.DelayReduction);
            // 两个字段都是 uint、且 Level 是 byte ⇒ 整个减法在 uint 域里做，
            // 一旦 Level*DelayReduction > DelayBase 就会回绕成 ~2^32 的巨大正数。
            var delays = new long[4];
            var secs = new long[4];
            var flags = new List<string>();

            for (int lv = 0; lv < 4; lv++)
            {
                delays[lv] = UintDelay(m.DelayBase, m.DelayReduction, (byte)lv);
                secs[lv] = (delays[lv] - 1) / 1000 + 1;      // 客户端提示的「剩余 X 秒」在刚施法时的取值

                if (delays[lv] < 0)
                {
                    flags.Add($"Lv{lv}负数({delays[lv]})");
                }
                else if (delays[lv] >= 0x80000000L)
                {
                    flags.Add($"Lv{lv}回绕({delays[lv]})");
                }
                else if (delays[lv] >= WarnMs)
                {
                    flags.Add($"Lv{lv}偏大");
                }

                // 正常的 long 算法（没回绕时应当等于上面）
                long sane = (long)m.DelayBase - (long)((byte)lv) * (long)m.DelayReduction;
                if (sane != delays[lv])
                {
                    flags.Add($"Lv{lv}应当={sane}");
                }
            }

            if (flags.Count > 0) bad++;

            sb.Append($"{i},{m.Spell},{m.Name},{m.DelayBase},{m.DelayReduction},");
            sb.Append($"{delays[0]},{secs[0]},{delays[1]},{secs[1]},{delays[2]},{secs[2]},{delays[3]},{secs[3]},");
            sb.AppendLine(string.Join(" / ", flags));
        }

        string path = Path.Combine(outDir, "magic_delay.csv");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));

        Console.WriteLine($"已写出 {Path.GetFullPath(path)}");
        Console.WriteLine($"其中 {bad} 条有异常标记");
        return 0;
    }

    /// <summary>复刻 UserMagic.GetDelay() 的真实（uint 回绕）语义。</summary>
    private static long UintDelay(uint delayBase, uint delayReduction, byte level)
    {
        uint delta = (uint)(level * delayReduction);   // byte * uint ⇒ uint
        return unchecked((uint)(delayBase - delta));   // uint - uint ⇒ 可能回绕
    }
}
