using log4net;
using System.Reflection;

namespace Server.MirForms
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Packet.IsServer = true;

            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            log4net.Config.XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            InstallGlobalCrashGuards();

            try
            {
                Settings.Load();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SMain());

                Settings.Save();
            }
            catch(Exception ex)
            {
                Logger.GetLogger().Error(ex);
            }
        }

        /// <summary>
        /// 全局崩溃兜底（10-30）：.NET 8 WinForms 下，UI 事件处理器里任何未捕获异常
        /// 都会直接终止进程（闪退）。这里三层拦截：
        ///   1. Application.ThreadException —— UI 事件（菜单/定时器/按钮）异常：记崩溃日志+弹框，进程保活；
        ///   2. AppDomain.UnhandledException —— 后台线程（重启线程/网络回调等）异常：记崩溃日志，
        ///      注意此回调之后进程必然退出，只能留现场无法救活；
        ///   3. SetUnhandledExceptionMode(CatchException) —— 确保第 1 条生效（默认即如此，显式声明防改动）。
        /// 崩溃日志独立落盘（不依赖 log4net 配置），文件在 CrashLogs\ 下按时间命名。
        /// </summary>
        static void InstallGlobalCrashGuards()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                WriteCrashLog("UI线程异常（进程保活）", e.Exception);
                try
                {
                    MessageBox.Show("发生错误（已记录到 CrashLogs\\，服务器继续运行）：\r\n\r\n" + e.Exception,
                        "服务器错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                WriteCrashLog("未处理异常（进程即将退出）", e.ExceptionObject as Exception);
        }

        static void WriteCrashLog(string title, Exception ex)
        {
            string text = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}{3}{4}",
                DateTime.Now, title, Environment.NewLine, ex, Environment.NewLine);
            try
            {
                Logger.GetLogger().Error(title + Environment.NewLine + ex);
            }
            catch { }
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashLogs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir,
                    "crash-" + DateTime.Now.ToString("yyyyMMdd") + ".log"), text);
            }
            catch { }
        }
    }
}
