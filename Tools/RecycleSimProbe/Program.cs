using System.Text;
using Server;
using Server.MirEnvir;
using Shared;

namespace RecycleSimProbe;

/// <summary>
/// 只读模拟：加载真实 Server.MirDB（绝不 SaveDB），统计每个套装的
/// 可回收件数与原价合计，并验证脚本里用的 35 个套装名都能被
/// RecycleManager 同款的 Enum.TryParse 解析成 ItemSet。
/// 用法: RecycleSimProbe.exe "E:\...\Mir2Server"
/// </summary>
internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "[通过] " : "[失败] ") + name);
        if (ok) _pass++; else _fail++;
    }

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        try { Settings.Load(); }
        catch (Exception ex) { Console.WriteLine($"!! 配置加载失败（继续）：{ex.Message}"); }

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 1;
        }

        Console.WriteLine($"已加载 {envir.ItemInfoList.Count} 件物品（只读，不回写）\n");

        // ---- 脚本里实际用到的 35 个套装名 ----
        string[] scriptSets =
        {
            "白骨套装","世轮套装","绿翠套装","道护套装","祈祷套装","赤兰套装","密火套装","破碎套装","幻魔石套",
            "灵玉套装","五玄套装","红玉套装","强红玉套","软玉套装","强软玉套","白金套装","强白金套","青玉套装",
            "强青玉套","虫血套装","记忆套装","鏃未套装","龙血套装","监视套装","暴压套装","贝玉套装","黑术套装",
            "贵人战套","贵人法套","贵人道套","贵人刺套","贵人弓套","天龙套装","圣龙套装","神龙套装"
        };

        // 1) 每个套装名必须能解析成 ItemSet（与 RecycleManager 的解析方式一致）
        var parsed = new Dictionary<string, ItemSet>();
        foreach (string name in scriptSets)
        {
            if (Enum.TryParse(name, true, out ItemSet set) && Enum.IsDefined(typeof(ItemSet), set) && set != ItemSet.非套装)
                parsed[name] = set;
            else
                Check($"套装名可解析: {name}", false);
        }
        Check($"35 个套装名全部可被 RECYCLE 解析（实际 {parsed.Count}）", parsed.Count == scriptSets.Length);

        // 2) 库里每个套装的可回收件数与原价合计（排除不可出售绑定）
        Console.WriteLine();
        Console.WriteLine("套装          库存件数   可回收件数   原价合计(单件)");
        Console.WriteLine("----------------------------------------------");
        int totalRecyclable = 0;

        foreach (var kv in parsed.OrderBy(x => (int)x.Value))
        {
            var all = envir.ItemInfoList.Where(x => x.Set == kv.Value).ToList();
            int canSell = all.Count(x => !x.Bind.HasFlag(BindMode.DontSell));
            ulong priceSum = (ulong)all.Where(x => !x.Bind.HasFlag(BindMode.DontSell)).Sum(x => (decimal)x.Price);
            totalRecyclable += canSell;
            Console.WriteLine($"{kv.Key,-12} {all.Count,7} {canSell,10} {priceSum,14:N0}");
        }

        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"可回收套装共 {parsed.Count} 类，可回收物品 {totalRecyclable} 件");
        Console.WriteLine();

        // 3) 一键回收令物品存在且 Shape=80
        var token = envir.ItemInfoList.FirstOrDefault(x => x.Name == "一键回收令");
        Check("数据库里有「一键回收令」且 Shape=80、类型=特殊消耗品",
              token != null && token.Shape == 80 && token.Type == ItemType.特殊消耗品);
        if (token != null)
            Console.WriteLine($"  -> [{token.Index}] 一键回收令 Shape={token.Shape} 堆叠={token.StackSize} 图={token.Image}");

        // 4) Shape=80 没有被第二个特殊消耗品占用
        int clash = envir.ItemInfoList.Count(x => x.Type == ItemType.特殊消耗品 && x.Shape == 80);
        Check("特殊消耗品 Shape=80 只有回收令一个", clash == 1);

        Console.WriteLine();
        Console.WriteLine($"结果: {_pass} 通过 / {_fail} 失败");
        return _fail == 0 ? 0 : 1;
    }
}
