using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MicroRedirectTest
{
    /// <summary>
    /// 验证「本机只给清单，文件 302 甩给远端」这条链路对真实客户端是否成立。
    /// HttpClient 的配置与 Client/Utils/ResourceDownloader.cs 的 SharedClient() 保持一致
    /// （HttpClientHandler + MaxConnectionsPerServer + 无限 Timeout），
    /// 因此重定向跟随、Range 保留、状态码判定的行为与游戏内一致。
    /// </summary>
    internal static class Program
    {
        private static HttpClient _client;
        private static string _base;
        private static string _root;
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failDetail = new List<string>();

        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            _base = args.Length > 0 ? args[0].TrimEnd('/') : "http://127.0.0.1:18082";
            _root = args.Length > 1 ? args[1] : @"D:\Mirserver\ClientResources";

            // UseProxy=false 仅用于排除本机沙箱代理（http_proxy=127.0.0.1:49509）干扰：
            // 该代理会把 302 原样回传且不跟随，导致误判。玩家机器无此环境变量时，
            // 客户端 SharedClient() 用的是 HttpClientHandler 默认（UseProxy=true）也会正常跟随。
            var handler = new HttpClientHandler { MaxConnectionsPerServer = 4, UseProxy = false };
            _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };

            Console.WriteLine("入口        : " + _base);
            Console.WriteLine("资源根      : " + _root);
            Console.WriteLine("Handler     : HttpClientHandler (AllowAutoRedirect 默认 true)");
            Console.WriteLine(new string('=', 78));

            await TestIndex();
            await TestPathStyle();
            await TestQueryStyle();
            await TestRange();
            await TestNotFoundAndTraversal();
            await TestFullIndex();

            Console.WriteLine(new string('=', 78));
            Console.WriteLine($"结果: {_pass} 通过 / {_fail} 失败");
            if (_failDetail.Count > 0)
            {
                Console.WriteLine("失败明细:");
                foreach (var s in _failDetail.Take(20)) Console.WriteLine("  " + s);
            }
            return _fail == 0 ? 0 : 1;
        }

        // ---------------- A. 清单 ----------------
        private static async Task TestIndex()
        {
            Section("A. 资源清单 /resindex");
            string text;
            try
            {
                using var resp = await _client.GetAsync(_base + "/resindex");
                Check((int)resp.StatusCode == 200, "状态码 200", ((int)resp.StatusCode).ToString());
                text = await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Check(false, "拉取清单", ex.Message);
                return;
            }

            var lines = text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            Check(lines.Count > 3000, "清单条数 > 3000", lines.Count + " 条");

            // BOM 必须查原始字节：解码后的字符串可能已经把 BOM 剥掉，看不出问题。
            // 清单带 BOM 会让第一个路径前多出 \uFEFF，那个文件永远 404。
            byte[] raw;
            using (var r2 = await _client.GetAsync(_base + "/resindex"))
                raw = await r2.Content.ReadAsByteArrayAsync();
            bool hasBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            Check(!hasBom, "清单无 BOM（原始字节）", hasBom ? "前三字节 EF BB BF" : "");
            Check(raw.Length > 0 && raw[0] != 0xEF, "首字节不是 BOM 前缀", raw.Length > 0 ? "0x" + raw[0].ToString("X2") : "空响应");

            Check(lines.All(x => x.IndexOf(':') < 0), "无 notrusted 之类异常行");
        }

        // ---------------- B. 路径式 ----------------
        private static async Task TestPathStyle()
        {
            Section("B. 路径式 /resource/<path>（302 跟随 + 内容校验）");
            await CompareWithLocal("/resource/Data/Monster/000.Lib", @"Data\Monster\000.Lib", "普通文件");
            await CompareWithLocal("/resource/Data/AWeapon/04%20L.Lib", @"Data\AWeapon\04 L.Lib", "带空格");
            await CompareWithLocal("/resource/Data/ARWeapon/00%20S.Lib", @"Data\ARWeapon\00 S.Lib", "带空格2");
            await CompareWithLocal("/resource/Data/%E8%87%AA%E5%AE%9A%E4%B9%89%E7%B4%A0%E6%9D%90.Lib", @"Data\自定义素材.Lib", "中文名");
        }

        // ---------------- C. 旧式 query（客户端当前实际用的格式） ----------------
        private static async Task TestQueryStyle()
        {
            Section("C. 旧式 /resource?f=<path>（客户端现状，不改客户端）");
            await CompareWithLocal("/resource?f=Data/Monster/000.Lib", @"Data\Monster\000.Lib", "普通文件");
            await CompareWithLocal("/resource?f=Data/AWeapon/04%20L.Lib", @"Data\AWeapon\04 L.Lib", "带空格（原先 404 的 43 个之一）");
            await CompareWithLocal("/resource?f=Data/%E8%87%AA%E5%AE%9A%E4%B9%89%E7%B4%A0%E6%9D%90.Lib", @"Data\自定义素材.Lib", "中文名（原先 404）");
        }

        // ---------------- D. 断点续传 ----------------
        private static async Task TestRange()
        {
            Section("D. 断点续传（302 之后 Range 是否保留）");
            string rel = @"Data\Effect.Lib";
            long total = new FileInfo(Path.Combine(_root, rel)).Length;

            // D1. 从尾部 50 字节续传
            long from = total - 50;
            var r1 = await ProbeRange("/resource/Data/Effect.Lib", from);
            Check(r1.code == 206, "Range 尾部 → 206", "实际 " + r1.code);
            Check(r1.len == 50, "只返回剩余 50 字节", "实际 " + r1.len + " / 全量 " + total);

            // D2. 中途续传
            long mid = total / 2;
            var r2 = await ProbeRange("/resource/Data/Effect.Lib", mid);
            Check(r2.code == 206, "Range 中段 → 206", "实际 " + r2.code);
            Check(r2.len == total - mid, "长度 = 总长-起点", "实际 " + r2.len + " / 期望 " + (total - mid));

            // D3. 旧式 query 也要能用续传
            var r3 = await ProbeRange("/resource?f=Data/Effect.Lib", from);
            Check(r3.code == 206 && r3.len == 50, "query 式续传同样 206 + 50B", "code " + r3.code + " len " + r3.len);
        }

        // ---------------- E. 404 与穿越 ----------------
        private static async Task TestNotFoundAndTraversal()
        {
            Section("E. 错误语义（404 永久拉黑 / 穿越拦截）");
            int c1 = await ProbeCode("/resource/Data/NoSuchFile.Lib");
            Check(c1 == 404, "远端没有的文件 → 404（客户端判永久缺失）", "实际 " + c1);

            int c2 = await ProbeCode("/resource?f=../../../../Windows/win.ini");
            Check(c2 == 404, "query 式目录穿越被拦", "实际 " + c2);

            int c3 = await ProbeCode("/resource?f=");
            Check(c3 == 404, "空参数被拦", "实际 " + c3);
        }

        // ---------------- F. 全量 ----------------
        private static async Task TestFullIndex()
        {
            Section("F. 全量探测（3661 条清单，Range: bytes=0-0）");
            string indexFile = Path.Combine(_root, "resindex.txt");
            if (!File.Exists(indexFile)) { Check(false, "清单文件存在", indexFile); return; }

            var entries = File.ReadAllLines(indexFile).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            Console.WriteLine("  清单条数: " + entries.Count);

            int ok = 0, bad = 0;
            var sw = Stopwatch.StartNew();
            var badList = new List<string>();

            using (var sem = new SemaphoreSlim(16))
            {
                var tasks = entries.Select(async e =>
                {
                    await sem.WaitAsync();
                    try
                    {
                        // 与客户端一致：路径原样拼 query，空格/中文由 Uri.EscapeDataString 编码并保留 /
                        string encoded = Uri.EscapeDataString(e).Replace("%2F", "/");
                        int code = await ProbeCode("/resource?f=" + encoded);
                        if (code == 206 || code == 200) Interlocked.Increment(ref ok);
                        else { Interlocked.Increment(ref bad); lock (badList) if (badList.Count < 15) badList.Add(e + " -> " + code); }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref bad);
                        lock (badList) if (badList.Count < 15) badList.Add(e + " -> " + ex.GetType().Name);
                    }
                    finally { sem.Release(); }
                }).ToArray();
                await Task.WhenAll(tasks);
            }
            sw.Stop();

            double pct = entries.Count == 0 ? 0 : ok * 100.0 / entries.Count;
            Console.WriteLine($"  耗时 {sw.ElapsedMilliseconds} ms，通过 {ok} / {entries.Count} = {pct:F2}%");
            if (badList.Count > 0)
            {
                Console.WriteLine("  失败样本:");
                foreach (var s in badList) Console.WriteLine("    " + s);
            }
            Check(bad == 0, "旧式 query 全量 100%", bad + " 个失败");
        }

        // ---------------- helpers ----------------
        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine("## " + title);
        }

        private static void Check(bool ok, string name, string extra = "")
        {
            if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name + (extra.Length > 0 ? "  | " + extra : "")); }
        }

        private static async Task CompareWithLocal(string urlPath, string localRel, string label)
        {
            string local = Path.Combine(_root, localRel);
            if (!File.Exists(local)) { Check(false, label + " 本地样本存在", local); return; }

            try
            {
                using var resp = await _client.GetAsync(_base + urlPath);
                if ((int)resp.StatusCode != 200)
                {
                    Check(false, label + " 返回 200", "实际 " + (int)resp.StatusCode);
                    return;
                }
                byte[] got = await resp.Content.ReadAsByteArrayAsync();
                byte[] want = File.ReadAllBytes(local);
                string md5got = Md5(got), md5want = Md5(want);
                Check(got.Length == want.Length && md5got == md5want,
                    label + " 内容一致",
                    $"{got.Length}B/{md5got} vs {want.Length}B/{md5want}");
            }
            catch (Exception ex)
            {
                Check(false, label, ex.Message);
            }
        }

        private static async Task<(int code, long len)> ProbeRange(string urlPath, long from)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _base + urlPath);
            req.Headers.Range = new RangeHeaderValue(from, null);
            using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            byte[] body = await resp.Content.ReadAsByteArrayAsync();
            return ((int)resp.StatusCode, body.LongLength);
        }

        private static async Task<int> ProbeCode(string urlPath)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _base + urlPath);
            req.Headers.Range = new RangeHeaderValue(0, 0);
            using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            await resp.Content.ReadAsByteArrayAsync();
            return (int)resp.StatusCode;
        }

        private static string Md5(byte[] data)
        {
            using var md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
