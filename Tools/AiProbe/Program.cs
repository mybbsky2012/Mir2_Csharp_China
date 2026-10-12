using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace AiProbe;

/// <summary>
/// 只读探针：统计本服数据库里怪物实际使用的 AI 编号 / 分布 / 数值，
/// 并通过 MonsterObject.GetMonster() 反查每个 AI 对应的服务端实现类。
/// 用法：把本程序放到服务端目录（与 Server.MirDB 同级）运行 AiProbe.exe
/// 输出：控制台汇总 + monsters.csv + ai_usage.csv
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        var list = envir.MonsterInfoList;
        Console.WriteLine($"怪物总数（MonsterInfoList）: {list.Count}");

        // AI -> 怪物
        var byAi = new SortedDictionary<int, List<MonsterInfo>>();
        foreach (var mi in list)
        {
            if (mi == null) continue;
            if (!byAi.TryGetValue(mi.AI, out var lst)) byAi[mi.AI] = lst = new List<MonsterInfo>();
            lst.Add(mi);
        }

        // 反查 AI -> 实现类名（构造一次即可；失败记异常类型）
        var aiClass = new Dictionary<int, string>();
        foreach (var ai in byAi.Keys)
        {
            aiClass[ai] = ProbeClass((ushort)ai);
        }

        // ---- monsters.csv ----
        var sb = new StringBuilder();
        sb.AppendLine("AI,实现类,Index,Name,GameName,Level,HP,MinDC,MaxDC,MinAC,MaxAC,AttackSpeed,MoveSpeed,ViewRange,CoolEye,Undead,AutoRev,Image,ImageName,CanTame,CanPush,SpawnScript,DieScript,DropPath");
        foreach (var mi in list)
        {
            if (mi == null) continue;
            string cls = aiClass.TryGetValue(mi.AI, out var c) ? c : "";
            sb.AppendLine(string.Join(",",
                mi.AI, Q(cls), mi.Index, Q(mi.Name), Q(mi.GameName), mi.Level,
                mi.Stats[Stat.HP], mi.Stats[Stat.MinDC], mi.Stats[Stat.MaxDC],
                mi.Stats[Stat.MinAC], mi.Stats[Stat.MaxAC],
                mi.AttackSpeed, mi.MoveSpeed, mi.ViewRange, mi.CoolEye,
                mi.Undead ? 1 : 0, mi.AutoRev ? 1 : 0,
                (int)mi.Image, mi.Image.ToString(),
                mi.CanTame ? 1 : 0, mi.CanPush ? 1 : 0,
                mi.HasSpawnScript ? 1 : 0, mi.HasDieScript ? 1 : 0, Q(mi.DropPath)));
        }
        File.WriteAllText("monsters.csv", sb.ToString(), new UTF8Encoding(false));

        // ---- ai_usage.csv ----
        var sb2 = new StringBuilder();
        sb2.AppendLine("AI,实现类,怪物数,示例怪物(最多8)");
        foreach (var kv in byAi)
        {
            string cls = aiClass.TryGetValue(kv.Key, out var c) ? c : "";
            string sample = string.Join(" / ", kv.Value.Take(8).Select(x => x.GameName));
            sb2.AppendLine($"{kv.Key},{Q(cls)},{kv.Value.Count},{Q(sample)}");
        }
        File.WriteAllText("ai_usage.csv", sb2.ToString(), new UTF8Encoding(false));

        // ---- 控制台 ----
        Console.WriteLine();
        Console.WriteLine($"=== 本服实际使用到的 AI 种类: {byAi.Count} / 工厂支持 268 种 ===");
        Console.WriteLine($"{"AI",-5} {"实现类",-28} {"怪物数",-6} 示例");
        foreach (var kv in byAi.OrderByDescending(x => x.Value.Count).ThenBy(x => x.Key))
        {
            string cls = aiClass.TryGetValue(kv.Key, out var c) ? c : "?";
            string sample = string.Join(" / ", kv.Value.Take(4).Select(x => x.GameName));
            if (kv.Value.Count > 4) sample += " …";
            Console.WriteLine($"{kv.Key,-5} {cls,-28} {kv.Value.Count,-6} {sample}");
        }

        Console.WriteLine();
        Console.WriteLine("已写出 monsters.csv / ai_usage.csv");
        return 0;
    }

    private static string ProbeClass(ushort ai)
    {
        try
        {
            var info = new MonsterInfo { AI = ai, Name = "probe", Index = -1, Stats = new Stats() };
            var obj = MonsterObject.GetMonster(info);
            if (obj == null) return "(null)";
            return obj.GetType().Name;
        }
        catch (Exception ex)
        {
            return "(ctor:" + ex.GetType().Name + ")";
        }
    }

    private static string Q(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
