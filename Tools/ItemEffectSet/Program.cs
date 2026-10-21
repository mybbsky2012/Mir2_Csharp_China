using System.Text;
using Server;
using Server.MirEnvir;
using Shared;

namespace ItemEffectSet;

/// <summary>
/// 把指定名字物品的 Effect 改成指定值（用于特殊消耗品 Effect=2 = 使用后不消失）。
///
/// 用法（会先把当前目录切到服务端目录）：
///     ItemEffectSet.exe "E:\...\Mir2Server" <物品名> <Effect>            仅打印
///     ItemEffectSet.exe "E:\...\Mir2Server" <物品名> <Effect> --apply    写库（写前自动备份）
///
/// 必须关闭服务端再 --apply。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 重定向时忽略 */ }

        bool apply = args.Any(a => a.Equals("--apply", StringComparison.OrdinalIgnoreCase));
        var rest = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();

        if (rest.Count < 2)
        {
            Console.WriteLine("用法: ItemEffectSet.exe <服务端目录> <物品名> <Effect值> [--apply]");
            return 2;
        }

        string dir = rest[0];
        string name = rest[1];
        if (!byte.TryParse(rest[2], out byte effect))
        {
            Console.WriteLine($"Effect 值无效: {rest[2]}");
            return 2;
        }

        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"目录不存在: {dir}");
            return 2;
        }

        dir = Path.GetFullPath(dir);
        Directory.SetCurrentDirectory(dir);

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"在 {dir} 下找不到 {Envir.DatabasePath}");
            return 2;
        }

        Console.WriteLine($"服务端目录 : {dir}");
        Console.WriteLine($"模式       : {(apply ? "写入 (--apply)" : "仅预览")}");
        Console.WriteLine();

        try { Settings.Load(); }
        catch (Exception ex) { Console.WriteLine($"!! 配置加载失败（继续）：{ex.Message}"); }

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败，放弃。");
            return 1;
        }

        var hits = envir.ItemInfoList.Where(x => x.Name == name).ToList();
        if (hits.Count == 0)
        {
            Console.WriteLine($"!! 找不到物品「{name}」");
            return 1;
        }

        foreach (var it in hits)
        {
            Console.WriteLine($"[{it.Index}] {it.Name}  类型={it.Type}  Shape={it.Shape}  Effect: {it.Effect} -> {effect}");
        }

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("== 这是预览模式，数据库未被修改。加 --apply 才会写入 ==");
            return 0;
        }

        // 写前自动备份
        string bakDir = Path.Combine("Back Up", "auto-" + DateTime.Now.ToString("yyyyMMdd_HHmm") + "-ItemEffectSet");
        Directory.CreateDirectory(bakDir);
        string bak = Path.Combine(bakDir, "Server.MirDB");

        try { File.Copy(Envir.DatabasePath, bak, true); }
        catch (Exception ex)
        {
            Console.WriteLine($"!! 备份失败（服务端可能还开着），已中止：{ex.Message}");
            return 1;
        }
        Console.WriteLine($"已备份 -> {bak}");

        foreach (var it in hits)
        {
            it.Effect = effect;
        }

        envir.SaveDB();
        Console.WriteLine("写库完成。");
        return 0;
    }
}
