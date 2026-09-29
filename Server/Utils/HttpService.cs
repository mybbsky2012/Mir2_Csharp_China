using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server.Library.Utils
{
    internal abstract class HttpService
    {
        protected string Host;
        private HttpListener _listener;
        private SocketHttpServer _socket;
        private bool _isActive = true;

        private readonly ManualResetEventSlim _listenDone = new ManualResetEventSlim(false);

        /// <summary>实际生效的监听地址（可能与配置值不同）</summary>
        public string ActivePrefix { get; private set; } = string.Empty;

        /// <summary>实际使用的通道：httpsys 或 socket</summary>
        public string Transport { get; private set; } = string.Empty;

        /// <summary>启动结果说明（成功或失败原因），供启动日志展示</summary>
        public string ListenReport { get; private set; } = string.Empty;

        /// <summary>等待「监听结果已确定」。启动日志靠它拿到真实生效的地址。</summary>
        public bool WaitListening(int millisecondsTimeout)
        {
            return _listenDone.Wait(millisecondsTimeout);
        }

        protected HttpService()
        {
        }

        /// <summary>
        /// 同时传输中的请求上限。
        ///
        /// 资源下发要「读盘 + 写 socket」，而外网玩家带宽参差不齐：
        /// 一个 200KB/s 的玩家下载 47MB 的大图库要花好几分钟，这期间那条连接
        /// 会一直占着处理线程。如果同时只允许一两条在跑，就等于一个慢玩家
        /// 把全服其他人的资源下载全部堵死。所以闸门要开得足够宽，
        /// 只用来防止上百人同时读盘把磁盘打爆，而不是用来串行化。
        /// </summary>
        private static SemaphoreSlim _transferGate;
        private static readonly object _gateLock = new object();

        internal static SemaphoreSlim TransferGate
        {
            get
            {
                if (_transferGate == null)
                {
                    lock (_gateLock)
                    {
                        if (_transferGate == null)
                        {
                            int limit = Settings.ResourceMaxConcurrent;
                            if (limit < 4) limit = 4;
                            _transferGate = new SemaphoreSlim(limit);
                        }
                    }
                }
                return _transferGate;
            }
        }

        public void Listen(object obj)
        {
            CancellationToken token = (CancellationToken)obj;

            if (!HttpListener.IsSupported)
            {
                throw new InvalidOperationException(
                    "要使用Http服务器，操作系统必须是Windows XP SP2或Server 2003或更高版本");
            }

            // 慢速客户端会长时间阻塞自己那条处理线程，线程池默认的最小线程数（≈CPU 核数）
            // 远远不够用：等线程池每秒一两个地慢慢爬升，请求会先卡在「等线程」上。
            // 这里按闸门宽度留足余量 —— 等待闸门的请求同样占着各自的线程。
            int want = Settings.ResourceMaxConcurrent * 4;
            if (want < 256) want = 256;
            ThreadPool.GetMinThreads(out int minWorker, out int minIo);
            if (minWorker < want) ThreadPool.SetMinThreads(want, minIo);

            var tried = new List<string>();

            string configured = NormalizePrefix(Host);
            int port = configured != null ? GetPortFromPrefix(configured) : 5679;
            bool loopbackIntent = configured != null && IsLoopbackPrefix(configured);

            string mode = string.IsNullOrWhiteSpace(Settings.HttpTransport)
                ? "auto"
                : Settings.HttpTransport.Trim().ToLowerInvariant();

            // ---------- 通道 1：http.sys（内核态，性能最好，但需要权限/登记）----------
            // 这里刻意不给环回兜底：http.sys 在 127.0.0.1 上永远是能起来的，
            // 如果「对外地址失败就退回环回」，就永远轮不到纯 Socket 通道接手，
            // 结果就是日志显示"开启成功"、而玩家一个都下不到资源。
            if (mode != "socket" && TryHttpSys(tried, configured, loopbackIntent, false))
            {
                _listenDone.Set();
                AcceptLoopHttpSys(token);
                return;
            }

            // ---------- 通道 2：纯 Socket（不需要管理员权限，绑定 0.0.0.0 也行）----------
            if (mode != "httpsys" && TrySocket(tried, configured, port))
            {
                _listenDone.Set();
                _socket.Run(token);
                return;
            }

            // ---------- 兜底：http.sys 只监听本机，至少服务器本机还能自测 ----------
            if (mode != "socket" && !loopbackIntent &&
                TryHttpSys(tried, "http://127.0.0.1:" + port + "/", true, true))
            {
                MessageQueue.Instance.Enqueue("Http服务器 警告：只能监听本机 127.0.0.1，局域网/外网玩家将无法下载资源！");
                _listenDone.Set();
                AcceptLoopHttpSys(token);
                return;
            }

            ListenReport = "Http服务器 启动失败：没有任何一个监听地址能够使用。" + Environment.NewLine +
                           string.Join(Environment.NewLine, tried.ToArray()) + Environment.NewLine +
                           "  → 任选一种处理方式：" + Environment.NewLine +
                           "     1) 端口被占用：先确认上一个 Server.exe 已经完全退出；" + Environment.NewLine +
                           "     2) 想用 http.sys 又不给权限：以管理员身份运行一次" + Environment.NewLine +
                           "        netsh http add urlacl url=http://+:" + port + "/ user=Everyone" + Environment.NewLine +
                           "     3) 或者把 Configs\\Setup.ini 里的 HttpTransport 改成 socket，" + Environment.NewLine +
                           "        用纯 Socket 模式（不需要管理员权限，和游戏主端口同一套权限模型）。";

            MessageQueue.Instance.Enqueue(ListenReport);
            _listenDone.Set();
        }

        /// <summary>
        /// 尝试用 http.sys 起监听。
        ///
        /// 注意事项：http.sys 只认 127.0.0.1 / localhost 免登记；
        /// 其它地址（具体网卡 IP、http://+:端口/、http://*:端口/）都要求
        /// 「管理员身份运行」或预先登记 URL 保留项，否则一律「拒绝访问」(5)。
        ///
        /// loopbackOnly=true 表示这一轮只试本机地址；配置成对外地址时第一轮必须
        /// 传 false，否则会「用环回地址假装成功」，把纯 Socket 通道挡在门外。
        /// </summary>
        private bool TryHttpSys(List<string> tried, string configured, bool loopbackIntent, bool loopbackOnly)
        {
            foreach (string prefix in BuildHttpSysCandidates(configured, loopbackIntent || loopbackOnly))
            {
                var listener = new HttpListener();

                try
                {
                    listener.Prefixes.Add(prefix);
                    listener.Start();

                    _listener = listener;
                    ActivePrefix = prefix;
                    Transport = "httpsys";

                    if (configured != null && !string.Equals(prefix, configured, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageQueue.Instance.Enqueue("Http服务器 提示：配置的监听地址 " + Host + " 用不了，已自动改用 " + prefix);
                    }

                    MessageQueue.Instance.Enqueue("Http服务器 成功开启 (http.sys): " + prefix);
                    return true;
                }
                catch (Exception err)
                {
                    tried.Add("  × [http.sys] " + prefix + " → " + DescribeListenError(err));
                    try { listener.Close(); } catch { }
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试用纯 Socket 起监听。绑 0.0.0.0 不需要管理员权限，
        /// 所以这条路在「非管理员 + 没登记 urlacl」的机器上也能work。
        /// </summary>
        private bool TrySocket(List<string> tried, string configured, int port)
        {
            var attempts = new List<IPAddress> { ResolveBindAddress(configured) };

            // 指定了具体网卡 IP 但绑不上（IP 已变、网卡没启用）时，退回监听全部网卡
            var primary = attempts[0];
            if (!primary.Equals(IPAddress.Any) && !primary.Equals(IPAddress.Loopback) &&
                !primary.Equals(IPAddress.IPv6Loopback))
            {
                attempts.Add(IPAddress.Any);
            }

            foreach (var address in attempts)
            {
                var server = new SocketHttpServer { Handler = ProcessRequestAsync };

                if (!server.Bind(address, port))
                {
                    tried.Add("  × [socket] " + address + ":" + port + " 绑定失败");
                    continue;
                }

                _socket = server;
                Transport = "socket";
                ActivePrefix = "http://" + BindAddressText(address) + ":" + port + "/";

                if (configured != null && !string.Equals(ActivePrefix, configured, StringComparison.OrdinalIgnoreCase))
                {
                    MessageQueue.Instance.Enqueue("Http服务器 提示：配置的监听地址 " + Host + " 无法生效，已改用 " + ActivePrefix);
                }

                MessageQueue.Instance.Enqueue("Http服务器 成功开启 (纯Socket模式，无需管理员权限): " + ActivePrefix);

                // 把 http.sys 那边失败的原因一并报到启动日志里：
                // 「为什么监听地址和配置的不一样」是最常被问到的问题，
                // 与其让人翻日志猜，不如直接把原因摆在台面上。
                if (tried.Count > 0)
                {
                    ListenReport = "http.sys 通道不可用，已自动改用纯 Socket 模式，原因：" + Environment.NewLine +
                                   string.Join(Environment.NewLine, tried.ToArray());
                    MessageQueue.Instance.Enqueue(ListenReport);
                }

                return true;
            }

            return false;
        }

        private void AcceptLoopHttpSys(CancellationToken token)
        {
            while (_isActive && !token.IsCancellationRequested)
            {
                HttpListenerContext context;

                try
                {
                    // GetContext 是阻塞取连接：取到之后立刻丢给线程池并行处理。
                    // 绝不能在这里把请求处理完再取下一个 —— 那正是「一个人下载、
                    // 全服排队」的根因。
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    if (!_isActive || token.IsCancellationRequested) break;
                    continue;
                }

                try
                {
                    var request = new HttpSysRequestAdapter(context.Request);
                    var response = new HttpSysResponseAdapter(context.Response);

                    _ = Task.Run(() => ProcessRequestAsync(request, response));
                }
                catch (Exception)
                {
                    try { context.Response.Abort(); } catch { }
                }
            }
        }

        /// <summary>
        /// 与通道无关的请求处理。
        ///
        /// 处理流程是异步的：请求在等并发闸门时会把线程还给线程池，
        /// 不然上百人同时来，光「排队等闸门」就要占掉上百个线程。
        /// </summary>
        private async Task ProcessRequestAsync(IHttpRequestLite request, IHttpResponseLite response)
        {
            bool acquired = false;

            try
            {
                if (Settings.ResourceVerboseLog)
                {
                    Console.WriteLine("{0} {1} HTTP/1.1", request.HttpMethod, request.PathAndQuery);
                }

                var clientIp = request.RemoteIp;

                if (!string.IsNullOrEmpty(clientIp))
                {
                    //微端资源请求在开启 ResourceAllowAnyIP 时对任意 IP 放行（其余管理接口仍限制信任 IP）
                    bool isResourceRequest = request.Path.StartsWith("/res", StringComparison.OrdinalIgnoreCase);
                    bool resourcePublic = isResourceRequest && Settings.ResourceAllowAnyIP;

                    if (clientIp != Settings.HTTPTrustedIPAddress && !resourcePublic)
                    {
                        WriteResponse(response, "notrusted:" + clientIp);
                        return;
                    }
                }

                // 闸门只是限流，不是排队等一个人下完：宽度足够同时容纳全服在线玩家的
                // 资源补下载，只有极端并发才会真正排队。
                await TransferGate.WaitAsync().ConfigureAwait(false);
                acquired = true;

                if (request.HttpMethod == "GET")
                {
                    OnGetRequest(request, response);
                }
                else
                {
                    OnPostRequest(request, response);
                }
            }
            catch (Exception ex)
            {
                try { WriteResponse(response, "request error: " + ex.Message); } catch { }
            }
            finally
            {
                if (acquired)
                {
                    try { TransferGate.Release(); } catch { }
                }

                // WriteResponse / WriteFileStream 内部已关闭输出流，这里再兜底一次，
                // 确保异常路径下连接不会悬着不放。
                try { response.Close(); } catch { }
            }
        }

        public void Stop()
        {
            _isActive = false;

            if (_listener != null && _listener.IsListening)
            {
                try { _listener.Stop(); } catch { }
            }

            if (_socket != null)
            {
                try { _socket.Stop(); } catch { }
            }

            _listenDone.Set();
        }

        public abstract void OnGetRequest(IHttpRequestLite request, IHttpResponseLite response);
        public abstract void OnPostRequest(IHttpRequestLite request, IHttpResponseLite response);

        // ------------------------------------------------------------------
        // 地址解析
        // ------------------------------------------------------------------

        /// <summary>
        /// 把配置里的地址整理成标准写法。
        ///
        /// 踩过的坑：
        ///  - IPAddress 填 0.0.0.0 表示「所有网卡」，但 HTTPIPAddress 是 URL 前缀，
        ///    http.sys 不认 0.0.0.0 这种主机写法，会直接抛「不支持该请求」(错误码 50)。
        ///    这里统一翻译成通配符 +。
        ///  - 前缀必须带路径部分，漏了尾部斜杠会被判非法。
        ///  - 漏写 http:// 或端口都要补齐。
        /// </summary>
        internal static string NormalizePrefix(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string s = raw.Trim().Trim('"');
            if (s.Length == 0) return null;

            int schemeIdx = s.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx < 0)
            {
                s = "http://" + s;
                schemeIdx = 4;
            }

            string scheme = s.Substring(0, schemeIdx).ToLowerInvariant();
            if (scheme != "http" && scheme != "https") return null;

            string rest = s.Substring(schemeIdx + 3);
            if (rest.Length == 0) return null;

            string authority = rest;
            string path = "/";

            int slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                authority = rest.Substring(0, slash);
                path = rest.Substring(slash);
            }

            if (authority.Length == 0) return null;

            string host = authority;
            string portPart = string.Empty;

            int colon = authority.LastIndexOf(':');
            if (colon >= 0 && authority.IndexOf(']') < colon)
            {
                host = authority.Substring(0, colon);
                portPart = authority.Substring(colon);
            }

            switch (host.Trim().ToLowerInvariant())
            {
                case "":
                case "*":
                case "+":
                case "0.0.0.0":
                case "::":
                case "[::]":
                case "[::0]":
                case "any":
                    host = "+";
                    break;
            }

            if (path.Length == 0) path = "/";

            return scheme + "://" + host + portPart + path;
        }

        /// <summary>
        /// 取前缀里的端口。
        ///
        /// 这里刻意不用 Uri 解析：http.sys 的通配符写法 http://+:5679/ 里的 "+"
        /// 不是合法主机名，Uri 会直接抛格式异常，结果就是端口悄悄退回默认值，
        /// 监听跑到了完全不相干的端口上（这个坑真踩过：配 5681 听成了 5679）。
        /// </summary>
        internal static int GetPortFromPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return 5679;

            string s = prefix;

            int schemeIdx = s.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx >= 0) s = s.Substring(schemeIdx + 3);

            int slash = s.IndexOf('/');
            if (slash >= 0) s = s.Substring(0, slash);

            int colon = s.LastIndexOf(':');
            if (colon >= 0 && s.IndexOf(']') < colon)
            {
                int port;
                if (int.TryParse(s.Substring(colon + 1), out port) && port > 0 && port <= 65535)
                    return port;
            }

            // 没写端口就是 http 默认的 80
            return 80;
        }

        /// <summary>取前缀里的主机部分（通配符原样返回，例如 + / * / 具体 IP / localhost）</summary>
        internal static string GetHostFromPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return string.Empty;

            string s = prefix;

            int schemeIdx = s.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx >= 0) s = s.Substring(schemeIdx + 3);

            int slash = s.IndexOf('/');
            if (slash >= 0) s = s.Substring(0, slash);

            int colon = s.LastIndexOf(':');
            if (colon >= 0 && s.IndexOf(']') < colon)
            {
                int port;
                if (int.TryParse(s.Substring(colon + 1), out port)) s = s.Substring(0, colon);
            }

            return s.Trim();
        }

        internal static bool IsLoopbackPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false;

            return prefix.StartsWith("http://127.", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("https://127.", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("http://[::1]", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("https://[::1]", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// http.sys 的候选前缀。
        ///
        /// 只试「配置里指定的」和「配置想要的」地址，绝不擅自退回环回：
        /// 127.0.0.1 上 http.sys 永远能起来，一旦把它放进这一轮，
        /// 日志就会显示「开启成功」而局域网玩家一个都连不上，
        /// 同时也把不需要权限的纯 Socket 通道挡在门外了。
        /// 真的要退回环回，由调用方在最后单独再试一次，并且会带上醒目警告。
        /// </summary>
        private static List<string> BuildHttpSysCandidates(string configured, bool onlyLoopback)
        {
            var list = new List<string>();

            if (configured != null) list.Add(configured);

            int port = configured != null ? GetPortFromPrefix(configured) : 5679;

            if (onlyLoopback)
            {
                AddUnique(list, "http://localhost:" + port + "/");
                AddUnique(list, "http://127.0.0.1:" + port + "/");
                return list;
            }

            AddUnique(list, "http://+:" + port + "/");

            foreach (string ip in GetLanIPv4())
            {
                AddUnique(list, "http://" + ip + ":" + port + "/");
            }

            return list;
        }

        private static void AddUnique(List<string> list, string value)
        {
            foreach (string item in list)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) return;
            }

            list.Add(value);
        }

        /// <summary>
        /// 纯 Socket 通道要绑哪个本地地址。通配符 → 全部网卡；
        /// 具体 IP → 该 IP；localhost / 127.x → 环回。
        /// </summary>
        private static IPAddress ResolveBindAddress(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return IPAddress.Any;

            try
            {
                string host = GetHostFromPrefix(prefix);

                if (host == "+" || host == "*" || host == "0.0.0.0" || host == "any") return IPAddress.Any;
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;

                IPAddress parsed;
                if (IPAddress.TryParse(host, out parsed)) return parsed;
            }
            catch
            {
            }

            return IPAddress.Any;
        }

        private static string BindAddressText(IPAddress address)
        {
            if (address.Equals(IPAddress.Any)) return "0.0.0.0";
            return address.ToString();
        }

        /// <summary>列出本机可用网卡的 IPv4 地址，有默认网关的（真实局域网/公网出口）排前面。</summary>
        internal static List<string> GetLanIPv4()
        {
            var primary = new List<string>();
            var secondary = new List<string>();

            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    bool hasGateway = false;
                    try
                    {
                        foreach (var gateway in nic.GetIPProperties().GatewayAddresses)
                        {
                            if (gateway != null && !gateway.Address.Equals(IPAddress.Any))
                            {
                                hasGateway = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }

                    var target = hasGateway ? primary : secondary;

                    try
                    {
                        foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                        {
                            if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                            string ip = unicast.Address.ToString();
                            if (ip.StartsWith("169.254.")) continue;   // APIPA，没意义
                            if (!target.Contains(ip)) target.Add(ip);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            foreach (string ip in secondary)
            {
                if (!primary.Contains(ip)) primary.Add(ip);
            }

            return primary;
        }

        /// <summary>把监听失败的异常翻译成能直接照做的说明。</summary>
        internal static string DescribeListenError(Exception err)
        {
            var listenerError = err as HttpListenerException;

            if (listenerError != null)
            {
                switch (listenerError.ErrorCode)
                {
                    case 5:     // ERROR_ACCESS_DENIED
                        return "拒绝访问(5)：该地址需要管理员权限，或先用管理员登记 URL 保留项";

                    case 50:    // ERROR_NOT_SUPPORTED
                        return "不支持该请求(50)：地址写法 http.sys 不认（比如主机写成 0.0.0.0，" +
                               "应写 + 或 * 或具体网卡 IP）";

                    case 183:   // ERROR_ALREADY_EXISTS
                    case 32:    // ERROR_SHARING_VIOLATION
                        return "端口已被占用(" + listenerError.ErrorCode + ")：多半是上一个 Server.exe 还没退出干净";

                    case 1312:  // ERROR_NO_SUCH_LOGON_SESSION
                        return "https 需要先给该端口绑定证书(1312)";

                    default:
                        return "错误码 " + listenerError.ErrorCode + "：" + listenerError.Message;
                }
            }

            var socketError = err as SocketException;
            if (socketError != null)
            {
                switch (socketError.SocketErrorCode)
                {
                    case SocketError.AddressAlreadyInUse:
                        return "端口已被占用(10048)：多半是上一个 Server.exe 还没退出干净";

                    case SocketError.AddressNotAvailable:
                        return "该地址在本机不存在(10049)：填的 IP 不是本机网卡地址";

                    case SocketError.AccessDenied:
                        return "拒绝访问(10013)：该端口被系统保留，或需要管理员权限";

                    default:
                        return "Socket 错误 " + socketError.SocketErrorCode + "：" + socketError.Message;
                }
            }

            return err.Message;
        }

        public void WriteResponse(IHttpResponseLite response, string responseString)
        {
            try
            {
                response.ContentLength64 = Encoding.UTF8.GetByteCount(responseString);
                response.ContentType = "text/html; charset=UTF-8";
            }
            finally
            {
                var output = response.OutputStream;
                var writer = new StreamWriter(output);
                writer.Write(responseString);
                writer.Close();
            }
        }

        /// <summary>
        /// 以二进制形式返回内存中的数据（小文件、清单等）
        /// </summary>
        public void WriteBinary(IHttpResponseLite response, byte[] data, string contentType = "application/octet-stream")
        {
            try
            {
                response.StatusCode = 200;
                response.ContentType = contentType;
                response.ContentLength64 = data.Length;
                var output = response.OutputStream;
                output.Write(data, 0, data.Length);
                output.Flush();
                output.Close();
            }
            catch
            {
                // 客户端提前断开等异常无需处理
            }
        }

        /// <summary>
        /// 流式发送磁盘文件。
        ///
        /// 和原来的 File.ReadAllBytes + WriteBinary 相比有两点关键差别：
        /// 1) 不把整个文件读进内存。最大的图库有 47MB，上百人并发时如果各占一份
        ///    内存副本，光内存就扛不住；流式发送的内存占用恒定为缓冲区大小。
        /// 2) 带传输时长上限。客户端卡死（断网、拔网线）时 socket 写会一直阻塞，
        ///    这条连接会永久占着并发闸门，超时后主动断开把名额放出来。
        ///
        /// 返回是否完整发送成功。
        /// </summary>
        public bool WriteFileStream(IHttpResponseLite response, string fullPath, long length)
        {
            int bufferSize = Settings.ResourceBufferKB * 1024;
            if (bufferSize < 16384) bufferSize = 65536;

            // 超时要按文件体积放宽，不能只用一个固定值：
            // 最大的地砖图库有 588MB，外网玩家以 256KB/s 下载要将近 40 分钟，
            // 用固定的 300 秒会把这种正当的慢速下载误判成卡死而掐断。
            // 这里按「最低可接受速度 256KB/s」推算需要多久，再取一个上限兜底。
            int timeoutSeconds = Settings.ResourceWriteTimeout;
            if (timeoutSeconds < 30) timeoutSeconds = 300;

            const long minAcceptSpeed = 256 * 1024;
            long needed = length / minAcceptSpeed;
            if (needed > timeoutSeconds) timeoutSeconds = (int)Math.Min(needed, 7200);

            try
            {
                response.StatusCode = 200;
                response.ContentType = "application/octet-stream";
                response.ContentLength64 = length;

                var watch = Stopwatch.StartNew();
                var output = response.OutputStream;
                byte[] buffer = new byte[bufferSize];

                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                               bufferSize, FileOptions.SequentialScan))
                {
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);

                        if (watch.Elapsed.TotalSeconds > timeoutSeconds)
                        {
                            response.Abort();
                            return false;
                        }
                    }
                }

                output.Flush();
                output.Close();
                return true;
            }
            catch
            {
                // 客户端中途断开、写超时等：直接掐掉连接，异常不外抛
                response.Abort();
                return false;
            }
        }

        /// <summary>
        /// 服务过载时返回 503。
        /// 客户端对这个状态码不做「资源不存在」的负缓存，稍后还会再试。
        /// </summary>
        public void WriteBusy(IHttpResponseLite response, string message = "server busy")
        {
            try
            {
                response.StatusCode = 503;
                response.AddHeader("Retry-After", "5");
                var buffer = Encoding.UTF8.GetBytes(message);
                response.ContentType = "text/plain; charset=UTF-8";
                response.ContentLength64 = buffer.Length;
                var output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Flush();
                output.Close();
            }
            catch
            {
            }
        }

        public void WriteNotFound(IHttpResponseLite response, string message = "not found")
        {
            try
            {
                response.StatusCode = 404;
                var buffer = Encoding.UTF8.GetBytes(message);
                response.ContentType = "text/plain; charset=UTF-8";
                response.ContentLength64 = buffer.Length;
                var output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Flush();
                output.Close();
            }
            catch
            {
            }
        }
    }
}
