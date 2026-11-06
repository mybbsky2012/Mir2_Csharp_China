using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace ArchiveFixTest;

/// <summary>
/// 归档暗坑修复的实跑验证（会在**当前目录**里写 Archive\、改 Server.MirADB）。
///
/// ★必须对副本目录运行：工具会故意让角色过期 → 触发真归档 → 改账号库落盘。
///   典型用法：
///     1) mkdir /tmp/aft && cp Server.MirADB Server.MirDB /tmp/aft/
///     2) cp -r Configs /tmp/aft/
///     3) cd /tmp/aft && ArchiveFixTest.exe
///
/// 覆盖的 6 个暗坑：
///   1) 重复归档（每次启动都写一份）
///   2) 前缀匹配（查「张三」命中「张三丰」的档）
///   3) 多份归档直接放弃（files.Length != 1 → 永久恢复不了）
///   4) Archive 目录不存在时抛异常
///   5) 角色归光后整账号被丢（连带账号仓库/金币/充值）
///   6) 文件名时间戳是 UTC（运维看到差 8 小时）
/// 外加：恢复后归档未清理、恢复后不落盘。
/// </summary>
internal static class Program
{
    private static int _pass, _fail;
    private static readonly List<string> Failures = new();

    private static void Check(bool ok, string name, string detail = null)
    {
        if (ok)
        {
            _pass++;
            Console.WriteLine($"  [PASS] {name}");
        }
        else
        {
            _fail++;
            Failures.Add(name + (detail == null ? "" : $"  ({detail})"));
            Console.WriteLine($"  [FAIL] {name}" + (detail == null ? "" : $"  -> {detail}"));
        }
    }

    private static Envir E;
    private static int CountArchive(string name) => E.GetArchivedCharacterFiles(name).Count;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.AccountPath) || !File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录缺 {Envir.AccountPath} 或 {Envir.DatabasePath}。");
            Console.WriteLine("★请在**服务端目录的副本**里运行，本工具会真归档、真写账号库。");
            return 2;
        }

        Console.WriteLine("== 归档暗坑修复 · 实跑验证 ==");
        Console.WriteLine($"工作目录: {Directory.GetCurrentDirectory()}");

        Settings.Load();

        // 读入阶段先阻断归档，方便我们挑角色造场景
        int realInactive = Settings.ArchiveInactiveCharacterAfterMonths;
        int realDeleted = Settings.ArchiveDeletedCharacterAfterMonths;
        Settings.ArchiveInactiveCharacterAfterMonths = 9999;
        Settings.ArchiveDeletedCharacterAfterMonths = 9999;

        E = Envir.Main;
        if (!E.LoadDB()) { Console.WriteLine("!! LoadDB 失败"); return 2; }

        // 正式服务端在 StartGame 里、LoadAccounts 之前灌；空列表会让 HeroInfo 的 Buff 构造抛异常
        E.BuffInfoList.Clear();
        foreach (var buff in BuffInfo.Load()) E.BuffInfoList.Add(buff);

        // --real：只读盘点当前 Archive 目录（完全不碰账号库），回答「现网这些档现在能不能恢复」
        if (args.Any(a => a.Equals("--real", StringComparison.OrdinalIgnoreCase)))
            return ListReal();

        E.LoadAccounts();

        Console.WriteLine($"  读入账号 {E.AccountList.Count} 个、角色 {E.CharacterList.Count} 个");

        var host = E.AccountList.FirstOrDefault(a => a.Characters.Count > 0);
        if (host == null) { Console.WriteLine("!! 账号库里没有可用角色，无法构造测试"); return 2; }

        var template = host.Characters[0];

        // ── 造场景 ────────────────────────────────────────────────
        host.Gold = 1234567;
        host.Credit = 890;

        var zombie = Clone(template, ++E.NextCharacterID, "测试僵尸");
        zombie.LastLoginDate = E.Now.AddYears(-2);
        zombie.CreationDate = E.Now.AddYears(-2);
        zombie.Deleted = false;
        host.Characters.Add(zombie);

        var zombie2 = Clone(template, ++E.NextCharacterID, "测试僵尸二号");   // 前缀陷阱用
        zombie2.LastLoginDate = E.Now.AddYears(-2);
        zombie2.CreationDate = E.Now.AddYears(-2);
        zombie2.Deleted = false;
        host.Characters.Add(zombie2);

        var alive = Clone(template, ++E.NextCharacterID, "测试活人");
        alive.LastLoginDate = E.Now;
        alive.CreationDate = E.Now;
        alive.Deleted = false;
        host.Characters.Add(alive);

        // 「归档后归光」的独立账号：只有 1 个角色，且该角色必被归档
        var doomedAcc = new AccountInfo
        {
            Index = ++E.NextAccountID,
            AccountID = "test_archive_to_empty",
            Gold = 777,
            Credit = 88,
        };
        var onlyChar = Clone(template, ++E.NextCharacterID, "测试归光角色");
        onlyChar.LastLoginDate = E.Now.AddYears(-2);
        onlyChar.CreationDate = E.Now.AddYears(-2);
        onlyChar.Deleted = false;
        onlyChar.AccountInfo = doomedAcc;
        doomedAcc.Characters.Add(onlyChar);
        E.AccountList.Add(doomedAcc);

        E.SaveAccounts();
        E.LoadAccounts();   // 让内存与落盘一致（此时仍在阻断归档）

        // 清掉历史归档，只留本次测试产生的
        ClearArchive();

        // ── 第一次启动：触发自动归档 ────────────────────────────────
        Console.WriteLine("\n[1] 第一次启动（阈值恢复 12 个月，触发自动归档）");
        Settings.ArchiveInactiveCharacterAfterMonths = realInactive;
        Settings.ArchiveDeletedCharacterAfterMonths = realDeleted;
        E.LoadAccounts();

        Check(CountArchive("测试僵尸") == 1, "自动归档写入 1 份", $"实际 {CountArchive("测试僵尸")} 份");
        Check(CountArchive("测试僵尸二号") == 1, "同名前缀的角色也各写 1 份");
        Check(CountArchive("测试活人") == 0, "未过期的角色不会被归档");

        var acc1 = E.AccountList.FirstOrDefault(a => a.AccountID == host.AccountID);
        Check(acc1 != null, "有角色被归档的账号仍保留在账号库");
        Check(acc1 != null && !acc1.Characters.Any(c => c.Name == "测试僵尸"), "归档角色已从账号中摘除");
        Check(acc1 != null && acc1.Characters.Any(c => c.Name == "测试活人"), "同账号的活角色没被误伤");
        Check(acc1 != null && acc1.Gold == 1234567 && acc1.Credit == 890, "账号金币/充值点数未受影响");

        // ★暗坑 5：角色归光 → 账号被丢
        var doomed1 = E.AccountList.FirstOrDefault(a => a.AccountID == "test_archive_to_empty");
        Check(doomed1 != null, "★角色被归档归光的账号没有被丢弃（原版这里整号消失）");
        Check(doomed1 != null && doomed1.Gold == 777 && doomed1.Credit == 88, "★归光账号的金币/点数仍在");

        // ★暗坑 6：文件名时间戳应为本地时间
        var f1 = E.GetArchivedCharacterFiles("测试僵尸").FirstOrDefault();
        if (f1 != null)
        {
            var stamp = Path.GetFileNameWithoutExtension(f1.Name).Substring("测试僵尸".Length).TrimStart('_');
            bool parsed = DateTime.TryParseExact(stamp, "MMddyyyy_HHmmss", null,
                System.Globalization.DateTimeStyles.None, out var t);
            Check(parsed && Math.Abs((t - DateTime.Now).TotalMinutes) < 10,
                "★归档文件名的时间戳是本地时间", $"文件名 {f1.Name}，解析 {t:HH:mm:ss}，本地 {DateTime.Now:HH:mm:ss}");
        }
        else Check(false, "★归档文件名的时间戳是本地时间", "找不到归档文件");

        // ── 第二次启动：幂等 ────────────────────────────────────────
        Console.WriteLine("\n[2] 第二次启动（模拟重启，验证不重复归档）");
        E.LoadAccounts();
        Check(CountArchive("测试僵尸") == 1, "★重启后仍是 1 份（原版每次启动多一份）", $"实际 {CountArchive("测试僵尸")} 份");
        Check(CountArchive("测试僵尸二号") == 1, "★前缀角色同样不重复");
        Check(E.AccountList.Any(a => a.AccountID == "test_archive_to_empty"), "第二次启动归光账号仍在");

        // ── 读取：前缀陷阱 / 多份 / 目录缺失 ────────────────────────
        Console.WriteLine("\n[3] 读取归档（原版三个毛病）");
        var got = E.GetArchivedCharacter("测试僵尸");
        Check(got != null && got.Name == "测试僵尸", "★查「测试僵尸」不会命中「测试僵尸二号」的档",
            got == null ? "返回 null" : "拿到 " + got.Name);

        // 手工造第二份历史归档（模拟老版本留下的重复），并让它更新
        var origin = E.GetArchivedCharacterFiles("测试僵尸").First();
        string dupPath = Path.Combine(Envir.ArchivePath, "测试僵尸_12312099_235959.MirCA");
        File.Copy(origin.FullName, dupPath, true);
        File.SetLastWriteTime(dupPath, DateTime.Now.AddMinutes(10));

        var got2 = E.GetArchivedCharacter("测试僵尸", out var usedFile);
        Check(got2 != null, "★同一角色有多份归档时仍能读到（原版返回 null = 永远恢复不了）");
        Check(usedFile != null && usedFile.Name == Path.GetFileName(dupPath), "★多份时取最新那一份",
            usedFile == null ? "null" : usedFile.Name);

        var byFull = E.GetArchivedCharacter("测试僵尸_12312099_235959.MirCA");
        Check(byFull != null, "可以直接用完整文件名读取（旧提示语承诺但从没实现）");
        File.Delete(dupPath);

        // Archive 目录不存在
        string moved = Envir.ArchivePath + "_tmpmove";
        bool threw = false;
        CharacterInfo missing = null;
        Directory.Move(Envir.ArchivePath, moved);
        try
        {
            try { missing = E.GetArchivedCharacter("测试僵尸"); }
            catch { threw = true; }
        }
        finally { Directory.Move(moved, Envir.ArchivePath); }
        Check(!threw && missing == null, "★Archive 目录不存在时不抛异常");

        // ── 恢复账号（RESTOREPLAYER 的核心步骤） ────────────────────
        Console.WriteLine("\n[4] 模拟 @RESTOREPLAYER 恢复账号");
        var accR = E.AccountList.First(a => a.AccountID == host.AccountID);
        int beforeCount = accR.Characters.Count;

        var restored = E.GetArchivedCharacter("测试僵尸", out var archiveFile);
        Check(restored != null, "读到待恢复的归档");
        if (restored == null) { return Finish(); }

        restored.AccountInfo = accR;
        accR.Characters.Add(restored);
        E.CharacterList.Add(restored);
        restored.Deleted = false;
        restored.DeleteDate = DateTime.MinValue;
        restored.LastLoginDate = E.Now;

        Check(accR.Characters.Count == beforeCount + 1, "角色已挂回账号");

        Check(E.MoveArchiveToRestored(archiveFile, out var moveErr), "归档文件移入 Archive\\Restored\\", moveErr);
        Check(!File.Exists(archiveFile.FullName), "原归档位置已清空（不会被反复恢复）");
        Check(File.Exists(Path.Combine(Envir.ArchiveRestoredPath, archiveFile.Name)), "Restored 目录里能找到它");
        Check(E.GetArchivedCharacter("测试僵尸") == null, "恢复后再查该角色归档 = 查不到");

        E.BeginSaveAccounts();
        WaitSave();

        // ── 第三次启动：恢复结果落盘了没 ────────────────────────────
        Console.WriteLine("\n[5] 第三次启动（验证恢复已落盘、角色不会再被归档）");
        E.LoadAccounts();
        var acc3 = E.AccountList.FirstOrDefault(a => a.AccountID == host.AccountID);
        Check(acc3 != null && acc3.Characters.Any(c => c.Name == "测试僵尸"), "★恢复后的角色重启仍在账号里");
        Check(CountArchive("测试僵尸") == 0, "★恢复后重启没有再归档一份");
        Check(acc3 != null && acc3.Gold == 1234567, "恢复流程没动账号金币");

        // ── 杂项排雷 ────────────────────────────────────────────────
        Console.WriteLine("\n[6] 额外排雷");

        // 幂等：重复调用 ArchiveCharacterOnce 不多写
        var z = acc3.Characters.First(c => c.Name == "测试僵尸");
        int n0 = CountArchive("测试僵尸");
        E.ArchiveCharacterOnce(z);
        E.ArchiveCharacterOnce(z);
        Check(CountArchive("测试僵尸") == n0 + 1, "ArchiveCharacterOnce 第一次写 1 份、第二次不写",
            $"{n0} -> {CountArchive("测试僵尸")}");

        // 同名同秒连点 BACKUPPLAYER 不覆盖
        int before = E.GetArchivedCharacterFiles("测试僵尸").Count;
        string a = E.SaveArchivedCharacter(z);
        string b = E.SaveArchivedCharacter(z);
        Check(a != b && E.GetArchivedCharacterFiles("测试僵尸").Count == before + 2,
            "同秒连点手动备份不会被覆盖");

        // 文件名通配陷阱：角色名是另一个角色名的前缀
        var fz = E.GetArchivedCharacterFiles("测试僵尸");
        var fz2 = E.GetArchivedCharacterFiles("测试僵尸二号");
        Check(fz.All(f => f.Name.StartsWith("测试僵尸_") || f.Name.Contains("测试僵尸" + "_")),
            "按名查询不会混入别的角色");
        Check(fz2.Count == 1 && fz2[0].Name.StartsWith("测试僵尸二号"), "「测试僵尸二号」只拿到自己的档");

        // 归档文件内容能原样读回
        var roundtrip = E.GetArchivedCharacter("测试僵尸二号");
        Check(roundtrip != null && roundtrip.Name == "测试僵尸二号" && roundtrip.Level == z.Level,
            "归档内容可完整读回（名字/等级一致）",
            roundtrip == null ? "null" : $"{roundtrip.Name} Lv{roundtrip.Level}");

        // 坏档：最新那份被写坏（断电/被占用），应回退到更早的一份，而不是整体失败
        string badPath = Path.Combine(Envir.ArchivePath, "测试僵尸_12312099_235959.MirCA");
        File.WriteAllBytes(badPath, new byte[] { 1, 2, 3, 4 });
        File.SetLastWriteTime(badPath, DateTime.Now.AddHours(1));
        var fallback = E.GetArchivedCharacter("测试僵尸", out var fbFile);
        Check(fallback != null, "★最新一份归档损坏时仍能读到（回退到更早的一份）");
        Check(fbFile != null && fbFile.Name != Path.GetFileName(badPath), "★回退时选的是可读的那一份，不会把坏档拿去挪走");
        File.Delete(badPath);

        return Finish();
    }

    /// <summary>只读盘点 Archive 目录：每个角色有几份、最新那份能不能读出来。</summary>
    private static int ListReal()
    {
        Console.WriteLine();
        Console.WriteLine("== Archive 目录盘点（只读，不动账号库）==");

        if (!Directory.Exists(Envir.ArchivePath))
        {
            Console.WriteLine("  Archive 目录不存在");
            return 0;
        }

        var files = Directory.GetFiles(Envir.ArchivePath, "*.MirCA");
        Console.WriteLine($"  .MirCA 文件 {files.Length} 个");

        var names = new Dictionary<string, int>();

        foreach (var f in files)
        {
            string b = Path.GetFileNameWithoutExtension(f);
            var m = System.Text.RegularExpressions.Regex.Match(b, @"^(.*?)_?\d{8}_\d{6}(_\d{1,3})?$");
            string n = m.Success ? m.Groups[1].Value : b;
            names[n] = names.TryGetValue(n, out var c) ? c + 1 : 1;
        }

        int ok = 0, bad = 0;

        foreach (var kv in names.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var info = E.GetArchivedCharacter(kv.Key, out var used);

            if (info != null)
            {
                ok++;
                Console.WriteLine($"  [可恢复] {kv.Key,-12} 份数 {kv.Value}  取用 {used?.Name}  ->  {info.Name} Lv{info.Level} {info.Class}");
            }
            else
            {
                bad++;
                Console.WriteLine($"  [读不出] {kv.Key,-12} 份数 {kv.Value}");
            }

            if (kv.Value > 1)
            {
                foreach (var ff in E.GetArchivedCharacterFiles(kv.Key))
                    Console.WriteLine($"        {ff.Name}  mtime={ff.LastWriteTime:yyyy-MM-dd HH:mm:ss}  {ff.Length}B");
            }
        }

        Console.WriteLine($"  合计：可恢复 {ok} 个角色，读不出 {bad} 个");
        return bad == 0 ? 0 : 1;
    }

    private static CharacterInfo Clone(CharacterInfo src, int newIndex, string newName)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            w.Write(Envir.Version);
            w.Write(Envir.CustomVersion);
            src.Save(w);
        }

        ms.Position = 0;
        using var r = new BinaryReader(ms);
        int ver = r.ReadInt32(), cver = r.ReadInt32();
        var c = new CharacterInfo(r, ver, cver);
        c.Index = newIndex;
        c.Name = newName;
        return c;
    }

    private static void ClearArchive()
    {
        if (!Directory.Exists(Envir.ArchivePath)) return;
        foreach (var f in Directory.GetFiles(Envir.ArchivePath, "*.MirCA")) File.Delete(f);
    }

    private static void WaitSave()
    {
        // BeginSaveAccounts 是异步落盘：写完 n 之后才 rename 成 AccountPath。等 n 消失即完成。
        for (int i = 0; i < 100; i++)
        {
            if (!File.Exists(Envir.AccountPath + "n")) return;
            Thread.Sleep(50);
        }
    }

    private static int Finish()
    {
        Console.WriteLine();
        Console.WriteLine($"== 结果：{_pass} 通过 / {_fail} 失败 ==");
        if (_fail > 0)
        {
            Console.WriteLine("失败项：");
            foreach (var f in Failures) Console.WriteLine("  - " + f);
        }
        return _fail == 0 ? 0 : 1;
    }
}
