using System.Text;
using Server;
using Server.MirEnvir;
using Server.MirDatabase;

namespace PotionProbe;

/// <summary>
/// 只读探针（配方诊断）：完整复刻 StartEnvir 里的配方加载，
/// 报告 RecipePath、文件数、每个配方的解析结果。dotnet PotionProbe.dll
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Settings.Load();
        Console.WriteLine($"RecipePath = {Settings.RecipePath}");
        Console.WriteLine($"目录存在 = {Directory.Exists(Settings.RecipePath)}");

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        // ── 物品详情模式：PotionProbe.dll --item 关键词 ──────────────────
        if (args.Length > 1 && args[0] == "--item")
        {
            string kw = args[1];
            var hits = envir.ItemInfoList.Where(x => x != null &&
                (x.Name.Contains(kw) || x.Index.ToString() == kw)).ToList();
            Console.WriteLine($"=== 物品匹配「{kw}」共 {hits.Count} 条 ===");
            foreach (var it in hits)
            {
                Console.WriteLine($"--- [{it.Index}] {it.Name}");
                Console.WriteLine($"    Type={it.Type}({(int)it.Type}) Shape={it.Shape} Effect={it.Effect} Grade={it.Grade} Image={it.Image}");
                Console.WriteLine($"    Durability={it.Durability} StackSize={it.StackSize} Price={it.Price} Weight={it.Weight} RequiredAmount={it.RequiredAmount}");
                Console.WriteLine($"    Bind={it.Bind} ClassBased={it.ClassBased} LevelBased={it.LevelBased} Unique={it.Unique}({(short)it.Unique})");
                var sb = new System.Text.StringBuilder();
                for (int si = 0; si < 80; si++)
                {
                    int v = 0;
                    try { v = it.Stats[(Stat)si]; } catch { break; }
                    if (v != 0) sb.Append($"{(Stat)si}={v} ");
                }
                string st = sb.ToString().Trim();
                Console.WriteLine($"    Stats: {(string.IsNullOrEmpty(st) ? "(空)" : st)}");
            }
            return 0;
        }

        // ── 扫描模式：PotionProbe.dll --scan <Type名或数字> ──────────────
        if (args.Length > 1 && args[0] == "--scan")
        {
            string t = args[1];
            ItemType? want = null;
            foreach (ItemType v in Enum.GetValues(typeof(ItemType)))
                if (v.ToString() == t || ((int)v).ToString() == t) { want = v; break; }
            if (want == null) { Console.WriteLine($"未知类型: {t}"); return 2; }
            var list = envir.ItemInfoList.Where(x => x != null && x.Type == want.Value)
                                         .OrderBy(x => x.Shape).ThenBy(x => x.Index).ToList();
            Console.WriteLine($"=== Type={want} 共 {list.Count} 条（按 Shape 排序）===");
            foreach (var it in list)
                Console.WriteLine($"[{it.Index,5}] {it.Name,-18} Shape={it.Shape,-4} Durability={it.Durability,-6} " +
                                  $"Stack={it.StackSize,-3} Unique={(short)it.Unique,-4} Effect={it.Effect} Bind={it.Bind}");
            return 0;
        }

        string[] files;
        try { files = Directory.GetFiles(Settings.RecipePath, "*.txt"); }
        catch (Exception ex) { Console.WriteLine("!! GetFiles 失败: " + ex.Message); return 2; }

        Console.WriteLine($"配方文件 {files.Length} 个，数据库物品 {envir.ItemInfoList.Count(it => it != null)} 个\n");

        int ok = 0, bad = 0;
        foreach (var path in files)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string err = null;
            try
            {
                var r = new RecipeInfo(name);
                if (!r.IsValid)
                    err = "无效（本体物品缺失/创建失败）";
                else if (r.Ingredients.Count == 0)
                    err = "有效但 Ingredients=0（NPC 端会拒收：『缺少材料』）";
                else
                {
                    ok++;
                    Console.WriteLine($"[OK] {name}  成品x{r.Item.Count} 材料{r.Ingredients.Count}种 工具{r.Tools.Count}种 金币{r.Gold} 概率{r.Chance}%");
                    continue;
                }
            }
            catch (Exception ex) { err = "解析异常: " + ex.Message; }

            bad++;
            Console.WriteLine($"[FAIL] {name} —— {err}");
        }

        Console.WriteLine($"\n合计: 可用 {ok} / 异常 {bad} / 共 {files.Length}");
        Console.WriteLine("（服务端启动时同样流程：可用数会进入 RecipeInfoList，0 个 = 所有工艺窗口无配方可做）");
        return 0;
    }
}
