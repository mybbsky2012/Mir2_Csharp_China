using System;
using System.Windows.Forms;

namespace MapFishingEditor
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 自检模式：MapFishingEditor.exe --selftest <map> <log>
            if (args.Length >= 3 && args[0] == "--selftest")
            {
                SelfTest.Run(args[1], args[2]);
                return;
            }

            // 界面截图模式：--shot <map> <png>（窗口建在屏幕外，不影响桌面）
            if (args.Length >= 3 && args[0] == "--shot")
            {
                SelfTest.Shot(args[1], args[2]);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
        }
    }
}
