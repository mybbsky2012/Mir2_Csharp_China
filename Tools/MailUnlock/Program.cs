using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MailUnlock;

/// <summary>
/// 把「卡在客栈取件」的旧邮件解锁（Collected=false → true），让玩家能在邮件界面直接领取。
/// 默认只预览，不改库；加 --apply 才写回 Server.MirADB（写前自动备份）。
/// ★必须关服运行（服务端会锁 Server.MirADB，运行时写库会被覆盖）。
/// 用法：MailUnlock.exe "E:\...\Mir2Server" [--apply]
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        bool apply = args.Any(a => a.Equals("--apply", StringComparison.OrdinalIgnoreCase));
        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        if (!File.Exists(Envir.AccountPath)) { Console.WriteLine("找不到 " + Envir.AccountPath); return 2; }

        Settings.Load();
        Console.WriteLine($"[设置] MailAutoSendItems={Settings.MailAutoSendItems}  MailAutoSendGold={Settings.MailAutoSendGold}");

        Envir envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        // LoadAccounts 读角色时会解析 BUFF（如「英雄灵气」），必须先按服务端启动顺序加载 BuffSystem.ini
        envir.BuffInfoList.Clear();
        foreach (BuffInfo buff in BuffInfo.Load()) envir.BuffInfoList.Add(buff);

        envir.LoadAccounts();

        int accounts = 0, chars = 0, totalMail = 0, stuck = 0, touchedChars = 0;

        foreach (AccountInfo acc in envir.AccountList)
        {
            accounts++;
            foreach (CharacterInfo ch in acc.Characters)
            {
                chars++;
                int s = 0;
                foreach (MailInfo m in ch.Mail)
                {
                    totalMail++;
                    if (m.Parcel && !m.Collected) s++;
                }

                Console.WriteLine($"  [{acc.AccountID}] {ch.Name}：邮件 {ch.Mail.Count} 封" + (s > 0 ? $"，其中待取件 {s} 封" : ""));

                if (s == 0) continue;
                touchedChars++;
                stuck += s;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"账号 {accounts} 个，角色 {chars} 个，邮件共 {totalMail} 封");
        Console.WriteLine($"卡住（包裹已到但按钮是灰的）{stuck} 封，涉及 {touchedChars} 个角色");

        if (stuck == 0)
        {
            Console.WriteLine("没有需要解锁的邮件。");
            return 0;
        }

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("预览模式，未改动任何数据。确认无误后加 --apply 执行解锁（请先关服）。");
            return 0;
        }

        string backupDir = Path.Combine("Back Up", "auto-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + "-MailUnlock");
        Directory.CreateDirectory(backupDir);
        string backupFile = Path.Combine(backupDir, "Server.MirADB");
        File.Copy(Envir.AccountPath, backupFile, true);
        Console.WriteLine();
        Console.WriteLine($"[备份] {Path.GetFullPath(backupFile)}");

        int changed = 0;
        foreach (AccountInfo acc in envir.AccountList)
        {
            foreach (CharacterInfo ch in acc.Characters)
            {
                foreach (MailInfo m in ch.Mail)
                {
                    if (!m.Parcel || m.Collected) continue;
                    m.Collected = true;
                    changed++;
                }
            }
        }

        envir.SaveAccounts();
        Console.WriteLine($"[完成] 已解锁 {changed} 封邮件，已写回 {Path.GetFullPath(Envir.AccountPath)}");
        Console.WriteLine("玩家重新登录/刷新邮件后，「领取」按钮即可点击。");
        return 0;
    }
}
