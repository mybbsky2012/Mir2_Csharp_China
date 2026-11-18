using System.Text;
using Server;
using Server.MirEnvir;
using Server.MirDatabase;

namespace AccountProbe;

/// <summary>
/// 只读探针：核对存档里角色的持久化 Buffs（羽化重数），确认选人界面下发的 HumUpStage 是否正确。
/// 用法：工作目录必须是服务端目录（Server.MirADB 所在处）
///     AccountProbe.exe            列出所有角色 + buff 类型 + 推算的羽化重数
///     AccountProbe.exe 角色名      只看某个角色
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine($"BuffInfo {envir.BuffInfoList.Count} 种");

        //★LoadDB 不会灌 BuffInfoList（那是 StartEnvir 里单独做的），
        //  不手工灌一次，角色存档里的 Buff 反序列化就会抛 NotImplementedException。
        envir.BuffInfoList.Clear();
        foreach (var b in BuffInfo.Load()) envir.BuffInfoList.Add(b);

        Console.WriteLine($"BuffInfo(载入后) {envir.BuffInfoList.Count} 种");
        envir.LoadAccounts();
        Console.WriteLine($"账号 {envir.AccountList.Count} 个 / Envir.Time={envir.Time}");

        string filter = args.Length > 0 ? args[0] : null;

        foreach (var acc in envir.AccountList)
        {
            if (acc == null) continue;
            foreach (var ch in acc.Characters)
            {
                if (ch == null) continue;
                if (filter != null && ch.Name != filter) continue;

                Console.WriteLine($"--- [{ch.Index}] {ch.Name} Lv{ch.Level} {ch.Class}/{ch.Gender} 存档Buffs={ch.Buffs.Count}");

                int stage = 0, fallback = 0;
                for (int i = 0; i < ch.Buffs.Count; i++)
                {
                    Buff b = ch.Buffs[i];
                    if (b == null) continue;
                    int t = (int)b.Type;
                    string info = b.Info == null ? "Info=null" : b.Info.Name;
                    bool expired = b.ExpireTime > 0 && envir.Time > b.ExpireTime;
                    Console.WriteLine($"      type={t} ({b.Type}) {info} expire={b.ExpireTime} expired={expired}");

                    if (expired) continue;
                    if (t >= 232 && t <= 241) stage = t - 232 + 1;
                    if (t == 230 && fallback == 0) fallback = 1;
                }

                if (stage == 0) stage = fallback;
                Console.WriteLine($"      => 推算 HumUpStage = {stage}");
            }
        }

        return 0;
    }
}
