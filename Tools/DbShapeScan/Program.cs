using System.Text;
using Server;
using Server.MirEnvir;

namespace DbShapeScan;

/// <summary>
/// 只读工具：扫描 Server.MirDB 里所有物品的 Shape 分布，用来评估「改羽化编码」会不会踩到原有装备。
///
/// 关注三件事：
///   1. 全库 Shape 的取值范围 —— ItemInfo.Shape 是 short，上界 32767；
///   2. 有多少「非羽化」物品的 Shape 落进 1001~1599 / 2001~2599
///      （落进去就会被客户端 IsHumUpEquipShape 当成羽化装备，外观全错）；
///   3. 现有羽化装备（登仙· 前缀）的 Shape 清单 —— 改编码时要逐个迁移的名单。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处）：
///     DbShapeScan.exe
/// 本工具只读，不写回数据库。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出重定向时可能失败 */ }

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

        var list = envir.ItemInfoList;
        Console.WriteLine($"物品总数 {list.Count}");
        Console.WriteLine(new string('-', 78));

        // ---- 1) 全库范围 ----
        short min = short.MaxValue, max = short.MinValue;
        int negative = 0;
        foreach (var it in list)
        {
            if (it.Shape < min) min = it.Shape;
            if (it.Shape > max) max = it.Shape;
            if (it.Shape < 0) negative++;
        }
        Console.WriteLine($"Shape 范围 {min} ~ {max}（short 上界 32767，负数 {negative} 个）");

        // ---- 2) 按装备类型看分布 ----
        Console.WriteLine();
        Console.WriteLine("按类型统计（只列衣服/武器/坐骑/翅膀等与 Shape 有关的）：");
        var byType = list.GroupBy(x => x.Type)
                         .OrderByDescending(g => g.Count())
                         .ToList();
        foreach (var g in byType)
        {
            var shapes = g.Select(x => x.Shape).ToList();
            Console.WriteLine($"  {g.Key,-12} {g.Count(),5} 件   Shape {shapes.Min()} ~ {shapes.Max()}");
        }

        // ---- 3) 落进羽化编码区间的「非登仙」物品 ----
        Console.WriteLine();
        Console.WriteLine("羽化编码区间占用检查（这些落进去 = 被误判成羽化装备）：");
        ScanZone(list, "盔甲段 1001~1599（100 套编码）", 1001, 1599);
        ScanZone(list, "武器段 2001~2599（100 套编码）", 2001, 2599);
        Console.WriteLine();
        ScanZone(list, "现行盔甲段 1001~1059（10 套编码）", 1001, 1059);
        ScanZone(list, "现行武器段 2001~2059（10 套编码）", 2001, 2059);

        // ---- 4) 现有羽化装备清单（迁移名单） ----
        Console.WriteLine();
        Console.WriteLine("现有羽化装备（名称含「登仙」或 Shape 落在现行羽化区间）：");
        var hum = list.Where(x => (x.Shape > 1000 && x.Shape <= 1059) || (x.Shape > 2000 && x.Shape <= 2059))
                      .OrderBy(x => x.Shape)
                      .ToList();
        if (hum.Count == 0)
        {
            Console.WriteLine("  （无）");
        }
        else
        {
            Console.WriteLine($"  {"Index",-6}{"Shape",-7}{"Image",-7}{"Type",-10}Name");
            foreach (var it in hum)
                Console.WriteLine($"  {it.Index,-6}{it.Shape,-7}{it.Image,-7}{it.Type,-10}{it.Name}");
        }

        // ---- 4b) 登仙装备属性详情（推断属性成长规律用） ----
        Console.WriteLine();
        Console.WriteLine("登仙装备属性详情：");
        foreach (var it in hum)
        {
            var st = string.Join(", ", it.Stats.Values.Where(p => p.Value != 0).Select(p => $"{p.Key}={p.Value}"));
            Console.WriteLine($"  [{it.Index}] {it.Name,-16} Shape={it.Shape,-5} 类型={it.Type} 品质={it.Grade} " +
                              $"职业={it.RequiredClass} 性别={it.RequiredGender} 需求={it.RequiredType}{it.RequiredAmount} " +
                              $"耐久={it.Durability} 重量={it.Weight} 价格={it.Price}");
            if (st.Length > 0) Console.WriteLine($"        属性: {st}");
            if (!string.IsNullOrEmpty(it.ToolTip)) Console.WriteLine($"        提示: {it.ToolTip}");
        }

        // ---- 5) 100 套编码迁移预演 ----
        Console.WriteLine();
        Console.WriteLine("按「1000 + 职业*100 + 档位」换算后的新 Shape（旧 → 新）：");
        if (hum.Count == 0)
        {
            Console.WriteLine("  （无需要迁移的条目）");
        }
        else
        {
            Console.WriteLine($"  {"Shape",-7}{"→",-3}{"新 Shape",-9}{"职业",-6}{"档位",-5}Name");
            foreach (var it in hum)
            {
                int baseVal = it.Shape >= 2000 ? 2000 : 1000;
                int idx = it.Shape - baseVal;
                int cls = idx / 10;
                int tier = idx % 10;
                int newShape = baseVal + cls * 100 + tier;
                Console.WriteLine($"  {it.Shape,-7}{"→",-3}{newShape,-9}{cls,-6}{tier,-5}{it.Name}");
            }
        }

        // ---- 6) 顶级普通武器基准（给羽化武器定数值参考） ----
        Console.WriteLine();
        Console.WriteLine("60 级以上普通武器（按等级降序，前 30 件）：");
        var topW = list.Where(x => x.Type == ItemType.武器 && x.RequiredType == RequiredType.Level && x.RequiredAmount >= 50)
                       .OrderByDescending(x => x.RequiredAmount).Take(30).ToList();
        foreach (var it in topW)
        {
            var st = string.Join(", ", it.Stats.Values.Where(p => p.Value != 0).Select(p => $"{p.Key}={p.Value}"));
            Console.WriteLine($"  L{it.RequiredAmount,3} {it.RequiredClass,-8}{it.Name,-20} Shape={it.Shape,-5} 耐久={it.Durability} 重量={it.Weight} {st}");
        }

        Console.WriteLine();
        Console.WriteLine("完成（未写入任何数据）。");
        return 0;
    }

    private static void ScanZone(List<ItemInfo> list, string label, int lo, int hi)
    {
        var hits = list.Where(x => x.Shape >= lo && x.Shape <= hi).ToList();
        Console.WriteLine($"  {label,-34} 占用 {hits.Count,3} 件");
        foreach (var it in hits.Take(15))
            Console.WriteLine($"        - {it.Index,5}  {it.Shape,6}  {it.Type,-10}{it.Name}");
        if (hits.Count > 15) Console.WriteLine($"        ... 其余 {hits.Count - 15} 件省略");
    }
}
