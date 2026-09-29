using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// 微端客户端逻辑验证。
///
/// 客户端的 ResourceDownloader 是 internal 的，正常无法从外部调用；
/// 这里用反射加载真实的 Client.dll，直接驱动它跑一遍完整的按需下载流程，
/// 从而在不启动游戏界面的前提下验证微端的核心承诺：
///
///   1. 启动阶段的批量图库登记**绝不产生任何下载**（否则就退化成"一次性全量加载"）；
///   2. 资源真正被用到时才下载，且请求立刻返回、不阻塞渲染线程；
///   3. 少数"缺了就没法继续"的资源可以有限等待；
///   4. 服务端没有的文件不做无谓的 HTTP 往返，失败后本次运行不再重试。
///
/// 用法：MicroTest.exe [客户端输出目录] [测试根目录] [资源服务地址]
/// </summary>
class Program
{
    private static string _clientDir;
    private static int _pass;
    private static int _fail;

    private static MethodInfo _ensure1;
    private static MethodInfo _ensure2;
    private static MethodInfo _ensureBlocking;
    private static MethodInfo _beginBulk;
    private static MethodInfo _endBulk;
    private static PropertyInfo _pending;
    private static PropertyInfo _completed;
    private static MethodInfo _getCount;
    private static MethodInfo _toRelative;

    static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        _clientDir = args.Length > 0 ? args[0] : @"D:\BaiduNetdiskDownload\mir2-20241027\Build\Client\Debug";
        string testRoot = args.Length > 1 ? args[1] : @"D:\mir2_micro_test";
        string host = args.Length > 2 ? args[2] : "http://127.0.0.1:5679/";

        // Client.dll 依赖 SlimDX / NAudio 等，从客户端输出目录解析
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            string p = Path.Combine(_clientDir, name.Name + ".dll");
            return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
        };

        Console.WriteLine("客户端目录: " + _clientDir);
        Console.WriteLine("测试根目录: " + testRoot);
        Console.WriteLine("资源服务  : " + host);
        Console.WriteLine();

        try
        {
            var asm = Assembly.LoadFrom(Path.Combine(_clientDir, "Client.dll"));

            var settings = asm.GetType("Client.Settings", true);
            SetStatic(settings, "MicroClient", true);
            SetStatic(settings, "MicroHost", host);
            SetStatic(settings, "MicroTimeout", 8000);
            SetStatic(settings, "MicroLog", false);
            SetStatic(settings, "MicroConcurrency", 2);
            SetStatic(settings, "MicroRateLimit", 0);
            SetStatic(settings, "MicroHint", false);
            SetStatic(settings, "P_Client", testRoot.TrimEnd('\\') + "\\");

            var rd = asm.GetType("Client.Utils.ResourceDownloader", true);
            const BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Static;

            _ensure1 = rd.GetMethod("EnsureLocalFile", SF, null, new[] { typeof(string) }, null);
            _ensure2 = rd.GetMethod("EnsureLocalFile", SF, null, new[] { typeof(string), typeof(bool).MakeByRefType() }, null);
            _ensureBlocking = rd.GetMethod("EnsureLocalFileBlocking", SF, null, new[] { typeof(string), typeof(int) }, null);
            _beginBulk = rd.GetMethod("BeginBulkLoad", SF);
            _endBulk = rd.GetMethod("EndBulkLoad", SF);
            _pending = rd.GetProperty("PendingCount", SF);
            _completed = rd.GetProperty("CompletedCount", SF);
            _getCount = rd.GetMethod("GetExpectedLibraryCount", SF);
            _toRelative = rd.GetMethod("ToRelativePath", SF);

            // 准备一个干净的测试根目录
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            Directory.CreateDirectory(Path.Combine(testRoot, "Data", "Monster"));

            // ---------- 用例 1：路径换算 ----------
            string local000 = Path.Combine(testRoot, "Data", "Monster", "000.Lib");
            string rel = (string)_toRelative.Invoke(null, new object[] { local000 });
            Check("路径换算 Data/Monster/000.Lib", rel == "Data/Monster/000.Lib", "实际=" + rel);

            // ---------- 用例 2：批量登记阶段绝不能触发下载 ----------
            // 模拟 Libraries.LoadGameLibraries：启动时把几百个图库目录登记一遍。
            // 这些文件在服务端**是真实存在的**，所以"一个都没下"才是正确行为。
            _beginBulk.Invoke(null, null);
            for (int i = 0; i < 200; i++)
            {
                string p = Path.Combine(testRoot, "Data", "Monster", i.ToString("000") + ".Lib");
                _ensure1.Invoke(null, new object[] { p });
            }
            int bulkPending = Pending();
            _endBulk.Invoke(null, null);

            Check("批量登记阶段不排队下载（剩余任务=0）", bulkPending == 0, "剩余=" + bulkPending);
            Check("批量登记阶段没有任何文件落地", !File.Exists(local000), "000.Lib 竟然被下载了");

            // ---------- 用例 3：真正用到时才下载，且不阻塞调用线程 ----------
            bool first = (bool)_ensure1.Invoke(null, new object[] { local000 });
            Check("首次请求立即返回 false（不阻塞渲染线程）", !first, "返回=" + first);
            Check("请求已排入后台队列", Pending() > 0, "剩余=" + Pending());

            bool arrived = WaitUntil(delegate () { return File.Exists(local000); }, 15000);
            Check("后台悄悄下载完成", arrived);
            if (arrived)
            {
                byte[] data = File.ReadAllBytes(local000);
                bool contentOk = data.Length == 107 && data[0] == (byte)'T' && data[1] == (byte)'E';
                Check("下载内容正确", contentOk, "长度=" + data.Length);
            }
            Check("二次调用命中本地缓存", (bool)_ensure1.Invoke(null, new object[] { local000 }));

            // ---------- 用例 4：queued 出参语义 ----------
            string local050 = Path.Combine(testRoot, "Data", "Monster", "050.Lib");
            object[] queueArgs = { local050, null };
            _ensure2.Invoke(null, queueArgs);
            Check("queued=true 表示本次已安排后台下载", queueArgs[1] is bool && (bool)queueArgs[1], "queued=" + queueArgs[1]);

            // ---------- 用例 5：必须就绪时可以有限等待（进地图 / 着色器 / 音效索引） ----------
            string local001 = Path.Combine(testRoot, "Data", "Monster", "001.Lib");
            bool blocked = (bool)_ensureBlocking.Invoke(null, new object[] { local001, 15000 });
            Check("阻塞式等待能拿到关键资源", blocked && File.Exists(local001), "返回=" + blocked);

            // ---------- 用例 6：服务端没有的文件不做无谓请求 ----------
            // 该文件既不在本地、也不在服务端清单里，应当被清单预判直接挡掉，
            // 一次 HTTP 都不该发出去（由 verify_microclient_load.py 在服务端侧核对）。
            string nowhere = Path.Combine(testRoot, "Data", "Monster", "999.Lib");
            bool miss = (bool)_ensure1.Invoke(null, new object[] { nowhere });
            Check("服务端也没有的资源安全失败", !miss && !File.Exists(nowhere), "返回=" + miss);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool miss2 = (bool)_ensure1.Invoke(null, new object[] { nowhere });
            long cost = watch.ElapsedMilliseconds;
            Check("失败后本次运行不再重试（负缓存）", !miss2 && cost < 50, "耗时=" + cost + "ms");

            // ---------- 用例 7：从服务端清单推算图库数量（服务端预置 000~199 共 200 个） ----------
            int count = (int)_getCount.Invoke(null, new object[] { Path.Combine(testRoot, "Data", "Monster") + "\\", "" });
            Check("清单推算图库数量=200", count == 200, "实际=" + count);

            // ---------- 用例 8：不在客户端目录内的路径不应尝试下载 ----------
            string outside = Path.Combine(Path.GetTempPath(), "not_in_client", "x.Lib");
            string relOutside = (string)_toRelative.Invoke(null, new object[] { outside });
            Check("目录外路径拒绝映射", relOutside == null, "返回=" + (relOutside ?? "null"));

            // ---------- 用例 9：后台队列最终会自己清空 ----------
            bool idle = WaitUntil(delegate () { return Pending() == 0; }, 30000);
            Check("后台队列最终清空（不会长期占用）", idle, "剩余=" + Pending());
            Console.WriteLine("  完成统计：累计下载 " + _completed.GetValue(null) + " 个文件");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("[异常] " + ex);
            _fail++;
        }

        Console.WriteLine();
        Console.WriteLine("通过 " + _pass + " 项，失败 " + _fail + " 项");
        return _fail == 0 ? 0 : 1;
    }

    private static int Pending()
    {
        return (int)_pending.GetValue(null);
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(30);
        }
        return condition();
    }

    private static void SetStatic(Type type, string field, object value)
    {
        var f = type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (f == null) throw new InvalidOperationException("找不到静态字段: " + field);
        f.SetValue(null, value);
    }

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok)
        {
            _pass++;
            Console.WriteLine("  [通过] " + name);
        }
        else
        {
            _fail++;
            Console.WriteLine("  [失败] " + name + "  " + detail);
        }
    }
}
