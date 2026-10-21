using System.Drawing;
using System.Windows.Forms;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace SendAwardUiTest;

/// <summary>
/// 只读验证：加载真实数据库 → 打开「发送邮件奖励」窗体 → 截图（不发任何邮件、不写库）。
/// 用法：SendAwardUiTest.exe "E:\...\Mir2Server" "D:\...\out.png"
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        string outPath = args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "sendaward_ui.png");

        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(dir);

        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        // 模拟服务端运行实例：LoadDB + LoadAccounts（与 Envir.StartEnvir/StartNetwork 相同的数据路径）
        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        // LoadAccounts 读角色时会解析 BUFF（如「英雄灵气」），必须先按服务端启动顺序加载 BuffSystem.ini
        foreach (BuffInfo buff in BuffInfo.Load()) envir.BuffInfoList.Add(buff);
        envir.LoadAccounts();

        Console.WriteLine($"[数据] 物品 {envir.ItemInfoList.Count} 件、角色 {envir.CharacterList.Count} 个");

        Application.EnableVisualStyles();
        using var form = new SendAwardForm();
        form.Show();
        Application.DoEvents();
        Thread.Sleep(400);
        Application.DoEvents();

        using var bmp = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);

        // 再切到「坐骑」分类截一张，验证按类型筛选正常
        string second = Path.ChangeExtension(outPath, ".mount.png");
        foreach (Control c in form.Controls)
        {
            if (c is ComboBox cb && c.Name == "cmbCategory")
            {
                int idx = cb.Items.IndexOf(ItemType.坐骑);
                if (idx >= 0)
                {
                    cb.SelectedIndex = idx;
                    Application.DoEvents();
                    Thread.Sleep(200);
                    Application.DoEvents();
                    using var bmp2 = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bmp2, new Rectangle(0, 0, form.Width, form.Height));
                    bmp2.Save(second, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        form.Close();

        Console.WriteLine($"[截图] {outPath}  ({form.Width}x{form.Height}) + {second}");
        return 0;
    }
}
