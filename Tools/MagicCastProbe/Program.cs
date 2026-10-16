using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MagicCastProbe;

/// <summary>
/// 只读工具：把 Server.MirADB 里每个角色「身上 BUFF 的剩余时间」与「技能冷却时间」导出，
/// 用来定位「技能冷却时间 2147340 秒」「某 BUFF 变成 29 天」这类异常值到底出在哪一格。
///
/// 两个字段的语义（务必分清）：
///   · Buff.ExpireTime      = 剩余毫秒（HumanObject.ProcessBuffs 里 ExpireTime -= change，
///                            <=0 即移除）。存档也是剩余毫秒。
///   · UserMagic.CastTime   = 内存态是绝对时间戳（Envir.Time），存档态是剩余毫秒
///                            （PlayerObject.Logout 里 -= Envir.Time，StartGameSuccess 里 += Envir.Time）。
///                            这一对转换只要有一次没成对执行（强杀 / 崩溃 / 自动保存后掉线），
///                            存档里就会残留绝对时间戳，下次登录再加 Envir.Time 就变成几十天。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirADB 所在处）：
///     MagicCastProbe.exe [输出目录]
/// 产出：
///     magic_casttime.csv   只列出异常条目（两种记录混排）
///
/// 注意：本工具只读，不会写回数据库。
/// </summary>
internal static class Program
{
    // 最长技能冷却 600000ms（10 分钟）、最长技能 BUFF 10 分钟、精力充沛上限 4 小时。
    // 超过 1 小时就说明这个「剩余量」已经不是正常值了。
    private const long WarnMs = 3600000;
    private const long DayMs = 86400000;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 重定向时可能失败 */ }

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

        // BuffInfoList 在正式启动流程里是由 StartEnvir 在 LoadDB 之后填充的
        // （Envir.cs:1862-1866）。LoadAccounts 读角色存档时会经 Buff(reader) -> GetBuffInfo，
        // 缺了会抛 NotImplementedException，所以这里必须手工补上同样的步骤。
        envir.BuffInfoList.Clear();
        foreach (var b in BuffInfo.Load())
        {
            envir.BuffInfoList.Add(b);
        }

        // 账号库是独立的 Server.MirADB。LoadAccounts 仅在文件不存在时才写盘，正常只读。
        envir.LoadAccounts();

        Console.WriteLine($"账号 {envir.AccountList.Count} 个 / 角色 {envir.CharacterList.Count} 个 / BUFF 定义 {envir.BuffInfoList.Count} 种");

        string outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".";

        var sb = new StringBuilder();
        sb.AppendLine("账号,角色,类别,Spell/Type,名称,等级,原始值(ms),换算天数,备注");

        int magicTotal = 0, magicBad = 0, buffTotal = 0, buffBad = 0;
        var perName = new Dictionary<string, int>();

        foreach (var acc in envir.AccountList)
        {
            if (acc == null) continue;
            foreach (var ch in acc.Characters)
            {
                if (ch == null) continue;

                // ---- 技能冷却 ----
                if (ch.Magics != null)
                {
                    foreach (var mg in ch.Magics)
                    {
                        if (mg == null) continue;
                        magicTotal++;

                        string name = mg.Info != null ? mg.Info.Name : "?";

                        // (a) 冷却剩余量异常（残留绝对时间戳）
                        long ct = mg.CastTime;
                        bool isMin = ct == int.MinValue;   // 合法「无冷却」标记
                        if (!isMin && (ct > WarnMs || ct < -WarnMs))
                        {
                            magicBad++;
                            perName["CastTime:" + name] = perName.TryGetValue("CastTime:" + name, out int c) ? c + 1 : 1;

                            sb.AppendLine($"{acc.AccountID},{ch.Name},技能冷却,{mg.Spell},{name},{mg.Level},{ct},{(double)ct / DayMs:F2},疑似残留绝对时间戳");
                        }

                        // (b) GetDelay() 用「玩家实际等级」算出来的冷却本身是否回绕
                        //     （DelayBase - Level*DelayReduction 是 uint 运算，减法为负会环绕成 ~2^32）
                        long d = 0;
                        try { d = mg.GetDelay(); } catch { }
                        if (d >= 0x80000000L || d < 0)
                        {
                            perName["Delay:" + name] = perName.TryGetValue("Delay:" + name, out int c2) ? c2 + 1 : 1;
                            sb.AppendLine($"{acc.AccountID},{ch.Name},技能延时,{mg.Spell},{name},{mg.Level},{d},{(double)d / DayMs:F2},GetDelay()回绕/负数");
                        }
                    }
                }

                // ---- 身上 BUFF 剩余时间 ----
                if (ch.Buffs != null)
                {
                    foreach (var bf in ch.Buffs)
                    {
                        if (bf == null) continue;
                        buffTotal++;

                        long et = bf.ExpireTime;
                        // Infinite 类 BUFF 用 long.MaxValue / 很大值表示不过期，属合法
                        bool isInfinite = bf.StackType == BuffStackType.Infinite;

                        bool isBad = !isInfinite && (et > WarnMs || et < 0);
                        if (!isBad) continue;

                        buffBad++;
                        string name = bf.Info != null ? bf.Info.Name : bf.Type.ToString();
                        perName["BUFF:" + name] = perName.TryGetValue("BUFF:" + name, out int c) ? c + 1 : 1;

                        string note = isInfinite ? "Infinite（合法）" : $"疑似异常剩余时间；约 {(double)et / DayMs:F2} 天";
                        sb.AppendLine($"{acc.AccountID},{ch.Name},BUFF,{(int)bf.Type},{name},,{et},{(double)et / DayMs:F2},{note}");
                    }
                }
            }
        }

        string path = Path.Combine(outDir, "magic_casttime.csv");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));

        Console.WriteLine($"技能记录 {magicTotal} 条 → 异常 {magicBad} 条");
        Console.WriteLine($"BUFF 记录 {buffTotal} 条 → 异常 {buffBad} 条");
        if (perName.Count > 0)
        {
            Console.WriteLine("按名称聚合：");
            foreach (var kv in perName.OrderByDescending(k => k.Value).Take(30))
                Console.WriteLine($"   {kv.Key} x{kv.Value}");
        }
        Console.WriteLine($"已写出 {Path.GetFullPath(path)}");
        return 0;
    }
}
