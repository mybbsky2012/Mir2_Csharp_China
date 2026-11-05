using System.Drawing;
using System.Windows.Forms;

namespace UiShot
{
    /// <summary>
    /// 把服务端的「服务器设置」窗体真跑起来截一张图，用来肉眼核对布局。
    /// 用法：UiShot.exe &lt;输出 png&gt; [标签页索引]
    /// 注意：只截图，不点关闭 —— 窗体关闭会触发 Settings.Save()，那会改到真配置。
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string output = args.Length > 0 ? args[0] : "shot.png";
            int pageIndex = args.Length > 1 ? int.Parse(args[1]) : -1;

            var form = new Server.ConfigForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(40, 40);
            form.TopMost = true;
            form.Show();

            Application.DoEvents();

            TabControl tabs = null;
            foreach (Control c in form.Controls)
            {
                if (c is TabControl tc) { tabs = tc; break; }
            }

            if (tabs != null)
            {
                int want = pageIndex < 0 ? tabs.TabCount - 1 : pageIndex;
                if (want >= 0 && want < tabs.TabCount) tabs.SelectedIndex = want;
                Application.DoEvents();

                Console.WriteLine("标签页数量: " + tabs.TabCount);
                for (int i = 0; i < tabs.TabCount; i++)
                {
                    Console.WriteLine("  [" + i + "] " + tabs.TabPages[i].Text
                        + "  size=" + tabs.TabPages[i].Width + "x" + tabs.TabPages[i].Height);
                }
            }

            // initest：在临时目录里跑一遍 Settings 保存→重新加载，
            // 验证三个键真的写进 Setup.ini、也能被 Load 读回来（只在临时目录，不碰真配置）。
            if (args.Length > 2 && args[2] == "initest")
            {
                Console.WriteLine("cwd = " + Directory.GetCurrentDirectory());

                Server.Settings.TameSuccessRate = 100;
                Server.Settings.TameIgnoreLevelLimit = true;
                Server.Settings.TameIgnoreHpPenalty = true;
                Server.Settings.TameIgnoreUntameable = true;
                Server.Settings.Save();

                string ini = Path.Combine(".", "Configs", "Setup.ini");
                foreach (string line in File.ReadAllLines(ini))
                {
                    if (line.Contains("Tame")) Console.WriteLine("  ini> " + line.Trim());
                }

                Server.Settings.TameSuccessRate = 0;
                Server.Settings.TameIgnoreLevelLimit = false;
                Server.Settings.TameIgnoreHpPenalty = false;
                Server.Settings.TameIgnoreUntameable = false;
                Server.Settings.Load();

                Console.WriteLine("reload TameSuccessRate     = " + Server.Settings.TameSuccessRate);
                Console.WriteLine("reload TameIgnoreLevelLimit = " + Server.Settings.TameIgnoreLevelLimit);
                Console.WriteLine("reload TameIgnoreHpPenalty  = " + Server.Settings.TameIgnoreHpPenalty);
                Console.WriteLine("reload TameIgnoreUntameable = " + Server.Settings.TameIgnoreUntameable);

                bool pass = Server.Settings.TameSuccessRate == 100
                         && Server.Settings.TameIgnoreLevelLimit
                         && Server.Settings.TameIgnoreHpPenalty
                         && Server.Settings.TameIgnoreUntameable;
                Console.WriteLine(pass ? "INITEST PASS" : "INITEST FAIL");
                Environment.Exit(pass ? 0 : 2);
            }

            // 让布局与绘制稳定下来再截
            for (int i = 0; i < 12; i++)
            {
                Application.DoEvents();
                Thread.Sleep(40);
            }

            // 可选：把成功率改成指定值，用来核对 0 / 中间值 / 100 三档的提示文案
            if (args.Length > 2 && args[2] != "savetest")
            {
                int rate = int.Parse(args[2]);
                foreach (NumericUpDown nud in FindAll<NumericUpDown>(form))
                {
                    nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, rate));
                    Console.WriteLine("NumericUpDown -> " + nud.Value);
                }
                for (int i = 0; i < 8; i++)
                {
                    Application.DoEvents();
                    Thread.Sleep(40);
                }
            }

            // savetest：模拟用户改完点「关闭」，验证 界面 → Settings 这一环真的写进去了。
            // 只在内存里改，不落盘（Settings.Save() 才会写 ini，这里不调）。
            if (args.Length > 2 && args[2] == "savetest")
            {
                foreach (NumericUpDown nud in FindAll<NumericUpDown>(form)) nud.Value = 87;
                foreach (CheckBox cb in FindAll<CheckBox>(form))
                {
                    if (cb.Text.StartsWith("允许召唤")) cb.Checked = true;
                }

                form.Save();

                Console.WriteLine("Settings.TameSuccessRate     = " + Server.Settings.TameSuccessRate);
                Console.WriteLine("Settings.TameIgnoreLevelLimit = " + Server.Settings.TameIgnoreLevelLimit);
                Console.WriteLine("Settings.TameIgnoreHpPenalty  = " + Server.Settings.TameIgnoreHpPenalty);
                Console.WriteLine("Settings.TameIgnoreUntameable = " + Server.Settings.TameIgnoreUntameable);

                bool pass = Server.Settings.TameSuccessRate == 87
                         && Server.Settings.TameIgnoreLevelLimit
                         && Server.Settings.TameIgnoreHpPenalty
                         && Server.Settings.TameIgnoreUntameable;
                Console.WriteLine(pass ? "SAVETEST PASS" : "SAVETEST FAIL");

                Environment.Exit(pass ? 0 : 2);
            }

            var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            bmp.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            bmp.Dispose();

            Console.WriteLine("saved: " + output);

            // 绝不走 Close()：那会触发 Settings.Save()
            Environment.Exit(0);
        }

        private static IEnumerable<T> FindAll<T>(Control root) where T : Control
        {
            foreach (Control c in root.Controls)
            {
                if (c is T t) yield return t;
                foreach (T sub in FindAll<T>(c)) yield return sub;
            }
        }
    }
}
