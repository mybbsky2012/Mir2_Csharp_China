using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace MicroRealPullTest
{
    /// <summary>
    /// 用真实生产参数跑一遍「读哪个下哪个」：
    ///   接口主机 = zxmir.51vip.biz:5679（现网配置里的黑洞主机）
    ///   目录兜底 = http://219.138.108.82:65534（远端，实际有全部文件）
    /// 直接反射调用真实 Client.dll，不复制任何逻辑。
    /// </summary>
    internal static class Program
    {
        private const BindingFlags Any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static Assembly _asm;
        private static Type RD;
        private static Type ST;

        private static int _pass, _fail;

        private static void Check(bool ok, string name, string detail = "")
        {
            if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : "  | " + detail)); }
        }

        private static void Set(string field, object value)
        {
            ST.GetField(field, Any).SetValue(null, value);
        }

        private static bool Ensure(string localPath)
        {
            return (bool)RD.GetMethod("EnsureLocalFile", Any, null, new[] { typeof(string) }, null)
                            .Invoke(null, new object[] { localPath });
        }

        private static bool WaitFor(string path, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) return true;
                Thread.Sleep(50);
            }
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            string realClient = args.Length > 0 ? args[0] : @"D:\龙之传奇";
            string dirHost = args.Length > 1 ? args[1] : "http://219.138.108.82:65534";
            string apiHost = args.Length > 2 ? args[2] : "http://zxmir.51vip.biz:5679";

            _asm = Assembly.Load("Client");
            RD = _asm.GetType("Client.Utils.ResourceDownloader", true);
            ST = _asm.GetType("Client.Settings", true);

            string root = Path.Combine(Path.GetTempPath(), "microreal_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Path.Combine(root, "Data"));

            // 真实清单缓存（保证图库数组按真实数量初始化）
            string realCache = Path.Combine(realClient, "Data", "MicroIndex.cache");
            if (!File.Exists(realCache))
            {
                Console.WriteLine("找不到清单缓存：" + realCache);
                return 2;
            }
            File.Copy(realCache, Path.Combine(root, "Data", "MicroIndex.cache"), true);

            Console.WriteLine("客户端根目录(临时) = " + root);
            Console.WriteLine("清单缓存 = " + realCache);
            Console.WriteLine("接口主机 = " + apiHost);
            Console.WriteLine("目录兜底 = " + dirHost);
            Console.WriteLine();

            // ---- 生产参数 ----
            Set("P_Client", root + "\\");
            Set("MicroClient", true);
            Set("MicroLog", true);
            Set("MicroTimeout", 8000);
            Set("MicroMapTimeout", 2000);
            Set("MicroConcurrency", 4);
            Set("MicroMaxRetry", 5);
            Set("MicroIndexHost", "");
            Set("MicroDirHost", dirHost);
            Set("MicroFileAtRoot", false);

            var pool = (IList<string>)ST.GetField("MicroHostPool", Any).GetValue(null);
            pool.Clear();
            pool.Add(apiHost);

            // ================= ① 清单（只读缓存，不许走网络） =================
            Console.WriteLine("## ① 清单可用性");
            var sw = Stopwatch.StartNew();
            var index = (IDictionary)RD.GetMethod("GetRemoteIndex", Any).Invoke(null, null);
            sw.Stop();

            int entries = index == null ? 0 : CountAll(index);
            Check(index != null, "拿到清单");
            Check(entries == 3652, "清单条数 3652", "实际 " + entries);
            Check(sw.ElapsedMilliseconds < 2000, "只读缓存，耗时 " + sw.ElapsedMilliseconds + " ms < 2000");

            var countMethod = RD.GetMethod("GetExpectedLibraryCount", Any);
            var countDirs = new[]
            {
                new { Dir = "Data\\AArmour\\",   Sfx = ""    },   // InitLibrary(path, "00")     → suffix 为空
                new { Dir = "Data\\AHumEffect\\", Sfx = ""    },
                new { Dir = "Data\\Monster\\",   Sfx = ""    },   // InitLibrary(path, "000")
                new { Dir = "Data\\Weapon\\",    Sfx = " L"  },   // InitLibrary(path, "00", " L")
            };

            int totalLibs = 0;
            foreach (var cd in countDirs)
            {
                int n = (int)countMethod.Invoke(null, new object[] { Path.Combine(root, cd.Dir), cd.Sfx });
                totalLibs += n;
                Console.WriteLine(string.Format("  {0,-20} 图库数 = {1}", cd.Dir, n));
            }
            Check(totalLibs > 300, "图库总数按清单初始化 = " + totalLibs + "（旧版清单拿不到时会退化成 0/1）");

            // ================= ② 真实文件按需下载 =================
            Console.WriteLine("\n## ② 真实缺文件按需下载（接口先撞黑洞）");
            string[] targets =
            {
                "Data/AArmour/00.Lib",
                "Data/AArmour/22.Lib",
                "Data/AHumEffect/00.Lib",
                "Data/AArmour/34.Lib",
                "Data/AArmour/52.Lib",
                "Data/AHumEffect/01.Lib",
            };

            long totalBytes = 0;
            sw.Restart();

            foreach (string rel in targets)
            {
                string local = Path.Combine(root, rel.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(local));

                var one = Stopwatch.StartNew();
                bool queued = Ensure(local);
                bool ok = WaitFor(local, 40000);
                one.Stop();

                long len = File.Exists(local) ? new FileInfo(local).Length : 0;
                totalBytes += len;

                string mark = ok ? "OK " : "ERR";
                Console.WriteLine(string.Format("  {0} {1,-26} {2,8} ms  {3,9} 字节", mark, rel, one.ElapsedMilliseconds, len));
                Check(ok && len > 0, "下载成功 " + rel);
            }

            sw.Stop();
            Console.WriteLine(string.Format("\n  合计 {0:0.00} MB / {1} ms → {2:0.00} MB/s",
                totalBytes / 1048576.0, sw.ElapsedMilliseconds,
                totalBytes / 1048576.0 / Math.Max(0.001, sw.ElapsedMilliseconds / 1000.0)));

            Check(totalBytes > 0, "有实际下载量");

            // ================= ③ 日志里应当出现目录兜底 =================
            Console.WriteLine("\n## ③ 兜底日志");
            string logPath = Path.Combine(AppContext.BaseDirectory, "MicroClient.log");
            string log = File.Exists(logPath) ? File.ReadAllText(logPath, Encoding.UTF8) : "";
            bool sawFallback = log.Contains("改用 HTTP 目录直取");
            Check(sawFallback, "日志出现「改用 HTTP 目录直取」");
            if (log.Length > 0)
            {
                Console.WriteLine("  ---- 日志尾部 ----");
                string[] lines = log.Replace("\r", "").Split('\n');
                int from = Math.Max(0, lines.Length - 12);
                for (int i = from; i < lines.Length; i++)
                    if (lines[i].Trim().Length > 0) Console.WriteLine("  " + lines[i]);
            }

            Console.WriteLine(string.Format("\n==== 通过 {0} 项，失败 {1} 项 ====", _pass, _fail));
            Console.WriteLine("（临时目录：" + root + "，可手动删除）");
            return _fail == 0 ? 0 : 1;
        }

        private static int CountAll(IDictionary index)
        {
            int n = 0;
            foreach (var value in index.Values)
            {
                var set = value as IEnumerable;
                if (set == null) continue;
                foreach (var _ in set) n++;
            }
            return n;
        }
    }
}
