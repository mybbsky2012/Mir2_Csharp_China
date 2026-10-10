using System.Text;

namespace Mir2MicroServer
{
    /// <summary>
    /// 极简日志：控制台 + 可选文件。
    ///
    /// 原来微端内嵌在 Server.exe 里时，日志走 MessageQueue（服务端会把消息广播到
    /// 管理端的消息队列）。独立进程没有那套东西，所以换成本地实现：
    /// 控制台实时输出，同时可选落一份 UTF-8 文本，方便运维事后翻。
    /// </summary>
    internal static class Log
    {
        private static readonly object _lock = new object();
        private static string _filePath;
        private static bool _fileFailed;

        /// <summary>是否把 INFO 级别的明细也打出来（对应 ResourceVerboseLog）</summary>
        public static bool Verbose;

        /// <summary>初始化日志文件。path 为空则不落文件。</summary>
        public static void Init(string path)
        {
            lock (_lock)
            {
                _filePath = string.IsNullOrWhiteSpace(path) ? null : path.Trim().Trim('"');
                _fileFailed = false;

                if (_filePath == null) return;

                try
                {
                    // 相对路径同样以 exe 所在目录为基准，和资源目录保持一致的心智模型
                    _filePath = MicroConfig.ResolveFromExeDir(_filePath, @".\Logs\MicroServer.log");

                    var dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
                catch
                {
                    // 目录建不出来也不致命，写的时候会再失败一次并降级为仅控制台
                }
            }
        }

        public static void Info(string message)
        {
            Write("信息", message);
        }

        /// <summary>明细日志：只有开了 Verbose 才输出（逐条请求这种高频内容走这里）</summary>
        public static void Detail(string message)
        {
            if (!Verbose) return;
            Write("明细", message);
        }

        public static void Warn(string message)
        {
            Write("警告", message);
        }

        public static void Error(string message)
        {
            Write("错误", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("错误", message + " | " + ex.GetType().Name + ": " + ex.Message);
        }

        private static void Write(string level, string message)
        {
            string line = string.Format("[{0:HH:mm:ss}] [{1}] {2}", DateTime.Now, level, message);

            lock (_lock)
            {
                Console.WriteLine(line);

                if (_filePath == null || _fileFailed) return;

                try
                {
                    File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // 磁盘满 / 文件被占用：只提示一次，之后不再尝试，避免刷屏
                    _fileFailed = true;
                    Console.WriteLine("[警告] 日志文件写入失败，已降级为仅控制台输出。");
                }
            }
        }
    }
}
