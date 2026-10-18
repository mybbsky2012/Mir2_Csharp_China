using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MagicCooldownAudit;

/// <summary>
/// 只读审计：找出「会变成超长冷却」的技能配置，以及角色存档里已经残留的超长冷却。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB / Server.MirADB 所在处）：
///     MagicCooldownAudit.exe [输出目录]
/// 产出：
///     magic_cooldown_audit.csv    技能配置风险（旧DLL语义 vs 修复后语义）
///     char_cooldown_audit.csv     角色存档里残留的冷却
/// 只读，不写回数据库。
/// </summary>
internal static class Program
{
    /// <summary>超过这个毫秒数就算「偏大」，需要人眼确认是不是设计值。</summary>
    private const long BigMs = 600_000;

    /// <summary>uint 回绕阈值：超过 2^31 必然是下溢出来的，不可能是正常配置。</summary>
    private const long WrapThreshold = 2_147_483_648L;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录下运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        // 角色存档在 Server.MirADB 里，必须单独加载。
        // 注意：Buff 解析要用到 BuffInfoList，服务端是 LoadDB 之后再 BuffInfo.Load() 填的，
        // 不先填会在读角色身上的 buff 时抛 NotImplementedException。
        if (File.Exists(Envir.AccountPath))
        {
            envir.BuffInfoList.Clear();
            foreach (var buff in BuffInfo.Load())
                envir.BuffInfoList.Add(buff);

            envir.LoadAccounts();
        }
        else
        {
            Console.WriteLine($"（未找到 {Envir.AccountPath}，跳过角色存档审计）");
        }

        string outDir = args.FirstOrDefault(a => !a.StartsWith("--")) ?? ".";

        int wrap = 0, clamp = 0, big = 0;
        AuditMagics(envir, outDir, ref wrap, ref clamp, ref big);

        int residual = 0, absStamp = 0;
        AuditCharacters(envir, outDir, ref residual, ref absStamp);

        Console.WriteLine();
        Console.WriteLine($"技能 {envir.MagicInfoList.Count} 个：旧DLL会回绕 {wrap} 个 / 修复后夹到0 {clamp} 个 / 配置偏大(>=10分钟) {big} 个");
        Console.WriteLine($"角色 {envir.CharacterList.Count} 个（账号 {envir.AccountList.Count} 个）：残留超长冷却 {residual} 处 / 疑似存成绝对时间 {absStamp} 处");
        return 0;
    }

    private static void AuditMagics(Envir envir, string outDir, ref int wrap, ref int clamp, ref int big)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Index,Spell,Name,DelayBase,DelayReduction," +
                      "旧Lv0,旧Lv1,旧Lv2,旧Lv3,新Lv0,新Lv1,新Lv2,新Lv3,风险");

        for (int i = 0; i < envir.MagicInfoList.Count; i++)
        {
            MagicInfo m = envir.MagicInfoList[i];
            if (m == null) continue;

            var oldV = new long[4];
            var newV = new long[4];
            var risks = new List<string>();

            for (int lv = 0; lv < 4; lv++)
            {
                oldV[lv] = OldUintDelay(m.DelayBase, m.DelayReduction, (byte)lv);
                newV[lv] = NewDelay(m.DelayBase, m.DelayReduction, (byte)lv);
            }

            bool hasWrap = false, hasClamp = false;
            for (int lv = 1; lv < 4; lv++)
            {
                if (oldV[lv] >= WrapThreshold) hasWrap = true;
                // 只有「配置本来算成负数」被夹到 0 才算问题；正好减到 0 是设计值
                long raw = (long)m.DelayBase - (long)lv * m.DelayReduction;
                if (raw < 0 && newV[lv] == 0) hasClamp = true;
            }

            if (hasWrap)
            {
                wrap++;
                var lvs = Enumerable.Range(1, 3).Where(lv => oldV[lv] >= WrapThreshold).Select(lv => "Lv" + lv);
                risks.Add($"旧DLL回绕({string.Join("/", lvs)})");
            }

            if (hasClamp)
            {
                clamp++;
                risks.Add("修复后为0秒");
            }

            if (m.DelayReduction > 0 && m.DelayReduction * 3 > m.DelayBase)
            {
                risks.Add("Reduction×3>Base");
            }

            if (newV[0] >= BigMs)
            {
                big++;
                risks.Add($"Lv0={newV[0] / 1000}秒偏大");
            }

            sb.Append($"{i},{m.Spell},{m.Name},{m.DelayBase},{m.DelayReduction},");
            sb.Append($"{oldV[0]},{oldV[1]},{oldV[2]},{oldV[3]},");
            sb.Append($"{newV[0]},{newV[1]},{newV[2]},{newV[3]},");
            sb.AppendLine(string.Join(" / ", risks));
        }

        string p = Path.Combine(outDir, "magic_cooldown_audit.csv");
        File.WriteAllText(p, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"技能表 -> {Path.GetFullPath(p)}");

        // 控制台只打有风险的，方便一眼看完
        Console.WriteLine();
        Console.WriteLine("== 有风险的技能 ==");
        foreach (var line in sb.ToString().Split('\n').Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.TrimEnd('\r').Split(',');
            if (cells.Length < 14) continue;
            string risk = cells[13];
            if (string.IsNullOrEmpty(risk)) continue;
            Console.WriteLine($"  {cells[2],-14} Base={cells[3],-7} Red={cells[4],-6} 旧Lv3={cells[9],-11} -> {risk}");
        }
    }

    private static void AuditCharacters(Envir envir, string outDir, ref int residual, ref int absStamp)
    {
        var sb = new StringBuilder();
        sb.AppendLine("账号,角色,技能,技能等级,存档CastTime(ms),换算小时,判定");

        foreach (AccountInfo acc in envir.AccountList)
        {
            if (acc?.Characters == null) continue;

            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch?.Magics == null) continue;

                foreach (UserMagic um in ch.Magics)
                {
                    // 等级只该是 0~3；越界会把 Level×Reduction 放大，冷却算得更离谱
                    if (um.Level > 3)
                    {
                        residual++;
                        sb.AppendLine($"{acc.UserName},{ch.Name},{um.Spell},{um.Level},{um.CastTime},-," +
                                      "技能等级越界(>3)");
                        continue;
                    }

                    // int.MinValue = 冷却已就绪，是正常值
                    if (um.CastTime == int.MinValue) continue;
                    if (um.CastTime <= 0) continue;

                    string verdict;
                    if (um.CastTime > 86_400_000)
                    {
                        absStamp++;
                        verdict = "疑似存成绝对时间，重登会再加一次";
                    }
                    else if (um.CastTime > BigMs)
                    {
                        verdict = "残留冷却偏长，需确认";
                    }
                    else
                    {
                        continue;
                    }

                    residual++;
                    sb.AppendLine($"{acc.UserName},{ch.Name},{um.Spell},{um.Level},{um.CastTime}," +
                                  $"{(um.CastTime / 3600000.0):F2},{verdict}");
                }
            }
        }

        string p = Path.Combine(outDir, "char_cooldown_audit.csv");
        File.WriteAllText(p, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"角色存档 -> {Path.GetFullPath(p)}");

        if (residual > 0)
        {
            Console.WriteLine();
            Console.WriteLine("== 残留冷却 ==");
            foreach (var line in sb.ToString().Split('\n').Skip(1))
            {
                if (!string.IsNullOrWhiteSpace(line)) Console.WriteLine("  " + line.TrimEnd('\r'));
            }
        }
    }

    /// <summary>未修复前的真实语义：uint 相减，负值会回绕。</summary>
    private static long OldUintDelay(uint delayBase, uint delayReduction, byte level)
    {
        uint delta = (uint)(level * delayReduction);
        return unchecked((uint)(delayBase - delta));
    }

    /// <summary>修复后的语义：long 相减并夹到 0。</summary>
    private static long NewDelay(uint delayBase, uint delayReduction, byte level)
    {
        long d = (long)delayBase - (long)level * delayReduction;
        return d > 0 ? d : 0;
    }
}
