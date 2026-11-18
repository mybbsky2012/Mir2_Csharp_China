using System.Text;
using Server;
using Server.MirEnvir;

namespace DbHumUpFill;

/// <summary>
/// 羽化（登仙）装备补齐工具（11-11）。
///
/// 按「60 级起步、每 10 级一档、主属性 +5/档」补齐五职业的盔甲与武器：
///
///   盔甲（品质圣物，男女各一件，防/魔御沿用现有登仙数值线恒定，主属性 +5/档）：
///     战士 1011~1018  AC6-22 MAC3-10  MaxDC = 1 +5(t-1)     已有 t1~t3，补 t4~t8
///     法师 1021~1028  AC3-11 MAC4-11  MaxMC = 8 +5(t-1)     已有 t1~t3，补 t4~t8
///     道士 1031~1038  AC2-13 MAC3-11  MaxSC = 7 +5(t-1)     已有 t1~t3，补 t4~t8
///     刺客 1041~1048  AC2-13 MAC4-14  MaxDC = 2 +5(t-1)     已有 t1~t3，补 t4~t8
///     弓箭 1051~1058  AC2-13 MAC4-14  MaxDC = 2 +5(t-1)     全缺，补 t1~t8
///
///   武器（不分男女，等级 60 起，主属性 +5/把，基准参考库内 65 级冰龙系）：
///     战士 2011~2015  MaxDC 60+5(t-1)   → UpWarWeapon 01~05
///     法师 2021~2024  MaxMC 26+5(t-1)   → UpWizWeapon 01~04
///     道士 2031~2034  MaxSC 24+5(t-1) 神圣4 → UpTaoWeapon 01~04
///     刺客 2041~2048  MaxDC 50+5(t-1) 攻速-2 → UpAssWeaponR/L 01~08
///     弓箭 2051~2058  MaxDC 52+5(t-1) 准确2 → UpArcWeapon 01~08
///
///   顺手修正道士 70/80 级男装的 shape 错乱（男70 1033→1032、男80 1034→1033）。
///
///   内观 Image：t4~t8 复用 t3 立绘（StateItem 素材只画了 1~3 档，空帧会兜底回落）；
///   弓箭素材 8 档立绘齐全，按 2454~2458/2470~2472（男）、2464~2468/2480~2482（女）直排。
///   武器 Image 留 0：内观武器层走客户端固定帧表（战 2440 / 法 2432 / 道 2434 / 刺 2436 / 弓 3395）。
///
/// 用法（工作目录必须是服务端目录，且**服务端已关闭**）：
///   DbHumUpFill.exe            只预览
///   DbHumUpFill.exe --apply    写入（自动备份 Server.MirDB.bak_时间戳）
/// </summary>
internal static class Program
{
    private const string Prefix = "登仙·";

    // 档位命名：t1~t3 沿用现有（玄骨/仙羽/不灭），t4~t8 新增
    private static readonly string[] TierWords = { "", "玄骨", "仙羽", "不灭", "通玄", "御圣", "擎天", "无极", "归真" };

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出重定向时可能失败 */ }

        bool apply = args.Contains("--apply");

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        if (IsServerRunning())
        {
            Console.WriteLine("!! 检测到 Server.exe 正在运行。请先关闭服务端再执行（否则保存会被覆盖）。");
            return 4;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine($"已载入物品 {envir.ItemInfoList.Count} 个");

        // ---------- 1) 修正道士 70/80 级男装 shape 错乱 ----------
        int fixedShapes = 0;
        FixShape(envir.ItemInfoList, "登仙·翊仙仙羽道袍(男)", 70, 1032, apply, ref fixedShapes);
        FixShape(envir.ItemInfoList, "登仙·翊仙不灭道袍(男)", 80, 1033, apply, ref fixedShapes);

        // ---------- 2) 生成待新增条目 ----------
        var toAdd = new List<ItemInfo>();

        // 盔甲。参数：职业, 系列名, 后缀, 主属性基础(+5/档), MinDC, 重量, ACmin, ACmax, MACmin, MACmax, 起始档, 最高档
        AddArmour(toAdd, RequiredClass.战士, "碧血", "战衣", 1, 1, 70, 6, 22, 3, 10, 4, 8);
        AddArmour(toAdd, RequiredClass.法师, "虹玄", "法袍", 8, 0, 25, 3, 11, 4, 11, 4, 8);
        AddArmour(toAdd, RequiredClass.道士, "翊仙", "道袍", 7, 0, 28, 2, 13, 3, 11, 4, 8);
        AddArmour(toAdd, RequiredClass.刺客, "飞燕", "战衣", 2, 2, 35, 2, 13, 4, 14, 4, 8);
        AddArmour(toAdd, RequiredClass.弓箭, "暗鬼", "羽衣", 2, 2, 40, 2, 13, 4, 14, 1, 8);

        // 武器。参数：职业, 系列名, 后缀, shape 起点, 件数, 主属性下限, 主属性基础(+5/把), 重量, 耐久, 附加
        AddWeapon(toAdd, RequiredClass.战士, "碧血", "刃", 2011, 5, 8, 60, 180, 60000);
        AddWeapon(toAdd, RequiredClass.法师, "虹玄", "杖", 2021, 4, 8, 26, 35, 30000);
        AddWeapon(toAdd, RequiredClass.道士, "翊仙", "扇", 2031, 4, 7, 24, 80, 35000, holy: 4);
        AddWeapon(toAdd, RequiredClass.刺客, "飞燕", "匕", 2041, 8, 10, 50, 105, 50000, atkSpeed: -2);
        AddWeapon(toAdd, RequiredClass.弓箭, "暗鬼", "弓", 2051, 8, 12, 52, 60, 38000, accuracy: 2);

        Console.WriteLine();
        Console.WriteLine($"待修正 shape {fixedShapes} 处，待新增 {toAdd.Count} 件。");

        if (!apply)
        {
            Console.WriteLine();
            foreach (var it in toAdd)
            {
                var st = string.Join(", ", it.Stats.Values.Where(p => p.Value != 0).Select(p => $"{p.Key}={p.Value}"));
                Console.WriteLine($"  [预览] L{it.RequiredAmount,3} {it.RequiredClass}/{it.RequiredGender} {it.Name,-22} Shape={it.Shape,-5} Image={it.Image,-5} 耐久={it.Durability} {st}");
            }
            Console.WriteLine();
            Console.WriteLine("【预览模式】未写入。确认无误后加 --apply 执行。");
            return 0;
        }

        string bak = Envir.DatabasePath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        File.Copy(Envir.DatabasePath, bak, true);
        Console.WriteLine($"已备份 → {Path.GetFileName(bak)}");

        int lastIndex = envir.ItemInfoList.Count > 0 ? envir.ItemInfoList.Max(x => x.Index) : 0;
        foreach (var it in toAdd)
        {
            it.Index = ++lastIndex;
            envir.ItemInfoList.Add(it);
        }

        envir.SaveDB();
        Console.WriteLine($"已保存，物品 {envir.ItemInfoList.Count} 个（新增 {toAdd.Count}，修正 shape {fixedShapes}）。");
        return 0;
    }

    // ---------------- 盔甲 ----------------

    private static void AddArmour(List<ItemInfo> list, RequiredClass reqCls, string series, string suffix,
        int mainBase, int minDc, int weight, int minAC, int maxAC, int minMAC, int maxMAC,
        int minTier, int maxTier)
    {
        short clsIdx = reqCls switch
        {
            RequiredClass.战士 => 1,
            RequiredClass.法师 => 2,
            RequiredClass.道士 => 3,
            RequiredClass.刺客 => 4,
            RequiredClass.弓箭 => 5,
            _ => 0,
        };

        // 主属性类型：战/刺/弓 MaxDC，法 MaxMC，道 MaxSC
        Stat mainStat = reqCls == RequiredClass.法师 ? Stat.MaxMC
                      : reqCls == RequiredClass.道士 ? Stat.MaxSC
                      : Stat.MaxDC;

        for (int t = minTier; t <= maxTier; t++)
        {
            for (int g = 0; g < 2; g++)   // 0=男 1=女
            {
                var it = new ItemInfo
                {
                    Name = $"{Prefix}{series}{TierWords[t]}{suffix}({(g == 0 ? "男" : "女")})",
                    Type = ItemType.盔甲,
                    Grade = ItemGrade.圣物,
                    RequiredType = RequiredType.Level,
                    RequiredClass = reqCls,
                    RequiredGender = g == 0 ? RequiredGender.男性 : RequiredGender.女性,
                    Shape = (short)(1000 + clsIdx * 10 + t),
                    Weight = (byte)weight,
                    RequiredAmount = (byte)(50 + 10 * t),
                    Image = (ushort)ArmourImage(reqCls, g == 0, t),
                    Durability = (ushort)Math.Min(65000, 50000 + 5000 * (t - 1)),   // ushort 上限 65535，t5 起封顶
                    Price = (uint)(30000 + 15000 * (t - 1)),
                };

                it.Stats[Stat.MinAC] = minAC;
                it.Stats[Stat.MaxAC] = maxAC;
                it.Stats[Stat.MinMAC] = minMAC;
                it.Stats[Stat.MaxMAC] = maxMAC;
                it.Stats[mainStat] = mainBase + 5 * (t - 1);
                if (minDc > 0) it.Stats[Stat.MinDC] = minDc;

                list.Add(it);
            }
        }
    }

    /// <summary>
    /// 内观 Image：战法道刺 t1~t3 用现有帧号（+5/+20 规律），t4~t8 复用 t3（素材只有 1~3 档）；
    /// 弓箭素材 8 档齐全：男 2454~2458 + 2470~2472，女 2464~2468 + 2480~2482（已逐一验证非空帧）。
    /// </summary>
    private static int ArmourImage(RequiredClass cls, bool man, int t)
    {
        if (cls == RequiredClass.弓箭)
        {
            int[] manFrames = { 2454, 2455, 2456, 2457, 2458, 2470, 2471, 2472 };
            int[] womanFrames = { 2464, 2465, 2466, 2467, 2468, 2480, 2481, 2482 };
            return (man ? manFrames : womanFrames)[Math.Clamp(t, 1, 8) - 1];
        }

        int baseFrame = cls switch
        {
            RequiredClass.战士 => 2450,
            RequiredClass.法师 => 2451,
            RequiredClass.道士 => 2452,
            RequiredClass.刺客 => 2453,
            _ => 2454,
        };
        if (!man) baseFrame += 10;   // 女半 2460~2463

        return t switch
        {
            1 => baseFrame,
            2 => baseFrame + 5,
            _ => baseFrame + 20,     // t3 及 t4~t8（复用 t3 立绘）
        };
    }

    // ---------------- 武器 ----------------

    private static void AddWeapon(List<ItemInfo> list, RequiredClass reqCls, string series, string suffix,
        short shapeBase, int count, int minMain, int mainBase, int weight, int dura,
        int holy = 0, int atkSpeed = 0, int accuracy = 0)
    {
        Stat mainStat = reqCls == RequiredClass.法师 ? Stat.MaxMC
                      : reqCls == RequiredClass.道士 ? Stat.MaxSC
                      : Stat.MaxDC;
        Stat minStat = reqCls == RequiredClass.法师 ? Stat.MinMC
                     : reqCls == RequiredClass.道士 ? Stat.MinSC
                     : Stat.MinDC;

        for (int t = 1; t <= count; t++)
        {
            var it = new ItemInfo
            {
                Name = $"{Prefix}{series}{TierWords[t]}{suffix}",
                Type = ItemType.武器,
                Grade = ItemGrade.圣物,
                RequiredType = RequiredType.Level,
                RequiredClass = reqCls,
                RequiredGender = RequiredGender.性别不限,
                Shape = (short)(shapeBase + (t - 1)),
                Weight = (byte)weight,
                RequiredAmount = (byte)(50 + 10 * t),
                Durability = (ushort)dura,
                Price = (uint)(30000 + 15000 * (t - 1)),
                Image = 0,   // 内观武器层走客户端固定帧表，Image 留 0
            };

            it.Stats[mainStat] = mainBase + 5 * (t - 1);
            it.Stats[minStat] = minMain;
            if (holy != 0) it.Stats[Stat.神圣] = holy;
            if (atkSpeed != 0) it.Stats[Stat.攻击速度] = atkSpeed;
            if (accuracy != 0) it.Stats[Stat.准确] = accuracy;

            list.Add(it);
        }
    }

    private static void FixShape(List<ItemInfo> list, string name, byte level, short newShape, bool apply, ref int count)
    {
        var hits = list.Where(x => x.Name == name && x.RequiredType == RequiredType.Level && x.RequiredAmount == level).ToList();
        foreach (var it in hits)
        {
            if (it.Shape == newShape) continue;
            Console.WriteLine($"  [修正] {it.Name} L{level}  Shape {it.Shape} → {newShape}");
            if (apply) it.Shape = newShape;
            count++;
        }
    }

    private static bool IsServerRunning()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("Server")
                       .Any(p => !p.HasExited);
        }
        catch { return false; }
    }
}
