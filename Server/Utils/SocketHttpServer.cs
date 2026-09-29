using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server.Library.Utils
{
    /// <summary>
    /// 纯 TcpListener 实现的极简 HTTP/1.1 服务端。
    ///
    /// 为什么需要它：HttpListener 走 Windows 的 http.sys，监听 127.0.0.1 / localhost
    /// 之外的地址必须先登记 URL 保留项（需要管理员权限），否则报「拒绝访问」(5)；
    /// 而把主机写成 0.0.0.0 这种写法 http.sys 又不认，直接报「不支持该请求」(50)。
    /// 对单机开服的用户来说，"改固定 IP 就起不来" 的根源就在这里。
    ///
    /// 换成裸 socket 之后权限模型和游戏主端口完全一致：绑 0.0.0.0 不需要管理员，
    /// 不需要 urlacl，防火墙也复用同一个程序的既有放行规则。
    ///
    /// 只实现资源下发用得到的部分：请求行 + 查询串 + Content-Length 响应。
    /// 不支持分块传输、不支持 Range、不支持 HTTP/1.0 的管道化；
    /// 带请求体的连接（POST）响应完即关闭，不做复用。
    /// </summary>
    internal sealed class SocketHttpServer
    {
        private TcpListener _tcp;
        private volatile bool _active;

        /// <summary>请求处理回调（与 http.sys 通道共用同一份业务逻辑）</summary>
        public Func<IHttpRequestLite, IHttpResponseLite, Task> Handler;

        /// <summary>请求头读取上限，防止恶意超长头把内存吃光</summary>
        private const int MaxHeaderBytes = 16 * 1024;

        /// <summary>首个请求的读取超时（毫秒）</summary>
        private const int HeaderTimeoutMs = 20000;

        /// <summary>长连接上空闲等待下一个请求的超时（毫秒）</summary>
        private const int KeepAliveIdleTimeoutMs = 60000;

        public int Port { get; private set; }
        public IPAddress BoundAddress { get; private set; }

        /// <summary>
        /// 绑定端口。返回 false 表示绑定失败（端口被占用等），调用方负责兜底。
        /// </summary>
        public bool Bind(IPAddress address, int port)
        {
            try
            {
                var tcp = new TcpListener(address, port);

                // 不设 ExclusiveAddressUse：和游戏主端口一样允许快速重启，
                // 避免上一次没退干净的进程把端口占住时连重启都起不来。
                tcp.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tcp.Start();

                _tcp = tcp;
                _active = true;
                Port = ((IPEndPoint)tcp.LocalEndpoint).Port;
                BoundAddress = address;
                return true;
            }
            catch (Exception err)
            {
                try { _tcp?.Stop(); } catch { }
                _tcp = null;
                MessageQueue.Instance.Enqueue("Http服务器(纯Socket模式) 绑定 " + address + ":" + port + " 失败: " +
                                              HttpService.DescribeListenError(err));
                return false;
            }
        }

        /// <summary>接受连接循环，阻塞直到 Stop() 或取消</summary>
        public void Run(CancellationToken token)
        {
            while (_active && !token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = _tcp.AcceptTcpClient();
                }
                catch (Exception)
                {
                    if (!_active || token.IsCancellationRequested) break;
                    continue;
                }

                // 和 http.sys 通道一样：取到连接立刻交给线程池，避免一条慢连接堵住 accept。
                // 用 Task.Run 是因为处理流程是 async 的（等并发闸门时要把线程还回去）。
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        public void Stop()
        {
            _active = false;
            try { _tcp?.Stop(); } catch { }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = HeaderTimeoutMs;

                // 发送方向的超时给得很大但不留空：外网小水管玩家下载 47MB 图库本来就慢，
                // 正常的慢速下载每写一块都会很快返回，不会碰到这个上限；
                // 真正会撞上的是「对端卡死、根本不读」——那时 120 秒后抛异常掐连接，
                // 免得一个僵死的连接永久占着并发闸门。
                client.SendTimeout = 120000;

                using (client)
                {
                    var stream = client.GetStream();
                    bool first = true;

                    while (_active)
                    {
                        var request = await ReadRequestAsync(stream, client, first).ConfigureAwait(false);
                        if (request == null) break;

                        first = false;

                        var response = new SocketResponse(stream, client.Client, request.KeepAlive && !request.HasBody);

                        try
                        {
                            if (Handler != null)
                            {
                                await Handler(request, response).ConfigureAwait(false);
                            }
                            else
                            {
                                response.StatusCode = 404;
                                response.WriteBody(Encoding.UTF8.GetBytes("no handler"));
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                if (!response.HeaderSent)
                                {
                                    response.StatusCode = 500;
                                    response.WriteBody(Encoding.UTF8.GetBytes("request error: " + ex.Message));
                                }
                            }
                            catch { }
                        }

                        bool reuse = response.CanKeepAlive;
                        response.Finish();
                        if (!reuse) break;
                    }
                }
            }
            catch
            {
                // 客户端提前断开、读超时等：直接丢弃这条连接
            }
        }

        /// <summary>读取并解析一个请求头。连接关闭 / 超时 / 报文非法时返回 null。</summary>
        private async Task<SocketRequest> ReadRequestAsync(NetworkStream stream, TcpClient client, bool firstRequest)
        {
            var buffer = new byte[MaxHeaderBytes];
            int len = 0;
            int headerEnd = -1;

            int timeout = firstRequest ? HeaderTimeoutMs : KeepAliveIdleTimeoutMs;

            while (true)
            {
                headerEnd = FindHeaderEnd(buffer, len);
                if (headerEnd >= 0) break;

                if (len >= buffer.Length) return null;

                using (var cts = new CancellationTokenSource(timeout))
                {
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer.AsMemory(len, buffer.Length - len), cts.Token)
                                           .ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }

                    if (read <= 0) return null;
                    len += read;
                }
            }

            var request = new SocketRequest();

            var ep = client.Client.RemoteEndPoint as IPEndPoint;
            request.RemoteIp = ep == null ? string.Empty : ep.Address.ToString();

            string head = Encoding.ASCII.GetString(buffer, 0, headerEnd);

            var lines = head.Split('\n');
            if (lines.Length == 0) return null;

            string requestLine = lines[0].TrimEnd('\r');
            string[] parts = requestLine.Split(' ');
            if (parts.Length < 2) return null;

            request.HttpMethod = parts[0].Trim().ToUpperInvariant();
            request.SetTarget(parts[1].Trim());

            bool http11 = parts.Length < 3 || parts[2].IndexOf("1.1", StringComparison.Ordinal) >= 0;
            bool keepAlive = http11;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();

                if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0) keepAlive = false;
                    else if (value.IndexOf("keep-alive", StringComparison.OrdinalIgnoreCase) >= 0) keepAlive = true;
                }
                else if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    int contentLength;
                    if (int.TryParse(value, out contentLength) && contentLength > 0) request.HasBody = true;
                }
                else if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    request.HasBody = true;
                }
            }

            request.KeepAlive = keepAlive;
            return request;
        }

        /// <summary>查找头结束位置（\r\n\r\n 或 \n\n），返回头块长度；未结束返回 -1</summary>
        private static int FindHeaderEnd(byte[] buffer, int len)
        {
            for (int i = 0; i + 1 < len; i++)
            {
                if (buffer[i] != '\n') continue;

                // \n\n
                if (buffer[i + 1] == '\n') return i + 1;

                // \r\n\r\n
                if (buffer[i + 1] == '\r' && i + 2 < len && buffer[i + 2] == '\n') return i + 2;
            }

            return -1;
        }
    }

    /// <summary>
    /// 响应体输出流。取 OutputStream 时才把响应头发出去，
    /// 这样业务代码可以按「先设 Content-Length/ContentType，再拿流写」的顺序来写。
    ///
    /// 关键点：Close/Dispose 只 flush，不关 socket —— 否则长连接就没法复用了。
    /// </summary>
    internal sealed class SocketBodyStream : Stream
    {
        private readonly SocketResponse _owner;
        private readonly NetworkStream _inner;

        public SocketBodyStream(SocketResponse owner, NetworkStream inner)
        {
            _owner = owner;
            _inner = inner;
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override long Length { get { throw new NotSupportedException(); } }

        public override long Position
        {
            get { return _owner.Written; }
            set { throw new NotSupportedException(); }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count <= 0) return;

            _owner.EnsureHeader();
            _inner.Write(buffer, offset, count);
            _owner.AddWritten(count);
        }

        public override void Flush()
        {
            if (!_owner.HeaderSent) return;
            try { _inner.Flush(); } catch { }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_owner.HeaderSent) _inner.Flush(); } catch { }
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class SocketResponse : IHttpResponseLite
    {
        private readonly NetworkStream _stream;
        private readonly Socket _socket;
        private readonly bool _requestedKeepAlive;
        private readonly Dictionary<string, string> _extraHeaders =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly SocketBodyStream _body;

        private bool _finished;

        public SocketResponse(NetworkStream stream, Socket socket, bool requestedKeepAlive)
        {
            _stream = stream;
            _socket = socket;
            _requestedKeepAlive = requestedKeepAlive;
            _body = new SocketBodyStream(this, stream);
        }

        public int StatusCode { get; set; } = 200;
        public string ContentType { get; set; } = "application/octet-stream";
        public long ContentLength64 { get; set; }

        public bool HeaderSent { get; private set; }
        public bool Aborted { get; private set; }
        public long Written { get; private set; }

        /// <summary>本次响应是否完整发出，且连接还能继续用</summary>
        public bool CanKeepAlive
        {
            get
            {
                return _requestedKeepAlive && !Aborted && HeaderSent && Written == ContentLength64;
            }
        }

        public Stream OutputStream
        {
            get
            {
                EnsureHeader();
                return _body;
            }
        }

        public void AddHeader(string name, string value)
        {
            if (HeaderSent || string.IsNullOrEmpty(name)) return;
            _extraHeaders[name] = value ?? string.Empty;
        }

        internal void AddWritten(int count)
        {
            Written += count;
        }

        /// <summary>把缓冲区当响应体发出去（内部用，正式路径走 OutputStream）</summary>
        internal void WriteBody(byte[] data)
        {
            ContentType = "text/plain; charset=UTF-8";
            ContentLength64 = data.Length;
            var output = OutputStream;
            output.Write(data, 0, data.Length);
            output.Flush();
        }

        internal void EnsureHeader()
        {
            if (HeaderSent || Aborted) return;

            HeaderSent = true;

            var sb = new StringBuilder(256);
            sb.Append("HTTP/1.1 ").Append(StatusCode).Append(' ').Append(StatusText(StatusCode)).Append("\r\n");
            sb.Append("Content-Type: ")
              .Append(string.IsNullOrEmpty(ContentType) ? "application/octet-stream" : ContentType)
              .Append("\r\n");
            sb.Append("Content-Length: ").Append(ContentLength64).Append("\r\n");
            sb.Append("Connection: ").Append(_requestedKeepAlive ? "keep-alive" : "close").Append("\r\n");

            foreach (var header in _extraHeaders)
            {
                sb.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            }

            sb.Append("\r\n");

            var bytes = Encoding.ASCII.GetBytes(sb.ToString());
            _stream.Write(bytes, 0, bytes.Length);
        }

        public void Abort()
        {
            Aborted = true;
            try { _socket.Close(0); } catch { }
        }

        public void Close()
        {
            Finish();
        }

        /// <summary>结束响应。不能复用连接时先把发送方向关掉（让客户端收到 FIN），再关 socket。</summary>
        internal void Finish()
        {
            if (_finished) return;
            _finished = true;

            // 兜底：处理过程中异常退出、一个字节都没来得及写时，
            // 至少回一个像样的 500，别让客户端收到一个「空响应」而完全不知道发生了什么。
            if (!HeaderSent && !Aborted)
            {
                try
                {
                    StatusCode = 500;
                    ContentType = "text/plain; charset=UTF-8";
                    ContentLength64 = 0;
                    EnsureHeader();
                }
                catch { }
            }

            try { if (HeaderSent) _stream.Flush(); } catch { }

            if (Aborted) return;

            if (!CanKeepAlive)
            {
                try { _socket.Shutdown(SocketShutdown.Send); } catch { }
                try { _socket.Close(); } catch { }
            }
        }

        private static string StatusText(int code)
        {
            switch (code)
            {
                case 200: return "OK";
                case 206: return "Partial Content";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 500: return "Internal Server Error";
                case 503: return "Service Unavailable";
                default: return "OK";
            }
        }
    }
}
