using System.Diagnostics;
using System.Net;
using System.Text;

namespace Mir2MicroServer
{
    /// <summary>
    /// 传输层宿主：负责"在哪监听"和"怎么写回响应"，不关心路由。
    ///
    /// 两条通道：
    ///   1) http.sys（HttpListener）—— 内核态，性能好，但监听非 127.0.0.1 的地址需要管理员登记 urlacl
    ///   2) 纯 Socket（TcpListener）—— 不需要任何权限，绑 0.0.0.0 也行，和游戏主端口同一套权限模型
    ///
    /// auto 模式先试 http.sys，失败自动退回 Socket —— 对普通用户来说就是"填什么 IP 都能起来"。
    /// </summary>
    internal abstract class HttpTransportHost
    {
        /// <summary>归一化后的监听前缀</summary>
        protected string Host;

        private HttpListener _listener;
        private SocketHttpServer _socket;
        private bool _isActive = true;

        private readonly ManualResetEventSlim _listenDone = new ManualResetEventSlim(false);

        /// <summary>实际生效的监听地址（可能和配置值不同）</summary>
        public string ActivePrefix { get; private set; } = string.Empty;

        /// <summary>实际使用的通道：httpsys 或 socket</summary>
        public string Transport { get; private set; } = string.Empty;

        /// <summary>监听过程的说明（成功或失败原因），启动时打印</summary>
        public string ListenReport { get; private set; } = string.Empty;

        /// <summary>是否已经成功监听</summary>
        public bool IsListening { get { return !string.IsNullOrEmpty(ActivePrefix); } }

        /// <summary>等待监听结果确定，返回是否在超时前确定</summary>
        public bool WaitListening(int millisecondsTimeout)
        {
            return _listenDone.Wait(millisecondsTimeout);
        }

        protected HttpTransportHost(string host)
        {
            Host = host;
        }

        // ==================================================================
        // 并发闸门
        // ==================================================================

        /// <summary>
        /// 同时传输的请求上限。
        ///
        /// 资源下发要读磁盘 + 写 socket，两个方向都吃资源且耗时不等长：
        /// 一个 200KB/s 的慢速客户端下 47MB 的大图库要卡几百秒，这期间它一直占着传输线程。
        /// 如果同时只允许一两个请求，就变成一个人下载、其他人全排队。
        /// 所以闸门要开得足够大，只要不把磁盘打爆就行（够多人并行，但仍有上限）。
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
                            int limit = MicroConfig.Current.MaxConcurrent;
                            if (limit < 4) limit = 4;
                            _transferGate = new SemaphoreSlim(limit);
                        }
                    }
                }
                return _transferGate;
            }
        }

        /// <summary>闸门剩余可用数（/resinfo 用）</summary>
        public static int GateAvailable
        {
            get { return TransferGate.CurrentCount; }
        }

        // ==================================================================
        // 监听
        // ==================================================================

        /// <summary>在专用线程里调用：协商通道并进入接受连接循环（阻塞）</summary>
        public void Listen(object obj)
        {
            CancellationToken token = (CancellationToken)obj;

            if (!HttpListener.IsSupported)
            {
                throw new InvalidOperationException(
                    "要使用 Http 服务，操作系统至少需要 Windows XP SP2 / Server 2003 及以上版本");
            }

            // 慢速客户端会长时间挂在连接上（下载大图库），线程池默认的最小线程数按 CPU 核数算，
            // 远远不够用；不调高的话后续请求会排在"等新线程"上。
            // 这里按闸门宽度放大：闸门放进来多少，就有多少线程同时占着各自的连接。
            int want = MicroConfig.Current.MaxConcurrent * 4;
            if (want < 256) want = 256;
            ThreadPool.GetMinThreads(out int minWorker, out int minIo);
            if (minWorker < want) ThreadPool.SetMinThreads(want, minIo);

            var tried = new List<string>();

            string configured = HttpUtil.NormalizePrefix(Host);
            int port = configured != null ? HttpUtil.GetPortFromPrefix(configured) : MicroConfig.Current.Port;
            bool loopbackIntent = configured != null && HttpUtil.IsLoopbackPrefix(configured);

            string mode = string.IsNullOrWhiteSpace(MicroConfig.Current.Transport)
                ? "auto"
                : MicroConfig.Current.Transport.Trim().ToLowerInvariant();

            // ---------- 通道 1：http.sys（内核态，性能最好，但可能要权限/登记）----------
            // 失败原因不在这里刷日志：http.sys 对 127.0.0.1 是永远可用的，
            // 大多数配置地址失败就该回落到下面这条。只有真的回落不了才提示。
            if (mode != "socket" && TryHttpSys(tried, configured, loopbackIntent, false))
            {
                _listenDone.Set();
                AcceptLoopHttpSys(token);
                return;
            }

            // ---------- 通道 2：纯 Socket（不需要管理员权限，绑 0.0.0.0 也行）----------
            if (mode != "httpsys" && TrySocket(tried, configured, port))
            {
                _listenDone.Set();
                _socket.Run(token);
                return;
            }

            // ---------- 兜底：http.sys 只监听本机，至少还能本地自测 ----------
            if (mode != "socket" && !loopbackIntent &&
                TryHttpSys(tried, "http://127.0.0.1:" + port + "/", true, true))
            {
                Log.Warn("Http服务：只监听了本机 127.0.0.1，外网/局域网将无法拉取资源。");
                _listenDone.Set();
                AcceptLoopHttpSys(token);
                return;
            }

            ListenReport =
                "Http服务 监听失败：没有任何一个监听地址能够使用。" + Environment.NewLine +
                string.Join(Environment.NewLine, tried.ToArray()) + Environment.NewLine +
                "  ◆ 可选解决办法：" + Environment.NewLine +
                "     1) 端口被占用：确认上一个微端进程已经完全退出" + Environment.NewLine +
                "     2) 需要 http.sys 权限：用管理员运行一次" + Environment.NewLine +
                "        netsh http add urlacl url=http://+:" + port + "/ user=Everyone" + Environment.NewLine +
                "     3) 或者把配置里的 Transport 改成 socket，" + Environment.NewLine +
                "        走纯 Socket 模式（不需要管理员权限，和游戏主端口同一套权限模型）";

            Log.Error(ListenReport);
            _listenDone.Set();
        }

        /// <summary>
        /// 逐个尝试 http.sys 监听前缀。
        ///
        /// 注意：http.sys 只对 127.0.0.1 / localhost 免登记；
        /// 其它地址（含具体 IP、http://+:端口/、http://*:端口/）都要先以
        /// 管理员身份登记过 URL 保留项，否则一律「拒绝访问」(5)。
        /// loopbackOnly=true 表示这一次只对本机地址试探。
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

                    Log.Info("Http服务 监听成功 (http.sys)：" + prefix);

                    // 比较要用归一化后的形式：用户填 0.0.0.0 时配置值是 http://+:端口/，
                    // 直接比字符串会把「本来就是它」误判成「地址被换掉了」。
                    if (configured != null && !SameEndpoint(prefix, configured))
                        Log.Info("提示：配置的监听地址 " + Host + " 用不了，已自动改用 " + prefix);

                    return true;
                }
                catch (Exception err)
                {
                    tried.Add("  ✗ [http.sys] " + prefix + " —— " + HttpUtil.DescribeListenError(err));
                    try { listener.Close(); } catch { }
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试用纯 Socket 监听。
        /// 配置了具体 IP 但绑不上（IP 变了、网卡没启用）时，再回退绑"全部网卡"。
        /// </summary>
        private bool TrySocket(List<string> tried, string configured, int port)
        {
            var attempts = new List<IPAddress> { HttpUtil.ResolveBindAddress(configured) };

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
                    tried.Add("  ✗ [socket] " + address + ":" + port + " —— 绑定失败");
                    continue;
                }

                _socket = server;
                Transport = "socket";
                ActivePrefix = "http://" + HttpUtil.BindAddressText(address) + ":" + port + "/";

                Log.Info("Http服务 监听成功（纯Socket模式，不需要管理员权限）：" + ActivePrefix);

                // 同上：Socket 通道把通配符还原成了 0.0.0.0，直接比字符串会误报
                if (configured != null && !SameEndpoint(ActivePrefix, configured))
                    Log.Info("提示：配置的监听地址 " + Host + " 无法生效，已改用 " + ActivePrefix);

                if (tried.Count > 0)
                {
                    // 把 http.sys 失败的原因一并留在日志里，省得用户只看结果不知道为什么换通道
                    ListenReport = "http.sys 通道不可用，已自动改用纯 Socket 模式。原因：" + Environment.NewLine +
                                   string.Join(Environment.NewLine, tried.ToArray());
                    Log.Info(ListenReport);
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
                    // GetContext 阻塞取连接，取出之后立刻丢给线程池处理，
                    // 否则一个慢请求就会把 accept 循环卡住，后面的人全排队。
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
        /// 想保持处理流程异步，是因为在等并发闸门时可以让线程还给线程池，
        /// 不然上百人同时下载会把"排队的闸门"变成占着上百个线程。
        /// </summary>
        private async Task ProcessRequestAsync(IHttpRequestLite request, IHttpResponseLite response)
        {
            bool acquired = false;

            try
            {
                Log.Detail(request.HttpMethod + " " + request.PathAndQuery + " 来自 " + request.RemoteIp);

                var clientIp = request.RemoteIp;

                if (!string.IsNullOrEmpty(clientIp) && !MicroConfig.Current.AllowAnyIP)
                {
                    if (clientIp != MicroConfig.Current.TrustedIP)
                    {
                        WriteResponse(response, "notrusted:" + clientIp);
                        return;
                    }
                }

                // 闸门只挡"正在传输"的那一批：挡不住就排队，进来之后才占用传输资源。
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

                // WriteResponse / WriteFileStream 内部已关闭输出流，这里再兜一次，
                // 确保异常路径下连接不会被漏掉。
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

        // ==================================================================
        // http.sys 前缀候选
        // ==================================================================

        /// <summary>
        /// http.sys 的候选前缀列表。
        ///
        /// 只对"用户明确指定的"和"真正需要的"地址做尝试，避免无谓回落；
        /// 127.0.0.1 在 http.sys 下永远可用，所以它一般放进最后兜底那一档。
        /// </summary>
        private static List<string> BuildHttpSysCandidates(string configured, bool onlyLoopback)
        {
            var list = new List<string>();

            if (configured != null) list.Add(configured);

            int port = configured != null ? HttpUtil.GetPortFromPrefix(configured) : MicroConfig.Current.Port;

            if (onlyLoopback)
            {
                AddUnique(list, "http://localhost:" + port + "/");
                AddUnique(list, "http://127.0.0.1:" + port + "/");
                return list;
            }

            AddUnique(list, "http://+:" + port + "/");

            foreach (string ip in HttpUtil.GetLanIPv4())
            {
                AddUnique(list, "http://" + ip + ":" + port + "/");
            }

            return list;
        }

        /// <summary>
        /// 判断两个监听前缀是不是同一个端点。
        /// 必须比归一化之后的形式：用户填 0.0.0.0，配置值是 http://+:端口/，
        /// 而 Socket 通道实际记的是 http://0.0.0.0:端口/ —— 归一化后三者相同。
        /// </summary>
        private static bool SameEndpoint(string a, string b)
        {
            string na = HttpUtil.NormalizePrefix(a);
            string nb = HttpUtil.NormalizePrefix(b);
            if (na == null || nb == null) return false;
            return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddUnique(List<string> list, string value)
        {
            foreach (string item in list)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) return;
            }

            list.Add(value);
        }

        // ==================================================================
        // 响应辅助
        // ==================================================================

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

        /// <summary>自定义内容类型地写一段内存数据（小文件、清单等）</summary>
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
                // 客户端提前断开，忽略
            }
        }

        /// <summary>
        /// 流式发送大文件。
        ///
        /// 相比 File.ReadAllBytes 有两个关键点：
        /// 1) 不把整个文件读进内存。几十 MB 的图库在上百人同时下载时，
        ///    内存占用恒定为缓冲区大小，而不是"人数 × 文件大小"。
        /// 2) 超时保护。客户端卡死不读时 socket 写入会一直挂着，
        ///    超时后主动断开并释放闸门名额。
        ///
        /// 返回是否发送成功。
        /// </summary>
        public bool WriteFileStream(IHttpResponseLite response, string fullPath, long length)
        {
            int bufferSize = MicroConfig.Current.BufferKB * 1024;
            if (bufferSize < 16384) bufferSize = 65536;

            int timeoutSeconds = MicroConfig.Current.WriteTimeout;

            // 按最低可接受速度（256KB/s）反推超时：文件越大给的时间越长，
            // 免得大图库把固定超时撑爆。
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
                // 客户端中途断开、写超时等：直接掐断连接
                response.Abort();
                return false;
            }
        }

        /// <summary>服务器忙（超时/过载）时回 503，并带 Retry-After，让客户端稍后重试。</summary>
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
