using System.Net;
using System.Text;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.Library.Utils
{
    class HttpServer : HttpService
    {
        Thread _thread;
        CancellationTokenSource tokenSource = new();

        public HttpServer()
        {
            Host = Settings.HTTPIPAddress;
        }

        public void Start()
        {
            _thread = new Thread(Listen);
            _thread.Start(tokenSource.Token);
        }

        public new void Stop()
        {
            base.Stop();
            
            tokenSource.Cancel();
            Thread.Sleep(1000);
            tokenSource.Dispose();

        }


        public override void OnGetRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            var url = request.PathAndQuery;
            if (url.Contains("?"))
                url = url.Substring(0, url.IndexOf("?", StringComparison.Ordinal));

            url = url.ToLower();

            // 「启用HTTP服务」(StartHTTPService) 管的是注册/改名/广播这类管理接口。
            // 微端资源下发是另一条独立能力：EnableResourceService 一开就必须要监听，
            // 但那时只放行 /resource、/resindex、/resinfo，管理接口一律 404 ——
            // 否则「关了 HTTP 服务却还能注册账号」会让人以为开关失效。
            if (!Settings.StartHTTPService && !url.StartsWith("/res", StringComparison.Ordinal))
            {
                WriteNotFound(response, "http service disabled (StartHTTPService=False)");
                return;
            }

            try
            {
                switch (url)
                {
                    case "/":
                        WriteResponse(response, GameLanguage.GameName);
                        break;
                    case "/newaccount":
                        var id = request.QueryParam("id");
                        var psd = request.QueryParam("psd");
                        var email = request.QueryParam("email");
                        var name = request.QueryParam("name");
                        var question = request.QueryParam("question");
                        var answer = request.QueryParam("answer");
                        var ip = request.QueryParam("ip");
                        var p = new ClientPackets.NewAccount();
                        p.AccountID = id;
                        p.Password = psd;
                        p.EMailAddress = email;
                        p.UserName = name;
                        p.SecretQuestion = question;
                        p.SecretAnswer = answer;
                        var result = Envir.Main.HTTPNewAccount(p, ip);
                        WriteResponse(response, result.ToString());
                        break;                               
                    case "/addnamelist":
                        id = request.QueryParam("id");
                        var fileName = request.QueryParam("fileName");
                        AddNameList(id, fileName);
                        WriteResponse(response, "true");
                        break;              
                    case "/broadcast":
                        var msg = request.QueryParam("msg");
                        if (msg.Length < 5)
                        {
                            WriteResponse(response, "short");
                            return;
                        }
                        Envir.Main.Broadcast(new S.Chat
                        {
                            Message = msg.Trim(),
                            Type = ChatType.Shout2
                        });
                        WriteResponse(response, "true");
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
                    default:
                        WriteResponse(response, "error");
                        break;
                }
            }
            catch (Exception error)
            {
                WriteResponse(response, "request error: " + error);
            }
        }      

        void AddNameList(string playerName, string fileName)
        {
            fileName = Path.Combine(Settings.NameListPath, fileName);
            var sDirectory = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(sDirectory ?? throw new InvalidOperationException());
            if (!File.Exists(fileName))
                File.Create(fileName).Close();

            var tempString = fileName;
            if (File.ReadAllLines(tempString).All(t => playerName != t))
            {
                using (var line = File.AppendText(tempString))
                {
                    line.WriteLine(playerName);
                }
            }
        }    

        /// <summary>
        /// 生成资源清单：每行一个相对客户端根目录的路径，如 Data/Monster/000.Lib。
        /// 客户端靠它推算各图库目录应有的文件数量，从而在本地目录为空时也能正确建立索引。
        ///
        /// 清单在本服是静态资源，生成一次后缓存一段时间。
        /// 缓存这里用锁做双检：客户端是并发请求的，如果上百个请求同时发现缓存为空，
        /// 就会一起全盘遍历目录，白白把磁盘打满。
        /// </summary>
        string BuildResourceIndex()
        {
            if (!Settings.EnableResourceService) return string.Empty;

            lock (_indexLock)
            {
                if (_resourceIndex != null && (DateTime.UtcNow - _resourceIndexTime).TotalMinutes < ResourceIndexCacheMinutes)
                    return _resourceIndex;

                try
                {
                    var root = Path.GetFullPath(Settings.ResourcePath);
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

                    MessageQueue.Instance.Enqueue(string.Format("微端资源清单已生成，共 {0} 个文件，{1} 字节", count, _resourceIndex.Length));
                    return _resourceIndex;
                }
                catch (Exception ex)
                {
                    MessageQueue.Instance.Enqueue("生成微端资源清单失败: " + ex.Message);
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 清单的字节形态（缓存）。
        /// 清单本身有几十 KB，若每个请求都做一次 UTF8 编码就是重复劳动，
        /// 缓存成 byte[] 后可以直接写 socket。
        /// </summary>
        byte[] BuildResourceIndexBytes()
        {
            var text = BuildResourceIndex();
            if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();

            lock (_indexLock)
            {
                if (_resourceIndexBytes != null && _resourceIndex == text)
                    return _resourceIndexBytes;
            }

            return Encoding.UTF8.GetBytes(text);
        }

        private readonly object _indexLock = new object();
        private string _resourceIndex;
        private byte[] _resourceIndexBytes;
        private DateTime _resourceIndexTime = DateTime.MinValue;
        private const int ResourceIndexCacheMinutes = 10;

        /// <summary>
        /// 微端资源服务：按相对路径返回客户端缺失的资源文件。
        /// 请求示例：GET /resource?f=Data/Monsters/000.Lib
        /// 相对路径以客户端根目录为基准，因此 Data 与 Map 目录都能覆盖。
        ///
        /// 大文件走流式发送，不整块读进内存：单人下载时无所谓，
        /// 上百人同时下 47MB 的图库时，整块读会直接把内存吃光。
        /// </summary>
        void HandleResourceRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            if (!Settings.EnableResourceService)
            {
                WriteNotFound(response, "resource service disabled");
                return;
            }

            var relative = request.QueryParam("f");
            if (string.IsNullOrWhiteSpace(relative))
            {
                WriteNotFound(response, "missing parameter: f");
                return;
            }

            var fullPath = ResolveResourcePath(relative);
            if (fullPath == null)
            {
                WriteNotFound(response, "invalid path");
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

            // 逐条写日志在高并发下本身就是瓶颈：每个请求一次消息入队 + 一次日志落盘。
            // 默认关闭，排障时再打开。
            if (ok && Settings.ResourceVerboseLog)
                MessageQueue.Instance.Enqueue(string.Format("微端下发资源 {0} ({1} 字节)", relative, length));
        }

        /// <summary>
        /// 允许对外提供的资源目录白名单（相对客户端根目录、小写）。
        ///
        /// 部署时资源目录通常直接指向客户端根目录，如果不限制目录，
        /// 攻击者可以顺着资源接口把 Mir2Config.ini 这类配置（内含保存的账号密码）一起拖走。
        /// </summary>
        private static readonly string[] ResourceAllowedRoots =
        {
            "data/", "map/", "sound/", "directx/", "localization/"
        };

        /// <summary>
        /// 把客户端传来的相对路径安全解析到资源根目录之内。
        /// 三重限制：目录白名单、规范化后必须仍在根目录内（防 ../ 穿越）、路径必须真实存在。
        /// </summary>
        string ResolveResourcePath(string relative)
        {
            try
            {
                relative = relative.Replace('\\', '/').TrimStart('/');

                bool allowed = false;
                string lower = relative.ToLowerInvariant();
                foreach (var prefix in ResourceAllowedRoots)
                {
                    if (lower.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed) return null;

                var root = Path.GetFullPath(Settings.ResourcePath);
                if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    root += Path.DirectorySeparatorChar;

                var combined = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

                if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return null;

                return combined;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 资源服务状态探测接口，方便客户端或运维确认是否配置正确。
        /// </summary>
        string GetResourceInfo()
        {
            try
            {
                var root = Path.GetFullPath(Settings.ResourcePath);
                return string.Format("{{\"enabled\":{0},\"exists\":{1},\"path\":\"{2}\",\"maxConcurrent\":{3},\"gateAvailable\":{4}}}",
                    Settings.EnableResourceService ? "true" : "false",
                    Directory.Exists(root) ? "true" : "false",
                    root.Replace("\\", "\\\\"),
                    Settings.ResourceMaxConcurrent,
                    TransferGate.CurrentCount);
            }
            catch (Exception ex)
            {
                return "{\"error\":\"" + ex.Message + "\"}";
            }
        }

        public override void OnPostRequest(IHttpRequestLite request, IHttpResponseLite response)
        {
            Console.WriteLine("POST request: {0}", request.PathAndQuery);
        }
    }

}
