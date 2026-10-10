using System.Text;

namespace Mir2MicroServer
{
    /// <summary>
    /// 独立微端服务的配置。
    ///
    /// 三个核心项（用户要求可直接改的）：
    ///   BindIP       —— 监听地址，0.0.0.0 = 所有网卡
    ///   Port         —— 监听端口
    ///   ResourcePath —— 资源根目录
    ///
    /// 取值优先级：命令行参数 &gt; 配置文件 &gt; 内置默认值。
    /// 配置文件默认是 exe 同目录的 Mir2MicroServer.ini，首次运行会自动生成一份带注释的模板。
    /// </summary>
    internal sealed class MicroConfig
    {
        // ---------- 网络 ----------
        /// <summary>监听地址。0.0.0.0 / any / + / * 都表示所有网卡</summary>
        public string BindIP = "0.0.0.0";

        /// <summary>监听端口</summary>
        public int Port = 5679;

        /// <summary>传输通道：auto(先试 http.sys 再退纯 Socket) / httpsys / socket</summary>
        public string Transport = "auto";

        // ---------- 资源 ----------
        /// <summary>资源根目录（客户端根目录），相对路径按 exe 目录解析</summary>
        public string ResourcePath = @".\ClientResources\";

        /// <summary>允许下发的资源子目录白名单（相对资源根目录，逗号分隔）。留空或 all 表示不限制</summary>
        public string AllowedRoots = "data,map,sound,directx,localization";

        // ---------- 访问控制 ----------
        /// <summary>是否允许任意 IP 拉取资源。false 时只放行 TrustedIP</summary>
        public bool AllowAnyIP = true;

        /// <summary>关闭 AllowAnyIP 时唯一放行的来源 IP</summary>
        public string TrustedIP = "127.0.0.1";

        // ---------- 性能 ----------
        /// <summary>同时传输的请求上限（防止一次性把磁盘打爆）</summary>
        public int MaxConcurrent = 64;

        /// <summary>单个请求最长传输秒数，超时断开清理</summary>
        public int WriteTimeout = 300;

        /// <summary>流式发送的分块大小（KB）</summary>
        public int BufferKB = 64;

        // ---------- 日志 ----------
        /// <summary>是否逐条记录资源下发（高并发下建议关闭）</summary>
        public bool VerboseLog = false;

        /// <summary>日志文件路径。留空 = 只输出到控制台</summary>
        public string LogFile = @".\Logs\MicroServer.log";

        /// <summary>根路径 / 返回的名字，纯装饰</summary>
        public string ServerName = "Mir2 微端资源服务";

        /// <summary>配置文件实际使用的路径</summary>
        public string ConfigPath = "Mir2MicroServer.ini";

        /// <summary>当前生效的配置（等价于原服务端的静态 Settings）</summary>
        public static MicroConfig Current { get; private set; } = new MicroConfig();

        /// <summary>资源白名单解析后的数组（小写、无空白）。空数组表示不限制</summary>
        public string[] AllowedRootArray
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AllowedRoots)) return Array.Empty<string>();

                string raw = AllowedRoots.Trim();
                if (raw.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                    raw.Equals("*", StringComparison.Ordinal)) return Array.Empty<string>();

                var list = new List<string>();
                foreach (string part in raw.Split(',', ';', '|'))
                {
                    string item = part.Trim().Replace('\\', '/').Trim('/').ToLowerInvariant();
                    if (item.Length == 0) continue;
                    if (!item.EndsWith("/")) item += "/";
                    if (!list.Contains(item)) list.Add(item);
                }

                return list.ToArray();
            }
        }

        /// <summary>
        /// 资源根目录的绝对路径。
        ///
        /// 相对路径一律相对【本程序所在目录】解析，而不是相对当前工作目录 ——
        /// 否则从桌面快捷方式、计划任务、别的目录里启动时，"…\ClientResources\"
        /// 会指到完全不同的地方，用户会以为配置没生效。
        /// </summary>
        public string AbsoluteResourcePath
        {
            get { return ResolveFromExeDir(ResourcePath, @".\ClientResources\"); }
        }

        /// <summary>把可能是相对的路径解析成绝对路径（基准 = exe 所在目录）</summary>
        public static string ResolveFromExeDir(string path, string fallback)
        {
            try
            {
                string p = (path ?? string.Empty).Trim().Trim('"');
                if (p.Length == 0) p = fallback;

                if (!Path.IsPathRooted(p)) p = Path.Combine(AppContext.BaseDirectory, p);

                return Path.GetFullPath(p);
            }
            catch
            {
                return path ?? string.Empty;
            }
        }

        /// <summary>组装成监听前缀，交给 HttpUtil.NormalizePrefix 归一化</summary>
        public string BuildListenPrefix()
        {
            string ip = (BindIP ?? string.Empty).Trim().Trim('"');
            if (ip.Length == 0) ip = "0.0.0.0";

            // 允许用户直接把整段 URL 写进 BindIP
            int scheme = ip.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) ip = ip.Substring(scheme + 3);

            int slash = ip.IndexOf('/');
            if (slash >= 0) ip = ip.Substring(0, slash);

            return "http://" + ip + ":" + Port + "/";
        }

        // ==================================================================
        // 加载
        // ==================================================================

        /// <summary>
        /// 解析配置：命令行 → 配置文件。
        /// 返回值为 null 表示参数里出现了 --help，调用方应打印用法后退出。
        /// </summary>
        public static MicroConfig Load(string[] args, out bool helpRequested)
        {
            helpRequested = false;
            var cfg = new MicroConfig();

            var cli = ParseArgs(args, ref helpRequested);
            if (helpRequested) return cfg;

            // 1) 配置文件位置：--config 优先，否则 exe 同目录的 Mir2MicroServer.ini
            string configPath;
            if (!cli.TryGetValue("config", out configPath) || string.IsNullOrWhiteSpace(configPath))
            {
                configPath = Path.Combine(AppContext.BaseDirectory, "Mir2MicroServer.ini");
            }
            else
            {
                configPath = ResolveFromExeDir(configPath, "Mir2MicroServer.ini");
            }

            cfg.ConfigPath = configPath;

            // 2) 配置文件不存在就落一份模板，方便用户直接改
            if (!File.Exists(configPath))
            {
                try
                {
                    // 带 BOM：Windows 记事本/写字板打开才不会把中文注释看成乱码
                    File.WriteAllText(configPath, DefaultIniTemplate, new UTF8Encoding(true));
                    Console.WriteLine("已生成默认配置文件：" + configPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("配置文件生成失败（将只用默认值运行）：" + ex.Message);
                }
            }

            // 3) 读配置文件
            if (File.Exists(configPath))
            {
                try
                {
                    cfg.ApplyIni(File.ReadAllLines(configPath));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("配置文件读取失败（将只用默认值运行）：" + ex.Message);
                }
            }

            // 4) 命令行覆盖
            cfg.ApplyCli(cli);

            // 5) 归一化 + 校验
            cfg.Normalize();

            Current = cfg;
            return cfg;
        }

        private void ApplyIni(string[] lines)
        {
            var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in lines)
            {
                if (raw == null) continue;

                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#' || line[0] == ';' || line[0] == '[') continue;   // 注释 / 段头直接跳过

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                // 行尾注释：只在 "空格+#" 或 "空格+;" 时才算注释，避免误伤路径里的特殊字符
                int comment = value.IndexOf(" #", StringComparison.Ordinal);
                if (comment < 0) comment = value.IndexOf(" ;", StringComparison.Ordinal);
                if (comment > 0) value = value.Substring(0, comment).Trim();

                if (key.Length > 0) kv[key] = value;
            }

            // --- 监听地址：先处理 URL 形式，再让独立的 IP / Port 覆盖它 ---
            string urlLike = FirstOf(kv, "HTTPIPAddress", "Address", "Url", "URL", "Endpoint", "Listen", "ListenUrl");
            if (!string.IsNullOrWhiteSpace(urlLike) && urlLike.Contains("://"))
            {
                string host, portText;
                if (HttpUtil.SplitEndpoint(urlLike, out host, out portText))
                {
                    if (!string.IsNullOrWhiteSpace(host)) BindIP = host;
                    int p;
                    if (int.TryParse(portText, out p)) Port = p;
                }
            }

            string ip = FirstOf(kv, "BindIP", "ServerIP", "ListenIP", "IPAddress", "IP", "Host");
            if (!string.IsNullOrWhiteSpace(ip))
            {
                string host, portText;
                if (ip.Contains("://") && HttpUtil.SplitEndpoint(ip, out host, out portText))
                {
                    BindIP = host;
                    int p;
                    if (int.TryParse(portText, out p)) Port = p;
                }
                else
                {
                    BindIP = ip;
                }
            }

            string portValue = FirstOf(kv, "Port", "HTTPPort", "ListenPort");
            if (!string.IsNullOrWhiteSpace(portValue))
            {
                int p;
                if (int.TryParse(portValue, out p)) Port = p;
            }

            // --- 资源目录 ---
            string rp = FirstOf(kv, "ResourcePath", "ResourceRoot", "ResourceDir", "ClientResources", "Root", "Dir", "Path");
            if (!string.IsNullOrWhiteSpace(rp)) ResourcePath = rp;

            // --- 其余 ---
            string v;

            v = FirstOf(kv, "AllowedRoots", "AllowRoots", "ResourceAllowedRoots", "ResourceWhitelist");
            if (v != null) AllowedRoots = v;

            v = FirstOf(kv, "Transport", "HttpTransport", "Mode");
            if (!string.IsNullOrWhiteSpace(v)) Transport = v;

            v = FirstOf(kv, "AllowAnyIP", "ResourceAllowAnyIP");
            if (v != null) AllowAnyIP = ParseBool(v, AllowAnyIP);

            v = FirstOf(kv, "TrustedIP", "HTTPTrustedIPAddress");
            if (!string.IsNullOrWhiteSpace(v)) TrustedIP = v;

            v = FirstOf(kv, "MaxConcurrent", "ResourceMaxConcurrent", "Concurrency");
            if (!string.IsNullOrWhiteSpace(v)) { int n; if (int.TryParse(v, out n)) MaxConcurrent = n; }

            v = FirstOf(kv, "WriteTimeout", "ResourceWriteTimeout", "Timeout");
            if (!string.IsNullOrWhiteSpace(v)) { int n; if (int.TryParse(v, out n)) WriteTimeout = n; }

            v = FirstOf(kv, "BufferKB", "ResourceBufferKB", "Buffer");
            if (!string.IsNullOrWhiteSpace(v)) { int n; if (int.TryParse(v, out n)) BufferKB = n; }

            v = FirstOf(kv, "VerboseLog", "ResourceVerboseLog", "Verbose");
            if (v != null) VerboseLog = ParseBool(v, VerboseLog);

            v = FirstOf(kv, "LogFile", "LogPath", "Log");
            if (v != null) LogFile = v;

            v = FirstOf(kv, "ServerName", "Name", "Title");
            if (!string.IsNullOrWhiteSpace(v)) ServerName = v;
        }

        private void ApplyCli(Dictionary<string, string> cli)
        {
            string v;

            if (cli.TryGetValue("ip", out v) && !string.IsNullOrWhiteSpace(v))
            {
                string host, portText;
                if (v.Contains("://") && HttpUtil.SplitEndpoint(v, out host, out portText))
                {
                    BindIP = host;
                    int p;
                    if (int.TryParse(portText, out p)) Port = p;
                }
                else BindIP = v;
            }

            if (cli.TryGetValue("port", out v) && !string.IsNullOrWhiteSpace(v))
            {
                int p;
                if (int.TryParse(v, out p)) Port = p;
            }

            if (cli.TryGetValue("path", out v) && !string.IsNullOrWhiteSpace(v)) ResourcePath = v;
            if (cli.TryGetValue("roots", out v)) AllowedRoots = v;
            if (cli.TryGetValue("transport", out v) && !string.IsNullOrWhiteSpace(v)) Transport = v;
            if (cli.TryGetValue("allowanyip", out v)) AllowAnyIP = ParseBool(v, AllowAnyIP);
            if (cli.TryGetValue("trustedip", out v) && !string.IsNullOrWhiteSpace(v)) TrustedIP = v;

            if (cli.TryGetValue("maxconcurrent", out v) && !string.IsNullOrWhiteSpace(v))
            { int n; if (int.TryParse(v, out n)) MaxConcurrent = n; }

            if (cli.TryGetValue("timeout", out v) && !string.IsNullOrWhiteSpace(v))
            { int n; if (int.TryParse(v, out n)) WriteTimeout = n; }

            if (cli.TryGetValue("buffer", out v) && !string.IsNullOrWhiteSpace(v))
            { int n; if (int.TryParse(v, out n)) BufferKB = n; }

            if (cli.TryGetValue("verbose", out v)) VerboseLog = ParseBool(v, VerboseLog);
            if (cli.TryGetValue("log", out v)) LogFile = v;
        }

        private void Normalize()
        {
            BindIP = (BindIP ?? string.Empty).Trim().Trim('"');
            if (BindIP.Length == 0) BindIP = "0.0.0.0";

            if (Port < 1 || Port > 65535) Port = 5679;

            Transport = (Transport ?? "auto").Trim().ToLowerInvariant();
            if (Transport != "auto" && Transport != "httpsys" && Transport != "socket") Transport = "auto";

            if (MaxConcurrent < 4) MaxConcurrent = 4;
            if (MaxConcurrent > 512) MaxConcurrent = 512;

            if (WriteTimeout < 30) WriteTimeout = 300;
            if (WriteTimeout > 7200) WriteTimeout = 7200;

            if (BufferKB < 1) BufferKB = 64;
            if (BufferKB > 4096) BufferKB = 4096;

            if (string.IsNullOrWhiteSpace(ServerName)) ServerName = "Mir2 微端资源服务";
            if (string.IsNullOrWhiteSpace(TrustedIP)) TrustedIP = "127.0.0.1";
        }

        // ==================================================================
        // 辅助
        // ==================================================================

        private static string FirstOf(Dictionary<string, string> kv, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value;
                if (kv.TryGetValue(key, out value)) return value;
            }

            return null;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (value == null) return fallback;

            switch (value.Trim().ToLowerInvariant())
            {
                case "": return true;      // 只写 key 不写值 → 视为开启
                case "1":
                case "true":
                case "yes":
                case "y":
                case "on":
                case "enable":
                case "enabled":
                case "是":
                case "开":
                    return true;

                case "0":
                case "false":
                case "no":
                case "n":
                case "off":
                case "disable":
                case "disabled":
                case "否":
                case "关":
                    return false;

                default:
                    return fallback;
            }
        }

        /// <summary>
        /// 把命令行参数收成 键→值 的形式，支持：
        ///   --port 5679 / --port=5679 / -p 5679 / -p=5679
        /// 像 --verbose 这种不带值的开关会被记成 "true"。
        /// </summary>
        private static Dictionary<string, string> ParseArgs(string[] args, ref bool helpRequested)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (args == null) return map;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrWhiteSpace(arg)) continue;

                if (arg[0] != '-' && arg[0] != '/')
                {
                    // 不带前缀的裸参数：位置约定为 端口
                    string bare = arg.Trim();
                    int parsed;
                    if (int.TryParse(bare, out parsed) && !map.ContainsKey("port")) map["port"] = bare;
                    else if (!map.ContainsKey("path")) map["path"] = bare;
                    continue;
                }

                string body = arg.TrimStart('-', '/');
                string key;
                string value;

                int eq = body.IndexOf('=');
                if (eq >= 0)
                {
                    key = body.Substring(0, eq);
                    value = body.Substring(eq + 1);
                }
                else
                {
                    key = body;
                    bool nextIsValue = i + 1 < args.Length && args[i + 1].Length > 0 &&
                                       args[i + 1][0] != '-' && args[i + 1][0] != '/';
                    if (nextIsValue)
                    {
                        value = args[++i];
                    }
                    else
                    {
                        value = "true";     // 开关型参数
                    }
                }

                key = ExpandAlias(key.Trim());
                if (key.Length == 0) continue;

                if (key.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("h", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("?", StringComparison.OrdinalIgnoreCase))
                {
                    helpRequested = true;
                    return map;
                }

                map[key] = value;
            }

            return map;
        }

        private static string ExpandAlias(string key)
        {
            switch (key.ToLowerInvariant())
            {
                case "i":
                case "addr":
                case "address":
                case "host":
                case "serverip":
                case "bind":
                case "bindip":
                    return "ip";

                case "p":
                    return "port";

                case "r":
                case "dir":
                case "root":
                case "res":
                case "resources":
                case "resourcepath":
                    return "path";

                case "c":
                    return "config";

                case "m":
                case "concurrency":
                case "maxconcurrent":
                    return "maxconcurrent";

                case "v":
                    return "verbose";

                case "h":
                case "?":
                    return "help";

                default:
                    return key.ToLowerInvariant();
            }
        }

        /// <summary>首次运行生成的配置模板（注释尽量写清楚，用户直接改这三行就能跑）</summary>
        private const string DefaultIniTemplate = @"# ===================================================================
#  Mir2 微端资源服务 —— 配置文件
#  改完保存后重启本程序即可生效，不需要重新编译。
#  下面三项是必改项：BindIP / Port / ResourcePath
# ===================================================================

[MicroServer]

# ---- 监听地址 ----
# 0.0.0.0 = 监听本机所有网卡（外网/局域网都能连，推荐）
# 127.0.0.1 = 只允许本机访问（外网玩家连不上，仅调试用）
# 也可以写具体 IP，例如 192.168.2.253
BindIP = 0.0.0.0

# ---- 监听端口 ----
# 注意：不要和游戏主端口、账号端口重复；改完记得同步改客户端的 MicroHost
Port = 5679

# ---- 资源根目录 ----
# 指向「客户端根目录」，里面应有 Data\ Map\ Sound\ 等目录
# 可以用相对路径（相对本程序所在目录），也可以用绝对路径
ResourcePath = .\ClientResources\

# ---- 传输通道 ----
# auto   = 优先 http.sys，失败自动退回纯 Socket（推荐，普通用户不用管理员权限）
# socket = 强制纯 Socket（绑 0.0.0.0 不需要管理员权限，和游戏主端口同一套权限模型）
# httpsys= 强制 http.sys（监听非 127.0.0.1 的地址需要先用管理员注册 urlacl）
Transport = auto

# ---- 允许下发的子目录（相对资源根目录，逗号分隔）----
# 防止别人顺着资源接口把 Mir2Config.ini 这类含账号密码的配置拖走
# 填 all 表示不限制（不建议）
AllowedRoots = data,map,sound,directx,localization

# ---- 访问控制 ----
# true  = 任意 IP 都能拉资源（微端正常用法）
# false = 只放行 TrustedIP
AllowAnyIP = true
TrustedIP = 127.0.0.1

# ---- 性能 ----
# 同时传输的请求上限：防止几十人同时下载把磁盘打爆，不建议调到 128 以上
MaxConcurrent = 64
# 单个请求最长传输秒数，超时断开
WriteTimeout = 300
# 流式发送的分块大小（KB）
BufferKB = 64

# ---- 日志 ----
# 是否逐条记录每次资源下发（高并发下建议 false，排障时再开）
VerboseLog = false
# 日志文件，留空表示只输出到控制台
LogFile = .\Logs\MicroServer.log

# 根路径 / 返回的名字，纯装饰
ServerName = Mir2 微端资源服务
";
    }
}
