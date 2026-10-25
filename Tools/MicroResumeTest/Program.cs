using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Client.Utils;
using SrvSettings = global::Server.Settings;
using Server.Library.Utils;

namespace MicroResumeTest
{
    /// <summary>
    /// 微端断点续传/失败重试自动测试。
    /// A 部分：真实 SocketHttpServer 通道跑服务端 /resource 的 Range 处理。
    /// B 部分：假 HTTP 服务端 + 反射调 ResourceDownloader.DownloadFile 跑客户端续传。
    /// 全部通过返回 0，任何失败返回 1。
    /// </summary>
    internal static class Program
    {
        private static int _pass, _fail;

        private static void Check(bool cond, string name)
        {
            if (cond)
            {
                _pass++;
                Console.WriteLine("  [PASS] " + name);
            }
            else
            {
                _fail++;
                Console.WriteLine("  [FAIL] " + name);
            }
        }

        private static byte[] MakePayload(int size)
        {
            var b = new byte[size];
            for (int i = 0; i < size; i++) b[i] = (byte)(i * 7 + i / 251);
            return b;
        }

        private static int Main()
        {
            Console.WriteLine("== 微端断点续传 自动测试 ==");
            Console.WriteLine();

            TestServerRange();
            TestClientResume();
            TestQualityPacketRoundTrip();

            Console.WriteLine();
            Console.WriteLine("结果: " + _pass + " 通过, " + _fail + " 失败");
            return _fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------
        // A. 服务端 Range（真实 socket 通道：请求头解析 + 206/416 + 偏移发送）
        // ------------------------------------------------------------------

        private static void TestServerRange()
        {
            Console.WriteLine("[A] 服务端 /resource 断点续传（纯 Socket 通道）");

            string root = Path.Combine(Path.GetTempPath(), "MicroResumeTestsrv_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string dir = Path.Combine(root, "Data", "Test");
            Directory.CreateDirectory(dir);

            byte[] payload = MakePayload(1000);
            string file = Path.Combine(dir, "000.Lib");
            File.WriteAllBytes(file, payload);

            SrvSettings.EnableResourceService = true;
            SrvSettings.ResourceAllowAnyIP = true;
            SrvSettings.ResourcePath = root + Path.DirectorySeparatorChar;

            var httpServer = new HttpServer();
            var socket = new SocketHttpServer
            {
                Handler = (request, response) =>
                {
                    httpServer.HandleResourceRequest(request, response);
                    return Task.CompletedTask;
                }
            };

            if (!socket.Bind(IPAddress.Loopback, 0))
            {
                Check(false, "SocketHttpServer 绑定端口");
                return;
            }
            Check(true, "SocketHttpServer 绑定端口");

            using (var cts = new CancellationTokenSource())
            {
                var runTask = Task.Run(() => socket.Run(cts.Token));

                try
                {
                    string url = "http://127.0.0.1:" + socket.Port + "/resource?f=Data/Test/000.Lib";

                    using (var client = new HttpClient())
                    {
                        // 1. 无 Range：200 全量 + Accept-Ranges
                        var resp = client.GetAsync(url).GetAwaiter().GetResult();
                        var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        Check((int)resp.StatusCode == 200, "无 Range -> 200");
                        Check(bytes.Length == payload.Length && ToStr(bytes) == ToStr(payload), "无 Range -> 完整内容");
                        Check((resp.Headers.AcceptRanges?.Contains("bytes") ?? false), "响应带 Accept-Ranges: bytes");
                        resp.Dispose();

                        // 2. bytes=500-：206 + Content-Range + 后半段内容
                        var req2 = new HttpRequestMessage(HttpMethod.Get, url);
                        req2.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(500, null);
                        resp = client.SendAsync(req2).GetAwaiter().GetResult();
                        bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        Check((int)resp.StatusCode == 206, "bytes=500- -> 206");
                        Check(resp.Content.Headers.ContentRange != null &&
                              resp.Content.Headers.ContentRange.ToString() == "bytes 500-999/1000",
                              "Content-Range == bytes 500-999/1000 (实际 " + resp.Content.Headers.ContentRange + ")");
                        Check(BytesEqual(bytes, payload, 500), "续传内容 == payload[500..999]");
                        resp.Dispose();

                        // 3. bytes=100-199：start-end 形式
                        var req3 = new HttpRequestMessage(HttpMethod.Get, url);
                        req3.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(100, 199);
                        resp = client.SendAsync(req3).GetAwaiter().GetResult();
                        bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        Check((int)resp.StatusCode == 206 && bytes.Length == 100 &&
                              ToStr(bytes) == ToStr(payload, 100, 100),
                              "bytes=100-199 -> 206 + 100 字节");
                        resp.Dispose();

                        // 4. 起点越界：416
                        var req4 = new HttpRequestMessage(HttpMethod.Get, url);
                        req4.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(2000, null);
                        resp = client.SendAsync(req4).GetAwaiter().GetResult();
                        Check((int)resp.StatusCode == 416, "bytes=2000- (越界) -> 416");
                        resp.Dispose();

                        // 5. 乱 Range：按全量 200
                        var req5 = new HttpRequestMessage(HttpMethod.Get, url);
                        req5.Headers.TryAddWithoutValidation("Range", "bytes=abc-");
                        resp = client.SendAsync(req5).GetAwaiter().GetResult();
                        bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        Check((int)resp.StatusCode == 200 && bytes.Length == payload.Length,
                              "非法 Range -> 200 全量（兼容）");
                        resp.Dispose();
                    }
                }
                finally
                {
                    cts.Cancel();
                    try { socket.Stop(); } catch { }
                    try { runTask.Wait(2000); } catch { }
                    try { Directory.Delete(root, true); } catch { }
                }
            }

            Console.WriteLine();
        }

        private static bool BytesEqual(byte[] a, byte[] b, int bOffset)
        {
            if (a.Length != b.Length - bOffset) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[bOffset + i]) return false;
            return true;
        }

        private static string ToStr(byte[] b) => Encoding.Latin1.GetString(b);
        private static string ToStr(byte[] b, int off, int len) => Encoding.Latin1.GetString(b, off, len);

        // ------------------------------------------------------------------
        // B. 客户端 DownloadFile 断点续传（假 HTTP 服务端 + 反射）
        // ------------------------------------------------------------------

        private static volatile string _lastRangeHeader = string.Empty;
        private static volatile bool _fakeSupportsRange = true;
        private static byte[] _payload;
        private static HttpListener _fakeListener;
        private static volatile bool _fakeStop;

        private static void TestClientResume()
        {
            Console.WriteLine("[B] 客户端 ResourceDownloader 断点续传");

            _payload = MakePayload(1000);

            // 起假服务端（HttpListener 在 127.0.0.1 上无需权限）
            int port = GetFreePort();
            _fakeListener = new HttpListener();
            _fakeListener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _fakeListener.Start();

            var listenTask = Task.Run(FakeServerLoop);

            string clientRoot = Path.Combine(Path.GetTempPath(), "MicroResumeTestcli_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Path.Combine(clientRoot, "Data", "Test"));

            // 配置客户端 Settings（全 public static 字段，直接赋值）
            Settings.P_Client = clientRoot + Path.DirectorySeparatorChar;
            Settings.MicroClient = true;
            Settings.MicroHost = "http://127.0.0.1:" + port + "/";
            Settings.MicroLog = false;      // 不写日志文件
            Settings.MicroRateLimit = 0;

            var download = typeof(ResourceDownloader).GetMethod("DownloadFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            string rel = "Data/Test/000.Lib";
            string local = Path.Combine(clientRoot, "Data", "Test", "000.Lib");
            string temp = local + ".tmp";

            try
            {
                // 用例 1：无 tmp 全新下载（服务端支持 Range 也不会带 Range 头）
                _fakeSupportsRange = true;
                _lastRangeHeader = string.Empty;
                object r = download.Invoke(null, new object[] { rel });
                Check(Convert.ToString(r) == "Success", "全新下载 -> Success (实际 " + r + ")");
                Check(File.Exists(local) && !File.Exists(temp), "全新下载 -> 落地文件存在且无 .tmp 残留");
                Check(File.ReadAllBytes(local).SequenceEqual(_payload), "全新下载 -> 内容完整");
                Check(_lastRangeHeader.Length == 0, "全新下载 -> 未发 Range 头");

                // 用例 2：半截 tmp 续传
                File.WriteAllBytes(temp, Slice(_payload, 0, 500));
                if (File.Exists(local)) File.Delete(local);
                _lastRangeHeader = string.Empty;
                r = download.Invoke(null, new object[] { rel });
                Check(Convert.ToString(r) == "Success", "半截 tmp 续传 -> Success (实际 " + r + ")");
                Check(_lastRangeHeader == "bytes=500-", "续传请求带 Range: bytes=500- (实际 " + (_lastRangeHeader.Length == 0 ? "(无)" : _lastRangeHeader) + ")");
                Check(File.Exists(local) && !File.Exists(temp), "续传完成 -> 转正且无 .tmp");
                Check(File.ReadAllBytes(local).SequenceEqual(_payload), "续传完成 -> 内容完整（前 500 字节来自 tmp，后 500 来自网络）");

                // 用例 3：服务端不支持 Range（回 200 全量）-> 丢弃 tmp 重下
                File.WriteAllBytes(temp, Slice(_payload, 0, 300));
                if (File.Exists(local)) File.Delete(local);
                _fakeSupportsRange = false;
                _lastRangeHeader = string.Empty;
                r = download.Invoke(null, new object[] { rel });
                Check(Convert.ToString(r) == "Success", "服务端不支持 Range -> Success (实际 " + r + ")");
                Check(File.ReadAllBytes(local).SequenceEqual(_payload), "不支持 Range -> 丢弃 tmp 重下后内容完整");
                _fakeSupportsRange = true;

                // 用例 4：tmp 已是完整内容（写完没改名就断了）-> 416 -> 转正
                File.WriteAllBytes(temp, _payload);
                if (File.Exists(local)) File.Delete(local);
                _lastRangeHeader = string.Empty;
                r = download.Invoke(null, new object[] { rel });
                Check(Convert.ToString(r) == "Success", "tmp 已完整 + 416 -> 转正 Success (实际 " + r + ")");
                Check(File.Exists(local) && !File.Exists(temp), "416 收尾 -> 文件转正且无 .tmp");
                Check(File.ReadAllBytes(local).SequenceEqual(_payload), "416 收尾 -> 内容完整");

                // 用例 5：404 -> NotFound
                r = download.Invoke(null, new object[] { "Data/Test/notexist.lib" });
                Check(Convert.ToString(r) == "NotFound", "不存在的文件 -> NotFound (实际 " + r + ")");
            }
            catch (Exception ex)
            {
                Check(false, "客户端测试异常: " + ex.Message + (ex.InnerException != null ? " / " + ex.InnerException.Message : ""));
            }
            finally
            {
                _fakeStop = true;
                try { _fakeListener.Stop(); } catch { }
                try { listenTask.Wait(2000); } catch { }
                try { Directory.Delete(clientRoot, true); } catch { }
            }

            Console.WriteLine();
        }

        private static byte[] Slice(byte[] src, int off, int len)
        {
            var b = new byte[len];
            Array.Copy(src, off, b, 0, len);
            return b;
        }

        // ------------------------------------------------------------------
        // C. 品质字段封包往返（防止字段读写顺序错位把登录包搞坏）
        // ------------------------------------------------------------------

        private static void TestQualityPacketRoundTrip()
        {
            Console.WriteLine("[C] 品质字段封包往返");

            try
            {
                var player = new ServerPackets.ObjectPlayer
                {
                    ObjectID = 12345,
                    Name = "测试角色",
                    Class = MirClass.战士,
                    Level = 50,
                    Weapon = 12,
                    WeaponEffect = 3,
                    Armour = 7,
                    ArmourGrade = 4,
                    WeaponGrade = 3,
                };

                byte[] raw = player.GetPacketBytes().ToArray();
                byte[] extra;
                var back = Packet.ReceivePacket(raw, out extra) as ServerPackets.ObjectPlayer;

                Check(back != null, "ObjectPlayer 反序列化成功");
                if (back != null)
                {
                    Check(back.ArmourGrade == 4 && back.WeaponGrade == 3,
                          "ObjectPlayer 品质字段往返一致 (衣服 " + back.ArmourGrade + " / 武器 " + back.WeaponGrade + ")");
                    Check(back.Armour == 7 && back.Weapon == 12 && back.Level == 50 && back.Name == "测试角色",
                          "ObjectPlayer 其它字段未被品质字段挤错位");
                }

                var update = new ServerPackets.PlayerUpdate
                {
                    ObjectID = 999,
                    Weapon = 5,
                    WeaponEffect = 1,
                    Armour = 6,
                    Light = 3,
                    WingEffect = 2,
                    ArmourGrade = 2,
                    WeaponGrade = 4,
                };

                byte[] raw2 = update.GetPacketBytes().ToArray();
                var back2 = Packet.ReceivePacket(raw2, out extra) as ServerPackets.PlayerUpdate;

                Check(back2 != null, "PlayerUpdate 反序列化成功");
                if (back2 != null)
                {
                    Check(back2.ArmourGrade == 2 && back2.WeaponGrade == 4,
                          "PlayerUpdate 品质字段往返一致 (衣服 " + back2.ArmourGrade + " / 武器 " + back2.WeaponGrade + ")");
                    Check(back2.Armour == 6 && back2.Weapon == 5 && back2.Light == 3 && back2.WingEffect == 2,
                          "PlayerUpdate 其它字段未错位");
                }
            }
            catch (Exception ex)
            {
                Check(false, "封包往返异常: " + ex.Message);
            }

            Console.WriteLine();
        }

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        private static void FakeServerLoop()
        {
            while (!_fakeStop)
            {
                HttpListenerContext ctx;
                try { ctx = _fakeListener.GetContext(); }
                catch { return; }

                try
                {
                    // 只服务测试用的已知文件，其它路径一律 404（模拟真实服务端行为）
                    if (!ctx.Request.Url.AbsolutePath.Equals("/resource", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(ctx.Request.QueryString["f"], "Data/Test/000.Lib", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 404;
                        ctx.Response.ContentLength64 = 9;
                        var nb = Encoding.ASCII.GetBytes("not found");
                        ctx.Response.OutputStream.Write(nb, 0, nb.Length);
                        ctx.Response.OutputStream.Close();
                        continue;
                    }

                    string range = ctx.Request.Headers["Range"] ?? string.Empty;
                    _lastRangeHeader = range;

                    ctx.Response.ContentType = "application/octet-stream";

                    bool hasRange = range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase);

                    if (hasRange && _fakeSupportsRange)
                    {
                        string spec = range.Substring(6).Trim();
                        int dash = spec.IndexOf('-');
                        long start = long.Parse(spec.Substring(0, dash).Trim());

                        if (start >= _payload.Length)
                        {
                            ctx.Response.StatusCode = 416;
                            ctx.Response.AddHeader("Content-Range", "bytes */" + _payload.Length);
                            ctx.Response.Close();
                            continue;
                        }

                        ctx.Response.StatusCode = 206;
                        ctx.Response.AddHeader("Content-Range", "bytes " + start + "-" + (_payload.Length - 1) + "/" + _payload.Length);
                        ctx.Response.ContentLength64 = _payload.Length - start;
                        ctx.Response.OutputStream.Write(_payload, (int)start, (int)(_payload.Length - start));
                        ctx.Response.OutputStream.Close();
                    }
                    else
                    {
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentLength64 = _payload.Length;
                        ctx.Response.OutputStream.Write(_payload, 0, _payload.Length);
                        ctx.Response.OutputStream.Close();
                    }
                }
                catch
                {
                }
            }
        }
    }
}
