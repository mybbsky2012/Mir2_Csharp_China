using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mir2MicroServer
{
    /// <summary>
    /// 监听地址处理与错误描述工具集。
    /// 全部是无状态静态方法，和业务无关，方便单测/复用。
    /// </summary>
    internal static class HttpUtil
    {
        /// <summary>
        /// 把用户填的地址归一化成标准监听前缀。
        ///
        /// 处理的坑：
        ///  - 0.0.0.0 表示"所有网卡"，但 http.sys 不认 0.0.0.0，会直接报不支持(50)，
        ///    所以统一转成通配符 +（纯 Socket 通道再自行还原成 IPAddress.Any）
        ///  - 漏写 http:// 、漏端口都要能补上
        ///  - 没写结尾斜杠会被判非法
        /// </summary>
        public static string NormalizePrefix(string raw)
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

        /// <summary>取前缀里的端口。注意这里不能用 Uri 解析——http.sys 专用写法 http://+:5679/ 里的 "+" 不是合法主机名。</summary>
        public static int GetPortFromPrefix(string prefix)
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

            return 80;   // 没写端口就是 http 默认的 80
        }

        /// <summary>取前缀里的主机名（原样返回，可能是 + / * / 具体 IP / localhost）</summary>
        public static string GetHostFromPrefix(string prefix)
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

        public static bool IsLoopbackPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false;

            return prefix.StartsWith("http://127.", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("https://127.", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("http://[::1]", StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith("https://[::1]", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把 http://ip:port/path 拆成 主机 与 端口字符串，解析失败返回 false</summary>
        public static bool SplitEndpoint(string raw, out string host, out string portText)
        {
            host = string.Empty;
            portText = string.Empty;

            if (string.IsNullOrWhiteSpace(raw)) return false;

            string normalized = NormalizePrefix(raw);
            if (normalized == null) return false;

            host = GetHostFromPrefix(normalized);
            int port = GetPortFromPrefix(normalized);
            portText = port.ToString();

            // NormalizePrefix 会把 0.0.0.0 变成 +，对外展示时还原成更好懂的写法
            if (host == "+" || host == "*") host = "0.0.0.0";

            return host.Length > 0;
        }

        /// <summary>纯 Socket 通道要绑的本机地址：通配符 → 全部网卡；具体 IP → 该 IP；localhost / 127.x → 回环</summary>
        public static IPAddress ResolveBindAddress(string prefix)
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

        public static string BindAddressText(IPAddress address)
        {
            if (address.Equals(IPAddress.Any)) return "0.0.0.0";
            return address.ToString();
        }

        /// <summary>列出本机可用的 IPv4（默认网关的排前面），用于 http.sys 多前缀尝试与启动提示</summary>
        public static List<string> GetLanIPv4()
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
                            if (ip.StartsWith("169.254.")) continue;   // APIPA，没连上
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

        /// <summary>把监听失败的异常翻成人话</summary>
        public static string DescribeListenError(Exception err)
        {
            var listenerError = err as HttpListenerException;

            if (listenerError != null)
            {
                switch (listenerError.ErrorCode)
                {
                    case 5:     // ERROR_ACCESS_DENIED
                        return "拒绝访问(5)：该地址需要管理员权限，请用管理员身份登记 URL 保留项";

                    case 50:    // ERROR_NOT_SUPPORTED
                        return "不支持该请求(50)：地址写法 http.sys 不认（例如直接写 0.0.0.0），应写 + 或 * 或具体 IP";

                    case 183:   // ERROR_ALREADY_EXISTS
                    case 32:    // ERROR_SHARING_VIOLATION
                        return "端口已被占用(" + listenerError.ErrorCode + ")：确认上一个微端进程没退干净";

                    case 1312:  // ERROR_NO_SUCH_LOGON_SESSION
                        return "https 需要先给端口绑定证书(1312)";

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
                        return "端口已被占用(10048)：确认上一个微端进程没退干净";

                    case SocketError.AddressNotAvailable:
                        return "该地址在本机不可用(10049)：填的 IP 不是本机地址";

                    case SocketError.AccessDenied:
                        return "拒绝访问(10013)：端口被系统保留，需要管理员权限";

                    default:
                        return "Socket 错误 " + socketError.SocketErrorCode + "：" + socketError.Message;
                }
            }

            return err.Message;
        }
    }
}
