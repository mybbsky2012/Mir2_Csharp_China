using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace MicroIndexCacheTest
{
    /// <summary>
    /// 验证 11-16 的微端改动（直接反射调用真实 Client.dll，不复制逻辑）：
    ///   ① 清单本地缓存：没有任何服务器在线时也能拿到清单（图库数组不再降级）
    ///   ② 清单解析 + 落盘缓存（无 BOM）
    ///   ③ 接口主机是黑洞时，靠 5 秒响应头超时 + 目录直取完成，不再白等 30 秒
    ///   ④ 接口已标记不可用后，下一个文件直接走目录（秒级）
    ///   ⑤ 「Sound/.wav」这类占位路径不再产生 HTTP 请求
    /// </summary>
    internal static class Program
    {
        private static readonly Assembly ClientAsm = Assembly.Load("Client");
        private static Type RD;   // Client.Utils.ResourceDownloader（internal）
        private static Type ST;   // Client.Settings（internal）

        private const BindingFlags Any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static int _pass, _fail;

        private static void Check(bool ok, string name, string detail = "")
        {
            if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : "  | " + detail)); }
        }

        private static void Section(string s) => Console.WriteLine("\n## " + s);

        private static void Set(string field, object value)
        {
            ST.GetField(field, Any).SetValue(null, value);
        }

        private static bool Ensure(string localPath)
        {
            return (bool)RD.GetMethod("EnsureLocalFile", Any, null, new[] { typeof(string) }, null)
                            .Invoke(null, new object[] { localPath });
        }

        [STAThread]
        private static int Main()
        {
            RD = ClientAsm.GetType("Client.Utils.ResourceDownloader", true);
            ST = ClientAsm.GetType("Client.Settings", true);

            string root = Path.Combine(Path.GetTempPath(), "microcache_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Path.Combine(root, "Data"));
            Directory.CreateDirectory(Path.Combine(root, "Sound"));

            var srv = new MiniHttp();
            srv.Files["/Sound/1.wav"] = Encoding.ASCII.GetBytes("RIFF....WAVEfmt ");
            srv.Files["/Data/Monster/000.Lib"] = Encoding.ASCII.GetBytes(new string('A', 4096));
            srv.Files["/resindex.txt"] = Encoding.ASCII.GetBytes("Data/Monster/000.Lib\nData/Monster/001.Lib\n");
            srv.Start();
            Console.WriteLine("迷你 HTTP 服务器: http://127.0.0.1:" + srv.Port);

            // ---- 客户端环境：全部指向临时目录，绝不碰真实客户端 ----
            Set("P_Client", root + "\\");
            Set("MicroClient", true);
            Set("MicroLog", false);
            Set("MicroTimeout", 8000);
            Set("MicroConcurrency", 1);
            Set("MicroMaxRetry", 5);
            Set("MicroIndexHost", "");
            Set("MicroDirHost", "http://127.0.0.1:" + srv.Port);
            Set("MicroFileAtRoot", false);

            var pool = (IList<string>)ST.GetField("MicroHostPool", Any).GetValue(null);
            pool.Clear();
            pool.Add("http://192.0.2.1:5679");   // TEST-NET-1：黑洞，连不上也不会被拒绝

            string cachePath = Path.Combine(root, "Data", "MicroIndex.cache");

            // ================= ① 清单缓存：无服务器也能用 =================
            Section("① 本地清单缓存");
            File.WriteAllText(cachePath,
                "Data/Monster/000.Lib\nData/Monster/001.Lib\nSound/1.wav\n",
                new UTF8Encoding(false));

            var sw = Stopwatch.StartNew();
            var index = (IDictionary)RD.GetMethod("GetRemoteIndex", Any).Invoke(null, null);
            sw.Stop();

            Check(index != null, "拿得到清单（缓存）");
            Check(CountAll(index) == 3, "清单条目 = 3", "实际 " + CountAll(index));
            Check((bool)RD.GetProperty("IsIndexAvailable", Any).GetValue(null), "IsIndexAvailable = true");
            Check(sw.ElapsedMilliseconds < 1000, "不依赖网络（耗时 " + sw.ElapsedMilliseconds + " ms < 1000）");

            // ================= ③ 黑洞接口 + 目录直取 =================
            Section("③ 接口主机是黑洞时的按需下载");
            string localLib = Path.Combine(root, "Data", "Monster", "000.Lib");

            sw.Restart();
            bool ready = Ensure(localLib);
            bool okLib = WaitFor(localLib, 20000);
            sw.Stop();

            Check(!ready, "第一次调用：文件不在本地 → 返回 false 并排队");
            Check(okLib, "文件最终下载成功（走目录兜底）");
            Check(sw.ElapsedMilliseconds < 15000,
                "耗时 " + sw.ElapsedMilliseconds + " ms < 15000（旧版 30 秒响应头超时必然超时）");
            Check(okLib && new FileInfo(localLib).Length == 4096, "文件大小正确（4096）");

            // ================= ④ 接口已标记不可用 → 直接走目录 =================
            Section("④ 接口不可用后的下一个文件");
            string localWav = Path.Combine(root, "Sound", "1.wav");

            sw.Restart();
            Ensure(localWav);
            bool okWav = WaitFor(localWav, 8000);
            sw.Stop();

            Check(okWav, "第二个文件下载成功");
            Check(sw.ElapsedMilliseconds < 3000, "耗时 " + sw.ElapsedMilliseconds + " ms < 3000（不再先撞死主机）");

            // ================= ⑤ 占位路径不再发请求 =================
            Section("⑤ 「Sound/.wav」占位路径");
            int before = srv.Requests;

            bool bogusA = Ensure(Path.Combine(root, "Sound", ".wav"));
            bool bogusB = Ensure(Path.Combine(root, "Sound", ".mp3"));

            Thread.Sleep(300);

            Check(!bogusA && !bogusB, "返回 false（本地没有，也不排队）");
            Check(srv.Requests == before, "没有产生任何 HTTP 请求", "请求数 " + before + " → " + srv.Requests);

            // ================= ② 解析 + 落盘 =================
            Section("② 清单解析与缓存落盘");
            File.Delete(cachePath);

            string rawText = "Data/Monster/000.Lib\nData/Monster/001.Lib\nSound/1.wav\nSound/2.wav\n";
            var parsed = (IDictionary)RD.GetMethod("ParseIndex", Any).Invoke(null, new object[] { rawText });
            RD.GetMethod("SaveIndexCache", Any).Invoke(null, new object[] { rawText });

            Check(File.Exists(cachePath), "缓存文件已写出");
            Check(CountAll(parsed) == 4, "解析条目 = 4", "实际 " + CountAll(parsed));

            byte[] head = File.ReadAllBytes(cachePath);
            Check(head.Length < 3 || !(head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF),
                "缓存无 BOM（有 BOM 会让第一个文件永远 404）");

            srv.Stop();

            Console.WriteLine();
            Console.WriteLine("==== 通过 " + _pass + " 项，失败 " + _fail + " 项 ====");

            try { Directory.Delete(root, true); } catch { }
            return _fail == 0 ? 0 : 1;
        }

        private static int CountAll(IDictionary dict)
        {
            if (dict == null) return 0;
            int n = 0;
            foreach (object set in dict.Values)
                foreach (object _ in (IEnumerable)set) n++;
            return n;
        }

        private static bool WaitFor(string path, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) return true;
                Thread.Sleep(50);
            }
            return File.Exists(path);
        }
    }

    /// <summary>够用就行的 HTTP 服务器：只回 Content-Length + 200/404，不需要 URL ACL。</summary>
    internal sealed class MiniHttp
    {
        private TcpListener _listener;
        public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public int Requests;
        public int Port;

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            var t = new Thread(Loop) { IsBackground = true };
            t.Start();
        }

        public void Stop()
        {
            try { _listener?.Stop(); } catch { }
        }

        private void Loop()
        {
            while (true)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { return; }

                ThreadPool.QueueUserWorkItem(delegate (object o)
                {
                    using (var c = (TcpClient)o)
                    {
                        try { Handle(c); } catch { }
                    }
                }, client);
            }
        }

        private void Handle(TcpClient client)
        {
            var stream = client.GetStream();
            var buf = new byte[8192];
            int total = 0;

            while (total < buf.Length)
            {
                int n = stream.Read(buf, total, buf.Length - total);
                if (n <= 0) return;
                total += n;
                if (Encoding.ASCII.GetString(buf, 0, total).Contains("\r\n\r\n")) break;
            }

            string requestLine = Encoding.ASCII.GetString(buf, 0, total).Split('\n')[0];
            string[] parts = requestLine.Split(' ');
            string path = parts.Length > 1 ? parts[1] : "/";
            int q = path.IndexOf('?');
            if (q >= 0) path = path.Substring(0, q);

            Interlocked.Increment(ref Requests);

            byte[] body;
            if (Files.TryGetValue(path, out body))
            {
                byte[] header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                stream.Write(header, 0, header.Length);
                stream.Write(body, 0, body.Length);
            }
            else
            {
                byte[] header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                stream.Write(header, 0, header.Length);
            }
        }
    }
}
