using System.Diagnostics;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace Mir2DbUpgrade;

public enum LogLevel { Info, Good, Warn, Error, Muted }

/// <summary>
/// 升级编排：备份 → 加载 → 快照 → 存盘 → 重新加载 → 逐条比对 → 不一致自动还原。
///
/// 全程在同一个进程里完成。这里踩过一个坑：Envir.LoadAccounts 里的 GameshopLog 是
/// Dictionary 且加载时不清空，同一进程第二次加载会抛「An item with the same key has
/// already been added」。所以每次加载前手动 Clear 它 —— 清掉之后二次加载的快照与
/// 独立进程加载的结果逐字节一致（已用 Tools/DbUpgradeTest 验证）。
/// 服务端自己只加载一次，所以这条限制只影响本工具。
/// </summary>
internal static class UpgradeRunner
{
    public static readonly int TargetVersion = Envir.Version;

    public sealed class Result
    {
        public bool Success;
        public string Message = "";
        public string BackupDir;
        public int DbBefore = -1, AdbBefore = -1;
        public int DbAfter = -1, AdbAfter = -1;
        public int DiffCount;
        public int SnapshotRows;
        public double Seconds;
    }

    // ===================== 预检（只读） =====================

    public static Result Precheck(string serverDir, Action<string, LogLevel> log)
    {
        var r = new Result();
        serverDir = Path.GetFullPath(serverDir);

        r.DbBefore = Worker.PeekVersion(Path.Combine(serverDir, "Server.MirDB"));
        r.AdbBefore = Worker.PeekVersion(Path.Combine(serverDir, "Server.MirADB"));

        try
        {
            var envir = LoadEnv(serverDir, log);
            var snap = DbSnapshot.Snapshot(envir);
            r.SnapshotRows = snap.Count;
            log($"快照 {snap.Count:N0} 行（升级后要拿它逐条比对）", LogLevel.Muted);
            log("", LogLevel.Info);
            Worker.Summary(envir, s => log(s, LogLevel.Info));
            Worker.ProbeRawAccounts(s => log(s, LogLevel.Info));
            r.Success = true;
        }
        catch (Exception ex)
        {
            r.Message = ex.Message;
            log("!! 预检失败：" + ex.Message, LogLevel.Error);
        }

        return r;
    }

    // ===================== 升级 =====================

    public static Result Run(string serverDir, Action<string, LogLevel> log, bool allowRestore = true)
    {
        var r = new Result();
        var sw = Stopwatch.StartNew();

        serverDir = Path.GetFullPath(serverDir);
        if (!File.Exists(Path.Combine(serverDir, "Server.MirDB")) ||
            !File.Exists(Path.Combine(serverDir, "Server.MirADB")))
        {
            r.Message = "目录里没有 Server.MirDB / Server.MirADB";
            return r;
        }

        r.DbBefore = Worker.PeekVersion(Path.Combine(serverDir, "Server.MirDB"));
        r.AdbBefore = Worker.PeekVersion(Path.Combine(serverDir, "Server.MirADB"));

        if (r.DbBefore < 0 || r.AdbBefore < 0)
        {
            r.Message = "读不出库版本，文件可能是坏的";
            return r;
        }

        if (r.DbBefore > TargetVersion || r.AdbBefore > TargetVersion)
        {
            r.Message = $"库版本（v{Math.Max(r.DbBefore, r.AdbBefore)}）比本工具（v{TargetVersion}）还新，读不了";
            return r;
        }

        if (r.DbBefore == TargetVersion && r.AdbBefore == TargetVersion)
        {
            r.Success = true;
            r.Message = $"两个库已经是 v{TargetVersion}，无需升级";
            r.DbAfter = r.DbBefore;
            r.AdbAfter = r.AdbBefore;
            return r;
        }

        try
        {
            // ---- 1. 备份 ----
            log("[1/4] 备份…", LogLevel.Info);
            r.BackupDir = CreateBackup(serverDir, r.DbBefore, r.AdbBefore);
            log($"      备份目录：{r.BackupDir}", LogLevel.Muted);

            // ---- 2. 加载 + 快照 ----
            log("[2/4] 读取数据库，记录升级前数据…", LogLevel.Info);
            var envir = LoadEnv(serverDir, log);
            var before = DbSnapshot.Snapshot(envir);
            r.SnapshotRows = before.Count;
            log($"      快照 {before.Count:N0} 行", LogLevel.Muted);

            // ---- 3. 存盘 ----
            log("[3/4] 按新版本重写数据库…", LogLevel.Info);
            envir.SaveDB();
            envir.SaveAccounts();

            r.DbAfter = Worker.PeekVersion(Path.Combine(serverDir, "Server.MirDB"));
            r.AdbAfter = Worker.PeekVersion(Path.Combine(serverDir, "Server.MirADB"));
            log($"      Server.MirDB  → v{r.DbAfter}  {Worker.SizeOf(Path.Combine(serverDir, "Server.MirDB")):N0} 字节", LogLevel.Muted);
            log($"      Server.MirADB → v{r.AdbAfter}  {Worker.SizeOf(Path.Combine(serverDir, "Server.MirADB")):N0} 字节", LogLevel.Muted);

            if (r.DbAfter != TargetVersion || r.AdbAfter != TargetVersion)
                throw new Exception($"存盘后版本异常：MirDB v{r.DbAfter} / MirADB v{r.AdbAfter}，期望 v{TargetVersion}");

            // ---- 4. 重新加载 + 比对 ----
            log("[4/4] 重新加载，逐条比对…", LogLevel.Info);
            envir.GameshopLog.Clear();
            if (!envir.LoadDB()) throw new Exception("升级后的库加载失败，数据可能已损坏");
            envir.LoadAccounts();

            var after = DbSnapshot.Snapshot(envir);
            log($"      快照 {after.Count:N0} 行", LogLevel.Muted);

            var diff = DiffLines(before, after);
            r.DiffCount = diff.Count;
            if (diff.Count > 0)
            {
                log($"      发现 {diff.Count} 处差异：", LogLevel.Error);
                foreach (var d in diff.Take(30)) log("        " + d, LogLevel.Error);
                if (diff.Count > 30) log($"        …（还有 {diff.Count - 30} 处）", LogLevel.Error);
                throw new Exception("升级前后数据不一致");
            }

            sw.Stop();
            r.Seconds = sw.Elapsed.TotalSeconds;
            r.Success = true;
            r.Message = $"v{r.DbBefore}/{r.AdbBefore} → v{TargetVersion}，{before.Count:N0} 行数据逐条一致";
            return r;
        }
        catch (Exception ex)
        {
            sw.Stop();
            r.Seconds = sw.Elapsed.TotalSeconds;
            r.Message = ex.Message;

            if (allowRestore && r.BackupDir != null)
            {
                try
                {
                    log("正在从备份还原…", LogLevel.Warn);
                    RestoreFrom(serverDir, r.BackupDir);
                    log("已还原为升级前的数据。", LogLevel.Good);
                    r.Message += "（已自动还原）";
                }
                catch (Exception ex2)
                {
                    log("!! 自动还原失败：" + ex2.Message, LogLevel.Error);
                    log($"!! 请手动把 {r.BackupDir} 里的两个文件复制回 {serverDir}", LogLevel.Error);
                    r.Message += $"（自动还原也失败了，请手动从 {r.BackupDir} 还原）";
                }
            }
            return r;
        }
    }

    // ===================== 加载 =====================

    /// <summary>加载服务端目录下的两个库。每次调用都先把工具侧的残留状态清干净。</summary>
    private static Envir LoadEnv(string serverDir, Action<string, LogLevel> log)
    {
        Directory.SetCurrentDirectory(serverDir);

        log("      读取 Configs\\Setup.ini", LogLevel.Muted);
        Settings.Load();

        var envir = Envir.Main;

        // LoadAccounts 不清这个字典，二次加载会抛重复键（服务端只加载一次，没有这个问题）
        envir.GameshopLog.Clear();

        // GetBuffInfo 只查 BuffInfoList，不灌进去的话读到带 BUFF 的角色会抛「XXX 尚未实施」
        envir.BuffInfoList.Clear();
        foreach (var b in BuffInfo.Load()) envir.BuffInfoList.Add(b);

        try
        {
            if (!envir.LoadDB())
                throw new Exception($"Server.MirDB 加载失败（版本 v{Envir.LoadVersion}，本工具支持 v{Envir.MinVersion} ~ v{TargetVersion}）");
        }
        catch (EndOfStreamException)
        {
            throw new Exception("Server.MirDB 读到一半就到底了 —— 文件被截断或损坏，请用备份恢复");
        }

        try
        {
            envir.LoadAccounts();
        }
        catch (EndOfStreamException)
        {
            throw new Exception("Server.MirADB 读到一半就到底了 —— 文件被截断或损坏，请用备份恢复");
        }
        catch (Exception ex)
        {
            throw new Exception($"Server.MirADB 加载失败：{ex.GetType().Name}: {ex.Message}");
        }

        log($"      地图 {envir.MapInfoList.Count} / 物品 {envir.ItemInfoList.Count} / 怪物 {envir.MonsterInfoList.Count}"
          + $" / 账号 {envir.AccountList.Count} / 角色 {envir.CharacterList.Count} / 英雄 {envir.HeroList.Count}", LogLevel.Muted);

        return envir;
    }

    // ===================== 备份 =====================

    public static string CreateBackup(string serverDir, int vDb, int vAdb)
    {
        string baseDir = Path.Combine(serverDir, "Back Up");
        Directory.CreateDirectory(baseDir);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string dir = Path.Combine(baseDir, $"dbupgrade-{stamp}");
        int n = 2;
        while (Directory.Exists(dir)) dir = Path.Combine(baseDir, $"dbupgrade-{stamp}_{n++}");

        Directory.CreateDirectory(dir);

        foreach (var name in new[] { "Server.MirDB", "Server.MirADB" })
        {
            string src = Path.Combine(serverDir, name);
            string dst = Path.Combine(dir, name);
            File.Copy(src, dst, true);

            string m1 = Worker.Md5(src);
            string m2 = Worker.Md5(dst);
            if (m1 != m2) throw new Exception($"备份校验失败（{name} 复制后 MD5 不一致）");
        }

        var mf = new StringBuilder();
        mf.AppendLine("Mir2 数据库升级备份");
        mf.AppendLine($"时间       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        mf.AppendLine($"源目录     : {serverDir}");
        mf.AppendLine($"工具目标版 : v{TargetVersion}");
        mf.AppendLine($"升级前版本 : MirDB v{vDb} / MirADB v{vAdb}");
        mf.AppendLine();
        foreach (var name in new[] { "Server.MirDB", "Server.MirADB" })
        {
            string p = Path.Combine(dir, name);
            mf.AppendLine($"{name,-14}: {new FileInfo(p).Length,12:N0} 字节   MD5 {Worker.Md5(p)}");
        }
        mf.AppendLine();
        mf.AppendLine("回滚：工具里点「回滚到备份」选本目录，或手动把这两个文件复制回上级目录。");
        File.WriteAllText(Path.Combine(dir, "备份说明.txt"), mf.ToString(), new UTF8Encoding(false));

        return dir;
    }

    public static void RestoreFrom(string serverDir, string backupDir)
    {
        foreach (var name in new[] { "Server.MirDB", "Server.MirADB" })
        {
            string src = Path.Combine(backupDir, name);
            if (!File.Exists(src)) throw new Exception($"备份里缺少 {name}");

            string dst = Path.Combine(serverDir, name);
            string tmp = dst + ".restore";

            File.Copy(src, tmp, true);
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(tmp, dst);
        }
    }

    // ===================== 比对 =====================

    /// <summary>比较两份快照。唯一允许的差异是版本行（v110 → v111）。</summary>
    public static List<string> DiffLines(IEnumerable<string> before, IEnumerable<string> after)
    {
        var ca = CountLines(before);
        var cb = CountLines(after);

        var result = new List<string>();
        foreach (var kv in ca)
        {
            if (kv.Key.StartsWith("VERSION|")) continue;
            cb.TryGetValue(kv.Key, out int other);
            for (int i = other; i < kv.Value; i++) result.Add("升级后缺失: " + Trim(kv.Key));
        }
        foreach (var kv in cb)
        {
            if (kv.Key.StartsWith("VERSION|")) continue;
            ca.TryGetValue(kv.Key, out int other);
            for (int i = other; i < kv.Value; i++) result.Add("升级后多出: " + Trim(kv.Key));
        }
        return result;
    }

    private static Dictionary<string, int> CountLines(IEnumerable<string> lines)
    {
        var d = new Dictionary<string, int>();
        foreach (var line in lines)
            d[line] = d.TryGetValue(line, out int n) ? n + 1 : 1;
        return d;
    }

    private static string Trim(string s) => s.Length <= 160 ? s : s.Substring(0, 160) + "…";

    public static string TempDir
    {
        get
        {
            string d = Path.Combine(Path.GetTempPath(), "Mir2DbUpgrade");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    // ===================== 服务端进程检测 =====================

    public static string DetectServerProcess()
    {
        foreach (var n in new[] { "Server", "Server.MirForms" })
        {
            try
            {
                if (Process.GetProcessesByName(n).Length > 0) return n;
            }
            catch { }
        }
        return null;
    }
}
