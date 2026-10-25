using System.Net;
using System.Text;

namespace PatchDiag;

internal static class Program
{
    private static HttpClient _http;
    private const int HashMarker = 0x3135444D; // "MD51" little-endian

    // ============================================================
    // 补丁链路诊断工具
    // 用途：排查「更新回来的文件是 404 页面」的问题。
    // 它和客户端更新器发完全一样的请求，但把每个环节的细节都打印出来：
    //   1. HTTP 状态码（旧版更新器不检查，404 页面会被写盘）
    //   2. Content-Type / Content-Length（反代错误页常见 200 + text/html）
    //   3. 内容前 64 字节预览（一眼看出是不是 HTML）
    //   4. 清单解析（有无 MD5 段）
    //   5. 实际文件与清单的大小 / MD5 对比
    //
    // 用法：
    //   PatchDiag                          自动从 D:\微端\Mir2Config.ini 读 Host
    //   PatchDiag <Host>                   指定补丁目录，如 http://host:8888/mir2/cmir/patch/
    //   PatchDiag --all <Host>             校验清单里全部文件（默认抽 8 个）
    //   PatchDiag <url> <url> ...          直接探测任意 URL
    // ============================================================

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string host = null;
        bool all = false;
        var urls = new List<string>();

        foreach (var a in args)
        {
            if (a == "--all") { all = true; continue; }
            if (a.StartsWith("http", StringComparison.OrdinalIgnoreCase)) urls.Add(a);
            else host = a;
        }

        if (host == null && urls.Count == 0)
        {
            foreach (var ini in new[] { @"D:\微端\Mir2Config.ini", @".\Mir2Config.ini" })
            {
                if (!File.Exists(ini)) continue;
                foreach (var line in File.ReadAllLines(ini))
                {
                    if (!line.TrimStart().StartsWith("Host=", StringComparison.OrdinalIgnoreCase)) continue;
                    string v = line.Substring(5).Trim();
                    if (v.Contains("patch")) { host = v; break; }
                }
                if (host != null) break;
            }
        }

        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        _http.Timeout = TimeSpan.FromSeconds(15);
        _http.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        if (urls.Count > 0)
        {
            foreach (var u in urls)
            {
                Console.WriteLine($"---- {u}");
                var f0 = await Fetch(u);
                Report(f0);
                if (f0.Data != null && LooksHtml(f0.Data))
                    Console.WriteLine("    → 内容是 HTML（错误页），不是目标文件");
                Console.WriteLine();
            }
            return 0;
        }

        if (host == null)
        {
            Console.WriteLine("!! 未找到 Host 配置。用法: PatchDiag [Host | --all Host | url ...]");
            return 1;
        }
        if (!host.EndsWith("/")) host += "/";
        Console.WriteLine($"==== 补丁链路诊断  Host = {host} ====\n");

        // ---------- 1. 清单 ----------
        Console.WriteLine("[1] 下载清单 PList.gz");
        var plist = await Fetch(host + "PList.gz");
        Report(plist);

        var entries = new List<Entry>();
        if (plist.Ok && plist.Data != null && plist.Data.Length > 4)
        {
            try
            {
                using var ms = new MemoryStream(plist.Data);
                using var r = new BinaryReader(ms);
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = r.ReadString();
                    int len = r.ReadInt32();
                    int comp = r.ReadInt32();
                    var creation = DateTime.FromBinary(r.ReadInt64());
                    entries.Add(new Entry(name, len, comp, creation, null));
                }

                bool hasMd5 = false;
                try
                {
                    if (r.ReadInt32() == HashMarker)
                    {
                        hasMd5 = true;
                        for (int i = 0; i < count; i++)
                            entries[i].Md5 = r.ReadBytes(16);
                    }
                }
                catch (EndOfStreamException) { }

                Console.WriteLine($"    清单解析成功：{count} 个文件；MD5 段 = {(hasMd5 ? "有（新版）" : "无（旧格式，需用新版 AutoPatcherAdmin 重新生成清单）")}");
                Console.WriteLine("    前 3 条: " + string.Join(" | ", entries.Take(3).Select(e => $"{e.Name} ({e.Len}B)")));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    !! 清单解析失败: {ex.Message}");
                if (plist.Data.Length > 0 && LooksHtml(plist.Data))
                    Console.WriteLine("    → 拿到的是 HTML 错误页，说明 PList.gz 这个地址本身就是 404");
            }
        }
        Console.WriteLine();

        // ---------- 2. 服务端对缺失文件的行为 ----------
        Console.WriteLine("[2] 探测不存在的文件（服务端 404 行为）");
        var missing = await Fetch(host + "__diag_not_exist__.bin");
        Report(missing);
        Console.WriteLine(missing.StatusCode >= 400
            ? "    → 返回了错误状态码。旧版更新器【不检查状态码】，把错误页正文当文件写盘，这就是 404 文件的来源。"
            : "    → 危险：缺失文件返回了 2xx/3xx，正文是错误页。这种情况状态码检查拦不住，必须靠大小 + MD5 校验。");
        Console.WriteLine();

        // ---------- 3. 抽样校验 ----------
        if (entries.Count == 0)
        {
            Console.WriteLine("[3] 清单为空，跳过文件校验。");
            return 0;
        }

        var sample = all ? entries : entries.OrderBy(_ => Guid.NewGuid()).Take(8).ToList();
        Console.WriteLine($"[3] 校验 {sample.Count}/{entries.Count} 个文件" + (all ? "" : "（加 --all 校验全部）"));

        int bad = 0;
        foreach (var e in sample)
        {
            bool gz = e.Comp != e.Len && e.Comp > 0 && e.Name != "PList.gz";
            string url = host + e.Name.Replace('\\', '/') + (gz ? ".gz" : "");
            var f = await Fetch(url);
            string verdict;

            if (!f.Ok || f.Data == null)
            {
                verdict = $"✗ 请求失败: {f.Error}";
                bad++;
            }
            else if (f.StatusCode >= 400)
            {
                verdict = $"✗ 服务端返回 {f.StatusCode}，正文 {f.Data.Length}B（旧版更新器会把它写盘）";
                bad++;
            }
            else if (LooksHtml(f.Data) && e.Len > 200)
            {
                verdict = $"✗ 状态码 {f.StatusCode} 但正文是 HTML 错误页（{f.Data.Length}B）";
                bad++;
            }
            else
            {
                byte[] content = f.Data;
                if (gz)
                {
                    try { content = Decompress(f.Data); }
                    catch { verdict = "✗ gzip 解压失败"; bad++; PrintEntry(e, f, verdict); continue; }
                }

                if (content.Length != e.Len)
                {
                    verdict = $"✗ 大小不符 清单={e.Len}B 实际={content.Length}B";
                    bad++;
                }
                else if (e.Md5 != null && !Md5Eq(System.Security.Cryptography.MD5.HashData(content), e.Md5))
                {
                    verdict = "✗ MD5 不符（服务端内容与清单不一致）";
                    bad++;
                }
                else
                {
                    verdict = $"✓ 正常 {f.Data.Length}B{(gz ? ", gzip" : "")}{(e.Md5 != null ? ", MD5 匹配" : ", 清单无 MD5")}";
                }
            }

            PrintEntry(e, f, verdict);
        }

        Console.WriteLine();
        Console.WriteLine(bad == 0
            ? "==== 结论：抽样全部正常 ===="
            : $"==== 结论：{bad}/{sample.Count} 个文件异常 ====");
        return bad == 0 ? 0 : 2;
    }

    private static void PrintEntry(Entry e, FetchResult f, string verdict)
    {
        Console.WriteLine($"    {e.Name}  [清单 {e.Len}B / 压缩 {e.Comp}B]");
        Console.WriteLine($"      {verdict}");
        if (f.Data != null && (verdict.Contains('✗') || verdict.Contains('⚠')))
            Console.WriteLine($"      正文预览: {Preview(f.Data)}");
    }

    private static async Task<FetchResult> Fetch(string url)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var data = await resp.Content.ReadAsByteArrayAsync();
            sw.Stop();
            return new FetchResult
            {
                Ok = true,
                StatusCode = (int)resp.StatusCode,
                Url = url,
                ContentType = resp.Content.Headers.ContentType?.MediaType,
                ContentLength = resp.Content.Headers.ContentLength ?? -1,
                Data = data,
                Ms = sw.ElapsedMilliseconds,
                Server = resp.Headers.Server.ToString(),
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new FetchResult { Ok = false, Url = url, Data = null, Ms = sw.ElapsedMilliseconds, Error = ex.Message };
        }
    }

    private static void Report(FetchResult f)
    {
        if (!f.Ok)
        {
            Console.WriteLine($"    ✗ 请求失败: {f.Error}");
            return;
        }
        Console.WriteLine($"    HTTP {f.StatusCode}  类型={f.ContentType ?? "?"}  ContentLength={f.ContentLength}  实际={f.Data?.Length ?? -1}B  {f.Ms}ms  Server={f.Server}");
        if (f.Data != null) Console.WriteLine($"    正文预览: {Preview(f.Data)}");
    }

    private static string Preview(byte[] d)
    {
        if (d == null || d.Length == 0) return "(空)";
        int n = Math.Min(64, d.Length);
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            char c = (char)d[i];
            sb.Append(c >= 0x20 && c < 0x7F ? c : '.');
        }
        return sb + (d.Length > 64 ? " ..." : "");
    }

    private static bool LooksHtml(byte[] d)
    {
        if (d == null || d.Length < 5) return false;
        var head = Encoding.ASCII.GetString(d, 0, Math.Min(32, d.Length)).TrimStart();
        return head.StartsWith("<") || head.Contains("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Decompress(byte[] raw)
    {
        using var gz = new System.IO.Compression.GZipStream(
            new MemoryStream(raw), System.IO.Compression.CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        return ms.ToArray();
    }

    private static bool Md5Eq(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != 16 || b.Length != 16) return false;
        for (int i = 0; i < 16; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private class Entry
    {
        public string Name; public int Len, Comp; public DateTime Creation; public byte[] Md5;
        public Entry(string n, int l, int c, DateTime t, byte[] m) { Name = n; Len = l; Comp = c; Creation = t; Md5 = m; }
    }

    private class FetchResult
    {
        public bool Ok; public int StatusCode; public string Url; public string ContentType;
        public long ContentLength; public byte[] Data; public long Ms; public string Server; public string Error;
    }
}
