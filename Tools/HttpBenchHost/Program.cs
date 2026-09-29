using System.Reflection;
using Server;

namespace HttpBenchHost
{
    /// <summary>
    /// 独立压测宿主：只为了在「不打断线上服务端」的前提下验证资源服务的并发改造。
    ///
    /// 做法是直接反射加载 Server.Library 里真实的 HttpServer 实现，把它挂到一个
    /// 单独的端口上，然后用同一套压测脚本对比改造前后的表现。
    /// 被测代码和正式运行的是同一份，不是复刻实现。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int port = args.Length > 0 ? int.Parse(args[0]) : 5680;
            string resourceRoot = args.Length > 1
                ? args[1]
                : @"D:\BaiduNetdiskDownload\20260915更新\20260915Client\";
            string transport = args.Length > 2 ? args[2] : "auto";
            string bindHost = args.Length > 3 ? args[3] : "127.0.0.1";

            // 必须在构造 HttpServer 之前设好：它的构造函数会读 Host
            Settings.HTTPIPAddress = "http://" + bindHost + ":" + port + "/";
            Settings.HttpTransport = transport;
            Settings.HTTPTrustedIPAddress = "127.0.0.1";
            Settings.EnableResourceService = true;
            Settings.ResourcePath = resourceRoot;
            Settings.ResourceAllowAnyIP = true;
            Settings.ResourceMaxConcurrent = 64;
            Settings.ResourceWriteTimeout = 300;
            Settings.ResourceBufferKB = 64;
            Settings.ResourceVerboseLog = false;

            var asm = typeof(Settings).Assembly;
            var type = asm.GetType("Server.Library.Utils.HttpServer", throwOnError: true);
            var server = Activator.CreateInstance(type);
            type.GetMethod("Start").Invoke(server, null);

            // 等监听结果确定，把真正生效的地址打出来（这一步就是为了验证地址协商逻辑）
            bool settled = (bool)type.GetMethod("WaitListening").Invoke(server, new object[] { 8000 });
            string active = (string)type.GetProperty("ActivePrefix").GetValue(server);
            string usedTransport = (string)type.GetProperty("Transport").GetValue(server);
            string report = (string)type.GetProperty("ListenReport").GetValue(server);

            Console.WriteLine("HttpBenchHost 已启动");
            Console.WriteLine("  配置地址 : " + Settings.HTTPIPAddress);
            Console.WriteLine("  通道模式 : " + transport);
            Console.WriteLine("  实际监听 : " + (string.IsNullOrEmpty(active) ? "(未监听!)" : active) + "  [" + usedTransport + "]");
            if (!string.IsNullOrEmpty(report)) Console.WriteLine("  失败说明 :" + Environment.NewLine + report);
            Console.WriteLine("  资源目录 : " + resourceRoot);
            Console.WriteLine("  并发上限 : " + Settings.ResourceMaxConcurrent);
            Console.WriteLine("输入 q 回车退出，或直接结束进程。");

            while (true)
            {
                var line = Console.ReadLine();
                if (line == null || line.Trim().ToLower() == "q") break;
            }

            return 0;
        }
    }
}
