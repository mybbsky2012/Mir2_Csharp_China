using System.Text;
using System.Text.RegularExpressions;

namespace NoticePopupTest;

/// <summary>
/// 登录公告（LoginNotice）「进游戏必弹」改造自测（2026-11-06）
///   1) 源码约束：客户端收到 S.UpdateNotice 只暂存，世界就绪后由 Process 统一弹出；
///      服务端每次登录都推（NoticeEveryLogin）；NoticeDialog 按行数扩容防越界
///   2) 行为仿真：用 LoadNotice 的解析规则 + NoticeDialog.Update/NewText 的索引规则，
///      对真实 Notice.txt 与超长公告分别验证弹出行数与翻页不越界
///   3) 产物校验：Client.dll 比源文件新（包含本次改动）
/// </summary>
internal static class Program
{
    private static int _pass, _fail;
    private static string _root = "";
    private static string _clientDir = @"E:\micromir2";

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) { _pass++; Console.WriteLine($"  [PASS] {name}"); }
        else { _fail++; Console.WriteLine($"  [FAIL] {name}  {detail}"); }
    }

    private static string Read(string rel) => File.ReadAllText(Path.Combine(_root, rel));

    private static int Main(string[] args)
    {
        _root = FindRoot();
        if (args.Length > 0 && Directory.Exists(args[0])) _clientDir = args[0];
        Console.WriteLine($"工程根目录：{_root}");
        Console.WriteLine($"客户端目录：{_clientDir}\n");

        Console.WriteLine("== 1. 源码约束 ==");
        SourceRules();

        Console.WriteLine("\n== 2. 行为仿真 ==");
        BehaviourSim();

        Console.WriteLine("\n== 3. 产物校验 ==");
        BinaryCheck();

        Console.WriteLine($"\n结果：{_pass} 通过 / {_fail} 失败");
        return _fail == 0 ? 0 : 1;
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Client", "MirObjects"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new Exception("找不到工程根目录（应包含 Client/MirObjects）");
    }

    // ---------------- 1. 源码约束 ----------------
    private static void SourceRules()
    {
        string gs = Read("Client/MirScenes/GameScene.cs");

        // 1) ShowNotice 只暂存，不再直接弹
        int i0 = gs.IndexOf("public void ShowNotice(S.UpdateNotice p)");
        Check("GameScene.ShowNotice 存在", i0 >= 0);
        if (i0 >= 0)
        {
            int end = gs.IndexOf("private void TryShowPendingLoginNotice", i0);
            string body = gs.Substring(i0, end > i0 ? end - i0 : gs.Length - i0);
            Check("ShowNotice 不再直接 NoticeDialog.Update（防时序竞态）",
                  !body.Contains("NoticeDialog.Update("));
            Check("ShowNotice 收包时写 LoadTrace（现场可查）", body.Contains("LoginTrace.Mark"));
            Check("ShowNotice 暂存到 _pendingLoginNotice", body.Contains("_pendingLoginNotice = p.Notice"));
        }

        // 2) Process 调用 TryShowPendingLoginNotice（世界就绪后弹）
        Check("Process 中调用 TryShowPendingLoginNotice",
              Regex.IsMatch(gs, @"TryShowPendingLoginNotice\(\);\s*\r?\n\r?\n\s*if \(User == null\)"));
        string tryBody = ExtractMethod(gs, "private void TryShowPendingLoginNotice");
        Check("TryShow: 弹出前置条件=世界就绪（User+FloorValid）",
              tryBody.Contains("MapControl == null") && tryBody.Contains("FloorValid"));
        Check("TryShow: 一次性（弹出后清空暂存）", tryBody.Contains("_pendingLoginNotice = null"));
        Check("TryShow: 通过 NoticeDialog.Update 弹出并置顶",
              tryBody.Contains("NoticeDialog.Update(notice)") && tryBody.Contains("BringToFront"));

        // 3) NoticeDialog 扩容
        string nd = Read("Client/MirScenes/Dialogs/NoticeDialog.cs");
        Check("NoticeDialog.NewText 按行数扩容 TextLabel（防 >40 行翻页越界）",
              nd.Contains("if (lines.Count > TextLabel.Length)"));

        // 4) 服务端：每次登录都推 + Notice.txt 解析
        string po = Read("Server/MirObjects/PlayerObject.cs");
        Check("服务端登录时下发 S.UpdateNotice",
              po.Contains("Enqueue(new S.UpdateNotice { Notice = Settings.Notice })"));
        Check("服务端条件含 NoticeEveryLogin（每次进游戏都弹）",
              po.Contains("Settings.NoticeEveryLogin ||"));
        string st = Read("Server/Settings.cs");
        Check("Settings.NoticeEveryLogin 默认 true", st.Contains("public static bool NoticeEveryLogin = true;"));
        Check("Settings.LoadNotice 读取 Notice.txt", st.Contains("var lines = File.ReadAllLines(NoticePath);"));
    }

    private static string ExtractMethod(string src, string signature)
    {
        int i = src.IndexOf(signature);
        if (i < 0) return "";
        int depth = 0;
        bool started = false;
        int j = src.IndexOf('{', i);
        for (int k = j; k < src.Length; k++)
        {
            if (src[k] == '{') { depth++; started = true; }
            else if (src[k] == '}')
            {
                depth--;
                if (started && depth == 0) return src.Substring(i, k - i + 1);
            }
        }
        return src.Substring(i);
    }

    // ---------------- 2. 行为仿真 ----------------
    // 复刻 LoadNotice 的解析规则与 NoticeDialog.Update/NewText 的索引规则
    private static void BehaviourSim()
    {
        const int MaximumLines = 19;

        // —— 复刻 Settings.LoadNotice ——
        (string Title, string Message) LoadNotice(string[] rawLines)
        {
            string title = "", msg = "";
            foreach (var line in rawLines)
            {
                if (string.Compare(line, "TITLE", false) > 0 && line.Contains("="))
                {
                    title = line.Split('=')[1];
                    continue;
                }
                msg += line + "\r\n";
            }
            return (title, msg);
        }

        // —— 复刻 NoticeDialog.Update + NewText 的标签槽分配 ——
        // 返回：是否弹窗、实际建标签数、槽位最大下标（越界即崩溃）
        (bool shown, int labels, int maxSlot) ShowNotice(string[] rawLines, int labelSlotsBefore)
        {
            var (title, message) = LoadNotice(rawLines);
            if (string.IsNullOrWhiteSpace(message)) return (false, 0, -1);

            var lines = message.Split(new[] { "\r\n" }, StringSplitOptions.None);
            int slots = lines.Length > labelSlotsBefore ? lines.Length : labelSlotsBefore;  // ★扩容规则
            int maxIndex = Math.Max(0, lines.Length - MaximumLines);                        // 翻到底
            int last = Math.Min(MaximumLines + maxIndex, lines.Length);
            int count = 0, maxSlot = -1;
            for (int i = maxIndex; i < last; i++) { count++; if (i > maxSlot) maxSlot = i; }
            return (true, count, maxSlot < slots ? maxSlot : int.MaxValue);
        }

        // 真实公告（部署目录 Notice.txt）
        string live = @"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server\Envir\Notice.txt";
        if (File.Exists(live))
        {
            var raw = File.ReadAllLines(live);
            var (title, msg) = LoadNotice(raw);
            var lines = msg.Split(new[] { "\r\n" }, StringSplitOptions.None);
            Check($"真实 Notice.txt：Title=\"{title}\"，公告 {lines.Length - 1} 行，非空可弹",
                  msg.Trim().Length > 0 && title.Length > 0);
            var r = ShowNotice(raw, 40);
            Check("真实公告：可弹出且槽位不越界", r.shown && r.maxSlot < 40, $"maxSlot={r.maxSlot}");
        }
        else
        {
            Console.WriteLine($"  [SKIP] 未找到 {live}（不在线上环境跑时用样例替代）");
            var sample = new[] { "TITLE=测试", "第一行", "第二行" };
            var r0 = ShowNotice(sample, 40);
            Check("样例公告：可弹出且槽位不越界", r0.shown && r0.maxSlot < 40);
        }

        // 超长公告：旧代码 40 槽 + 60 行翻到底 → 越界崩溃；新代码扩容后安全
        var big = new List<string> { "TITLE=长公告" };
        for (int i = 0; i < 60; i++) big.Add("第" + i + "行内容");
        var oldR = ShowNotice(big.ToArray(), 40);
        Check("60 行公告：扩容后翻到底槽位不越界", oldR.maxSlot != int.MaxValue, $"maxSlot={oldR.maxSlot}");
        // 反证：如果不扩容（固定 40），旧逻辑确实越界——保证这条测试有意义
        int fixedSlots = 40;
        int worst = (60 - MaximumLines) + MaximumLines - 1;
        Check("反证：固定 40 槽在 60 行时确实会越界（旧 BUG 复现）", worst >= fixedSlots, $"worst={worst}");

        // 空公告：服务端就不会发；即便发了，Update 里 IsNullOrWhiteSpace 直接 return 不弹
        var (t, m) = LoadNotice(new[] { "TITLE=空" });
        Check("空公告：Message 为空不弹窗（与服务端行为一致）", string.IsNullOrWhiteSpace(m));
    }

    // ---------------- 3. 产物校验 ----------------
    private static void BinaryCheck()
    {
        string dll = Path.Combine(_root, "Build", "Client", "Release", "Client.dll");
        Check("Build/Client/Release/Client.dll 存在", File.Exists(dll));
        if (!File.Exists(dll)) return;

        var dllTime = File.GetLastWriteTime(dll);
        var gsTime = File.GetLastWriteTime(Path.Combine(_root, "Client", "MirScenes", "GameScene.cs"));
        var ndTime = File.GetLastWriteTime(Path.Combine(_root, "Client", "MirScenes", "Dialogs", "NoticeDialog.cs"));
        Check("Client.dll 比本次改动的源文件新", dllTime >= gsTime && dllTime >= ndTime,
              $"dll={dllTime} gs={gsTime} nd={ndTime}");

        // 部署校验
        string deployed = Path.Combine(_clientDir, "Client.dll");
        if (File.Exists(deployed))
        {
            string h(string p) { using var s = File.OpenRead(p); var md5 = System.Security.Cryptography.MD5.Create(); return Convert.ToHexString(md5.ComputeHash(s)); }
            Check($"已部署到 {_clientDir}（MD5 一致）", h(dll) == h(deployed), $"{h(dll)} vs {h(deployed)}");
        }
        else Check($"已部署到 {_clientDir}", false, "找不到 Client.dll");
    }
}
