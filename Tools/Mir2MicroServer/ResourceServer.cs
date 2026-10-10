using System.Text;

namespace Mir2MicroServer
{
    /// <summary>
    /// 微端资源服务本体。
    ///
    /// 对外只有三个接口（和客户端 ResourceDownloader 的契约完全一致，协议没动过）：
    ///   GET /resource?f=Data/Monsters/000.Lib   下载单个资源
    ///   GET /resindex                           资源清单（每行一个相对路径）
    ///   GET /resinfo                            服务状态自检（浏览器直接打开就能看）
    ///
    /// 原版内嵌在 Server.exe 里的 HttpServer 还带 /newaccount、/broadcast、/addnamelist
    /// 三个管理接口，那些要直接操作游戏世界（Envir.Main），独立进程没有游戏世界，
    /// 所以这里只保留资源相关部分，管理接口一律回 404 并给出说明。
    /// </summary>
    internal sealed class ResourceServer : HttpTransportHost
    {
        public ResourceServer(string host) : base(host)
        {
        }

        public override void OnGetRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            string url = request.PathAndQuery;
            if (url.Contains("?"))
                url = url.Substring(0, url.IndexOf("?", StringComparison.Ordinal));

            url = url.ToLowerInvariant();

            try
            {
                switch (url)
                {
                    case "/":
                        WriteResponse(response, MicroConfig.Current.ServerName);
                        break;

                    case "/resource":
                        HandleResourceRequest(request, response);
                        break;

                    case "/resindex":
                        WriteBinary(response, BuildResourceIndexBytes(), "text/plain; charset=UTF-8");
                        break;

                    case "/resinfo":
                        WriteResponse(response, GetResourceInfo());
                        break;

                    // 原服务端的管理接口：独立版没有游戏世界，明确告知而不是静默 404
                    case "/newaccount":
                    case "/broadcast":
                    case "/addnamelist":
                        WriteNotFound(response, "独立微端服务不提供管理接口，请使用主服务端的 HTTP 服务");
                        break;

                    case "/favicon.ico":
                        WriteNotFound(response, "no favicon");
                        break;

                    default:
                        WriteNotFound(response, "unknown endpoint: " + url +
                                                 "  （可用：/resource?f=路径 、/resindex 、/resinfo）");
                        break;
                }
            }
            catch (Exception error)
            {
                WriteResponse(response, "request error: " + error);
            }
        }

        public override void OnPostRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            WriteNotFound(response, "this service only supports GET");
        }

        // ==================================================================
        // /resource
        // ==================================================================

        /// <summary>
        /// 按相对路径返回客户端缺失的资源文件。
        /// 请求示例：GET /resource?f=Data/Monsters/000.Lib
        /// 相对路径以客户端根目录（ResourcePath）为基准，因此 Data 与 Map 目录都能覆盖。
        ///
        /// 大文件走流式发送，不整块读进内存。
        /// </summary>
        private void HandleResourceRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            string relative = request.QueryParam("f");
            if (string.IsNullOrWhiteSpace(relative))
            {
                WriteNotFound(response, "missing parameter: f");
                return;
            }

            string fullPath = ResolveResourcePath(relative);
            if (fullPath == null)
            {
                WriteNotFound(response, "invalid path: " + relative);
                return;
            }

            long length;
            try
            {
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    WriteNotFound(response, "file not found: " + relative);
                    return;
                }
                length = info.Length;
            }
            catch (Exception ex)
            {
                WriteNotFound(response, "read error: " + ex.Message);
                return;
            }

            bool ok = WriteFileStream(response, fullPath, length);

            // 逐条写日志在高并发下本身就是瓶颈（每个请求一次磁盘写）。
            // 默认关闭，排障时把 VerboseLog 打开即可。
            if (ok) Log.Detail("下发资源 " + relative + " (" + length + " 字节)");
        }

        /// <summary>
        /// 把客户端传来的相对路径安全解析到资源根目录之内。
        /// 三重限制：目录白名单、规范化后必须仍在根目录内（防 ../ 穿越）、路径真实存在。
        ///
        /// 部署时资源目录通常直接指向客户端根目录，如果不限制目录，
        /// 攻击者可以顺着资源接口把 Mir2Config.ini 这类配置（内含保存的账号密码）一起拖走。
        /// </summary>
        private string ResolveResourcePath(string relative)
        {
            try
            {
                relative = relative.Replace('\\', '/').TrimStart('/');

                string[] allowed = MicroConfig.Current.AllowedRootArray;

                // 白名单为空 = 不限制。默认是有白名单的。
                if (allowed.Length > 0)
                {
                    bool ok = false;
                    string lower = relative.ToLowerInvariant();

                    foreach (string prefix in allowed)
                    {
                        if (lower.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            ok = true;
                            break;
                        }
                    }

                    if (!ok) return null;
                }

                string root = MicroConfig.Current.AbsoluteResourcePath;
                if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    root += Path.DirectorySeparatorChar;

                string combined = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

                if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return null;

                return combined;
            }
            catch
            {
                return null;
            }
        }

        // ==================================================================
        // /resindex
        // ==================================================================

        /// <summary>
        /// 生成资源清单：每行一个相对客户端根目录的路径，如 Data/Monster/000.Lib。
        /// 客户端靠它推算各图库目录应有的文件数量，从而在本地目录为空时也能正确建立索引。
        ///
        /// 清单在本服是静态资源，生成一次后缓存一段时间。
        /// 缓存这里用锁做双检：客户端是并发请求的，如果上百个请求同时发现缓存为空，
        /// 就会一起全盘遍历目录，白白把磁盘打满。
        /// </summary>
        private string BuildResourceIndex()
        {
            lock (_indexLock)
            {
                if (_resourceIndex != null && (DateTime.UtcNow - _resourceIndexTime).TotalMinutes < ResourceIndexCacheMinutes)
                    return _resourceIndex;

                try
                {
                    string root = MicroConfig.Current.AbsoluteResourcePath;
                    if (!Directory.Exists(root)) return string.Empty;

                    int rootLength = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                        ? root.Length
                        : root.Length + 1;

                    var sb = new StringBuilder();
                    int count = 0;

                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        if (file.Length <= rootLength) continue;

                        sb.Append(file.Substring(rootLength).Replace('\\', '/'));
                        sb.Append('\n');
                        count++;
                    }

                    _resourceIndex = sb.ToString();
                    _resourceIndexBytes = Encoding.UTF8.GetBytes(_resourceIndex);
                    _resourceIndexTime = DateTime.UtcNow;

                    Log.Info(string.Format("资源清单已生成，共 {0} 个文件，{1} 字节", count, _resourceIndex.Length));
                    return _resourceIndex;
                }
                catch (Exception ex)
                {
                    Log.Error("生成资源清单失败：" + ex.Message);
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 清单的字节形态（缓存）。
        /// 清单本身有几十 KB，若每个请求都做一次 UTF8 编码就是重复劳动，
        /// 缓存成 byte[] 后可以直接写 socket。
        /// </summary>
        private byte[] BuildResourceIndexBytes()
        {
            string text = BuildResourceIndex();
            if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();

            lock (_indexLock)
            {
                if (_resourceIndexBytes != null && _resourceIndex == text)
                    return _resourceIndexBytes;
            }

            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>控制台输入 index 时调用：清掉缓存，下次请求重新全盘扫描（加了新资源后用它）</summary>
        public void InvalidateIndex()
        {
            lock (_indexLock)
            {
                _resourceIndex = null;
                _resourceIndexBytes = null;
                _resourceIndexTime = DateTime.MinValue;
            }
        }

        private readonly object _indexLock = new object();
        private string _resourceIndex;
        private byte[] _resourceIndexBytes;
        private DateTime _resourceIndexTime = DateTime.MinValue;
        private const int ResourceIndexCacheMinutes = 10;

        // ==================================================================
        // /resinfo
        // ==================================================================

        /// <summary>服务状态探测接口，方便客户端或运维确认是否配置正确。</summary>
        private string GetResourceInfo()
        {
            try
            {
                var cfg = MicroConfig.Current;
                string root = cfg.AbsoluteResourcePath;

                int fileCount = 0;
                long totalBytes = 0;

                if (Directory.Exists(root))
                {
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                        {
                            fileCount++;
                            try { totalBytes += new FileInfo(f).Length; } catch { }
                        }
                    }
                    catch
                    {
                    }
                }

                var sb = new StringBuilder();
                sb.Append('{');
                sb.Append("\"name\":\"").Append(JsonEscape(cfg.ServerName)).Append("\",");
                sb.Append("\"listening\":").Append(IsListening ? "true" : "false").Append(',');
                sb.Append("\"prefix\":\"").Append(JsonEscape(ActivePrefix)).Append("\",");
                sb.Append("\"transport\":\"").Append(JsonEscape(Transport)).Append("\",");
                sb.Append("\"exists\":").Append(Directory.Exists(root) ? "true" : "false").Append(',');
                sb.Append("\"path\":\"").Append(JsonEscape(root)).Append("\",");
                sb.Append("\"fileCount\":").Append(fileCount).Append(',');
                sb.Append("\"totalBytes\":").Append(totalBytes).Append(',');
                sb.Append("\"maxConcurrent\":").Append(cfg.MaxConcurrent).Append(',');
                sb.Append("\"gateAvailable\":").Append(GateAvailable).Append(',');
                sb.Append("\"allowAnyIP\":").Append(cfg.AllowAnyIP ? "true" : "false").Append(',');
                sb.Append("\"allowedRoots\":\"").Append(JsonEscape(cfg.AllowedRoots)).Append('"');
                sb.Append('}');

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "{\"error\":\"" + JsonEscape(ex.Message) + "\"}";
            }
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
