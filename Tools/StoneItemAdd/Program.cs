using System.Text;
using Server;
using Server.MirEnvir;
using Shared;

namespace StoneItemAdd;

/// <summary>
/// 往 Server.MirDB 里补 4 个新物品：气血石 / 魔血石 / 回城石 / 随机石。
///
/// 用法（会先把当前目录切到服务端目录，Setup.ini / Server.MirDB 都在那里）：
///     StoneItemAdd.exe "E:\...\Mir2Server"            仅打印将要写入的内容，不写库
///     StoneItemAdd.exe "E:\...\Mir2Server" --apply    写库（写前自动备份 Server.MirDB）
///
/// 必须关闭服务端再 --apply，否则写库会被服务端进程占用，且内存里的旧库会覆盖回去。
/// 已存在的同名物品会被跳过，重复执行不会产生重复物品。
/// </summary>
internal static class Program
{
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

        try
        {
            Settings.Load();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"!! 配置加载失败（继续）：{ex.Message}");
        }

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败，放弃。");
            return 1;
        }

        int oldCount = envir.ItemInfoList.Count;
        int maxIndex = oldCount > 0 ? envir.ItemInfoList.Max(x => x.Index) : 0;
        Console.WriteLine($"现有物品 {oldCount} 个，ItemIndex={envir.ItemIndex}，最大 Index={maxIndex}");
        Console.WriteLine();

        ushort ImageOf(string name, ushort fallback)
        {
            ItemInfo src = envir.ItemInfoList.FirstOrDefault(x => x.Name == name);
            if (src == null)
            {
                Console.WriteLine($"  (提示) 库里没有「{name}」，图标回退为 {fallback}");
                return fallback;
            }
            return src.Image;
        }

        // 图标直接借用现有物品，省得自己猜 Items.Lib 的索引
        ushort hpImage = ImageOf("体力恢复药(大)", 0);
        ushort mpImage = ImageOf("魔力恢复药(大)", 0);
        ushort townImage = ImageOf("回城卷", 0);
        ushort randomImage = ImageOf("随机传送卷", 0);

        var defs = new List<ItemInfo>
        {
            new ItemInfo
            {
                Name = "气血石",
                Type = ItemType.气血石,
                Grade = ItemGrade.普通,
                RequiredType = RequiredType.Level,
                RequiredClass = RequiredClass.全职业,
                RequiredGender = RequiredGender.性别不限,
                Set = ItemSet.非套装,
                Shape = 0,
                Weight = 1,
                Image = hpImage,
                Durability = 60000,          // 60 点电量，每 2 秒扣 1 点 = 持续 2 分钟
                Stats = new Stats { [Stat.HP] = 30 },   // 每跳回复 30 点体力
                Price = 50000,
                StackSize = 1,
                ToolTip = "放在背包中自动生效，每 2 秒恢复 30 点体力，电量耗尽后消失。",
            },
            new ItemInfo
            {
                Name = "魔血石",
                Type = ItemType.魔血石,
                Grade = ItemGrade.普通,
                RequiredType = RequiredType.Level,
                RequiredClass = RequiredClass.全职业,
                RequiredGender = RequiredGender.性别不限,
                Set = ItemSet.非套装,
                Shape = 0,
                Weight = 1,
                Image = mpImage,
                Durability = 60000,
                Stats = new Stats { [Stat.MP] = 30 },   // 每跳回复 30 点魔力
                Price = 50000,
                StackSize = 1,
                ToolTip = "放在背包中自动生效，每 2 秒恢复 30 点魔力，电量耗尽后消失。",
            },
            new ItemInfo
            {
                Name = "回城石",
                Type = ItemType.卷轴,
                Grade = ItemGrade.普通,
                RequiredType = RequiredType.Level,
                RequiredClass = RequiredClass.全职业,
                RequiredGender = RequiredGender.性别不限,
                Set = ItemSet.非套装,
                Shape = 20,                  // 卷轴 Shape 20 = 回城石
                Weight = 1,
                Image = townImage,
                Durability = 30000,          // 30 次
                Price = 30000,
                StackSize = 1,
                ToolTip = "双击回到回城点，每次消耗 1 点电量，共 30 次。",
            },
            new ItemInfo
            {
                Name = "随机石",
                Type = ItemType.卷轴,
                Grade = ItemGrade.普通,
                RequiredType = RequiredType.Level,
                RequiredClass = RequiredClass.全职业,
                RequiredGender = RequiredGender.性别不限,
                Set = ItemSet.非套装,
                Shape = 21,                  // 卷轴 Shape 21 = 随机石
                Weight = 1,
                Image = randomImage,
                Durability = 30000,          // 30 次
                Price = 30000,
                StackSize = 1,
                ToolTip = "双击随机传送到当前地图任意位置，每次消耗 1 点电量，共 30 次。",
            },
        };

        var existing = new HashSet<string>(envir.ItemInfoList.Select(x => x.Name), StringComparer.Ordinal);
        var toAdd = new List<ItemInfo>();
        int nextIndex = maxIndex + 1;

        foreach (ItemInfo def in defs)
        {
            if (existing.Contains(def.Name))
            {
                Console.WriteLine($"跳过（已存在）: {def.Name}");
                continue;
            }

            def.Index = nextIndex++;
            toAdd.Add(def);
            Console.WriteLine($"待写入: [{def.Index}] {def.Name} 类型={def.Type} Shape={def.Shape} " +
                              $"图={def.Image} 持久={def.Durability} 属性={string.Join(",", def.Stats.Values.Select(kv => kv.Key + "=" + kv.Value))}");
        }

        Console.WriteLine();
        if (toAdd.Count == 0)
        {
            Console.WriteLine("没有需要新增的物品，数据库未修改。");
            return 0;
        }

        if (!apply)
        {
            Console.WriteLine("== 这是预览模式，数据库未被修改。加 --apply 才会写入 ===");
            return 0;
        }

        string bakDir = Path.Combine(Envir.BackUpPath, $"auto-{DateTime.Now:yyyyMMdd-HHmmss}-加四石头前");
        Directory.CreateDirectory(bakDir);
        string bak = Path.Combine(bakDir, "Server.MirDB");

        try
        {
            File.Copy(Envir.DatabasePath, bak, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"!! 备份失败（服务端可能还开着），已中止：{ex.Message}");
            return 1;
        }
        Console.WriteLine($"已备份 -> {bak}");

        envir.ItemInfoList.AddRange(toAdd);
        envir.ItemIndex = Math.Max(envir.ItemIndex, nextIndex - 1);
        envir.SaveDB();

        Console.WriteLine($"写库完成：物品 {oldCount} -> {envir.ItemInfoList.Count} 个，ItemIndex={envir.ItemIndex}");
        return 0;
    }
}
