using System.Text;

namespace Mir2MicroServer
{
    /// <summary>
    /// 独立微端资源服务（不依赖 Server.Library.dll，可单独拷走运行）。
    ///
    /// 用法：
    ///   Mir2MicroServer.exe                        读同目录的 Mir2MicroServer.ini
    ///   Mir2MicroServer.exe --ip 0.0.0.0 --port 5679 --path E:\mir2\ClientResources
    ///   Mir2MicroServer.exe --config D:\cfg\micro.ini
    ///
    /// 配置项含义见 Mir2MicroServer.ini 里的注释。
    /// </summary>
    internal static class Program
    {
        private static ResourceServer _server;
        private static volatile bool _stopping;

        private static int Main(string[] args)
        {
            bool helpRequested;
            var cfg = MicroConfig.Load(args, out helpRequested);

            if (helpRequested)
            {
                PrintUsage();
                return 0;
            }

            Log.Init(cfg.LogFile);
            Log.Verbose = cfg.VerboseLog;

            PrintBanner(cfg);

            _server = new ResourceServer(cfg.BuildListenPrefix());

            // Ctrl+C 时优雅退出，不然端口可能被占着要等一会儿才能再起
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Shutdown();
            };

            // 监听是阻塞的，丢到专用线程；主线程留给控制台交互
            var thread = new Thread(_server.Listen) { IsBackground = true, Name = "MicroResListen" };
            thread.Start(CancellationToken.None);

            if (!_server.WaitListening(8000))
            {
                Log.Error("启动超时：8 秒内没有拿到监听结果，请检查端口是否被占用。");
            }
            else if (!_server.IsListening)
            {
                Log.Error("启动失败，请按上面的提示处理后重试。");
                WaitForExitKey();
                return 1;
            }
            else
            {
                PrintAccessHint(cfg);
            }

            Console.WriteLine();
            Console.WriteLine("输入 q 回车退出；输入 help 查看可用命令。");

            ConsoleLoop();

            return 0;
        }

        // ==================================================================
        // 控制台
        // ==================================================================

        private static void ConsoleLoop()
        {
            while (!_stopping)
            {
                string line;
                try
                {
                    line = Console.ReadLine();
                }
                catch
                {
                    break;
                }

                if (line == null) break;                     // 输入被重定向且已结束

                string cmd = line.Trim().ToLowerInvariant();
                if (cmd.Length == 0) continue;

                switch (cmd)
                {
                    case "q":
                    case "quit":
                    case "exit":
                        Shutdown();
                        return;

                    case "help":
                    case "h":
                    case "?":
                        PrintCommands();
                        break;

                    case "info":
                    case "status":
                        PrintStatus();
                        break;

                    case "index":
                    case "reindex":
                        _server?.InvalidateIndex();
                        Log.Info("资源清单缓存已清空，下次请求 /resindex 会重新扫描目录。");
                        break;

                    case "verbose":
                        Log.Verbose = !Log.Verbose;
                        Log.Info("明细日志已" + (Log.Verbose ? "打开" : "关闭") + "。");
                        break;

                    default:
                        Console.WriteLine("未知命令：" + line + "，输入 help 查看可用命令。");
                        break;
                }
            }
        }

        private static void PrintCommands()
        {
            Console.WriteLine();
            Console.WriteLine("可用命令：");
            Console.WriteLine("  info / status   查看当前运行状态（监听地址、资源目录、文件数、并发）");
            Console.WriteLine("  index           清空资源清单缓存，下次请求 /resindex 时重新扫描");
            Console.WriteLine("  verbose         切换明细日志（逐条记录每次资源下发）");
            Console.WriteLine("  help            显示本帮助");
            Console.WriteLine("  q               退出（等于 quit / exit，也可直接关窗口）");
            Console.WriteLine();
        }

        private static void PrintStatus()
        {
            var cfg = MicroConfig.Current;

            Console.WriteLine();
            Console.WriteLine("---------------- 运行状态 ----------------");
            Console.WriteLine("  配置的监听地址 : " + cfg.BuildListenPrefix());
            Console.WriteLine("  实际监听地址   : " + (string.IsNullOrEmpty(_server.ActivePrefix) ? "(未监听)" : _server.ActivePrefix));
            Console.WriteLine("  传输通道       : " + _server.Transport);
            Console.WriteLine("  资源根目录     : " + cfg.AbsoluteResourcePath);
            Console.WriteLine("  资源目录存在   : " + (Directory.Exists(cfg.AbsoluteResourcePath) ? "是" : "否"));
            Console.WriteLine("  允许的目录     : " + (cfg.AllowedRootArray.Length == 0 ? "(不限制)" : cfg.AllowedRoots));
            Console.WriteLine("  任意 IP 可访问 : " + (cfg.AllowAnyIP ? "是" : "否（只放行 " + cfg.TrustedIP + "）"));
            Console.WriteLine("  并发上限       : " + cfg.MaxConcurrent + "（当前空闲名额 " + HttpTransportHost.GateAvailable + "）");
            Console.WriteLine("  配置文件       : " + cfg.ConfigPath);
            Console.WriteLine("  日志文件       : " + (string.IsNullOrWhiteSpace(cfg.LogFile) ? "(仅控制台)" : cfg.LogFile));
            Console.WriteLine("------------------------------------------");
            Console.WriteLine();
        }

        // ==================================================================
        // 启动提示
        // ==================================================================

        private static void PrintBanner(MicroConfig cfg)
        {
            Console.WriteLine();
            Console.WriteLine("==================================================================");
            Console.WriteLine("  Mir2 微端资源服务（独立版） v1.0");
            Console.WriteLine("==================================================================");
            Console.WriteLine("  监听地址     : " + cfg.BindIP + ":" + cfg.Port);
            Console.WriteLine("  传输通道     : " + cfg.Transport);
            Console.WriteLine("  资源根目录   : " + cfg.AbsoluteResourcePath);

            if (!Directory.Exists(cfg.AbsoluteResourcePath))
            {
                Console.WriteLine("  ⚠ 警告       : 资源根目录不存在！请修改配置里的 ResourcePath。");
            }

            Console.WriteLine("  并发上限     : " + cfg.MaxConcurrent);
            Console.WriteLine("  配置文件     : " + cfg.ConfigPath);
            Console.WriteLine("==================================================================");
            Console.WriteLine();
        }

        private static void PrintAccessHint(MicroConfig cfg)
        {
            Console.WriteLine();
            Console.WriteLine("------ 客户端 Mir2Config.ini 里请这样填 ------[MicroClient]------");

            string portPart = cfg.Port == 80 ? string.Empty : ":" + cfg.Port;
            var printed = new List<string>();

            // 监听本机回环时只提示回环；监听全部网卡时把本机各网卡 IP 都列出来
            string boundHost = HttpUtil.GetHostFromPrefix(HttpUtil.NormalizePrefix(_server.ActivePrefix) ?? string.Empty);
            bool loopbackOnly = boundHost == "localhost" || boundHost == "127.0.0.1" || boundHost == "::1";

            if (loopbackOnly)
            {
                printed.Add("http://127.0.0.1" + portPart + "/");
            }
            else if (boundHost != "+" && boundHost != "*" && boundHost.Length > 0)
            {
                printed.Add("http://" + boundHost + portPart + "/");
            }
            else
            {
                foreach (string ip in HttpUtil.GetLanIPv4()) printed.Add("http://" + ip + portPart + "/");

                if (printed.Count == 0)
                {
                    Console.WriteLine("  （没有检测到可用的局域网 IPv4，请确认网卡已连接）");
                }
            }

            foreach (string host in printed)
            {
                Console.WriteLine("  Host = " + host);
            }

            Console.WriteLine();
            Console.WriteLine("  自检：浏览器打开 " + (printed.Count > 0 ? printed[0] : "http://127.0.0.1" + portPart + "/") + "resinfo");
            Console.WriteLine("  说明：客户端里 Host 填【本机局域网 IP】；多个 IP 时填玩家能访问到的那个。");
            Console.WriteLine("----------------------------------------------------------------");
        }

        private static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("Mir2 微端资源服务（独立版）");
            Console.WriteLine();
            Console.WriteLine("用法：");
            Console.WriteLine("  Mir2MicroServer.exe [选项]");
            Console.WriteLine();
            Console.WriteLine("选项：");
            Console.WriteLine("  -c, --config <文件>     配置文件路径（默认：同目录的 Mir2MicroServer.ini）");
            Console.WriteLine("  -i, --ip <地址>         监听地址，如 0.0.0.0 / 127.0.0.1 / 192.168.2.253");
            Console.WriteLine("  -p, --port <端口>       监听端口，如 5679");
            Console.WriteLine("  -r, --path <目录>       资源根目录（客户端根目录）");
            Console.WriteLine("      --roots <列表>      允许下发的子目录，逗号分隔；all 表示不限制");
            Console.WriteLine("      --transport <模式>  auto | socket | httpsys");
            Console.WriteLine("      --allowanyip <布尔> 是否允许任意 IP 拉取（默认 true）");
            Console.WriteLine("      --trustedip <地址>  关闭 allowanyip 时唯一放行的来源 IP");
            Console.WriteLine("      --maxconcurrent <n> 并发传输上限（默认 64）");
            Console.WriteLine("      --timeout <秒>      单请求传输超时（默认 300）");
            Console.WriteLine("      --buffer <KB>       流式发送分块大小（默认 64）");
            Console.WriteLine("      --log <文件>        日志文件；留空则只输出控制台");
            Console.WriteLine("  -v, --verbose           打开明细日志");
            Console.WriteLine("  -h, --help              显示本帮助");
            Console.WriteLine();
            Console.WriteLine("示例：");
            Console.WriteLine("  Mir2MicroServer.exe --ip 0.0.0.0 --port 5679 --path E:\\mir2\\ClientResources");
            Console.WriteLine("  Mir2MicroServer.exe -p 5680 -r .\\Res --transport socket");
            Console.WriteLine();
        }

        private static void WaitForExitKey()
        {
            Console.WriteLine();
            Console.WriteLine("按回车键退出。");
            try { Console.ReadLine(); } catch { }
        }

        private static void Shutdown()
        {
            if (_stopping) return;
            _stopping = true;

            Console.WriteLine();
            Log.Info("正在停止服务...");

            try { _server?.Stop(); } catch { }

            Log.Info("已停止。");
        }
    }
}
