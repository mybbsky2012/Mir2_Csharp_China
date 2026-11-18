using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace HumUpQuestSetup;

/// <summary>
/// 羽化登仙任务链写库工具（12 环 + 材料 + NPC）。
///
/// 走官方 LoadDB → 追加/更新 → SaveDB 通道，不手写二进制，
/// 因此不会破坏 Map / Monster / GameShop 等其他表。
///
/// 用法（工作目录必须是服务端目录，即 Server.MirDB 所在处；写库前必须关服）：
///   HumUpQuestSetup.exe --maps                 只读：导出地图清单
///   HumUpQuestSetup.exe --probe 命运之书        只读：打印物品完整字段
///   HumUpQuestSetup.exe --search 印章 心脏      只读：按关键字找图标
///   HumUpQuestSetup.exe --items / --npcs / --quests / --all     预览
///   HumUpQuestSetup.exe --all --apply          写入（自动备份 Server.MirDB.bak_时间戳）
///
/// 幂等：物品按 Name、NPC 按 FileName、任务按 FileName 匹配；已存在则就地更新，不重复追加。
/// </summary>
internal static class Program
{
    private sealed record ItemDef(string Name, ushort Image, ushort StackSize, uint Price, BindMode Bind);

    private sealed record NpcDef(string File, string Name, int MapIndex, int X, int Y, ushort Image);

    private sealed record QuestDef(string File, string Name, string Group, int MinLevel, bool Repeat);

    private const string QuestGroup = "羽化登仙";

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

        Console.WriteLine($"已载入：物品 {envir.ItemInfoList.Count} / NPC {envir.NPCInfoList.Count} / 任务 {envir.QuestInfoList.Count} / 地图 {envir.MapInfoList.Count}");

        if (args.Contains("--maps")) { DumpMaps(envir); return 0; }
        if (args.Contains("--probe")) { ProbeItems(envir, args); return 0; }
        if (args.Contains("--search")) { SearchItems(envir, args); return 0; }
        if (args.Contains("--verify")) { VerifyQuests(envir); return 0; }

        bool apply = args.Contains("--apply");
        bool all = args.Contains("--all");

        if (all || args.Contains("--items")) WriteItems(envir, apply);
        if (all || args.Contains("--npcs")) WriteNpcs(envir, apply);
        if (all || args.Contains("--quests")) WriteQuests(envir, apply);

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("【预览模式】未写入数据库。确认无误后加 --apply 执行（请先关服）。");
        }

        return 0;
    }

    // ───────────────────────────── 数据 ─────────────────────────────

    /// <summary>
    /// 任务物品。Bind：
    ///   旧书 = DontDrop | DontSell（韩服：可交易/可仓库存放，但不可丢弃、不可卖店）
    ///   其余材料不绑定。
    /// </summary>
    private static List<ItemDef> BuildItems() => new()
    {
        new("旧书",               4062, 1,    50000, BindMode.DontDrop | BindMode.DontSell),
        new("幻身书",             4062, 1,     1000, BindMode.None),
        new("资质确认书",         4062, 1,     1000, BindMode.None),
        new("羽化登仙秘笈",       4062, 1,     1000, BindMode.DontDrop),
        new("武陵桃源之印",       4279, 5000, 10000, BindMode.None),
        new("猿的心脏",           2126, 100,    100, BindMode.None),
        new("血之结晶块",         4485, 500,    500, BindMode.None),
        new("精炼研磨石",         3733, 100,   5000, BindMode.None),
        new("银块",                285, 100,   5000, BindMode.None),
        new("狐的心脏",           2126, 100,    100, BindMode.None),
        new("白之结晶块",         4490, 500,    500, BindMode.None),
        new("黑贵钢铁线",          281, 100,   5000, BindMode.None),
        new("破坏的神石",          614, 100,   1000, BindMode.None),
        new("道力的神石",          619, 100,   1000, BindMode.None),
        new("魔力的神石",          624, 100,   1000, BindMode.None),
        new("兵器的神石",          614, 100,   1000, BindMode.None),
        new("魔法防御的神石",      619, 100,   1000, BindMode.None),
        new("超越之油",            408, 100,   1000, BindMode.None),
    };

    /// <summary>
    /// NPC。MapIndex 为 MapInfo.Index（不是地图文件名）：
    ///   315 = 1002 道馆馆长屋 / 317 = 1004 道馆图书馆 / 423 = D2070 般若神殿大厅
    ///   500 = NAMMAN 南蛮 / 675 = Utopia2 武陵桃园 / 312 = 11 道馆
    /// 坐标已用地图碰撞逐点校验（tools\poscheck.py），均为可走且四邻可走。
    /// </summary>
    private static List<NpcDef> BuildNpcs() => new()
    {
        new("羽化登仙/破关虎",     "羽化登仙_破关虎",     315, 17, 17, 14),
        new("羽化登仙/书桌",       "羽化登仙_书桌",       317, 11, 16, 45),
        new("羽化登仙/日宝",       "羽化登仙_日宝",       423, 14, 28, 18),
        new("羽化登仙/赵英",       "羽化登仙_赵英",       500, 692, 128, 18),
        new("羽化登仙/活风",       "羽化登仙_活风",       675, 95, 90, 14),
        new("羽化登仙/水天灵",     "羽化登仙_水天灵",     675, 88, 82, 14),
        new("羽化登仙/天川灵",     "羽化登仙_天川灵",     675, 98, 84, 14),
        new("羽化登仙/羽化使者",   "羽化登仙_羽化使者",   312, 168, 340, 14),
        new("羽化登仙/武陵公告板", "羽化登仙_武陵公告板", 675, 93, 88, 45),
        new("羽化登仙/猿猴王",     "羽化登仙_猿猴王",     675, 97, 93, 9),
    };

    /// <summary>
    /// 12 环主线 + 1 条重复任务（冰寒地狱）。顺序即链序，写入时自动串 RequiredQuest。
    /// </summary>
    private static List<QuestDef> BuildQuests() => new()
    {
        new("humup01", "泥色的旧书",           QuestGroup, 60, false),
        new("humup02", "古书的秘密",           QuestGroup, 60, false),
        new("humup03", "再访破关虎",           QuestGroup, 60, false),
        new("humup04", "去见日宝",             QuestGroup, 60, false),
        new("humup05", "日宝归来",             QuestGroup, 60, false),
        new("humup06", "工匠的纪念藏品",       QuestGroup, 60, false),
        new("humup07", "工匠的资质考验",       QuestGroup, 60, false),
        new("humup08", "前往武陵桃源",         QuestGroup, 60, false),
        new("humup09", "为天守解除封印",       QuestGroup, 60, false),
        new("humup10", "为神兽解除封印",       QuestGroup, 60, false),
        new("humup11", "羽化登仙",             QuestGroup, 60, false),
        new("humup12", "驯服神兽",             QuestGroup, 60, false),
        new("humup_rep", "净化冰寒地狱",       QuestGroup, 60, true),
    };

    // ───────────────────────────── 写入 ─────────────────────────────

    private static void WriteItems(Envir envir, bool apply)
    {
        var defs = BuildItems();
        int maxIndex = envir.ItemInfoList.Where(i => i != null).Select(i => i.Index).DefaultIfEmpty(0).Max();
        if (envir.ItemIndex < maxIndex) envir.ItemIndex = maxIndex;

        Console.WriteLine();
        Console.WriteLine("==== 任务物品 ====");
        Console.WriteLine($"{"状态",-6}{"Index",-8}{"名称",-18}{"Image",-8}{"Stack",-7}{"绑定"}");

        int next = envir.ItemIndex;
        var toAdd = new List<(ItemDef Def, ItemInfo Info)>();

        foreach (var d in defs)
        {
            var exist = envir.ItemInfoList.FirstOrDefault(x => x != null && x.Name == d.Name);
            if (exist != null)
            {
                Console.WriteLine($"{"更新",-6}{exist.Index,-8}{d.Name,-18}{d.Image,-8}{d.StackSize,-7}{d.Bind}");
                if (apply)
                {
                    exist.Image = d.Image;
                    exist.StackSize = d.StackSize;
                    exist.Price = d.Price;
                    exist.Bind = d.Bind;
                    exist.Type = ItemType.任务物品;
                    exist.Weight = 0;
                    exist.Durability = 0;
                    exist.RequiredAmount = 0;
                }
                continue;
            }

            next++;
            Console.WriteLine($"{"新增",-6}{next,-8}{d.Name,-18}{d.Image,-8}{d.StackSize,-7}{d.Bind}");
            toAdd.Add((d, null));
        }

        if (!apply) return;

        if (toAdd.Count > 0) Backup(envir);

        foreach (var (d, _) in toAdd)
        {
            var info = new ItemInfo
            {
                Index = ++envir.ItemIndex,
                Name = d.Name,
                Type = ItemType.任务物品,
                Grade = ItemGrade.普通,
                RequiredType = RequiredType.Level,
                RequiredClass = RequiredClass.全职业,
                RequiredGender = RequiredGender.性别不限,
                Set = ItemSet.非套装,
                Shape = 0,
                Weight = 0,
                Light = 0,
                RequiredAmount = 0,
                Image = d.Image,
                Durability = 0,
                Price = d.Price,
                StackSize = d.StackSize,
                Bind = d.Bind,
                RandomStatsId = 255,
            };
            envir.ItemInfoList.Add(info);
        }

        envir.SaveDB();
        Console.WriteLine($"物品已保存，现有 {envir.ItemInfoList.Count} 个。");
    }

    private static void WriteNpcs(Envir envir, bool apply)
    {
        var defs = BuildNpcs();
        int maxIndex = envir.NPCInfoList.Where(i => i != null).Select(i => i.Index).DefaultIfEmpty(0).Max();
        if (envir.NPCIndex < maxIndex) envir.NPCIndex = maxIndex;

        Console.WriteLine();
        Console.WriteLine("==== 任务 NPC ====");
        Console.WriteLine($"{"状态",-6}{"Index",-8}{"名称",-18}{"地图",-8}{"坐标",-12}脚本");

        int next = envir.NPCIndex;
        var toAdd = new List<NpcDef>();

        foreach (var d in defs)
        {
            var exist = envir.NPCInfoList.FirstOrDefault(x => x != null && x.FileName == d.File);
            var map = envir.MapInfoList.FirstOrDefault(m => m != null && m.Index == d.MapIndex);
            string mapName = map == null ? "!!地图不存在" : map.Title;

            if (exist != null)
            {
                Console.WriteLine($"{"更新",-6}{exist.Index,-8}{d.Name,-18}{mapName,-8}{$"{d.X},{d.Y}",-12}{d.File}");
                if (apply)
                {
                    exist.Name = d.Name;
                    exist.MapIndex = d.MapIndex;
                    exist.Location = new System.Drawing.Point(d.X, d.Y);
                    exist.Image = d.Image;
                }
                continue;
            }

            next++;
            Console.WriteLine($"{"新增",-6}{next,-8}{d.Name,-18}{mapName,-8}{$"{d.X},{d.Y}",-12}{d.File}");
            toAdd.Add(d);
        }

        if (!apply) return;

        if (toAdd.Count > 0) Backup(envir);

        foreach (var d in toAdd)
        {
            var info = new NPCInfo
            {
                Index = ++envir.NPCIndex,
                FileName = d.File,
                Name = d.Name,
                MapIndex = d.MapIndex,
                Location = new System.Drawing.Point(d.X, d.Y),
                Image = d.Image,
                Rate = 100,
                ShowOnBigMap = false,
                BigMapIcon = 0,
                CanTeleportTo = false,
                ConquestVisible = true,
            };
            envir.NPCInfoList.Add(info);
        }

        envir.SaveDB();
        Console.WriteLine($"NPC 已保存，现有 {envir.NPCInfoList.Count} 个。");
    }

    private static void WriteQuests(Envir envir, bool apply)
    {
        var defs = BuildQuests();
        int maxIndex = envir.QuestInfoList.Where(i => i != null).Select(i => i.Index).DefaultIfEmpty(0).Max();
        if (envir.QuestIndex < maxIndex) envir.QuestIndex = maxIndex;

        Console.WriteLine();
        Console.WriteLine("==== 任务 ====");
        Console.WriteLine($"{"状态",-6}{"Index",-8}{"FileName",-12}{"名称",-20}{"前置",-8}{"等级",-6}类型");

        int prevIndex = 0;
        int cursor = envir.QuestIndex;
        var result = new List<(QuestDef Def, int Index, int Prev)>();

        foreach (var d in defs)
        {
            var exist = envir.QuestInfoList.FirstOrDefault(x => x != null && x.FileName == d.File);
            int index = exist?.Index ?? (cursor + 1);
            if (exist == null) cursor++;
            string state = exist != null ? "更新" : "新增";

            result.Add((d, index, prevIndex));
            Console.WriteLine($"{state,-6}{index,-8}{d.File,-12}{d.Name,-20}{prevIndex,-8}{d.MinLevel,-6}{(d.Repeat ? "重复" : "一般")}");

            if (!d.Repeat) prevIndex = index;
        }

        if (!apply) return;

        envir.QuestIndex = Math.Max(envir.QuestIndex, cursor);

        Backup(envir);

        foreach (var (d, index, prev) in result)
        {
            var exist = envir.QuestInfoList.FirstOrDefault(x => x != null && x.FileName == d.File);
            if (exist == null)
            {
                exist = new QuestInfo { Index = index };
                envir.QuestInfoList.Add(exist);
            }

            exist.Name = d.Name;
            exist.Group = d.Group;
            exist.FileName = d.File;
            exist.RequiredMinLevel = d.MinLevel;
            exist.RequiredMaxLevel = ushort.MaxValue;
            exist.RequiredQuest = d.Repeat ? 0 : prev;
            exist.RequiredClass = RequiredClass.全职业;
            exist.Type = d.Repeat ? QuestType.重复 : QuestType.一般;
            exist.TimeLimitInSeconds = 0;
        }

        envir.SaveDB();
        Console.WriteLine($"任务已保存，现有 {envir.QuestInfoList.Count} 条。");

        // 给 NPC 脚本用的索引表
        string mapPath = Path.Combine(AppContext.BaseDirectory, "quests_index.txt");
        File.WriteAllText(mapPath, string.Join(Environment.NewLine,
            result.Select(r => $"{r.Def.File}\t{r.Index}\t{r.Def.Name}\t前置={r.Prev}")), new UTF8Encoding(false));
        Console.WriteLine($"索引表：{mapPath}");
    }

    private static void Backup(Envir envir)
    {
        string bak = Envir.DatabasePath + ".bak_humupquest_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (!File.Exists(bak))
        {
            File.Copy(Envir.DatabasePath, bak, true);
            Console.WriteLine($"已备份 → {Path.GetFileName(bak)}");
        }
    }

    // ───────────────────────────── 只读辅助 ─────────────────────────────

    private static void VerifyQuests(Envir envir)
    {
        Console.WriteLine();
        Console.WriteLine("==== 羽化登仙任务校验（文本解析结果） ====");

        foreach (var q in envir.QuestInfoList.Where(x => x != null && x.FileName.StartsWith("humup")).OrderBy(x => x.Index))
        {
            Console.WriteLine();
            Console.WriteLine($"[{q.Index}] {q.Name}  ({q.FileName})  等级≥{q.RequiredMinLevel}  前置={q.RequiredQuest}  类型={q.Type}");

            if (q.Description.Count == 0) Console.WriteLine("   !! Description 为空（文本未加载或路径不对）");

            if (q.KillTasks.Count == 0 && q.ItemTasks.Count == 0 && q.FlagTasks.Count == 0)
                Console.WriteLine("   条件：无（跑腿任务，接完即可交）");
            foreach (var k in q.KillTasks)
                Console.WriteLine($"   击杀：{k.Monster.Name} × {k.Count}");
            foreach (var i in q.ItemTasks)
                Console.WriteLine($"   交物：{i.Item.Name} × {i.Count}");
            foreach (var c in q.CarryItems)
                Console.WriteLine($"   携带：{c.Item.Name} × {c.Count}");

            foreach (var r in q.FixedRewards)
                Console.WriteLine($"   固定奖励：{r.Item.Name} × {r.Count}");
            foreach (var r in q.SelectRewards)
                Console.WriteLine($"   可选奖励：{r.Item.Name} × {r.Count}");
            Console.WriteLine($"   经验 {q.ExpReward} / 金币 {q.GoldReward}");
        }

        Console.WriteLine();
        Console.WriteLine("==== NPC ====");
        foreach (var n in envir.NPCInfoList.Where(x => x != null && x.FileName.StartsWith("羽化登仙/")).OrderBy(x => x.Index))
        {
            var map = envir.MapInfoList.FirstOrDefault(m => m != null && m.Index == n.MapIndex);
            Console.WriteLine($"[{n.Index}] {n.Name}  {map?.Title}({map?.FileName})  {n.Location.X},{n.Location.Y}  脚本={n.FileName}");
        }
    }

    private static void DumpMaps(Envir envir)
    {
        string outPath = Path.Combine(AppContext.BaseDirectory, "maps_dump.txt");
        var sb = new StringBuilder();

        foreach (var m in envir.MapInfoList.Where(x => x != null).OrderBy(x => x.Index))
        {
            sb.AppendFormat("[{0} {1}] file={2} safe={3} npc={4}", m.Index, m.Title, m.FileName, m.SafeZones.Count, m.NPCs.Count);
            if (m.SafeZones.Count > 0)
            {
                var s = m.SafeZones[0];
                sb.AppendFormat(" firstSafe=({0},{1}) range={2}", s.Location.X, s.Location.Y, s.Size);
            }
            sb.AppendLine();
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"地图清单已写出：{outPath}（{envir.MapInfoList.Count} 张）");
    }

    private static void SearchItems(Envir envir, string[] args)
    {
        int i = Array.IndexOf(args, "--search");
        var kws = args.Skip(i + 1).Where(a => !a.StartsWith("--")).ToList();
        if (kws.Count == 0) kws = new List<string> { "印章", "心脏", "结晶", "神石", "油", "银" };

        Console.WriteLine($"{"Index",-7}{"名称",-18}{"类型",-10}{"Image",-8}{"Stack",-7}{"Shape",-7}Price");
        foreach (string kw in kws)
        {
            Console.WriteLine($"---------- [{kw}] ----------");
            foreach (var it in envir.ItemInfoList.Where(x => x != null && x.Name.Contains(kw)).Take(12))
                Console.WriteLine($"{it.Index,-7}{it.Name,-18}{it.Type,-10}{it.Image,-8}{it.StackSize,-7}{it.Shape,-7}{it.Price}");
        }
    }

    private static void ProbeItems(Envir envir, string[] args)
    {
        int i = Array.IndexOf(args, "--probe");
        var names = args.Skip(i + 1).Where(a => !a.StartsWith("--")).ToList();
        if (names.Count == 0) names = new List<string> { "命运之书", "书页", "比奇令牌(1)" };

        foreach (string n in names)
        {
            var info = envir.ItemInfoList.FirstOrDefault(x => x != null && x.Name == n);
            if (info == null) { Console.WriteLine($"!! 找不到物品：{n}"); continue; }

            var t = info.GetType();
            Console.WriteLine($"==== {info.Name} ====");
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (f.Name == "Stats" || f.Name == "RandomStats") continue;
                object v = f.GetValue(info);
                if (v is string s && s.Length == 0) continue;
                Console.WriteLine($"  {f.Name} = {v}");
            }
            Console.WriteLine();
        }
    }
}
