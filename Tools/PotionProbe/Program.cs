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
