using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace SlowDownloadTest
{
    /// <summary>
    /// 用反射驱动真实 Client.dll，在「限速的外网环境」下下载一个大文件。
    ///
    /// 要证明的事：下载时长超出固定超时（旧实现是 32 秒）之后**不再被判失败**。
    /// 旧实现用 HttpClient.Timeout 卡死整个下载，而 588MB 的地砖图库
    /// 对 2.5MB/s 的外网玩家要 4 分钟 —— 那意味着外网微端在大文件上必然失败。
    /// 现在改成「多久没收到数据」的静默超时，只要数据还在流就不该中断。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            if (args.Length < 5)
            {
                Console.WriteLine("用法: SlowDownloadTest <客户端目录> <资源服务地址> <本地根目录> <相对路径> <等待毫秒>");
                return 2;
            }

            string clientDir = args[0];
            string host = args[1];
            string testRoot = args[2];
            string relative = args[3];
            int waitMs = int.Parse(args[4]);

            // EnsureLocalFileBlocking 收的是「本地绝对路径」，不是相对路径
            string localPath = Path.Combine(testRoot.TrimEnd('\\'), relative.Replace('/', '\\'));

            AssemblyLoadContext.Default.Resolving += (ctx, name) =>
            {
                string p = Path.Combine(clientDir, name.Name + ".dll");
                return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
            };

            var asm = Assembly.LoadFrom(Path.Combine(clientDir, "Client.dll"));

            var settings = asm.GetType("Client.Settings", true);
            SetStatic(settings, "MicroClient", true);
            SetStatic(settings, "MicroHost", host);
            SetStatic(settings, "MicroTimeout", 8000);
            SetStatic(settings, "MicroLog", true);
            SetStatic(settings, "MicroConcurrency", 2);
            SetStatic(settings, "MicroRateLimit", 0);
            SetStatic(settings, "P_Client", testRoot.TrimEnd('\\') + "\\");

            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            Directory.CreateDirectory(Path.Combine(testRoot, "Data"));

            var rd = asm.GetType("Client.Utils.ResourceDownloader", true);
            const BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Static;

            var ensureBlocking = rd.GetMethod("EnsureLocalFileBlocking", SF, null,
                new[] { typeof(string), typeof(int) }, null);
            var warmUp = rd.GetMethod("WarmUp", SF, null, Type.EmptyTypes, null);

            Console.WriteLine("资源服务 : " + host);
            Console.WriteLine("目标资源 : " + relative);
            Console.WriteLine("等待上限 : " + (waitMs / 1000) + " 秒");
            Console.WriteLine();

            warmUp?.Invoke(null, null);
            Thread.Sleep(1000);

            var sw = Stopwatch.StartNew();
            object result = ensureBlocking.Invoke(null, new object[] { localPath, waitMs });
            sw.Stop();

            string local = localPath;
            long size = File.Exists(local) ? new FileInfo(local).Length : 0;

            Console.WriteLine("下载返回 : " + result);
            Console.WriteLine("实际耗时 : " + sw.Elapsed.TotalSeconds.ToString("F1") + " 秒");
            Console.WriteLine("落盘字节 : " + size);

            bool pass = result is bool b && b && size > 0;
            Console.WriteLine(pass ? "PASS" : "FAIL");
            return pass ? 0 : 1;
        }

        private static void SetStatic(Type type, string name, object value)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, value);
                return;
            }

            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (prop != null && prop.CanWrite) prop.SetValue(null, value);
        }
    }
}
