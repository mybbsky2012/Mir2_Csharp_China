using System.Text;
using System.Text.RegularExpressions;

// ═══════════════════════════════════════════════════════════════════
// 假人日志静音排雷：「假人聊天不进聊天日志 / 假人上下线不进日志」
//
// 这三件事都必须在「唯一写入口」上加闸，否则只要有一条旁路，
// 2 万假人就能把 GUI 的两个文本框刷到卡死。所以本工具只做四件事：
//   ① 证明写入口唯一（EnqueueChat / LogOutReason 各只有一个调用点）
//   ② 证明该调用点被 `!(this is FakePlayerObject)` 紧贴着包住
//   ③ 证明假人自己的登录/下线路径（FakePlayerManager.Login）不写日志
//   ④ 证明服务端产物比源码新（防「改了源码没重编」）
//
// 用法：BotLogFilterTest.exe <源码根目录>
// ═══════════════════════════════════════════════════════════════════

internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, Func<string> body)
    {
        try
        {
            string detail = body();
            _pass++;
            Console.WriteLine("  [OK]   " + name + (string.IsNullOrEmpty(detail) ? "" : "  → " + detail));
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine("  [FAIL] " + name + "\n         " + ex.Message);
        }
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string root = args.Length > 0 ? args[0] : @"D:\mir2-20241027";
        string serverDir = Path.Combine(root, "Server");
        string playerPath = Path.Combine(serverDir, @"MirObjects\PlayerObject.cs");
        string managerPath = Path.Combine(serverDir, @"MirObjects\Player\FakePlayerManager.cs");
        string brainPath = Path.Combine(serverDir, @"MirObjects\Player\FakePlayerBrain.cs");
        string dllPath = Path.Combine(root, @"Build\Server\Release\Server.Library.dll");

        Console.WriteLine("源码根目录： " + root);
        Console.WriteLine();

        foreach (string p in new[] { playerPath, managerPath, brainPath })
            if (!File.Exists(p))
            {
                Console.WriteLine("找不到文件：" + p);
                return 2;
            }

        string player = File.ReadAllText(playerPath);
        string manager = File.ReadAllText(managerPath);
        string brain = File.ReadAllText(brainPath);

        // ══════════════════════════════════════════════
        Console.WriteLine("──── A. 聊天日志（聊天日志页签 ← MessageQueue.ChatLog）────");

        Check("①全服只有一个聊天日志写入口（除了 MessageQueue 自身的 EnqueueChat 定义）", () =>
        {
            var hits = new List<string>();
            foreach (string f in Directory.EnumerateFiles(serverDir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(@"\obj\") || f.Contains(@"\bin\")) continue;
                int n = Regex.Matches(File.ReadAllText(f), @"EnqueueChat\s*\(").Count;
                // MessageQueue.cs 里是方法定义，不算写入
                if (Path.GetFileName(f) == "MessageQueue.cs") n -= 1;
                for (int i = 0; i < n; i++) hits.Add(f.Substring(serverDir.Length + 1));
            }
            Assert(hits.Count == 1, "聊天日志写入点应为 1 个，实际 " + hits.Count + " 个：" + string.Join("、", hits.Distinct().ToArray()));
            return hits[0];
        });

        Check("②聊天日志写入被「假人排除」紧贴包住（中间不许夹别的语句）", () =>
        {
            string body = MethodBody(player, "public void Chat(string message");
            Assert(body.Length > 0, "没找到 PlayerObject.Chat 方法体");
            AssertGuarded(body, "MessageQueue.EnqueueChat(", "聊天日志");
            return "第一句之后";
        });

        Check("③假人发言确实走统一 Chat（没有绕过去直接广播）", () =>
        {
            Assert(Regex.IsMatch(brain, @"Player\.Chat\("), "FakePlayerBrain 里没调用 Player.Chat，假人可能另有发言通道");
            foreach (string f in Directory.EnumerateFiles(Path.Combine(serverDir, @"MirObjects\Player"), "*.cs"))
            {
                if (Path.GetFileName(f) != "FakePlayerBrain.cs") continue;
                string src = File.ReadAllText(f);
                Assert(!src.Contains("EnqueueChat"), "假人 AI 里直接写了聊天日志");
            }
            return "Player.Chat → 统一闸门";
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── B. 登录 / 退出日志（日志页签 ← MessageQueue.Log）────");

        Check("④退出日志被「假人排除」紧贴包住", () =>
        {
            string body = MethodBody(player, "public void StopGame(byte reason)");
            Assert(body.Length > 0, "没找到 PlayerObject.StopGame 方法体");
            AssertGuarded(body, "MessageQueue.Enqueue(logReason)", "退出日志");
            return "下线清理段";
        });

        Check("⑤LogOutReason 全服只有一处被调用（别处不会偷偷再记一份）", () =>
        {
            int calls = 0;
            foreach (string f in Directory.EnumerateFiles(serverDir, "*.cs", SearchOption.AllDirectories))
            {
                if (f.Contains(@"\obj\") || f.Contains(@"\bin\")) continue;
                string src = File.ReadAllText(f);
                int n = Regex.Matches(src, @"LogOutReason\s*\(\s*reason\s*\)").Count;   // 定义处是 (byte reason)
                calls += n;
            }
            Assert(calls == 1, "LogOutReason(reason) 调用点应为 1 个，实际 " + calls + " 个");
            return "唯一调用";
        });

        Check("⑥假人登录路径的失败日志必须限流（每秒重试不能每秒一条）", () =>
        {
            string body = MethodBody(manager, "private static bool Login(BotRecord rec)");
            Assert(body.Length > 0, "没找到 FakePlayerManager.Login 方法体");

            int count = 0;
            foreach (Match m in Regex.Matches(body, @"MessageQueue\.Instance\.Enqueue"))
            {
                count++;
                int prev = m.Index > 0 ? body.LastIndexOf("MessageQueue.Instance.Enqueue", m.Index - 1, StringComparison.Ordinal) : -1;
                int from = prev < 0 ? 0 : prev + 32;
                string pre = body.Substring(from, m.Index - from);
                Assert(pre.Contains("rec.FailCount <= 3"),
                    "Login 里有不受限流的日志写入（连败时会刷屏）：" + pre.Replace("\r", "").Replace("\n", " ").Trim());
            }
            Assert(count > 0, "Login 里一条日志都没有？失败路径至少要能看见");
            return count + " 处，全部限流";
        });

        Check("⑦假人管理器的日志一律带 [假人] 前缀（不冒充玩家上下线文案）", () =>
        {
            var bad = new List<string>();
            foreach (Match m in Regex.Matches(manager, @"MessageQueue\.Instance\.Enqueue\(\s*\$?""([^""]*)"))
            {
                if (!m.Groups[1].Value.StartsWith("[假人]"))
                    bad.Add(m.Groups[1].Value);
            }
            // 变量形式的参数无法静态判断，列出来要求人工核对
            int dynamicArgs = Regex.Matches(manager, @"MessageQueue\.Instance\.Enqueue\(\s*[^""$\s]").Count;
            Assert(bad.Count == 0, "这些日志没带 [假人] 前缀，容易被当成玩家事件：" + string.Join("；", bad.ToArray()));
            Assert(dynamicArgs == 0, "有 " + dynamicArgs + " 处 Enqueue 传的是变量，无法静态确认，请人工核对");
            return "全部带前缀";
        });

        Check("⑧真人登录日志不受影响（只拦假人，别把真人下线也静音）", () =>
        {
            Assert(Regex.IsMatch(player, @"if \(!\(this is FakePlayerObject\)\)\s*\r?\n\s*MessageQueue\.Enqueue\(logReason\)"),
                "假人闸门写反了？应当是「不是假人才写日志」");
            return "!(this is FakePlayerObject)";
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── C. 产物 ────");

        Check("⑨服务端产物比源码新（防「改了源码没重编」）", () =>
        {
            Assert(File.Exists(dllPath), "找不到 " + dllPath);
            DateTime src = File.GetLastWriteTime(playerPath);
            DateTime dll = File.GetLastWriteTime(dllPath);
            Assert(dll >= src, string.Format("Server.Library.dll（{0:HH:mm}）比 PlayerObject.cs（{1:HH:mm}）旧，需要重新编译",
                dll, src));
            return string.Format("{0:yyyy-MM-dd HH:mm}", dll);
        });

        Console.WriteLine();
        Console.WriteLine("──────────────────────────────────────────");
        Console.WriteLine(string.Format("通过 {0} 项，失败 {1} 项", _pass, _fail));
        return _fail == 0 ? 0 : 1;
    }

    // ── 抓方法体：从签名起做花括号配平（注释里的括号不影响，够用即可） ──
    private static string MethodBody(string src, string signature)
    {
        int i = src.IndexOf(signature, StringComparison.Ordinal);
        if (i < 0) return string.Empty;

        int brace = src.IndexOf('{', i);
        if (brace < 0) return string.Empty;

        int depth = 0;
        for (int j = brace; j < src.Length; j++)
        {
            if (src[j] == '{') depth++;
            else if (src[j] == '}')
            {
                depth--;
                if (depth == 0) return src.Substring(brace, j - brace + 1);
            }
        }
        return string.Empty;
    }

    // ── 断言某次写入前紧贴着一个「假人排除」判断 ──
    private static void AssertGuarded(string body, string call, string label)
    {
        int i = body.IndexOf(call, StringComparison.Ordinal);
        if (i < 0) throw new Exception("方法体里没有 " + call);

        int ifIdx = body.LastIndexOf("if (", i, StringComparison.Ordinal);
        if (ifIdx < 0 || i - ifIdx > 220)
            throw new Exception(label + " 前面没有紧邻的 if 判断 —— 可能被无条件写入（假人也会记）");

        string cond = body.Substring(ifIdx, i - ifIdx);
        if (!cond.Contains("!(this is FakePlayerObject)"))
            throw new Exception(label + " 前的判断不是「假人排除」：" + cond.Replace("\r", "").Replace("\n", " ").Trim());

        if (cond.Contains(";"))
            throw new Exception(label + " 与判断之间夹了别的语句，闸门没真正包住这次写入");
    }
}
