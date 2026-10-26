using System.Text;

namespace Mir2DbUpgrade;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // 子进程：只加载/存盘，不弹界面
        if (args.Length > 0 && args[0] == "--worker")
            return Worker.Run(args);

        // 无界面一键升级（可写进批处理）：--auto <服务端目录>
        if (args.Length >= 2 && args[0] == "--auto")
            return AutoUpgrade(args[1]);

        // 无界面预检：--check <服务端目录>
        if (args.Length >= 2 && args[0] == "--check")
            return AutoCheck(args[1]);

        string initialDir = null;
        if (args.Length >= 2 && args[0] == "--dir") initialDir = args[1];

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(initialDir));
        return 0;
    }

    private static int AutoUpgrade(string dir)
    {
        // WinExe 下 Console.OutputEncoding 经常不生效（stdout 被重定向时按控制台代码页走），
        // 直接用 UTF-8 包一层标准输出，保证重定向到文件/管道时不乱码。
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };

        var sb = new StringBuilder();
        void Log(string msg, LogLevel lv)
        {
            string prefix = lv switch
            {
                LogLevel.Error => "[错误] ",
                LogLevel.Warn => "[警告] ",
                LogLevel.Good => "[成功] ",
                _ => "",
            };
            string line = prefix + msg;
            stdout.WriteLine(line);
            sb.AppendLine(line);
        }

        dir = Path.GetFullPath(dir);
        Log($"服务端目录: {dir}", LogLevel.Info);

        string proc = UpgradeRunner.DetectServerProcess();
        if (proc != null)
        {
            Log($"检测到服务端进程「{proc}」正在运行，已中止。请先关闭服务端。", LogLevel.Error);
            return 2;
        }

        var r = UpgradeRunner.Run(dir, Log);
        Log(r.Success ? $"完成：{r.Message}（{r.Seconds:F1} 秒）" : $"失败：{r.Message}", r.Success ? LogLevel.Good : LogLevel.Error);
        if (r.BackupDir != null) Log($"备份：{r.BackupDir}", LogLevel.Muted);

        try
        {
            string outLog = Path.Combine(UpgradeRunner.TempDir, "auto_upgrade.log");
            File.WriteAllText(outLog, sb.ToString(), new UTF8Encoding(false));
            stdout.WriteLine($"日志：{outLog}");
        }
        catch { }

        return r.Success ? 0 : 1;
    }

    private static int AutoCheck(string dir)
    {
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };

        dir = Path.GetFullPath(dir);
        stdout.WriteLine($"服务端目录: {dir}");

        var r = UpgradeRunner.Precheck(dir, (msg, lv) => stdout.WriteLine(msg));

        stdout.WriteLine(r.Success ? "预检通过。" : "预检失败：" + r.Message);
        return r.Success ? 0 : 1;
    }
}
