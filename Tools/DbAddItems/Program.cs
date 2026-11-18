using System.Text;
using Server;
using Server.MirEnvir;

namespace DbAddItems;

/// <summary>
/// 羽化专属装备写入工具（11-10）。
///
/// 走官方 LoadDB → 追加 ItemInfo → SaveDB 路径，不手写二进制，
/// 因此不会破坏 Map / Monster / NPC / Quest / GameShop 等其他表。
///
/// 产出 26 件「登仙」系列盔甲：
///   · 战法道刺 各 3 套专属（60/70/80 级），男女各一件 → 24 件
///   · 通用套（战法道可穿，90 级），男女各一件 → 2 件
///
/// Shape 约定（客户端据此判定羽化外观 + 选职业库）：
///   1000 通用 / 1001 战士 / 1002 法师 / 1003 道士 / 1004 刺客
/// Image 就是本职业 StateItem_职业_性别.Lib 里的整身立绘帧号。
///
/// 用法（工作目录必须是服务端目录，且服务端已关闭）：
///   DbAddItems.exe            只预览，不写库
///   DbAddItems.exe --apply    写入（自动备份 Server.MirDB.bak_时间戳）
/// </summary>
internal sealed record EquipDef(
    string Name,
    RequiredGender Gender,
    RequiredClass Class,
    byte Level,
    short Shape,
    ushort Image,
    int MinAC, int MaxAC,
    int MinMAC, int MaxMAC,
    int MainStat, int MinMain, int MaxMain,   // MainStat = (int)Stat.MaxDC/…，-1 表示无主属性
    ushort Durability,
    byte Weight,
    uint Price);

internal static class Program
{
    private const short ShapeCommon = 1000;
    private const short ShapeWarrior = 1001;
    private const short ShapeWizard = 1002;
    private const short ShapeTaoist = 1003;
    private const short ShapeAssassin = 1004;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出重定向时可能失败 */ }

        bool apply = args.Contains("--apply");

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

        Console.WriteLine($"已载入物品 {envir.ItemInfoList.Count} 个");

        var defs = BuildDefs();

        // 重名检查：已存在就跳过整批，避免重复运行把装备加两份
        var existing = new HashSet<string>(envir.ItemInfoList.Where(i => i != null).Select(i => i.Name));
        var dup = defs.Where(d => existing.Contains(d.Name)).Select(d => d.Name).ToList();
        if (dup.Count > 0)
        {
            Console.WriteLine($"!! 以下 {dup.Count} 件已存在，本批不重复写入：");
            foreach (var n in dup) Console.WriteLine("   " + n);
            Console.WriteLine("（如需强制重写，先手工删除旧条目）");
            return 3;
        }

        // Index 分配：跟官方新增路径一致（++ItemIndex），但先校正到现有最大值之后
        int maxIndex = envir.ItemInfoList.Where(i => i != null).Select(i => i.Index).DefaultIfEmpty(0).Max();
        if (envir.ItemIndex < maxIndex) envir.ItemIndex = maxIndex;

        Console.WriteLine();
        Console.WriteLine($"{"Index",-7}{"等级",-6}{"职业",-8}{"性别",-6}{"Shape",-7}{"Image",-7}名称");
        int next = envir.ItemIndex;
        foreach (var d in defs)
        {
            next++;
            Console.WriteLine($"{next,-7}{d.Level,-6}{d.Class,-8}{d.Gender,-6}{d.Shape,-7}{d.Image,-7}{d.Name}");
        }

        Console.WriteLine();
        Console.WriteLine($"共 {defs.Count} 件，Index {envir.ItemIndex + 1} ~ {next}");

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("【预览模式】未写入。确认无误后加 --apply 执行。");
            return 0;
        }

        // 备份
        string bak = Envir.DatabasePath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(Envir.DatabasePath, bak, true);
        Console.WriteLine($"已备份 → {Path.GetFileName(bak)}");

        foreach (var d in defs)
        {
            var info = new ItemInfo
            {
                Index = ++envir.ItemIndex,
                Name = d.Name,
                Type = ItemType.盔甲,
                Grade = ItemGrade.圣物,
                RequiredType = RequiredType.Level,
                RequiredClass = d.Class,
                RequiredGender = d.Gender,
                Set = ItemSet.非套装,
                Shape = d.Shape,
                Weight = d.Weight,
                Light = 0,
                RequiredAmount = d.Level,
                Image = d.Image,
                Durability = d.Durability,
                Price = d.Price,
                StackSize = 1,
                Slots = 0,
                Bind = BindMode.None,
                CanAwakening = false,
                RandomStatsId = 255,
            };

            info.Stats[Stat.MinAC] = d.MinAC;
            info.Stats[Stat.MaxAC] = d.MaxAC;
            info.Stats[Stat.MinMAC] = d.MinMAC;
            info.Stats[Stat.MaxMAC] = d.MaxMAC;
            ApplyMainStat(info, d);

            envir.ItemInfoList.Add(info);
        }

        envir.SaveDB();
        Console.WriteLine($"已写入并保存，现有物品 {envir.ItemInfoList.Count} 个。");
        return 0;
    }

    /// <summary>主属性按 Min/Max 成对写入（DC/MC/SC 各自有 Min 与 Max 两个枚举值）。</summary>
    private static void ApplyMainStat(ItemInfo info, EquipDef d)
    {
        if (d.MainStat < 0) return;

        switch ((Stat)d.MainStat)
        {
            case Stat.MaxDC:
                info.Stats[Stat.MinDC] = d.MinMain;
                info.Stats[Stat.MaxDC] = d.MaxMain;
                break;
            case Stat.MaxMC:
                info.Stats[Stat.MinMC] = d.MinMain;
                info.Stats[Stat.MaxMC] = d.MaxMain;
                break;
            case Stat.MaxSC:
                info.Stats[Stat.MinSC] = d.MinMain;
                info.Stats[Stat.MaxSC] = d.MaxMain;
                break;
        }
    }

    private static List<EquipDef> BuildDefs()
    {
        var list = new List<EquipDef>();

        // ── 四职业专属：3 套 × 男女 ────────────────────────────────
        // tier 0/1/2 → 60/70/80 级，主属性上限每档 +5（用户定稿 11-10）
        AddClass(list, RequiredClass.战士, ShapeWarrior, "碧血",
                 new[] { "玄骨战衣", "仙羽战衣", "不灭战衣" },
                 (2450, 2460), (2455, 2465), (2470, 2480),
                 minAC: 6, maxAC: 22, minMAC: 3, maxMAC: 10,
                 (int)Stat.MaxDC, mainMin: 1, mainBase: 1, weight: 70);

        AddClass(list, RequiredClass.法师, ShapeWizard, "虹玄",
                 new[] { "玄光法袍", "仙羽法袍", "不灭法袍" },
                 (2451, 2461), (2456, 2466), (2471, 2481),
                 minAC: 3, maxAC: 11, minMAC: 4, maxMAC: 11,
                 (int)Stat.MaxMC, mainMin: 0, mainBase: 8, weight: 25);

        AddClass(list, RequiredClass.道士, ShapeTaoist, "翊仙",
                 new[] { "玄木道袍", "仙羽道袍", "不灭道袍" },
                 (2452, 2462), (2457, 2467), (2472, 2482),
                 minAC: 2, maxAC: 13, minMAC: 3, maxMAC: 11,
                 (int)Stat.MaxSC, mainMin: 0, mainBase: 7, weight: 28);

        AddClass(list, RequiredClass.刺客, ShapeAssassin, "飞燕",
                 new[] { "鬼角战衣", "仙羽战衣", "不灭战衣" },
                 (2453, 2463), (2458, 2468), (2473, 2483),
                 minAC: 2, maxAC: 13, minMAC: 4, maxMAC: 14,
                 (int)Stat.MaxDC, mainMin: 2, mainBase: 2, weight: 35);

        // ── 通用套：战法道可穿，90 级，无主属性（参考昆仑龙极甲）────────
        list.Add(new EquipDef("登仙·通用羽衣(男)", RequiredGender.男性, RequiredClass.战法道, 90,
                              ShapeCommon, 2454, 22, 30, 8, 16, -1, 0, 0, 65000, 15, 120000));
        list.Add(new EquipDef("登仙·通用羽衣(女)", RequiredGender.女性, RequiredClass.战法道, 90,
                              ShapeCommon, 2464, 22, 30, 8, 16, -1, 0, 0, 65000, 15, 120000));

        return list;
    }

    private static void AddClass(
        List<EquipDef> list, RequiredClass cls, short shape, string prefix,
        string[] suffixes, (int m, int w) img1, (int m, int w) img2, (int m, int w) img3,
        int minAC, int maxAC, int minMAC, int maxMAC,
        int mainStat, int mainMin, int mainBase, byte weight)
    {
        var imgs = new[] { img1, img2, img3 };
        var levels = new byte[] { 60, 70, 80 };
        var duras = new ushort[] { 50000, 55000, 60000 };
        var prices = new uint[] { 30000, 45000, 65000 };

        for (int tier = 0; tier < 3; tier++)
        {
            byte lv = levels[tier];
            int mainMax = mainBase + 5 * tier;

            list.Add(new EquipDef($"登仙·{prefix}{suffixes[tier]}(男)", RequiredGender.男性, cls, lv,
                                  shape, (ushort)imgs[tier].m, minAC, maxAC, minMAC, maxMAC,
                                  mainStat, mainMin, mainMax, duras[tier], weight, prices[tier]));
            list.Add(new EquipDef($"登仙·{prefix}{suffixes[tier]}(女)", RequiredGender.女性, cls, lv,
                                  shape, (ushort)imgs[tier].w, minAC, maxAC, minMAC, maxMAC,
                                  mainStat, mainMin, mainMax, duras[tier], weight, prices[tier]));
        }
    }
}
