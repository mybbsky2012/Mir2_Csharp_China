using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;

namespace RepairCmdTest;

/// <summary>
/// 只读验证 NPC 脚本命令 `REPAIR &lt;部位&gt; [special]`：
///   解析：部位可写中文枚举名（武器/盔甲/…）或数字 0~13；第二参数 special/特修/1 = 特修
///   执行：直接读 Info.Equipment[slot] 修满；普修扣耐久上限（缺口 1/30），特修不扣
/// 用法：RepairCmdTest.exe "E:\...\Mir2Server"，不写数据库。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        // 找一把有持久的武器当样本（只读，不会动数据库）
        ItemInfo weapon = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.武器 && x.Durability > 0);
        if (weapon == null) { Console.WriteLine("库里没有带持久的武器物品"); return 1; }
        ItemInfo armor = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.盔甲 && x.Durability > 0);

        Console.WriteLine($"[样本] 武器 [{weapon.Index}] {weapon.Name} 持久={weapon.Durability}" +
                          (armor != null ? $"  盔甲 [{armor.Index}] {armor.Name}" : string.Empty));
        Console.WriteLine();

        int fail = 0;

        // ---------- 解析 ----------
        var page = new NPCPage("[@MAIN]");
        var seg = new NPCSegment(page, new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());

        void CheckParse(string line, int expectCount, ActionType expectType, string expectSlot, string expectArg)
        {
            var acts = new List<NPCActions>();
            seg.ParseAct(acts, line);
            bool ok = acts.Count == expectCount;
            if (ok && expectCount > 0)
            {
                ok = acts[0].Type == expectType
                     && acts[0].Params.Count > 0 && acts[0].Params[0] == expectSlot
                     && (expectArg == null || (acts[0].Params.Count > 1 && acts[0].Params[1] == expectArg));
            }
            string got = acts.Count == 0 ? "（无）" : $"{acts[0].Type} [{string.Join(", ", acts[0].Params)}]";
            Console.WriteLine($"[解析] {line,-28} → {got}");
            if (!ok) { Console.WriteLine("       !! 期望不符"); fail++; }
        }

        CheckParse("REPAIR 武器 special", 1, ActionType.Repair, "武器", "special");
        CheckParse("REPAIR 盔甲", 1, ActionType.Repair, "盔甲", "");
        CheckParse("REPAIR 0", 1, ActionType.Repair, "0", "");
        CheckParse("REPAIR 13 特修", 1, ActionType.Repair, "13", "特修");
        CheckParse("REPAIR", 0, ActionType.Repair, null, null);   // 缺部位：不生成动作

        Console.WriteLine();

        // ---------- 执行 ----------
        MethodInfo actMethod = typeof(NPCSegment).GetMethod("Act", BindingFlags.NonPublic | BindingFlags.Instance,
                                                           null, new[] { typeof(IList<NPCActions>), typeof(PlayerObject) }, null);
        if (actMethod == null) { Console.WriteLine("!! 找不到 Act(IList<NPCActions>, PlayerObject)"); return 1; }

        void RunAct(PlayerObject bot, params string[] words)
        {
            var list = new List<NPCActions> { new NPCActions(ActionType.Repair, words) };
            try { actMethod.Invoke(seg, new object[] { list, bot }); }
            catch (TargetInvocationException ex) { Console.WriteLine("  [异常] " + ex.InnerException?.Message); fail++; }
        }

        // 1) 特修：12000 上限 / 3000 当前 → 修满且上限不变
        FakePlayerObject bot = NewBot();
        UserItem w = MakeItem(weapon, 70001, 12000, 3000);
        bot.Info.Equipment[(int)EquipmentSlot.武器] = w;
        RunAct(bot, "武器", "special");
        Console.WriteLine($"[1] 特修武器 12000/3000 → {w.MaxDura}/{w.CurrentDura}（期望 12000/12000）{(w.MaxDura == 12000 && w.CurrentDura == 12000 ? "✓" : "✗")}");
        if (w.MaxDura != 12000 || w.CurrentDura != 12000) fail++;

        // 2) 普修：12000 上限 / 3000 当前 → 扣缺口 1/30（300）后再修满 → 11700/11700
        FakePlayerObject bot2 = NewBot();
        UserItem w2 = MakeItem(weapon, 70002, 12000, 3000);
        bot2.Info.Equipment[(int)EquipmentSlot.武器] = w2;
        RunAct(bot2, "武器");
        Console.WriteLine($"[2] 普修武器 12000/3000 → {w2.MaxDura}/{w2.CurrentDura}（期望 11700/11700）{(w2.MaxDura == 11700 && w2.CurrentDura == 11700 ? "✓" : "✗")}");
        if (w2.MaxDura != 11700 || w2.CurrentDura != 11700) fail++;

        // 3) 数字部位 + 特修：修盔甲
        if (armor != null)
        {
            FakePlayerObject bot3 = NewBot();
            UserItem a = MakeItem(armor, 70003, 8000, 1000);
            bot3.Info.Equipment[(int)EquipmentSlot.盔甲] = a;
            RunAct(bot3, "1", "1");
            Console.WriteLine($"[3] 数字部位 1 + \"1\" 特修盔甲 8000/1000 → {a.MaxDura}/{a.CurrentDura}（期望 8000/8000）{(a.MaxDura == 8000 && a.CurrentDura == 8000 ? "✓" : "✗")}");
            if (a.MaxDura != 8000 || a.CurrentDura != 8000) fail++;
        }

        // 4) 已经满持久：不做任何改动
        FakePlayerObject bot4 = NewBot();
        UserItem w4 = MakeItem(weapon, 70004, 9000, 9000);
        bot4.Info.Equipment[(int)EquipmentSlot.武器] = w4;
        RunAct(bot4, "武器", "special");
        Console.WriteLine($"[4] 满持久 9000/9000 → {w4.MaxDura}/{w4.CurrentDura}（期望不变）{(w4.MaxDura == 9000 && w4.CurrentDura == 9000 ? "✓" : "✗")}");
        if (w4.MaxDura != 9000 || w4.CurrentDura != 9000) fail++;

        // 5) 空装备位：不崩、无副作用
        FakePlayerObject bot5 = NewBot();
        RunAct(bot5, "武器", "special");
        Console.WriteLine($"[5] 空武器位 → 未抛异常 ✓");
        if (bot5.Info.Equipment[(int)EquipmentSlot.武器] != null) fail++;

        // 6) 部位名写错：直接忽略
        FakePlayerObject bot6 = NewBot();
        UserItem w6 = MakeItem(weapon, 70006, 12000, 3000);
        bot6.Info.Equipment[(int)EquipmentSlot.武器] = w6;
        RunAct(bot6, "不存在的部位", "special");
        Console.WriteLine($"[6] 部位名写错 → {w6.MaxDura}/{w6.CurrentDura}（期望不变 12000/3000）{(w6.MaxDura == 12000 && w6.CurrentDura == 3000 ? "✓" : "✗")}");
        if (w6.MaxDura != 12000 || w6.CurrentDura != 3000) fail++;

        // 7) 修全身：把「修全身」脚本原样跑一遍，前面几件已满/空位时不能挡住后面的部位
        FakePlayerObject bot7 = NewBot();
        var allSlots = new (string Name, int Slot, ItemType Type)[]
        {
            ("武器", 0, ItemType.武器), ("盔甲", 1, ItemType.盔甲), ("头盔", 2, ItemType.头盔),
            ("项链", 4, ItemType.项链), ("左手镯", 5, ItemType.手镯), ("右手镯", 6, ItemType.手镯),
            ("左戒指", 7, ItemType.戒指), ("右戒指", 8, ItemType.戒指), ("腰带", 10, ItemType.腰带),
            ("靴子", 11, ItemType.靴子),
        };

        var placed = new Dictionary<int, UserItem>();
        int seq = 0;
        foreach (var s in allSlots)
        {
            ItemInfo sample = s.Type == ItemType.武器 ? weapon
                             : envir.ItemInfoList.FirstOrDefault(x => x.Type == s.Type && x.Durability > 0);
            if (sample == null) continue; // 库里没这类样本就跳过该部位

            // 故意造成三种情况：第 1 件已满持久、第 2 件空位、其余破损
            if (s.Slot == 0) { placed[s.Slot] = MakeItem(sample, (ulong)(71000 + seq), 10000, 10000); }
            else if (s.Slot == 1) { /* 空位，不放 */ }
            else { placed[s.Slot] = MakeItem(sample, (ulong)(71000 + seq), 10000, 2500); }
            if (placed.ContainsKey(s.Slot)) bot7.Info.Equipment[s.Slot] = placed[s.Slot];
            seq++;
        }

        // 用 ParseAct 逐行生成，模拟真实脚本的 #ACT 段
        var allActs = new List<NPCActions>();
        foreach (var s in allSlots) seg.ParseAct(allActs, $"REPAIR {s.Name} special");

        int brokenSlots = placed.Count(kv => kv.Value.CurrentDura < kv.Value.MaxDura); // 修前
        try { actMethod.Invoke(seg, new object[] { allActs, bot7 }); }
        catch (TargetInvocationException ex) { Console.WriteLine("  [异常] " + ex.InnerException?.Message); fail++; }

        int stillBroken = placed.Count(kv => kv.Key != 0 && kv.Value.CurrentDura < kv.Value.MaxDura); // 修后（武器本来满的，只看其余）
        Console.WriteLine($"[7] 修全身（{allActs.Count} 条命令）：放了 {placed.Count} 个部位、修前破损 {brokenSlots} 个 → 修后仍破损 {stillBroken} 个（期望 0）");
        if (brokenSlots == 0 || stillBroken != 0) fail++;

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过 ✓" : $"失败 {fail} 项 ✗");
        return fail == 0 ? 0 : 1;
    }

    private static UserItem MakeItem(ItemInfo info, ulong id, ushort maxDura, ushort curDura)
    {
        return new UserItem(info)
        {
            UniqueID = id,
            Count = 1,
            MaxDura = maxDura,
            CurrentDura = curDura,
        };
    }

    private static FakePlayerObject NewBot()
    {
        CharacterInfo info = new CharacterInfo
        {
            Index = 1,
            Name = "修复测试",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 45,
            HP = 500,
            MP = 200,
        };

        var bot = new FakePlayerObject(info, new BotConnection(1) { Account = new AccountInfo { Index = 1, AccountID = "repair_test" } });
        return bot;
    }
}
