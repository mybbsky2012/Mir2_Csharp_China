using System.Globalization;
using System.Text;
using Server;
using Server.MirEnvir;

namespace ItemImport;

/// <summary>
/// 把 117 版导出表（Exports\2_物品数据.csv）里存在、而 110 版 Server.MirDB 中缺失的物品补进数据库。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处）：
///     ItemImport.exe "&lt;2_物品数据.csv&gt;"            仅体检，不写库
///     ItemImport.exe "&lt;2_物品数据.csv&gt;" --apply      实际写库（写前自动备份）
///
/// 说明：两代数据库结构不同，不能直接搬二进制，因此按字段逐项转换。
///      新物品一律分配全新 Index，绝不与旧库已有 Index 冲突；旧库原有数据原样保留。
/// </summary>
internal static class Program
{
    /// <summary>
    /// CSV 里的 58 个 Stat 列。其顺序与 110 版 Stat 枚举严格一致，
    /// 因此直接写 110 的 Stat 数值，避免按名字映射时踩到改名（如 暴击倍率→暴击率）。
    /// </summary>
    private static readonly int[] StatOrder =
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,          // MinAC..MaxSC
        10, 11, 12, 13, 14, 15,                // 准确 敏捷 HP MP 攻击速度 幸运
        16, 17, 18,                            // 背包负重 腕力负重 装备负重
        19, 20, 21, 22, 23,                    // 反弹伤害 强度 神圣 冰冻伤害 毒素伤害
        30, 31, 32, 33, 34, 35, 36,            // 魔法躲避 毒物躲避 生命恢复 法力恢复 中毒恢复 暴击倍率 暴击伤害
        40, 41, 42, 43, 44, 45, 46, 47, 48,    // 最大防御/魔御/物攻/魔攻/道攻数率 攻速 生命 法力 吸血
        100, 101, 102, 103, 104, 105, 106, 107, 108,
        120, 121, 123, 124, 125, 126, 127, 128, 129
    };

    private static readonly StringBuilder Log = new StringBuilder();

    private static void W(string line = "")
    {
        Log.AppendLine(line);
        Console.WriteLine(line);
    }

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出被重定向时可能失败，忽略 */ }

        bool apply = args.Any(a => a.Equals("--apply", StringComparison.OrdinalIgnoreCase));
        string csvPath = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            Console.WriteLine("用法: ItemImport.exe \"<2_物品数据.csv>\" [--apply]");
            return 2;
        }

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"找不到 CSV: {csvPath}");
            return 2;
        }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}。请把本程序放到服务端目录（与 Server.exe 同级）再运行。");
            return 2;
        }

        W($"数据库 : {Path.GetFullPath(Envir.DatabasePath)}");
        W($"CSV    : {Path.GetFullPath(csvPath)}");
        W($"模式   : {(apply ? "写入(apply)" : "仅体检(dry-run)")}");
        W();

        // ---- 1. 读设置（RandomItemStats 等在此加载） ----
        try
        {
            Settings.Load();
            W($"配置加载完成：随机属性表 {Settings.RandomItemStatsList.Count} 条");
        }
        catch (Exception ex)
        {
            W($"!! 配置加载失败（继续，但随机属性可能不生效）：{ex.Message}");
        }

        // ---- 2. 读旧库 ----
        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            W("!! LoadDB 失败，放弃。");
            return 1;
        }

        int oldCount = envir.ItemInfoList.Count;
        int maxIndex = oldCount > 0 ? envir.ItemInfoList.Max(x => x.Index) : 0;
        W($"旧库物品 {oldCount} 个，ItemIndex={envir.ItemIndex}，最大物品 Index={maxIndex}");

        var existing = new HashSet<string>(envir.ItemInfoList.Select(x => x.Name), StringComparer.Ordinal);
        W($"旧库物品名去重后 {existing.Count} 个");

        // ---- 3. 解析 CSV ----
        List<string[]> rows = ParseCsv(csvPath);
        if (rows.Count < 2) { W("!! CSV 内容为空。"); return 1; }
        string[] header = rows[0];
        var col = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < header.Length; i++)
            if (!string.IsNullOrWhiteSpace(header[i]) && !col.ContainsKey(header[i]))
                col[header[i]] = i;

        W($"CSV 字段 {header.Length} 列，数据行 {rows.Count - 1} 行");
        W();

        // ---- 4. 差集：CSV 里有、旧库没有的 ----
        var missing = new List<string[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] row in rows.Skip(1))
        {
            if (row.Length < 3) continue;
            string name = row[col["ItemName"]].Trim();
            if (name.Length == 0) continue;
            if (existing.Contains(name)) continue;
            if (!seen.Add(name)) continue;   // CSV 内部重名只取第一条
            missing.Add(row);
        }

        W($"==> 需要补入的物品 {missing.Count} 个");
        W();

        // ---- 5. 逐个转换 ----
        int nextIndex = maxIndex + 1;
        var imported = new List<ItemInfo>();
        var warnings = new List<string>();
        int noPrev = 0, noArr = 0;
        var usedRandomStats = new SortedSet<int>();
        var setFallback = new SortedSet<string>();
        int lightMapped = 0, intensityDropped = 0, heroStatDropped = 0;

        foreach (string[] row in missing)
        {
            string name = row[col["ItemName"]].Trim();
            try
            {
                ItemInfo info = new ItemInfo
                {
                    Index = nextIndex++,
                    Name = name,
                    Type = ParseEnum<ItemType>(row[col["ItemType"]], name),
                    Grade = ParseEnum<ItemGrade>(row[col["ItemGrade"]], name),
                    RequiredType = ParseEnum<RequiredType>(row[col["ItemRequiredType"]], name),
                    RequiredGender = ParseEnum<RequiredGender>(row[col["ItemRequiredGender"]], name),
                    RequiredClass = ParseEnum<RequiredClass>(row[col["ItemRequiredClass"]], name),
                    Set = ParseSet(row[col["ItemSet"]], name, setFallback),

                    RandomStatsId = Num<byte>(row[col["ItemRandomStatsId"]], name, "ItemRandomStatsId"),
                    RequiredAmount = Num<byte>(row[col["ItemRequiredAmount"]], name, "ItemRequiredAmount"),
                    Image = Num<ushort>(row[col["ItemImage"]], name, "ItemImage"),
                    Shape = Num<short>(row[col["ItemShape"]], name, "ItemShape"),
                    Effect = Num<byte>(row[col["ItemEffect"]], name, "ItemEffect"),
                    StackSize = Num<ushort>(row[col["ItemStackSize"]], name, "ItemStackSize"),
                    Slots = Num<byte>(row[col["ItemSlots"]], name, "ItemSlots"),
                    Weight = Num<byte>(row[col["ItemWeight"]], name, "ItemWeight"),
                    Durability = Num<ushort>(row[col["ItemDurability"]], name, "ItemDurability"),
                    Price = Num<uint>(row[col["ItemPrice"]], name, "ItemPrice"),
                    ToolTip = row[col["ItemToolTip"]],

                    StartItem = Bool(row[col["StartItem"]]),
                    NeedIdentify = Bool(row[col["NeedIdentify"]]),
                    ShowGroupPickup = Bool(row[col["ShowGroupPickup"]]),
                    GlobalDropNotify = Bool(row[col["GlobalDropNotify"]]),
                    ClassBased = Bool(row[col["ClassBased"]]),
                    LevelBased = Bool(row[col["LevelBased"]]),
                    CanMine = Bool(row[col["CanMine"]]),
                    CanFastRun = Bool(row[col["CanFastRun"]]),
                    CanAwakening = Bool(row[col["CanAwakening"]])
                };

                // 117 把照明拆成「范围 + 强度」两个字段，110 只有一个 Light（当作范围用），
                // 这里取范围，强度丢弃（只有极少数物品非 0，影响仅限发光亮度）。
                byte lightRange = Num<byte>(row[col["ItemLightRange"]], name, "ItemLightRange");
                info.Light = lightRange;
                if (lightRange != 0) lightMapped++;
                if (Num<byte>(row[col["ItemLightIntensity"]], name, "ItemLightIntensity") != 0) intensityDropped++;

                // 58 个 Stat 列，按位置对应 110 的 Stat
                if (StatOrder.Length != 58)
                    throw new Exception("StatOrder 长度异常，应为 58");
                int statCol = col["StatMinAC"];
                for (int i = 0; i < StatOrder.Length; i++)
                {
                    int v = Num<int>(row[statCol + i], name, header[statCol + i]);
                    if (v != 0)
                    {
                        var st = (Stat)StatOrder[i];
                        if (st == Stat.Hero) { heroStatDropped++; continue; }   // 110 无此属性，丢弃
                        info.Stats[st] = v;
                    }
                }

                // Bind / Special：CSV 列名去掉前缀就是 110 的枚举名
                short bind = 0;
                int bindCol = col["BindDontDeathdrop"];
                for (int i = 0; i < 16; i++)
                {
                    if (!Bool(row[bindCol + i])) continue;
                    string mem = header[bindCol + i].Substring("Bind".Length);
                    if (Enum.TryParse(mem, true, out BindMode m)) bind |= (short)m;
                    else warnings.Add($"{name}: 无法识别的绑定项 {header[bindCol + i]}");
                }
                info.Bind = (BindMode)bind;

                short spec = 0;
                int specCol = col["SpecialParalize"];
                for (int i = 0; i < 12; i++)
                {
                    if (!Bool(row[specCol + i])) continue;
                    string mem = header[specCol + i].Substring("Special".Length);
                    if (Enum.TryParse(mem, true, out SpecialItemMode m)) spec |= (short)m;
                    else warnings.Add($"{name}: 无法识别的特殊属性 {header[specCol + i]}");
                }
                info.Unique = (SpecialItemMode)spec;

                // 与 LoadDB 保持一致地挂上随机属性表
                if (info.RandomStatsId > 0)
                {
                    usedRandomStats.Add(info.RandomStatsId);
                    noPrev += 0;
                    if (info.RandomStatsId < Settings.RandomItemStatsList.Count)
                        info.RandomStats = Settings.RandomItemStatsList[info.RandomStatsId];
                    else
                        warnings.Add($"{name}: RandomStatsId={info.RandomStatsId} 超出随机属性表({Settings.RandomItemStatsList.Count} 条)，无随机属性");
                }
                else
                {
                    noPrev++;
                }

                if (info.Type == ItemType.项链 || info.Type == ItemType.手镯 || info.Type == ItemType.戒指 ||
                    info.Type == ItemType.武器 || info.Type == ItemType.盔甲 || info.Type == ItemType.头盔)
                    noArr++;

                imported.Add(info);
            }
            catch (Exception ex)
            {
                warnings.Add($"{name}: 转换失败 -> {ex.Message}");
            }
        }

        W("---- 转换结果 ----");
        W($"成功转换 {imported.Count} 个，失败 {missing.Count - imported.Count} 个");
        W($"新 Index 区间: {maxIndex + 1} ~ {nextIndex - 1}");
        W($"用到的 RandomStatsId: {(usedRandomStats.Count == 0 ? "(无)" : string.Join(",", usedRandomStats))}，无随机属性 {noPrev} 个");
        W($"其中装备类 {noArr} 个");
        W($"Light 非 0 的 {lightMapped} 个；117 独有的 LightIntensity 非 0 的 {intensityDropped} 个（已丢弃）");
        W($"117 独有的 StatHero 非 0 的属性被丢弃 {heroStatDropped} 处");
        if (setFallback.Count > 0)
            W($"套装名 110 中不存在，已按「非套装」导入: {string.Join(",", setFallback)}");
        if (warnings.Count > 0)
        {
            W();
            W($"---- 提示 {warnings.Count} 条 ----");
            foreach (string s in warnings.Take(40)) W("  " + s);
            if (warnings.Count > 40) W($"  ...另有 {warnings.Count - 40} 条");
        }

        W();
        W("---- 抽样预览（前 10 个）----");
        foreach (ItemInfo i in imported.Take(10))
            W($"  [{i.Index}] {i.Name}  类型={i.Type} 品级={i.Grade} 需求等级={i.RequiredAmount} " +
              $"图={i.Image} 持久={i.Durability} 价={i.Price} 负重={i.Weight} 属性数={i.Stats.Values.Count}");

        if (!apply)
        {
            W();
            W("== 这是体检模式，数据库未被修改。加 --apply 才会写入。 ==");
            return 0;
        }

        // ---- 6. 备份 + 写库 ----
        string bakDir = Path.Combine(Envir.BackUpPath, $"auto-{DateTime.Now:yyyyMMdd-HHmmss}-导入物品前");
        Directory.CreateDirectory(bakDir);
        string bak = Path.Combine(bakDir, "Server.MirDB");
        File.Copy(Envir.DatabasePath, bak, true);
        W($"已备份旧库 -> {bak}");

        envir.ItemInfoList.AddRange(imported);
        envir.ItemIndex = Math.Max(envir.ItemIndex, nextIndex - 1);
        envir.SaveDB();

        W($"已写回数据库：物品 {oldCount} -> {envir.ItemInfoList.Count} 个，ItemIndex={envir.ItemIndex}");

        string logPath = Path.Combine(bakDir, "导入报告.txt");
        File.WriteAllText(logPath, Log.ToString(), new UTF8Encoding(false));
        W($"报告已存 -> {logPath}");
        return 0;
    }

    private static T ParseEnum<T>(string s, string item) where T : struct, Enum
    {
        if (Enum.TryParse(s.Trim(), true, out T v)) return v;
        throw new Exception($"枚举 {typeof(T).Name} 中不存在 '{s.Trim()}'");
    }

    /// <summary>110 的 ItemSet 里没有 117 的「昆仑套装」（套装加成是硬编码的，110 本来也没它的加成），按非套装处理。</summary>
    private static ItemSet ParseSet(string s, string item, SortedSet<string> fallback)
    {
        string v = s.Trim();
        if (Enum.TryParse(v, true, out ItemSet set)) return set;
        fallback.Add(v);
        return ItemSet.非套装;
    }

    private static bool Bool(string s) => s.Trim().Equals("True", StringComparison.OrdinalIgnoreCase) || s.Trim() == "1";

    private static T Num<T>(string s, string item, string field) where T : struct, IConvertible
    {
        string v = s.Trim();
        if (v.Length == 0) return default;
        try
        {
            return (T)Convert.ChangeType(v, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            throw new Exception($"字段 {field} 的值 '{v}' 无法转成 {typeof(T).Name}");
        }
    }

    /// <summary>最小 CSV 解析（支持双引号包裹与 "" 转义），足够读本表。</summary>
    private static List<string[]> ParseCsv(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);
        var rows = new List<string[]>();
        var cur = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { cur.Add(sb.ToString()); sb.Clear(); }
            else if (c == '\n') { cur.Add(sb.ToString()); sb.Clear(); if (cur.Any(x => x.Length > 0)) rows.Add(cur.ToArray()); cur.Clear(); }
            else if (c != '\r') sb.Append(c);
        }

        if (sb.Length > 0 || cur.Count > 0)
        {
            cur.Add(sb.ToString());
            if (cur.Any(x => x.Length > 0)) rows.Add(cur.ToArray());
        }

        return rows;
    }
}
