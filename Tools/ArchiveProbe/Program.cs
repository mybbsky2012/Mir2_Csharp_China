using System.Text;
using Server;
using Server.MirEnvir;

namespace ArchiveProbe;

/// <summary>
/// 只读诊断：dump 账号库里的全部角色 + 时间戳，并标出「按当前 Setup.ini 规则，下次启动谁会被归档」。
///
/// ★必须在**副本目录**里跑：Envir.LoadAccounts() 内部就会执行归档（写 Archive 目录、写 .MirCA）。
///   本工具在调用前把两个阈值临时改成 9999 个月来阻断归档，但为了绝对安全，仍请对副本运行：
///     1) 复制 Server.MirADB / Server.MirDB / Configs\Setup.ini 到某个临时目录
///     2) 在临时目录执行 ArchiveProbe.exe
///
/// 用法：ArchiveProbe.exe [--all]
///       默认只列出「会被归档」「已标记删除」「时间戳异常」的角色，--all 列出全部。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        bool all = args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase));

        int ai = Array.FindIndex(args, a => a.Equals("--char", StringComparison.OrdinalIgnoreCase));
        string charFile = (ai >= 0 && ai + 1 < args.Length) ? args[ai + 1] : null;

        if (!File.Exists(Envir.AccountPath) && charFile == null)
        {
            Console.WriteLine($"当前目录下找不到 {Envir.AccountPath}，请在服务端目录（或它的副本目录）运行。");
            return 2;
        }

        Settings.Load();

        // ★这里的两个值必须在 LoadAccounts 之前改掉：归档判定写在 AccountInfo 的构造函数里，
        //   一旦进去就来不及拦了（会真的落 .MirCA 文件、并从账号里摘掉角色）。
        int realInactive = Settings.ArchiveInactiveCharacterAfterMonths;
        int realDeleted = Settings.ArchiveDeletedCharacterAfterMonths;
        Settings.ArchiveInactiveCharacterAfterMonths = 9999;
        Settings.ArchiveDeletedCharacterAfterMonths = 9999;

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败（缺 Server.MirDB？）");
            return 2;
        }

        // ★必须先灌 BuffInfoList：HeroInfo.Load 里的 Buff 构造会调 Envir.GetBuffInfo，
        //   列表为空时对「气流术」这类会直接抛 NotImplementedException（账号库整个读不下去）。
        //   正式服务端是在 StartGame 里、LoadAccounts 之前灌的，这里照做。
        envir.BuffInfoList.Clear();
        foreach (var buff in Server.MirDatabase.BuffInfo.Load())
            envir.BuffInfoList.Add(buff);

        // --char：只看某个 .MirCA 归档文件里的角色时间戳（回答「他为什么会过期」）
        if (charFile != null)
        {
            if (!File.Exists(charFile)) { Console.WriteLine("找不到文件: " + charFile); return 2; }
            using var fs = File.OpenRead(charFile);
            using var br = new BinaryReader(fs);
            int ver = br.ReadInt32();
            int cver = br.ReadInt32();
            var c = new Server.MirDatabase.CharacterInfo(br, ver, cver);
            DateTime nowChar = envir.Now;
            Console.WriteLine("== " + Path.GetFileName(charFile) + " ==");
            Console.WriteLine($"  存档版本 {ver}/{cver}");
            Console.WriteLine($"  {c.Name}  Lv{c.Level} {c.Class}  索引 {c.Index}");
            Console.WriteLine($"  建号日期   {c.CreationDate:yyyy-MM-dd HH:mm} (UTC)");
            Console.WriteLine($"  最后登录   {(c.LastLoginDate == DateTime.MinValue ? "(从未登录 = MinValue)" : c.LastLoginDate.ToString("yyyy-MM-dd HH:mm") + " (UTC)")}");
            Console.WriteLine($"  已删除     {c.Deleted}   删除日期 {(c.DeleteDate == DateTime.MinValue ? "-" : c.DeleteDate.ToString("yyyy-MM-dd HH:mm"))}");
            Console.WriteLine("  判定依据   " + ExplainArchive(c, realInactive, realDeleted, nowChar));
            return 0;
        }

        envir.LoadAccounts();

        DateTime now = envir.Now;

        Console.WriteLine("== 归档规则（Setup.ini [Archive]） ==");
        Console.WriteLine($"  未激活/未登录 阈值 : {realInactive} 个月");
        Console.WriteLine($"  已删除角色    阈值 : {realDeleted} 个月");
        Console.WriteLine($"  Envir.Now（UTC）   : {now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        Console.WriteLine($"== 账号/角色 ==");
        Console.WriteLine($"  账号 {envir.AccountList.Count} 个，角色 {envir.CharacterList.Count} 个");

        int totalChars = 0;
        int willArchiveInactive = 0, willArchiveDeleted = 0, noLastLogin = 0;
        int accountsAllCharsArchived = 0;
        var listed = new List<string>();

        foreach (var acc in envir.AccountList)
        {
            if (acc == null) continue;

            int chars = acc.Characters == null ? 0 : acc.Characters.Count;
            totalChars += chars;

            int doomedInAcc = 0;
            foreach (var c in acc.Characters)
            {
                if (c == null) continue;

                bool del = c.Deleted && c.DeleteDate.AddMonths(realDeleted) <= now;
                bool inactiveNoLogin = c.LastLoginDate == DateTime.MinValue
                                       && c.CreationDate.AddMonths(realInactive) <= now;
                bool inactive = c.LastLoginDate > DateTime.MinValue
                                && c.LastLoginDate.AddMonths(realInactive) <= now;

                if (c.LastLoginDate == DateTime.MinValue) noLastLogin++;
                if (del) { willArchiveDeleted++; doomedInAcc++; }
                else if (inactiveNoLogin || inactive) { willArchiveInactive++; doomedInAcc++; }

                bool interesting = all || del || inactiveNoLogin || inactive || c.LastLoginDate == DateTime.MinValue;

                if (interesting)
                {
                    listed.Add(string.Format(
                        "  [{0}] {1,-14} Lv{2,-3} {3,-8} 建号={4:yyyy-MM-dd} 最后登录={5} 删除={6} 删日期={7}  ->  {8}",
                        acc.AccountID, c.Name, c.Level, c.Class,
                        c.CreationDate,
                        c.LastLoginDate == DateTime.MinValue ? "(从未登录)" : c.LastLoginDate.ToString("yyyy-MM-dd"),
                        c.Deleted ? "是" : "否",
                        c.DeleteDate == DateTime.MinValue ? "-" : c.DeleteDate.ToString("yyyy-MM-dd"),
                        del ? "★已删超期→归档"
                             : (inactiveNoLogin || inactive) ? "★超期未登录→归档"
                             : "保留"));
                }
            }

            if (chars > 0 && doomedInAcc == chars) accountsAllCharsArchived++;
        }

        Console.WriteLine();
        Console.WriteLine("== 明细（★ = 下次启动会被归档） ==");
        if (listed.Count == 0) Console.WriteLine("  （无）");
        foreach (string s in listed) Console.WriteLine(s);

        Console.WriteLine();
        Console.WriteLine("== 汇总 ==");
        Console.WriteLine($"  角色总数                 : {totalChars}");
        Console.WriteLine($"  从未登录过的角色(LastLoginDate=MinValue) : {noLastLogin}");
        Console.WriteLine($"  下次启动会因「超期未登录」被归档 : {willArchiveInactive}");
        Console.WriteLine($"  下次启动会因「已删除超期」被归档 : {willArchiveDeleted}");
        Console.WriteLine($"  归档后账号里一个角色都不剩     : {accountsAllCharsArchived} 个账号（这些账号会从账号库里消失）");

        Console.WriteLine();
        Console.WriteLine("== Archive 目录自检 ==");
        if (!Directory.Exists(Envir.ArchivePath))
        {
            Console.WriteLine("  Archive 目录不存在（说明从未归档过；此时 @LOADPLAYER/@RESTOREPLAYER 会抛异常）");
        }
        else
        {
            var files = new DirectoryInfo(Envir.ArchivePath).GetFiles("*.MirCA");
            Console.WriteLine($"  .MirCA 文件 {files.Length} 个");

            foreach (var g in files.GroupBy(f => Strip(f.Name)).OrderByDescending(g => g.Count()))
            {
                bool restoreOk = g.Count() == 1;
                Console.WriteLine(string.Format("  {0,-16} {1} 份   {2}",
                    g.Key, g.Count(), restoreOk ? "可恢复" : "★不可恢复（GetArchivedCharacter 要求恰好 1 份）"));
            }

            // 前缀撞名检测：A 的归档文件也会被 "A*" 匹配到
            var names = files.Select(f => Strip(f.Name)).Distinct().ToList();
            foreach (string n in names)
            {
                var collide = names.Where(o => o != n && o.StartsWith(n, StringComparison.Ordinal)).ToList();
                if (collide.Count > 0)
                    Console.WriteLine($"  ★前缀撞名：搜索「{n}」会同时命中 {string.Join("、", collide)}");
            }
        }

        return 0;
    }

    private static string ExplainArchive(Server.MirDatabase.CharacterInfo c, int inactiveMonths, int deletedMonths, DateTime now)
    {
        if (c.Deleted && c.DeleteDate.AddMonths(deletedMonths) <= now)
            return $"已删除且超过 {deletedMonths} 个月";
        if (c.LastLoginDate == DateTime.MinValue)
            return c.CreationDate.AddMonths(inactiveMonths) <= now
                ? $"从未登录过 + 建号已超过 {inactiveMonths} 个月"
                : $"从未登录，但建号未满 {inactiveMonths} 个月 → 保留";
        return c.LastLoginDate.AddMonths(inactiveMonths) <= now
            ? $"最后登录已超过 {inactiveMonths} 个月"
            : $"最后登录未满 {inactiveMonths} 个月 → 保留";
    }

    /// <summary>从「角色名_MMddyyyy_HHmmss.MirCA」里剥出角色名。</summary>
    private static string Strip(string fileName)
    {
        string s = Path.GetFileNameWithoutExtension(fileName);
        int i = s.LastIndexOf('_');
        if (i > 0)
        {
            int j = s.LastIndexOf('_', i - 1);
            if (j > 0) return s.Substring(0, j);
        }
        return s;
    }
}
