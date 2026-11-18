using System.Security.Cryptography;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace Mir2DbUpgrade;

/// <summary>
/// 子进程工作体。GUI 主进程不碰 Envir（静态单例只能初始化一次，且 LoadAccounts 里的
/// GameshopLog 是 Dictionary 且不清空，同进程二次加载必崩），所有读写都甩给独立的子进程。
///
/// 调用：Mir2DbUpgrade.exe --worker &lt;mode&gt; &lt;服务端目录&gt; &lt;日志文件&gt; [快照文件]
///   mode = snapshot  只读加载，写出快照文件
///   mode = upgrade   加载后存盘（把旧版本库写成工具内置的 Envir.Version）
/// </summary>
internal static class Worker
{
    private static StreamWriter _log;

    public static int Run(string[] args)
    {
        if (args.Length < 4) return 9;

        string mode = args[1];
        string dir = Path.GetFullPath(args[2]);
        string logPath = args[3];
        string extra = args.Length > 4 ? args[4] : null;

        _log = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };

        try
        {
            Directory.SetCurrentDirectory(dir);

            Write($"工作目录 : {dir}");
            if (!File.Exists("Server.MirDB") || !File.Exists("Server.MirADB"))
            {
                Write("!! 目录里找不到 Server.MirDB 或 Server.MirADB");
                return 2;
            }

            Write($"MirDB    : v{PeekVersion("Server.MirDB")}  {SizeOf("Server.MirDB"):N0} 字节");
            Write($"MirADB   : v{PeekVersion("Server.MirADB")}  {SizeOf("Server.MirADB"):N0} 字节");
            Write($"工具内置 : Envir.Version={Envir.Version}（最低可读 v{Envir.MinVersion}）");
            Write("");

            var envir = LoadAll();
            if (envir == null) return 3;

            switch (mode)
            {
                case "snapshot":
                    return DoSnapshot(envir, extra);

                case "upgrade":
                    return DoUpgrade(envir);

                case "roundtrip":
                    return DoRoundtrip(envir, extra);

                default:
                    Write($"!! 未知模式: {mode}");
                    return 9;
            }
        }
        catch (Exception ex)
        {
            Write("!! 未处理异常:");
            Write(ex.ToString());
            return 8;
        }
        finally
        {
            try { _log.Flush(); _log.Dispose(); } catch { }
        }
    }

    // ===================== 模式 =====================

    private static int DoSnapshot(Envir envir, string snapPath)
    {
        var snap = DbSnapshot.Snapshot(envir);
        File.WriteAllLines(snapPath, snap, new UTF8Encoding(false));
        Write($"快照已写出: {snap.Count:N0} 行 → {Path.GetFileName(snapPath)}");
        Write("");
        Summary(envir, Write);
        ProbeRawAccounts(Write);
        return 0;
    }

    private static int DoUpgrade(Envir envir)
    {
        Write("开始存盘（会把整库按当前版本重写）…");

        envir.SaveDB();
        Write($"  Server.MirDB  → v{PeekVersion("Server.MirDB")}  {SizeOf("Server.MirDB"):N0} 字节");

        envir.SaveAccounts();
        Write($"  Server.MirADB → v{PeekVersion("Server.MirADB")}  {SizeOf("Server.MirADB"):N0} 字节");

        Write("");
        Write($"MD5 MirDB  = {Md5("Server.MirDB")}");
        Write($"MD5 MirADB = {Md5("Server.MirADB")}");
        Write("存盘完成。");
        return 0;
    }

    // ===================== 单进程往返验证 =====================

    /// <summary>
    /// 同一个进程内：加载 → 快照A → 存盘 → 清状态 → 重新加载 → 快照B。
    /// 用来验证「清掉 GameshopLog 后二次加载是否干净」，如果这条路成立，
    /// 升级流程就不需要再启动子进程了。
    /// </summary>
    private static int DoRoundtrip(Envir envir, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) prefix = "roundtrip";

        var a = DbSnapshot.Snapshot(envir);
        File.WriteAllLines(prefix + "_before.txt", a, new UTF8Encoding(false));
        Write($"快照A {a.Count:N0} 行");

        envir.GameshopLog.Clear();
        envir.SaveDB();
        envir.SaveAccounts();
        Write("存盘完成");

        envir.GameshopLog.Clear();
        Write("清空 GameshopLog，重新加载…");

        if (!envir.LoadDB())
        {
            Write("!! 重新 LoadDB 失败");
            return 5;
        }

        try
        {
            envir.LoadAccounts();
        }
        catch (Exception ex)
        {
            Write($"!! 重新 LoadAccounts 失败: {ex.GetType().Name}: {ex.Message}");
            return 6;
        }

        var b = DbSnapshot.Snapshot(envir);
        File.WriteAllLines(prefix + "_after.txt", b, new UTF8Encoding(false));
        Write($"快照B {b.Count:N0} 行");

        int same = 0;
        for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            if (a[i] == b[i]) same++;

        Write($"逐行相同 {same:N0} 行 / 较短一方 {Math.Min(a.Count, b.Count):N0} 行");
        Write(same == Math.Min(a.Count, b.Count) && a.Count == b.Count ? "★ 两次快照完全一致" : "!! 快照有差异");
        return 0;
    }

    // ===================== 加载 =====================
    private static Envir LoadAll()
    {
        Write("读取配置 Configs\\Setup.ini …");
        Settings.Load();

        var envir = Envir.Main;

        // GetBuffInfo 只查 Envir.BuffInfoList，不灌进去的话读到带 BUFF 的角色直接抛
        // 「XXX 尚未实施」（服务端启动时也是先灌再读）。
        envir.BuffInfoList.Clear();
        foreach (var buff in BuffInfo.Load()) envir.BuffInfoList.Add(buff);
        Write($"BUFF 定义 {envir.BuffInfoList.Count} 种");

        Write("加载 Server.MirDB …");
        if (!envir.LoadDB())
        {
            Write("!! LoadDB 失败（库版本超出本工具支持范围）");
            return null;
        }
        Write($"  地图 {envir.MapInfoList.Count} / 物品 {envir.ItemInfoList.Count} / 怪物 {envir.MonsterInfoList.Count} "
            + $"/ NPC {envir.NPCInfoList.Count} / 任务 {envir.QuestInfoList.Count}");

        Write("加载 Server.MirADB …");
        try
        {
            envir.LoadAccounts();
        }
        catch (Exception ex)
        {
            Write($"!! LoadAccounts 失败: {ex.GetType().Name}: {ex.Message}");
            Write(ex.StackTrace);
            return null;
        }
        Write($"  账号 {envir.AccountList.Count} / 角色 {envir.CharacterList.Count} / 英雄 {envir.HeroList.Count}");
        Write("");
        return envir;
    }

    public static void Summary(Envir e, Action<string> w)
    {
        int mail = e.CharacterList.Sum(c => c?.Mail?.Count ?? 0);
        int bag = e.CharacterList.Sum(c => c?.Inventory?.Count(i => i != null) ?? 0);
        int eq = e.CharacterList.Sum(c => c?.Equipment?.Count(i => i != null) ?? 0);
        var resp = e.MapInfoList.Where(m => m?.Respawns != null).Sum(m => m.Respawns.Count);

        w("---- 数据规模 ----");
        w($"  地图 {e.MapInfoList.Count}（刷怪点 {resp:N0}）  物品 {e.ItemInfoList.Count}  怪物 {e.MonsterInfoList.Count}  NPC {e.NPCInfoList.Count}");
        w($"  任务 {e.QuestInfoList.Count}  技能 {e.MagicInfoList.Count}  商店 {e.GameShopList.Count}  攻城 {e.ConquestInfoList.Count}");
        w($"  账号 {e.AccountList.Count}  角色 {e.CharacterList.Count}  英雄 {e.HeroList.Count}  拍卖 {e.Auctions.Count}");
        w($"  邮件 {mail}  角色背包物品 {bag}  角色装备 {eq}");
        w($"  ID: 账号={e.NextAccountID} 角色={e.NextCharacterID} 英雄={e.NextHeroID} 物品={e.NextUserItemID} 邮件={e.NextMailID}");
        w("");
    }

    /// <summary>
    /// 羽化状态体检：谁登仙了、几重、有没有旧编码残留。
    /// 迁移前后各跑一次，能直观看出「有没有动到人」。
    /// </summary>
    public static void HumUpAudit(Envir e, Action<string> w)
    {
        var dist = new int[11];
        var names = new List<string>();
        int legacyBuffs = 0;
        var legacyNames = new List<string>();

        foreach (var c in e.CharacterList)
        {
            if (c == null) continue;

            int legacy = 0;
            if (c.Buffs != null)
            {
                foreach (var b in c.Buffs)
                {
                    if (b == null) continue;
                    int t = (int)b.Type;
                    if (t == (int)BuffType.Custom1 || (t >= 232 && t <= 241)) legacy++;
                }
            }

            if (legacy > 0)
            {
                legacyBuffs += legacy;
                if (legacyNames.Count < 10) legacyNames.Add($"{c.Name}({legacy})");
            }

            int stage = c.GetSelectHumUpStage();
            if (stage <= 0) continue;

            dist[Math.Clamp(stage, 1, 10)]++;
            if (names.Count < 20)
                names.Add($"{c.Name}  Lv{c.Level}  {c.Class}  {stage} 重");
        }

        int total = dist.Sum();
        var distText = string.Join("  ", Enumerable.Range(1, 10).Where(i => dist[i] > 0).Select(i => $"{i}重={dist[i]}"));

        w("---- 羽化状态 ----");
        w($"  羽化角色 {total} 个" + (total > 0 ? $"（{distText}）" : ""));
        w(legacyBuffs > 0
            ? $"  旧编码残留 {legacyBuffs} 条：{string.Join(", ", legacyNames)}"
            : "  旧编码残留 0 条（已是 159~169）");

        foreach (var n in names) w("    " + n);
        if (total > names.Count) w($"    …（还有 {total - names.Count} 个）");
        w("");
    }

    /// <summary>
    /// 按二进制格式再数一遍账号记录（不经过 LoadAccounts 的过滤）。
    /// Envir.LoadAccounts 里有一条 `if (i > 0 && NextAccount.Characters.Count == 0) continue;`
    /// —— 历史遗留的清理逻辑，会在加载时把空账号丢掉且不可逆，升级前必须让用户知道。
    /// </summary>
    public static void ProbeRawAccounts(Action<string> w)
    {
        try
        {
            using var fs = File.OpenRead("Server.MirADB");
            using var br = new BinaryReader(fs);

            int ver = br.ReadInt32();
            int cver = br.ReadInt32();
            br.ReadInt32();   // NextAccountID
            br.ReadInt32();   // NextCharacterID
            br.ReadUInt64();  // NextUserItemID
            if (ver > 98) br.ReadInt32(); // NextHeroID
            br.ReadInt32();   // GuildCount
            br.ReadInt32();   // NextGuildID

            if (ver > 102)
            {
                int hc = br.ReadInt32();
                for (int i = 0; i < hc; i++) _ = new HeroInfo(br, ver, cver);
            }

            int accCount = br.ReadInt32();
            int empty = 0, total = 0;
            var emptyNames = new List<string>();

            for (int i = 0; i < accCount; i++)
            {
                var a = new AccountInfo(br);
                total++;
                if (i > 0 && a.Characters.Count == 0)
                {
                    empty++;
                    if (emptyNames.Count < 10) emptyNames.Add(a.AccountID);
                }
            }

            w("---- 原库体检 ----");
            w($"  账号记录 {total} 条，其中空账号（0 角色）{empty} 条");
            if (empty > 0)
            {
                w($"  !! 这 {empty} 条会在升级时被清理（历史清理逻辑，不可逆）");
                w($"     示例: {string.Join(", ", emptyNames)}");
            }
            w("");
        }
        catch (Exception ex)
        {
            w($"[体检] 原始账号扫描跳过: {ex.GetType().Name}: {ex.Message}");
            w("");
        }
    }

    // ===================== 公共小工具 =====================

    public static int PeekVersion(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);
            return br.ReadInt32();
        }
        catch { return -1; }
    }

    public static long SizeOf(string path)
    {
        try { return new FileInfo(path).Length; } catch { return -1; }
    }

    public static string Md5(string path)
    {
        using var md5 = MD5.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(fs)).ToLowerInvariant();
    }

    private static void Write(string s) => _log?.WriteLine(s);
}
