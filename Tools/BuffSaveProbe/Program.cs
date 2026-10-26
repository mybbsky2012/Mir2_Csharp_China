using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace BuffSaveProbe;

/// <summary>
/// 只读探针：统计 Server.MirDB 里玩家角色与英雄各自存了多少条 BUFF。
/// 用来验证「退出游戏后 BUFF 消失」到底是没存盘，还是存了没恢复。
/// 用法：BuffSaveProbe.exe "E:\...\Mir2Server" [关键词]
/// 只读取，不写库。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        string keyword = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine("找不到 " + Envir.DatabasePath);
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        // 角色/英雄都在账号库里（Server.MirADB），LoadDB 只管物品/怪物/地图
        // 读 Buff 构造时要查 Envir.GetBuffInfo，必须先把 BuffInfoList 填好
        // （LoadDB 不填这张表；不填会抛 NotImplementedException「XX 尚未实施」）
        envir.BuffInfoList.Clear();
        foreach (BuffInfo info in BuffInfo.Load())
            envir.BuffInfoList.Add(info);

        envir.LoadAccounts();

        Console.WriteLine("=== 玩家角色（CharacterInfo.Buffs）===");
        int pcTotal = 0, pcWithBuff = 0;

        foreach (CharacterInfo ch in envir.CharacterList)
        {
            if (ch == null) continue;
            pcTotal++;

            int n = ch.Buffs?.Count ?? 0;
            if (n > 0) pcWithBuff++;

            if (keyword != null && !ch.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

            Console.WriteLine($"[{ch.Index}] {ch.Name} Lv{ch.Level}  BUFF={n}");
            if (ch.Buffs == null) continue;
            foreach (Buff b in ch.Buffs)
                Console.WriteLine($"       · {b.Type}  剩余={b.ExpireTime} ms  ObjectID={b.ObjectID}  Stats={Describe(b)}");
        }

        Console.WriteLine($"玩家合计：{pcTotal} 个角色，其中 {pcWithBuff} 个带 BUFF");

        Console.WriteLine();
        Console.WriteLine("=== 英雄（HeroInfo.Buffs）===");
        int heroTotal = 0, heroWithBuff = 0;

        foreach (HeroInfo h in envir.HeroList)
        {
            if (h == null) continue;
            heroTotal++;

            int n = h.Buffs?.Count ?? 0;
            if (n > 0) heroWithBuff++;

            if (keyword != null && !h.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

            Console.WriteLine($"[{h.Index}] {h.Name} Lv{h.Level}  BUFF={n}");
            if (h.Buffs == null) continue;
            foreach (Buff b in h.Buffs)
                Console.WriteLine($"       · {b.Type}  剩余={b.ExpireTime} ms");
        }

        Console.WriteLine($"英雄合计：{heroTotal} 个，其中 {heroWithBuff} 个带 BUFF");

        Console.WriteLine();
        Console.WriteLine("=== 结论提示 ===");
        Console.WriteLine("英雄 BUFF 若全为 0，而玩家有值 → 英雄的 BUFF 根本没进库（HeroInfo.Save/Load 不含 Buffs）");
        return 0;
    }

    private static string Describe(Buff b)
    {
        if (b.Stats == null) return "null";
        var parts = new List<string>();
        foreach (var kv in b.Stats.Values)
            parts.Add($"{kv.Key}={kv.Value}");
        return string.Join(",", parts);
    }
}
