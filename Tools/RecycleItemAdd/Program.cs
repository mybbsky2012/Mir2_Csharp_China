using System.Text;
using Server;
using Server.MirEnvir;
using Shared;

namespace RecycleItemAdd;

/// <summary>
/// 往 Server.MirDB 里补 1 个新物品：一键回收令（特殊消耗品，Shape = 80）。
///
/// 用法（会先把当前目录切到服务端目录）：
///     RecycleItemAdd.exe "E:\...\Mir2Server"            仅打印将要写入的内容，不写库
///     RecycleItemAdd.exe "E:\...\Mir2Server" --apply    写库（写前自动备份 Server.MirDB）
///
/// 必须关闭服务端再 --apply。
/// </summary>
internal static class Program
{
    private const short RecycleShape = 80;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 重定向时忽略 */ }

        bool apply = args.Any(a => a.Equals("--apply", StringComparison.OrdinalIgnoreCase));
        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(dir))
            dir = Directory.GetCurrentDirectory();

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

        // 先检查 Shape=80 是否已被其他特殊消耗品占用
        var clash = envir.ItemInfoList.Where(x => x.Type == ItemType.特殊消耗品 && x.Shape == RecycleShape).ToList();
        if (clash.Count > 0)
        {
            Console.WriteLine("!! 以下特殊消耗品已占用 Shape=80，会与回收令的脚本触发冲突：");
            foreach (var c in clash) Console.WriteLine($"   [{c.Index}] {c.Name}");
            return 1;
        }

        int oldCount = envir.ItemInfoList.Count;
        int maxIndex = oldCount > 0 ? envir.ItemInfoList.Max(x => x.Index) : 0;
        Console.WriteLine($"现有物品 {oldCount} 个，ItemIndex={envir.ItemIndex}，最大 Index={maxIndex}");
        Console.WriteLine();

        // 图标借用现有卷轴类物品
        ItemInfo iconSrc = envir.ItemInfoList.FirstOrDefault(x => x.Name == "破天的核心")
                           ?? envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.特殊消耗品 && x.Image > 0);
        ushort image = iconSrc != null ? iconSrc.Image : (ushort)0;
        Console.WriteLine($"图标借用: {(iconSrc != null ? iconSrc.Name + " (Image=" + image + ")" : "无，用 0")}");

        var def = new ItemInfo
        {
            Name = "一键回收令",
            Type = ItemType.特殊消耗品,
            Grade = ItemGrade.普通,
            RequiredType = RequiredType.Level,
            RequiredClass = RequiredClass.全职业,
            RequiredGender = RequiredGender.性别不限,
            Set = ItemSet.非套装,
            Shape = RecycleShape,               // 特殊消耗品 Shape=80 → 触发 [@_UseItem(80)]
            Weight = 0,
            Image = image,
            Price = 1000,
            StackSize = 100,                    // 可堆叠
            ToolTip = "双击打开一键回收菜单，按套装把背包里的装备换成金币（价格按物品原价）。",
        };

        var existing = new HashSet<string>(envir.ItemInfoList.Select(x => x.Name), StringComparer.Ordinal);

        if (existing.Contains(def.Name))
        {
            Console.WriteLine($"跳过（已存在）: {def.Name}，数据库未修改。");
            return 0;
        }

        def.Index = maxIndex + 1;
        Console.WriteLine($"待写入: [{def.Index}] {def.Name} 类型={def.Type} Shape={def.Shape} 图={def.Image} 堆叠={def.StackSize}");

        if (!apply)
        {
            Console.WriteLine("== 这是预览模式，数据库未被修改。加 --apply 才会写入 ==");
            return 0;
        }

        string bakDir = Path.Combine(Envir.BackUpPath, $"auto-{DateTime.Now:yyyyMMdd-HHmmss}-加一键回收令前");
        Directory.CreateDirectory(bakDir);
        string bak = Path.Combine(bakDir, "Server.MirDB");

        try { File.Copy(Envir.DatabasePath, bak, true); }
        catch (Exception ex)
        {
            Console.WriteLine($"!! 备份失败（服务端可能还开着），已中止：{ex.Message}");
            return 1;
        }
        Console.WriteLine($"已备份 -> {bak}");

        envir.ItemInfoList.Add(def);
        envir.ItemIndex = Math.Max(envir.ItemIndex, def.Index);
        envir.SaveDB();

        Console.WriteLine($"写库完成：物品 {oldCount} -> {envir.ItemInfoList.Count} 个，ItemIndex={envir.ItemIndex}");
        return 0;
    }
}
