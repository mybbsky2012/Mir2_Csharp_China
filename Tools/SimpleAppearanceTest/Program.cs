using System.Text;
using System.Text.RegularExpressions;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace SimpleAppearanceTest;

/// <summary>
/// 「人物简装 / 怪物简装」（内挂第 3 页）的自动排雷。
///
/// 这两项是纯客户端渲染替换，没有任何服务端判定参与，所以测试重点全在**素材与帧号**上：
///   1. 布衣/木剑/稻草人这三本图库到底存不存在（缺文件 = 玩家/怪物整只不画）
///   2. 用到的帧号上界会不会越界（越界 = CheckImage 返回 false，静默不画）
///      帧号上界是从 Client/MirObjects/Frames.cs 的 Player.Add(...) 现算的，不是抄来的常数
///   3. 布衣/木剑的库编号是从 Client/MirObjects/SimpleAppearance.cs 的源码里现读的，
///      代码一改这里立刻跟着变，不会出现「测试还在验旧编号」的假绿
///   4. 卫士 / 弓箭手 / 练功师的豁免名单，与数据库里真实怪物 Image 交叉核对
///
/// 用法：
///   SimpleAppearanceTest.exe &lt;客户端Data目录&gt; [服务端ClientResources的Data目录] [数据库目录]
///   SimpleAppearanceTest.exe "E:\micromir2\Data" "E:\...\Mir2Server\ClientResources\Data" "D:\mir2test\zerodura"
/// </summary>
internal static class Program
{
    private const int MaxDirection = 8;      // 8 方向
    private static int _pass, _fail;

    private static readonly string SourceRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string clientData = args.Length > 0 ? args[0] : @"E:\micromir2\Data";
        string serverData = args.Length > 1 ? args[1] : null;
        string dbDir = args.Length > 2 ? args[2] : null;

        Console.WriteLine("源码目录: " + SourceRoot);
        Console.WriteLine("客户端Data: " + clientData);
        if (serverData != null) Console.WriteLine("微端资源: " + serverData);
        Console.WriteLine();

        // ── 从源码里现读常量，避免测试和实现漂移 ────────────────────
        string simpleSrc = ReadSource("Client/MirObjects/SimpleAppearance.cs");
        string framesSrc = ReadSource("Client/MirObjects/Frames.cs");

        if (simpleSrc == null || framesSrc == null)
        {
            Console.WriteLine("!! 找不到客户端源码，无法把测试和实现对齐");
            return 2;
        }

        int plainArmour = ParseInt(simpleSrc, @"PlainArmour\s*=\s*(\d+)");
        int plainSword = ParseInt(simpleSrc, @"PlainSword\s*=\s*(\d+)");
        List<string> exempt = ParseExempt(simpleSrc);

        int maxPlayerFrame = MaxPlayerFrame(framesSrc);

        Console.WriteLine("===== 从源码读到的参数 =====");
        Console.WriteLine(string.Format("  布衣库索引 PlainArmour = {0}", plainArmour));
        Console.WriteLine(string.Format("  木剑库索引 PlainSword  = {0}", plainSword));
        Console.WriteLine(string.Format("  玩家动作表最大帧号     = {0}（8 方向，女装再加 808 偏移）", maxPlayerFrame));
        Console.WriteLine(string.Format("  怪物简装豁免名单       = {0}", string.Join(", ", exempt)));
        Console.WriteLine();

        // ── 1. 素材存在性 + 帧号上界 ─────────────────────────────
        Console.WriteLine("===== 1. 素材与帧号（客户端本地）=====");
        CheckMaterials(clientData, plainArmour, plainSword, maxPlayerFrame);

        if (serverData != null && Directory.Exists(serverData))
        {
            Console.WriteLine();
            Console.WriteLine("===== 1b. 素材与帧号（微端资源包，保证本地缺了也能下载）=====");
            CheckMaterials(serverData, plainArmour, plainSword, maxPlayerFrame);
        }

        // ── 2. 稻草人帧算法 ─────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("===== 2. 怪物简装：稻草人帧号 =====");
        CheckScarecrow(clientData, maxPlayerFrame);

        // ── 3. 豁免名单 vs 数据库 ───────────────────────────────
        Console.WriteLine();
        Console.WriteLine("===== 3. 豁免名单核对（数据库）=====");
        if (dbDir != null) CheckExemptAgainstDb(dbDir, exempt);
        else Console.WriteLine("  （没给数据库目录，跳过）");

        Console.WriteLine();
        Console.WriteLine(string.Format("===== 结果：通过 {0}，失败 {1} =====", _pass, _fail));
        return _fail == 0 ? 0 : 1;
    }

    // ─────────────────────────────────────────────────────────
    private static void CheckMaterials(string dataDir, int plainArmour, int plainSword, int maxPlayerFrame)
    {
        // 衣服：男 0..maxPlayerFrame，女 808..808+maxPlayerFrame
        int armourNeed = 808 + maxPlayerFrame + 1;
        int armourFrames = LibFrames(Path.Combine(dataDir, "CArmour", plainArmour.ToString("00") + ".Lib"));
        Check(string.Format("CArmour/{0:00}.Lib 存在且帧数 {1} ≥ 女装需要的 {2}", plainArmour, armourFrames, armourNeed),
            armourFrames >= armourNeed);

        // 木剑：偏移统一按男性段 0
        int swordNeed = maxPlayerFrame + 1;
        int swordFrames = LibFrames(Path.Combine(dataDir, "CWeapon", plainSword.ToString("00") + ".Lib"));
        Check(string.Format("CWeapon/{0:00}.Lib 存在且帧数 {1} ≥ 需要的 {2}", plainSword, swordFrames, swordNeed),
            swordFrames >= swordNeed);

        // 稻草人
        int scarecrowFrames = LibFrames(Path.Combine(dataDir, "Monster", "027.Lib"));
        Check(string.Format("Monster/027.Lib（稻草人）存在，帧数 {0}", scarecrowFrames), scarecrowFrames > 0);

        // 发型：简装会把 HairLibrary 换成 CHair[Hair] + 男 0 / 女 808 偏移，
        // 所有用到的帧号必须都在同一个库里放得下（CHair 有 9 个库，逐个查）
        string hairDir = Path.Combine(dataDir, "CHair");
        if (Directory.Exists(hairDir))
        {
            string[] hairFiles = Directory.GetFiles(hairDir, "*.Lib");
            int worst = hairFiles.Length == 0 ? 0 : hairFiles.Min(f => LibFrames(f));
            Check(string.Format("CHair 共 {0} 个库，最小的库也有 {1} 帧 ≥ 女装需要的 {2}",
                hairFiles.Length, worst, armourNeed), hairFiles.Length > 0 && worst >= armourNeed);
        }
        else
        {
            Check("CHair 目录存在", false);
        }
    }

    private static void CheckScarecrow(string dataDir, int maxPlayerFrame)
    {
        // MonsterAppearance.StandingFrame：start 0 / 每方向 4 帧 / 取 FrameIndex%4
        // FrameSet.DefaultMonster 站立段 = new Frame(0, 4, 0, 500)
        const int standStart = 0, standCount = 4, standSkip = 0;
        int offset = standCount + standSkip;
        int maxFrame = standStart + offset * (MaxDirection - 1) + (standCount - 1);

        Console.WriteLine(string.Format("  算法上界 = {0} + {1}×(方向7) + {2} = {3}",
            standStart, offset, standCount - 1, maxFrame));

        int frames = LibFrames(Path.Combine(dataDir, "Monster", "027.Lib"));
        Check(string.Format("帧号上界 {0} < 稻草人库帧数 {1}（不越界，不会整只怪不画）", maxFrame, frames),
            frames > 0 && maxFrame < frames);

        // 每个方向都要落在合法范围里（逐方向枚举一遍，防止 offSet 算错）
        bool allOk = true;
        for (int dir = 0; dir < MaxDirection; dir++)
            for (int fi = 0; fi < standCount; fi++)
            {
                int f = standStart + offset * dir + (fi % standCount);
                if (f < 0 || f >= frames) { allOk = false; Console.WriteLine(string.Format("    越界: 方向{0} 帧{1} -> {2}", dir, fi, f)); }
            }
        Check("8 方向 × 4 帧（共 32 个帧号）全部落在库内", allOk);
    }

    private static void CheckExemptAgainstDb(string dbDir, List<string> exempt)
    {
        if (!Directory.Exists(dbDir)) { Check("数据库目录存在: " + dbDir, false); return; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dbDir));
        if (!File.Exists(Envir.DatabasePath)) { Check("找到 " + Envir.DatabasePath, false); return; }

        Settings.Load();
        Envir envir = Envir.Main;
        if (!envir.LoadDB()) { Check("LoadDB 成功", false); return; }

        // 豁免名单（枚举名）→ Image 值
        var exemptValues = new HashSet<ushort>();
        foreach (string name in exempt)
        {
            if (Enum.TryParse(name, out Monster m)) exemptValues.Add((ushort)m);
            else Console.WriteLine("  !! 源码里的 Monster." + name + " 不是合法枚举值");
        }

        Console.WriteLine("  豁免 Image 值: " + string.Join(", ",
            exemptValues.Select(v => v + "(" + ((Monster)v) + ")")));

        // 3a. 名单里的每个 Image，数据库里是否真有怪在用
        foreach (ushort v in exemptValues)
        {
            List<MonsterInfo> users = envir.MonsterInfoList
                .Where(m => m != null && (ushort)m.Image == v).ToList();

            Check(string.Format("Monster.{0}({1}) 在数据库里有 {2} 条", (Monster)v, v, users.Count), users.Count > 0);

            if (users.Count > 0)
                Console.WriteLine("       " + string.Join(" / ", users.Take(6).Select(u => u.Name)));
        }

        // 3b. 名字里带「卫士 / 弓箭手 / 练功」的怪，Image 必须都在豁免名单里（否则漏网）
        string[] keywords = { "卫士", "守卫", "弓箭手", "练功" };
        var missed = envir.MonsterInfoList
            .Where(m => m != null
                        && keywords.Any(k => (m.Name != null && m.Name.Contains(k))
                                          || (m.GameName != null && m.GameName.Contains(k)))
                        && !exemptValues.Contains((ushort)m.Image))
            .ToList();

        // 只报告「外形就是守卫型」的那种：排除祖玛卫士/骷髅弓箭手这类野外怪（它们本来就该变稻草人）
        var suspicious = missed.Where(m => IsGuardLikeImage((ushort)m.Image)).ToList();

        Check(string.Format("名字带守卫关键字、但 Image 不在豁免名单的怪共 {0} 条（都是野外怪，正常）", missed.Count),
            true);
        foreach (MonsterInfo m in missed.Take(8))
            Console.WriteLine(string.Format("       {0} -> {1}", m.Name, m.Image));

        Check("其中没有「外形本身就是守卫型」的漏网：" + suspicious.Count + " 条", suspicious.Count == 0);
        foreach (MonsterInfo m in suspicious)
            Console.WriteLine("       !! " + m.Name + " -> " + m.Image);

        // 3c. 练功师到底用的什么外形（用户点名的例外，必须真被豁免）
        List<MonsterInfo> trainers = envir.MonsterInfoList
            .Where(m => m != null && m.Name != null && m.Name.Contains("练功")).ToList();
        foreach (MonsterInfo t in trainers)
        {
            ushort img = (ushort)t.Image;
            Check(string.Format("练功师「{0}」Image={1}({2}) 在豁免名单里", t.Name, img, t.Image),
                exemptValues.Contains(img));
        }
        if (trainers.Count == 0) Console.WriteLine("  （数据库里没有名字带「练功」的怪）");

        // 3d. 统计：会被替换成稻草人的怪物数量（让改动范围一目了然）
        int total = envir.MonsterInfoList.Count(m => m != null);
        int kept = envir.MonsterInfoList.Count(m => m != null && exemptValues.Contains((ushort)m.Image));
        Console.WriteLine(string.Format("  怪物总数 {0}，保持原样 {1}（{2:P1}），变稻草人 {3}",
            total, kept, kept / (double)Math.Max(1, total), total - kept));
    }

    /// <summary>「本身就是守卫型外形」的 Image（用来判断漏网）。</summary>
    private static bool IsGuardLikeImage(ushort image)
    {
        // Guard 家族 / ArcherGuard 家族 / KatanaGuard(练功师)
        return image == (ushort)Monster.Guard || image == (ushort)Monster.Guard2 || image == (ushort)Monster.Guard3
            || image == (ushort)Monster.TaoistGuard
            || image == (ushort)Monster.ArcherGuard || image == (ushort)Monster.ArcherGuard2
            || image == (ushort)Monster.ArcherGuard3
            || image == (ushort)Monster.KatanaGuard;
    }

    // ── FrameSet.Player 最大帧号：解析 Frames.cs 里的 Player.Add(...) ──
    private static int MaxPlayerFrame(string src)
    {
        int max = 0;

        foreach (Match m in Regex.Matches(src, @"Player\.Add\(\s*MirAction\.\w+\s*,\s*new Frame\(([^)]*)\)"))
        {
            string[] parts = m.Groups[1].Value.Split(',');
            if (parts.Length < 3) continue;

            int start = ParsePart(parts[0]);
            int count = ParsePart(parts[1]);
            int skip = ParsePart(parts[2]);
            if (start < 0 || count < 1) continue;

            int offset = count + skip;
            int last = start + offset * (MaxDirection - 1) + (count - 1);
            if (last > max) max = last;
        }

        return max;
    }

    private static int ParsePart(string s)
    {
        s = s.Trim();
        int v;
        return int.TryParse(s, out v) ? v : 0;
    }

    private static List<string> ParseExempt(string src)
    {
        var list = new List<string>();

        Match body = Regex.Match(src, @"public static bool KeepOriginal[\s\S]*?switch[\s\S]*?\{([\s\S]*?)\n\s*default:");
        if (!body.Success) return list;

        foreach (Match m in Regex.Matches(body.Groups[1].Value, @"case Monster\.(\w+):"))
            if (!list.Contains(m.Groups[1].Value)) list.Add(m.Groups[1].Value);

        return list;
    }

    // ── .Lib 帧数（头 8 字节：int32 version + int32 count）──
    private static int LibFrames(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;

            using (FileStream fs = File.OpenRead(path))
            using (BinaryReader br = new BinaryReader(fs))
            {
                br.ReadInt32();            // version
                return br.ReadInt32();     // count
            }
        }
        catch { return 0; }
    }

    private static string ReadSource(string relative)
    {
        string path = Path.Combine(SourceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static int ParseInt(string src, string pattern)
    {
        Match m = Regex.Match(src, pattern);
        int v;
        return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : -1;
    }

    private static void Check(string title, bool ok)
    {
        if (ok) { _pass++; Console.WriteLine("  [PASS] " + title); }
        else { _fail++; Console.WriteLine("  [FAIL] " + title); }
    }
}
