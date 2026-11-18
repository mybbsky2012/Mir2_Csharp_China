using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;

namespace MicroUrlTest
{
    /// <summary>
    /// 微端「清单走本机 / 文件直连远端」改造的验证工具。
    /// 直接反射调用真实 Client.dll 里的 ResourceDownloader.BuildUrl，
    /// 再用和客户端同参数的 HttpClient 真打一遍这两个 URL。
    /// </summary>
    internal static class Program
    {
        private const string ClientDir = @"D:\Mirserver\ClientResources";
        private const string IndexHost = "http://127.0.0.1:8888";
        private const string FileHost  = "http://219.138.108.82:65534";

        private static int _pass, _fail;

        private static void Check(bool ok, string name, string detail = "")
        {
            if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name + (detail.Length > 0 ? "  | " + detail : "")); }
        }

        /// <summary>清单是 Dictionary&lt;扩展名, HashSet&lt;文件名&gt;&gt;，文件总数要按集合累加。</summary>
        private static int CountAll(System.Collections.IDictionary dict)
        {
            if (dict == null) return 0;
            int n = 0;
            foreach (object set in dict.Values)
                foreach (object _ in (System.Collections.IEnumerable)set) n++;
            return n;
        }

        private static void Section(string s) => Console.WriteLine("\n## " + s);

        private static HttpClient MakeClient()
        {
            // 关键：本机有 http_proxy 环境变量（沙箱/系统代理），不关掉会污染测试结果
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                UseProxy = false,
                MaxConnectionsPerServer = 8,
                ConnectTimeout = TimeSpan.FromSeconds(15),
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        }

        private static string Md5(byte[] data)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        // Client 里 Settings / ResourceDownloader 都是 internal，只能走反射
        private static readonly Assembly ClientAsm = Assembly.Load("Client");

        private static FieldInfo SField(string name)
            => ClientAsm.GetType("Client.Settings", true).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        private static void SetStr(string name, string v) => SField(name).SetValue(null, v);
        private static void SetBool(string name, bool v) => SField(name).SetValue(null, v);

        private static string BuildUrl(string path, string key, string value)
        {
            var t = ClientAsm.GetType("Client.Utils.ResourceDownloader", true);
            var m = t.GetMethod("BuildUrl", BindingFlags.NonPublic | BindingFlags.Static);
            return (string)m.Invoke(null, new object[] { path, key, value });
        }

        private static void SetHostPool(params string[] hosts)
        {
            var list = (List<string>)SField("MicroHostPool").GetValue(null);
            list.Clear();
            list.AddRange(hosts);
        }

        private static int Main()
        {
            var client = MakeClient();
            var local = new Dictionary<string, string>();
            foreach (var rel in new[]
            {
                "Data/Monster/000.Lib",
                "Data/AWeapon/04 L.Lib",
                "Data/自定义素材.Lib",
                "Data/Effect.Lib",
                "Sound/S_Mon1_die.wav",
            })
            {
                var p = Path.Combine(ClientDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(p)) local[rel] = Md5(File.ReadAllBytes(p));
            }

            // ------------------------------------------------------------------
            Section("A. BuildUrl 生成的 URL 形态（反射真方法）");
            SetHostPool(FileHost);

            SetStr("MicroIndexHost", IndexHost);
            SetBool("MicroFileAtRoot", true);

            string idx = BuildUrl("resindex", null, null);
            Check(idx == IndexHost + "/resindex", "清单 URL = IndexHost/resindex", idx);

            string f1 = BuildUrl("resource", "f", "Data/Monster/000.Lib");
            Check(f1 == FileHost + "/Data/Monster/000.Lib", "文件 URL = Host/相对路径（无 /resource 前缀）", f1);
            Check(!f1.Contains("/resource"), "文件 URL 不含 /resource 中转路径", f1);

            string f2 = BuildUrl("resource", "f", "Data/AWeapon/04 L.Lib");
            Check(f2 == FileHost + "/Data/AWeapon/04%20L.Lib", "空格文件名 percent-encode 到路径里", f2);

            string f3 = BuildUrl("resource", "f", "Data/自定义素材.Lib");
            Check(f3.Contains("%E8%87%AA%E5%AE%9A%E4%B9%89%E7%B4%A0%E6%9D%90"), "中文文件名 percent-encode 到路径里", f3);

            // 回退：不改配置就是原行为
            SetBool("MicroFileAtRoot", false);
            SetStr("MicroIndexHost", "");
            string oldIdx = BuildUrl("resindex", null, null);
            string oldFile = BuildUrl("resource", "f", "Data/Monster/000.Lib");
            Check(oldIdx == FileHost + "/resindex", "未配置 IndexHost 时清单走 Host（原行为）", oldIdx);
            Check(oldFile == FileHost + "/resource?f=Data/Monster/000.Lib", "未开 FileAtRoot 时仍走 ?f=（老部署不受影响）", oldFile);

            // 恢复本次要用的配置
            SetStr("MicroIndexHost", IndexHost);
            SetBool("MicroFileAtRoot", true);

            // ------------------------------------------------------------------
            Section("B. 清单真的从本机拿（76KB，只取一次）");
            byte[] indexBytes = null;
            try
            {
                indexBytes = client.GetByteArrayAsync(idx).GetAwaiter().GetResult();
                Check(indexBytes.Length > 50000, "清单 200 且体积正常", indexBytes.Length + " B");
                var text = System.Text.Encoding.UTF8.GetString(indexBytes);
                // ★不能用 text.StartsWith("\uFEFF")：U+FEFF 是「可忽略字符」，
                //   区域性比较下对任意字符串都返回 true，必须按字节或 Ordinal 判断
                bool hasBom = indexBytes.Length >= 3 && indexBytes[0] == 0xEF && indexBytes[1] == 0xBB && indexBytes[2] == 0xBF;
                Check(!hasBom, "清单无 BOM（首字节不是 EF BB BF）", BitConverter.ToString(indexBytes, 0, 3));
                int lines = text.Split('\n').Count(x => x.Trim().Length > 0);
                Check(lines > 3000, "清单条数 > 3000", lines + " 条");
                Check(text.Contains("Data/Monster/000.Lib"), "清单里有普通文件路径");
                Check(text.Contains("Data/自定义素材.Lib"), "清单里有中文文件名（未乱码）");
            }
            catch (Exception ex) { Check(false, "清单获取", ex.Message); }

            // ------------------------------------------------------------------
            Section("C. 文件真的从远端直取（不经本机、无 302）");
            foreach (var rel in local.Keys)
            {
                string url = BuildUrl("resource", "f", rel);
                try
                {
                    using var resp = client.GetAsync(url).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                    {
                        Check(false, rel, "HTTP " + (int)resp.StatusCode + " " + url);
                        continue;
                    }
                    Check(resp.Version == HttpVersion.Version11 || resp.Version == HttpVersion.Version20, rel + " 走远端", "HTTP/" + resp.Version);
                    var data = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    bool same = Md5(data) == local[rel];
                    Check(same, rel + " 内容 md5 与本地一致", same ? "" : (data.Length + "B vs 本地"));
                }
                catch (Exception ex) { Check(false, rel, ex.Message); }
            }

            // ------------------------------------------------------------------
            Section("D. 关键点：远端直取时确实没有 302 中转");
            {
                string url = BuildUrl("resource", "f", "Data/Monster/000.Lib");
                var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
                using var raw = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                using var resp = raw.GetAsync(url).GetAwaiter().GetResult();
                Check(resp.StatusCode == HttpStatusCode.OK, "首跳就是 200（不是 302）", "实际 " + (int)resp.StatusCode);
                Check(resp.Headers.Location == null, "响应里没有 Location 头");
            }

            // ------------------------------------------------------------------
            Section("E. 断点续传（远端直取时的 Range）");
            {
                string url = BuildUrl("resource", "f", "Data/Effect.Lib");
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 99);
                using var resp = client.SendAsync(req).GetAwaiter().GetResult();
                Check(resp.StatusCode == HttpStatusCode.PartialContent, "Range 请求 → 206", "实际 " + (int)resp.StatusCode);
                var data = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                Check(data.Length == 100, "只返回 100 字节", data.Length + " B");
                Check(resp.Content.Headers.ContentRange != null, "有 Content-Range 头",
                      resp.Content.Headers.ContentRange?.ToString() ?? "无");
            }

            // ------------------------------------------------------------------
            Section("F. 清单与文件的两个地址互不影响（关掉本机也不影响文件下载）");
            {
                // 用一个不可达的清单地址模拟"主服挂了"，验证文件 URL 仍指向远端
                SetStr("MicroIndexHost", "http://127.0.0.1:1");
                string idxBad = BuildUrl("resindex", null, null);
                string fileGood = BuildUrl("resource", "f", "Data/Monster/000.Lib");
                Check(idxBad.StartsWith("http://127.0.0.1:1/"), "清单地址跟着 IndexHost 走", idxBad);
                Check(fileGood.StartsWith(FileHost + "/"), "文件地址不受 IndexHost 影响", fileGood);
                SetStr("MicroIndexHost", IndexHost);
            }

            // ------------------------------------------------------------------
            Section("G. 清单兜底：静态站上没有 /resindex 时的 /resindex.txt 回退");
            {
                // 起一个「纯静态」迷你服务器：只有 /resindex.txt 能取到，/resindex 一律 404
                int port = 18093;
                var listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                var srv = System.Threading.Tasks.Task.Run(() =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try { ctx = listener.GetContext(); } catch { break; }
                        try
                        {
                            string path = ctx.Request.Url.AbsolutePath;
                            string file = path == "/resindex.txt" ? Path.Combine(ClientDir, "resindex.txt") : null;
                            if (file != null && File.Exists(file))
                            {
                                var bytes = File.ReadAllBytes(file);
                                ctx.Response.StatusCode = 200;
                                ctx.Response.ContentType = "text/plain";
                                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                            else
                            {
                                ctx.Response.StatusCode = 404;
                            }
                        }
                        catch { }
                        finally { try { ctx.Response.Close(); } catch { } }
                    }
                });

                var t = ClientAsm.GetType("Client.Utils.ResourceDownloader", true);
                var getIndex = t.GetMethod("GetRemoteIndex", BindingFlags.NonPublic | BindingFlags.Static);
                var fIdx = t.GetField("_remoteIndex", BindingFlags.NonPublic | BindingFlags.Static);
                var fNextTry = t.GetField("_indexNextTry", BindingFlags.NonPublic | BindingFlags.Static);

                SetStr("MicroIndexHost", "http://127.0.0.1:" + port);
                SetStr("MicroLog", null);   // 占位，避免误用
                SetBool("MicroLog", false); // 测试时不写 MicroClient.log

                fIdx.SetValue(null, null);
                if (fNextTry != null) fNextTry.SetValue(null, 0);

                var result = getIndex.Invoke(null, null) as System.Collections.IDictionary;
                int n1 = CountAll(result);
                Check(result != null && n1 > 3000,
                      "只有 resindex.txt 的静态站 → 清单仍然拿到",
                      result == null ? "null" : (result.Count + " 个扩展名 / " + n1 + " 个文件"));

                // 再验证一次：指向本机 8888（有 /resindex 重写）也照样通
                fIdx.SetValue(null, null);
                if (fNextTry != null) fNextTry.SetValue(null, 0);
                SetStr("MicroIndexHost", IndexHost);
                var result2 = getIndex.Invoke(null, null) as System.Collections.IDictionary;
                int n2 = CountAll(result2);
                Check(result2 != null && n2 > 3000,
                      "本机 /resindex 也照样拿到",
                      result2 == null ? "null" : (result2.Count + " 个扩展名 / " + n2 + " 个文件"));

                try { listener.Stop(); } catch { }
                SetBool("MicroLog", false);
            }

            // ------------------------------------------------------------------
            Section("H. 配置往返：Mir2Config.ini 读进来、存回去后两个新键还在");
            {
                string tmp = Path.Combine(Path.GetTempPath(), "micro_ini_rt");
                Directory.CreateDirectory(tmp);
                File.WriteAllText(Path.Combine(tmp, "Mir2Config.ini"),
                    "[MicroClient]\r\nEnabled=True\r\nHost=http://219.138.108.82:65534\r\n" +
                    "IndexHost=http://127.0.0.1:8888\r\nFileAtRoot=True\r\nConcurrency=4\r\n",
                    new System.Text.UTF8Encoding(false));

                string oldCwd = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(tmp);
                    var st = ClientAsm.GetType("Client.Settings", true);
                    try
                    {
                        st.GetMethod("Load", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                        Check((bool)SField("MicroFileAtRoot").GetValue(null), "Load 后 FileAtRoot = True");
                        Check((string)SField("MicroIndexHost").GetValue(null) == "http://127.0.0.1:8888",
                              "Load 后 IndexHost 正确", (string)SField("MicroIndexHost").GetValue(null));

                        st.GetMethod("Save", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                        string saved = File.ReadAllText(Path.Combine(tmp, "Mir2Config.ini"));
                        Check(saved.Contains("IndexHost=http://127.0.0.1:8888"), "Save 后 IndexHost 仍在");
                        Check(System.Text.RegularExpressions.Regex.IsMatch(saved, @"FileAtRoot=\s*True", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                              "Save 后 FileAtRoot 仍在且为 True");
                    }
                    catch (Exception ex)
                    {
                        Check(false, "Load/Save 往返", (ex.InnerException ?? ex).Message);
                    }
                }
                finally { Directory.SetCurrentDirectory(oldCwd); }
            }

            // ------------------------------------------------------------------
            Section("I. 清单跨主机回退：IndexHost 连不上 → 自动用 Host 的 resindex.txt");
            {
                int port = 18094;
                var listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                System.Threading.Tasks.Task.Run(() =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try { ctx = listener.GetContext(); } catch { break; }
                        try
                        {
                            string path = ctx.Request.Url.AbsolutePath;
                            string file = path == "/resindex.txt" ? Path.Combine(ClientDir, "resindex.txt") : null;
                            if (file != null && File.Exists(file))
                            {
                                var bytes = File.ReadAllBytes(file);
                                ctx.Response.StatusCode = 200;
                                ctx.Response.ContentType = "text/plain";
                                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                            else ctx.Response.StatusCode = 404;
                        }
                        catch { }
                        finally { try { ctx.Response.Close(); } catch { } }
                    }
                });

                var t = ClientAsm.GetType("Client.Utils.ResourceDownloader", true);
                var getIndex = t.GetMethod("GetRemoteIndex", BindingFlags.NonPublic | BindingFlags.Static);
                var fIdx = t.GetField("_remoteIndex", BindingFlags.NonPublic | BindingFlags.Static);
                var fNextTry = t.GetField("_indexNextTry", BindingFlags.NonPublic | BindingFlags.Static);

                SetHostPool("http://127.0.0.1:" + port);      // 文件服务器（有 resindex.txt）
                SetStr("MicroIndexHost", "http://127.0.0.1:1"); // 清单源：故意填一个连不上的端口
                fIdx.SetValue(null, null);
                if (fNextTry != null) fNextTry.SetValue(null, 0);

                var result = getIndex.Invoke(null, null) as System.Collections.IDictionary;
                int n = CountAll(result);
                Check(result != null && n > 3000,
                      "IndexHost 挂了 → 自动用 Host 的清单",
                      result == null ? "null" : (result.Count + " 个扩展名 / " + n + " 个文件"));

                try { listener.Stop(); } catch { }
            }

            // ------------------------------------------------------------------
            Section("J. 目录兜底：只在微端接口「连不上」时生效");
            {
                // 起一个纯静态目录站（模拟远端），能按相对路径给文件
                int port = 18095;
                string probeRel = "Data/__fallback_probe.Lib";
                string probeFull = Path.Combine(ClientDir, probeRel.Replace('/', Path.DirectorySeparatorChar));

                var listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                System.Threading.Tasks.Task.Run(() =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try { ctx = listener.GetContext(); } catch { break; }
                        try
                        {
                            string rel = Uri.UnescapeDataString(ctx.Request.Url.AbsolutePath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
                            string file = Path.Combine(ClientDir, rel);
                            if (File.Exists(file))
                            {
                                var bytes = File.ReadAllBytes(file);
                                ctx.Response.StatusCode = 200;
                                ctx.Response.ContentType = "application/octet-stream";
                                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            }
                            else ctx.Response.StatusCode = 404;
                        }
                        catch { }
                        finally { try { ctx.Response.Close(); } catch { } }
                    }
                });

                var t = ClientAsm.GetType("Client.Utils.ResourceDownloader", true);
                var dl = t.GetMethod("DownloadFile", BindingFlags.NonPublic | BindingFlags.Static);
                var dirUrlM = t.GetMethod("BuildDirUrl", BindingFlags.NonPublic | BindingFlags.Static);
                var apiDown = t.GetField("_apiDownUntil", BindingFlags.NonPublic | BindingFlags.Static);

                // 下载落到临时目录，别污染客户端目录
                string tmpRoot = Path.Combine(Path.GetTempPath(), "micro_dl_test");
                if (Directory.Exists(tmpRoot)) Directory.Delete(tmpRoot, true);
                Directory.CreateDirectory(tmpRoot);
                SField("P_Client").SetValue(null, tmpRoot + "\\");
                SetBool("MicroLog", true);

                // J1：接口连不上（端口 1 拒绝连接）→ 应自动改走目录并下载成功
                SetHostPool("http://127.0.0.1:1");
                SetStr("MicroDirHost", "http://127.0.0.1:" + port);
                SetBool("MicroFileAtRoot", false);
                if (apiDown != null) apiDown.SetValue(null, 0);

                string expect = Path.Combine(tmpRoot, "Data", "Monster", "000.Lib");
                if (File.Exists(expect)) File.Delete(expect);
                var r1 = dl.Invoke(null, new object[] { "Data/Monster/000.Lib" });
                Check(r1 != null && r1.ToString() == "Success", "接口不通 → 目录兜底下载成功", r1?.ToString());
                Check(File.Exists(expect), "文件确实落盘");
                if (File.Exists(expect))
                {
                    Check(Md5(File.ReadAllBytes(expect)) == local["Data/Monster/000.Lib"], "兜底下载内容 md5 一致");
                    File.Delete(expect);
                }

                // J2：接口正常但文件不存在（404）→ 不应改走目录（目录里有同名文件也不该用）
                Directory.CreateDirectory(Path.GetDirectoryName(probeFull));
                File.WriteAllBytes(probeFull, new byte[] { 1, 2, 3, 4 });
                SetHostPool(IndexHost);                       // 接口 = 本机 8888（会 404）
                if (apiDown != null) apiDown.SetValue(null, 0);
                string probeLocal = Path.Combine(tmpRoot, probeRel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(probeLocal)) File.Delete(probeLocal);
                var r2 = dl.Invoke(null, new object[] { probeRel });
                Check(r2 != null && r2.ToString() != "Success", "接口 404 时不改走目录", r2?.ToString());
                Check(!File.Exists(probeLocal), "接口 404 时没有偷偷落盘目录版本");
                try { File.Delete(probeFull); } catch { }

                // J3：纯静态模式（FileAtRoot=true 且未配 DirHost）→ 目录地址取 Host
                SetStr("MicroDirHost", "");
                SetBool("MicroFileAtRoot", true);
                SetHostPool(FileHost);
                string du = (string)dirUrlM.Invoke(null, new object[] { "Data/Monster/000.Lib" });
                Check(du == FileHost + "/Data/Monster/000.Lib", "纯静态模式目录地址 = Host/相对路径", du);
                SetBool("MicroFileAtRoot", false);

                try { listener.Stop(); } catch { }

                string logPath = Path.Combine(AppContext.BaseDirectory, "MicroClient.log");
                if (File.Exists(logPath))
                {
                    Console.WriteLine("    --- MicroClient.log 尾部 ---");
                    foreach (var ln in File.ReadAllLines(logPath).Reverse().Take(8).Reverse())
                        Console.WriteLine("    " + ln);
                }
                try { Directory.Delete(tmpRoot, true); } catch { }
            }

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("结果: " + _pass + " 通过 / " + _fail + " 失败");
            return _fail == 0 ? 0 : 1;
        }
    }
}
