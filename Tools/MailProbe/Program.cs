using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MailProbe;

/// <summary>
/// 只读验证邮件「领取」流程的标志位，不写数据库、不启动服务端。
/// 用法：MailProbe.exe "E:\...\Mir2Server"
/// 关键：MailInfo.Collected=false → 客户端「领取」按钮灰、服务端 CollectMail 拒绝（要求先去客栈取件）
///      MailInfo.Collected=true  → 客户端按钮可点，服务端 CollectMail 直接发放
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        string setupPath = Path.GetFullPath(Path.Combine("Configs", "Setup.ini"));
        Console.WriteLine($"[路径] {setupPath} 存在={File.Exists(setupPath)}");
        if (File.Exists(setupPath))
        {
            string[] lines = File.ReadAllLines(setupPath);
            Console.WriteLine("[原文] " + string.Join(" | ", lines.Where(l => l.Contains("AutoSend") || l.StartsWith("Gold=") || l.StartsWith("Items=")).Take(8)));
        }

        Settings.Load();
        Console.WriteLine($"[设置] MailAutoSendItems={Settings.MailAutoSendItems}  MailAutoSendGold={Settings.MailAutoSendGold}");
        Console.WriteLine($"[设置] MailCostPer1KGold={Settings.MailCostPer1KGold}  MailFreeWithStamp={Settings.MailFreeWithStamp}");

        Envir envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        CharacterInfo ch = envir.CharacterList.FirstOrDefault();
        if (ch == null)
        {
            // 没有角色数据（空库/未初始化）时造一个内存角色，只为跑通 Send() 分支，不写库
            ch = new CharacterInfo { Index = 990001, Name = "邮件探针", Class = MirClass.战士, Gender = MirGender.男性, Level = 1 };
            envir.CharacterList.Add(ch);
            Console.WriteLine("[角色] 库里没有角色，已用内存假角色验证");
        }
        Console.WriteLine($"[角色] {ch.Name}（Index={ch.Index}），现有邮件 {ch.Mail.Count} 封");
        Console.WriteLine();

        ItemInfo info = envir.ItemInfoList.FirstOrDefault(x => x.StackSize > 1) ?? envir.ItemInfoList.FirstOrDefault();
        if (info == null) { Console.WriteLine("物品库为空"); return 2; }

        int fail = 0;

        // 1) 只有物品（商城购买就是这种）
        MailInfo m1 = new MailInfo(ch.Index) { Sender = "游戏商城", Message = "商城购买", Items = new List<UserItem> { envir.CreateFreshItem(info) } };
        m1.Send();
        Report("[1] 只有物品（商城购买）", m1);

        // 2) 只有金币
        MailInfo m2 = new MailInfo(ch.Index) { Sender = "系统", Message = "金币", Gold = 1000 };
        m2.Send();
        Report("[2] 只有金币", m2);

        // 3) 物品 + 金币
        MailInfo m3 = new MailInfo(ch.Index) { Sender = "系统", Message = "物品+金币", Gold = 500, Items = new List<UserItem> { envir.CreateFreshItem(info) } };
        m3.Send();
        Report("[3] 物品+金币", m3);

        // 4) 纯文本（无附件）—— 不关心，只看不和上面的混淆
        MailInfo m4 = new MailInfo(ch.Index) { Sender = "GM", Message = "纯文本" };
        m4.Send();
        Console.WriteLine($"[4] 纯文本邮件 → Parcel={m4.Parcel} Collected={m4.Collected}");

        bool expectCollectable = Settings.MailAutoSendItems && Settings.MailAutoSendGold;
        bool all = m1.Collected && m2.Collected && m3.Collected;
        Console.WriteLine();
        if (expectCollectable)
        {
            Console.WriteLine(all ? "结论：配置生效——包裹邮件到手即可在邮件界面点「领取」✓" : "结论：配置未生效，仍有邮件是 Collected=false ✗");
            if (!all) fail++;
        }
        else
        {
            Console.WriteLine("结论：AutoSend 未全开 → 包裹邮件 Collected=false，客户端按钮灰，必须先到客栈 NPC 取件（本服目前没有该 NPC）");
        }

        Console.WriteLine(fail == 0 ? "检查通过" : $"失败 {fail} 项");
        return fail == 0 ? 0 : 1;
    }

    private static void Report(string label, MailInfo m)
    {
        Console.WriteLine($"{label} → Parcel={m.Parcel} Collected={m.Collected}  " +
                          (m.Collected ? "可在邮件里直接领取" : "按钮灰，需客栈取件"));
    }
}
