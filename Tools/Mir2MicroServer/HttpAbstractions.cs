using System.Net;
using System.Text;

namespace Mir2MicroServer
{
    /// <summary>
    /// HTTP 请求的最小抽象。
    ///
    /// 抽出这层是为了让同一套资源下发逻辑既能跑在 Windows 的 http.sys 上，
    /// 也能跑在纯 TcpListener 上。http.sys 监听 127.0.0.1 / localhost 之外的地址
    /// 必须先登记 URL 保留项（要管理员权限），否则报「拒绝访问」(5) 或「不支持该请求」(50，
    /// 比如把主机直接写成 0.0.0.0)；而对开服的用户来说，这两条都是硬门槛。
    /// </summary>
    internal interface IHttpRequestLite
    {
        /// <summary>GET / POST ...</summary>
        string HttpMethod { get; }

        /// <summary>URL 的路径部分，已去掉查询串，例如 /resource</summary>
        string Path { get; }

        /// <summary>路径 + 查询串，例如 /resource?f=Data/Monsters/000.Lib</summary>
        string PathAndQuery { get; }

        /// <summary>对端 IP（拿不到时为空字符串）</summary>
        string RemoteIp { get; }

        /// <summary>取查询参数，已做 URL 解码；参数不存在返回空字符串</summary>
        string QueryParam(string name);
    }

    internal interface IHttpResponseLite
    {
        int StatusCode { get; set; }
        string ContentType { get; set; }
        long ContentLength64 { get; set; }

        /// <summary>响应体输出流。取用时才真正发送响应头，所以可写属性必须先设好。</summary>
        Stream OutputStream { get; }

        void AddHeader(string name, string value);

        /// <summary>异常路径下强行掐断连接</summary>
        void Abort();

        /// <summary>结束本次响应（正常路径）</summary>
        void Close();
    }

    /// <summary>http.sys 通道的请求适配</summary>
    internal sealed class HttpSysRequestAdapter : IHttpRequestLite
    {
        private readonly HttpListenerRequest _request;

        public HttpSysRequestAdapter(HttpListenerRequest request)
        {
            _request = request;
        }

        public string HttpMethod
        {
            get { return _request.HttpMethod ?? "GET"; }
        }

        public string Path
        {
            get
            {
                var uri = _request.Url;
                if (uri == null) return "/";
                return uri.AbsolutePath;
            }
        }

        public string PathAndQuery
        {
            get
            {
                var uri = _request.Url;
                if (uri == null) return "/";
                return uri.PathAndQuery;
            }
        }

        public string RemoteIp
        {
            get
            {
                var ep = _request.RemoteEndPoint;
                if (ep == null) return string.Empty;
                return ep.Address.ToString();
            }
        }

        public string QueryParam(string name)
        {
            try
            {
                return _request.QueryString[name] ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>http.sys 通道的响应适配</summary>
    internal sealed class HttpSysResponseAdapter : IHttpResponseLite
    {
        private readonly HttpListenerResponse _response;

        public HttpSysResponseAdapter(HttpListenerResponse response)
        {
            _response = response;
        }

        public int StatusCode
        {
            get { return _response.StatusCode; }
            set { try { _response.StatusCode = value; } catch { } }
        }

        public string ContentType
        {
            get { return _response.ContentType; }
            set { try { _response.ContentType = value; } catch { } }
        }

        public long ContentLength64
        {
            get { return _response.ContentLength64; }
            set { try { _response.ContentLength64 = value; } catch { } }
        }

        public Stream OutputStream
        {
            get { return _response.OutputStream; }
        }

        public void AddHeader(string name, string value)
        {
            try { _response.AddHeader(name, value); } catch { }
        }

        public void Abort()
        {
            try { _response.Abort(); } catch { }
        }

        public void Close()
        {
            try { _response.Close(); } catch { }
        }
    }

    /// <summary>
    /// 纯 Socket 通道的请求。头是流式读出来的，只带业务真正用得到的字段。
    /// </summary>
    internal sealed class SocketRequest : IHttpRequestLite
    {
        public string HttpMethod { get; set; } = "GET";
        public string RawTarget = "/";
        public string RemoteIp { get; set; } = string.Empty;

        /// <summary>HTTP/1.1 且没有 Connection: close 时为 true</summary>
        public bool KeepAlive;

        /// <summary>带请求体（有 Content-Length/Transfer-Encoding）时为 true，这种连接不保持复用</summary>
        public bool HasBody;

        private string _path = "/";
        private string _query = string.Empty;

        public string Path { get { return _path; } }
        public string PathAndQuery { get { return _query.Length == 0 ? _path : _path + "?" + _query; } }

        public void SetTarget(string target)
        {
            RawTarget = string.IsNullOrEmpty(target) ? "/" : target;

            string t = RawTarget;

            // 少数客户端会发绝对地址（GET http://host:port/x HTTP/1.1）
            int scheme = t.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0)
            {
                int slash = t.IndexOf('/', scheme + 3);
                t = slash < 0 ? "/" : t.Substring(slash);
            }

            int q = t.IndexOf('?');
            if (q >= 0)
            {
                _path = t.Substring(0, q);
                _query = t.Substring(q + 1);
            }
            else
            {
                _path = t;
                _query = string.Empty;
            }

            if (_path.Length == 0) _path = "/";
        }

        public string QueryParam(string name)
        {
            if (_query.Length == 0 || string.IsNullOrEmpty(name)) return string.Empty;

            foreach (var pair in _query.Split('&'))
            {
                if (pair.Length == 0) continue;

                int eq = pair.IndexOf('=');
                string key = eq < 0 ? pair : pair.Substring(0, eq);
                if (!string.Equals(UrlDecode(key), name, StringComparison.OrdinalIgnoreCase)) continue;

                return eq < 0 ? string.Empty : UrlDecode(pair.Substring(eq + 1));
            }

            return string.Empty;
        }

        /// <summary>
        /// 自己做 URL 解码。不用 Uri.UnescapeDataString 是有原因的：
        /// 它会把 + 原样留下（按 form 规则 + 应该还原成空格），
        /// 而资源路径里的 + 需要保留成字面量。
        /// </summary>
        public static string UrlDecode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0) return value ?? string.Empty;

            var bytes = new List<byte>(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '%' && i + 2 < value.Length)
                {
                    int hi = HexValue(value[i + 1]);
                    int lo = HexValue(value[i + 2]);
                    if (hi >= 0 && lo >= 0)
                    {
                        bytes.Add((byte)((hi << 4) | lo));
                        i += 2;
                        continue;
                    }
                }

                if (c < 0x80)
                {
                    bytes.Add((byte)c);
                    continue;
                }

                // 非 ASCII 原样按 UTF-8 编回去
                bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}
