using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;

namespace StoneTest;

/// <summary>
/// 只读验证气血石 / 魔血石（石头格）的新规则：
///   参数全部来自 Settings（守护石选项卡 / GemSystem.ini [Stone] 节）：
///   触发阈值%、回复间隔秒、每跳增量 = 总持久×%、每跳扣持久点 —— HP / MP 各自独立
///   加什么看外观值 Shape：1=HP 2=MP 3=HP+MP，其它不生效
///   掉到阈值以下才启动，启动后一直补到满；Shape=3 时 HP/MP 各按各的节奏跳、各扣各的持久
/// 用法：StoneTest.exe "E:\...\Mir2Server"，不写数据库。
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

        ItemInfo info = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.气血石)
                     ?? envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.魔血石);
        if (info == null) { Console.WriteLine("没有气血石/魔血石物品"); return 1; }

        Console.WriteLine($"[物品] [{info.Index}] {info.Name} 类型={info.Type} 持久={info.Durability} 当前Shape={info.Shape}");
        Console.WriteLine();

        MethodInfo ps = typeof(PlayerObject).GetMethod("ProcessStones", BindingFlags.NonPublic | BindingFlags.Instance);
        if (ps == null) { Console.WriteLine("!! 找不到 ProcessStones"); return 1; }

        FieldInfo timeField = typeof(Envir).GetField("<Time>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (timeField == null) { Console.WriteLine("!! 找不到 Envir.Time"); return 1; }
        long clock = 100000;
        timeField.SetValue(envir, clock);

        // 固定测试配置（线上 ini 的实际值会变，期望值必须基于已知配置）
        Settings.StoneHPTriggerPercent = 50; Settings.StoneHPDelay = 4;
        Settings.StoneHPGainPercent = 1;     Settings.StoneHPDuraCost = 100;
        Settings.StoneMPTriggerPercent = 50; Settings.StoneMPDelay = 4;
        Settings.StoneMPGainPercent = 1;     Settings.StoneMPDuraCost = 100;

        Console.WriteLine($"[配置] HP: 阈值{Settings.StoneHPTriggerPercent}% 间隔{Settings.StoneHPDelay}s 增量{Settings.StoneHPGainPercent}% 耗电{Settings.StoneHPDuraCost} | MP: 阈值{Settings.StoneMPTriggerPercent}% 间隔{Settings.StoneMPDelay}s 增量{Settings.StoneMPGainPercent}% 耗电{Settings.StoneMPDuraCost}");
        Console.WriteLine();

        int slot = (int)EquipmentSlot.守护石;
        short savedShape = info.Shape;
        int fail = 0;
        int fixedAmount = Math.Max(1, info.Durability / 100); // 固定回复量 = 物品满持久的 1%

        // 1) Shape=1：只回血，量 = 满持久 1%，扣 100
        info.Shape = 1;
        FakePlayerObject bot = NewBot(1000, 1000, 400, 400);
        UserItem stone = MakeStone(info, 90001, 60000);
        bot.Info.Equipment[slot] = stone;
        int hp = Invoke(bot, ps, out int mp, out string err);
        Console.WriteLine($"[1] Shape=1 满持久{info.Durability} → 回血 {hp}(期望{fixedAmount}) 回蓝 {mp}(期望0) 持久 {stone.CurrentDura}(期望59900){err}");
        if (hp != fixedAmount || mp != 0 || stone.CurrentDura != 59900) fail++;

        // 2) Shape=2：只回蓝
        info.Shape = 2;
        FakePlayerObject bot2 = NewBot(1000, 1000, 400, 400);
        UserItem stone2 = MakeStone(info, 90002, 60000);
        bot2.Info.Equipment[slot] = stone2;
        hp = Invoke(bot2, ps, out mp, out err);
        Console.WriteLine($"[2] Shape=2 → 回血 {hp}(期望0) 回蓝 {mp}(期望{fixedAmount}) 持久 {stone2.CurrentDura}(期望59900){err}");
        if (hp != 0 || mp != fixedAmount || stone2.CurrentDura != 59900) fail++;

        // 3) Shape=3：两样都回，持久只扣一次
        info.Shape = 3;
        FakePlayerObject bot3 = NewBot(1000, 1000, 400, 400);
        UserItem stone3 = MakeStone(info, 90003, 60000);
        bot3.Info.Equipment[slot] = stone3;
        hp = Invoke(bot3, ps, out mp, out err);
        Console.WriteLine($"[3] Shape=3 → 回血 {hp} 回蓝 {mp}（期望 {fixedAmount}/{fixedAmount}）持久 {stone3.CurrentDura}(期望59800，HP/MP各扣各的){err}");
        if (hp != fixedAmount || mp != fixedAmount || stone3.CurrentDura != 59800) fail++;

        // 4) Shape=0：不生效，不扣持久
        info.Shape = 0;
        FakePlayerObject bot4 = NewBot(1000, 1000, 400, 400);
        UserItem stone4 = MakeStone(info, 90004, 60000);
        bot4.Info.Equipment[slot] = stone4;
        hp = Invoke(bot4, ps, out mp, out err);
        Console.WriteLine($"[4] Shape=0 → 回血 {hp}(期望0) 回蓝 {mp}(期望0) 持久不变={stone4.CurrentDura == 60000}{err}");
        if (hp != 0 || mp != 0 || stone4.CurrentDura != 60000) fail++;

        // 5) 50% 门槛：60% 不启动
        info.Shape = 3;
        FakePlayerObject bot5 = NewBot(1000, 1000, 600, 600);
        UserItem stone5 = MakeStone(info, 90005, 60000);
        bot5.Info.Equipment[slot] = stone5;
        hp = Invoke(bot5, ps, out mp, out err);
        Console.WriteLine($"[5] HP/MP 都 60% → 回血 {hp}(期望0) 回蓝 {mp}(期望0) 持久不变={stone5.CurrentDura == 60000}{err}");
        if (hp != 0 || mp != 0 || stone5.CurrentDura != 60000) fail++;

        // 6) 40% 启动后，中途超过一半仍继续，直到满
        FakePlayerObject bot6 = NewBot(1000, 1000, 400, 1000);
        UserItem stone6 = MakeStone(info, 90006, 40000); // 电量 40000，但每跳仍是固定的 fixedAmount
        bot6.Info.Equipment[slot] = stone6;
        var marks = new List<string>();
        for (int i = 0; i < 5 && bot6.HP < bot6.Stats[Stat.HP]; i++)
        {
            int g = Invoke(bot6, ps, out int m, out err);
            bot6.HP = Math.Min(bot6.Stats[Stat.HP], bot6.HP + g);
            bot6.MP = Math.Min(bot6.Stats[Stat.MP], bot6.MP + m);
            marks.Add($"{bot6.HP}");
            if (g == 0 && m == 0) break;
        }
        Console.WriteLine($"[6] 40% 启动、每跳 {fixedAmount} → HP 轨迹 {string.Join(" → ", marks)}（期望补到 1000 满）{err}");
        if (bot6.HP != 1000) fail++;

        // 7) 固定值：回复量按「满持久」算，当前持久只剩 5000 也照样回 满持久1%
        info.Shape = 1;
        FakePlayerObject bot7 = NewBot(1000, 1000, 400, 400);
        UserItem stone7 = MakeStone(info, 90007, 5000);
        bot7.Info.Equipment[slot] = stone7;
        hp = Invoke(bot7, ps, out mp, out err);
        Console.WriteLine($"[7] 满持久{info.Durability}、当前只5000 → 回血 {hp}(期望{fixedAmount}，与持久无关) 持久 {stone7.CurrentDura}(期望4900){err}");
        if (hp != fixedAmount || stone7.CurrentDura != 4900) fail++;

        // 7b) 同一颗石头，电量快耗尽时回复量仍不变（旧规则会衰减）
        FakePlayerObject bot7b = NewBot(1000, 1000, 400, 400);
        UserItem stone7b = MakeStone(info, 90017, 300);
        bot7b.Info.Equipment[slot] = stone7b;
        int gainLow = Invoke(bot7b, ps, out mp, out err);
        Console.WriteLine($"[7b] 同满持久、当前只剩300 → 回血 {gainLow}(期望{fixedAmount}，无衰减) 持久 {stone7b.CurrentDura}(期望200){err}");
        if (gainLow != fixedAmount || stone7b.CurrentDura != 200) fail++;

        // 8) 电量不足一跳（50 < 100）仍先回复，然后耗尽消失
        FakePlayerObject bot8 = NewBot(1000, 1000, 400, 400);
        UserItem stone8 = MakeStone(info, 90008, 50);
        bot8.Info.Equipment[slot] = stone8;
        hp = Invoke(bot8, ps, out mp, out err);
        Console.WriteLine($"[8] 电量50 → 回血 {hp}(期望{fixedAmount}) 持久 {stone8.CurrentDura}(期望0，石头消失={bot8.Info.Equipment[slot] == null}){err}");
        if (hp != fixedAmount || bot8.Info.Equipment[slot] != null) fail++;

        // 9) 装石头不污染最大 HP/MP
        FakePlayerObject bot9 = NewBot(1000, 1000, 400, 400);
        bot9.RefreshStats();
        int hpBefore = bot9.Stats[Stat.HP], mpBefore = bot9.Stats[Stat.MP];
        bot9.Info.Equipment[slot] = MakeStone(info, 90009, 60000);
        bot9.RefreshStats();
        Console.WriteLine($"[9] 装石头前后最大 HP {hpBefore}→{bot9.Stats[Stat.HP]}，最大 MP {mpBefore}→{bot9.Stats[Stat.MP]}（应相同）");
        if (bot9.Stats[Stat.HP] != hpBefore || bot9.Stats[Stat.MP] != mpBefore) fail++;

        // 10) 续航
        long jumps = info.Durability / Math.Max(1, Settings.StoneHPDuraCost);
        Console.WriteLine($"[10] 持久 {info.Durability} / 每跳 {Settings.StoneHPDuraCost} → {jumps} 跳 × {Settings.StoneHPDelay} 秒 ≈ {jumps * Settings.StoneHPDelay / 60:F0} 分钟");

        // 11) 配置生效：改 Settings 后行为跟着变
        info.Shape = 1;
        Settings.StoneHPTriggerPercent = 30;
        Settings.StoneHPDelay = 2;
        Settings.StoneHPGainPercent = 5;
        Settings.StoneHPDuraCost = 7;
        FakePlayerObject bot11 = NewBot(1000, 1000, 700, 700); // 70% > 30% → 不启动
        UserItem stone11 = MakeStone(info, 90011, 60000);
        bot11.Info.Equipment[slot] = stone11;
        hp = Invoke(bot11, ps, out mp, out err);
        Console.WriteLine($"[11a] 阈值30%、HP=70% → 回血 {hp}(期望0) 持久不变={stone11.CurrentDura == 60000}{err}");
        if (hp != 0 || stone11.CurrentDura != 60000) fail++;

        bot11.HP = 250; // 25% < 30% → 启动，每跳 = 60000×5% = 3000，扣 7
        hp = Invoke(bot11, ps, out mp, out err);
        Console.WriteLine($"[11b] HP=25% 启动 → 回血 {hp}(期望3000) 持久 {stone11.CurrentDura}(期望59993){err}");
        if (hp != 3000 || stone11.CurrentDura != 59993) fail++;

        // 恢复默认（12~17 按默认配置断言）
        Settings.StoneHPTriggerPercent = 50;
        Settings.StoneHPDelay = 4;
        Settings.StoneHPGainPercent = 1;
        Settings.StoneHPDuraCost = 100;

        // 12) 允许背包内使用：不佩戴时自动用背包里的石头（Shape=3 双回各扣各的）
        info.Shape = 3;
        Settings.StoneAllowBagUse = true;
        FakePlayerObject bot12 = NewBot(1000, 1000, 400, 400);
        UserItem bagStone12 = MakeStone(info, 90012, 60000);
        bot12.Info.Inventory[10] = bagStone12;
        hp = Invoke(bot12, ps, out mp, out err);
        Console.WriteLine($"[12] 未佩戴、背包有石头 → 回血 {hp}(期望{fixedAmount}) 回蓝 {mp}(期望{fixedAmount}) 持久 {bagStone12.CurrentDura}(期望59800){err}");
        if (hp != fixedAmount || mp != fixedAmount || bagStone12.CurrentDura != 59800) fail++;

        // 13) 关闭开关：背包里的石头不生效
        Settings.StoneAllowBagUse = false;
        FakePlayerObject bot13 = NewBot(1000, 1000, 400, 400);
        UserItem bagStone13 = MakeStone(info, 90013, 60000);
        bot13.Info.Inventory[10] = bagStone13;
        hp = Invoke(bot13, ps, out mp, out err);
        Console.WriteLine($"[13] 开关关闭 → 回血 {hp}(期望0) 回蓝 {mp}(期望0) 持久不变={bagStone13.CurrentDura == 60000}{err}");
        if (hp != 0 || mp != 0 || bagStone13.CurrentDura != 60000) fail++;

        // 14) 开关开但背包没石头 → 不生效
        Settings.StoneAllowBagUse = true;
        FakePlayerObject bot14 = NewBot(1000, 1000, 400, 400);
        hp = Invoke(bot14, ps, out mp, out err);
        Console.WriteLine($"[14] 开关开、背包无石头 → 回血 {hp}(期望0) 回蓝 {mp}(期望0){err}");
        if (hp != 0 || mp != 0) fail++;

        // 15) 背包多块：没电的跳过，取第一块有电量的
        FakePlayerObject bot15 = NewBot(1000, 1000, 400, 400);
        UserItem deadStone = MakeStone(info, 90015, 0);
        UserItem aliveStone = MakeStone(info, 90016, 60000);
        bot15.Info.Inventory[7] = deadStone;
        bot15.Info.Inventory[10] = aliveStone;
        hp = Invoke(bot15, ps, out mp, out err);
        Console.WriteLine($"[15] 没电的跳过 → 回血 {hp}(期望{fixedAmount}) 没电块不变={deadStone.CurrentDura == 0} 有电块扣电={aliveStone.CurrentDura == 59800}{err}");
        if (hp != fixedAmount || deadStone.CurrentDura != 0 || aliveStone.CurrentDura != 59800) fail++;

        // 16) 装备槽优先：佩戴 + 背包都有 → 只消耗佩戴的
        FakePlayerObject bot16 = NewBot(1000, 1000, 400, 400);
        UserItem equipStone16 = MakeStone(info, 90018, 60000);
        UserItem bagStone16 = MakeStone(info, 90019, 60000);
        bot16.Info.Equipment[slot] = equipStone16;
        bot16.Info.Inventory[10] = bagStone16;
        hp = Invoke(bot16, ps, out mp, out err);
        Console.WriteLine($"[16] 佩戴优先 → 佩戴块扣电={equipStone16.CurrentDura == 59800} 背包块不变={bagStone16.CurrentDura == 60000}{err}");
        if (equipStone16.CurrentDura != 59800 || bagStone16.CurrentDura != 60000) fail++;

        // 17) 背包石头耗尽自动移除
        FakePlayerObject bot17 = NewBot(1000, 1000, 400, 400);
        UserItem bagStone17 = MakeStone(info, 90020, 50);   // 电量不足一跳
        bot17.Info.Inventory[10] = bagStone17;
        hp = Invoke(bot17, ps, out mp, out err);
        Console.WriteLine($"[17] 背包块电量50耗尽 → 回血 {hp}(期望{fixedAmount}) 背包移除={bot17.Info.Inventory[10] == null}{err}");
        if (hp != fixedAmount || bot17.Info.Inventory[10] != null) fail++;

        // 恢复默认
        Settings.StoneHPTriggerPercent = 50;
        Settings.StoneHPDelay = 4;
        Settings.StoneHPGainPercent = 1;
        Settings.StoneHPDuraCost = 100;
        Settings.StoneAllowBagUse = false;

        info.Shape = savedShape;
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过 ✓" : $"失败 {fail} 项 ✗");
        return fail == 0 ? 0 : 1;
    }

    private static int Invoke(PlayerObject bot, MethodInfo m, out int mpGain, out string err)
    {
        // 推进时钟到下一跳（间隔取 HP/MP 配置较大者 + 1 秒）
        FieldInfo timeField = typeof(Envir).GetField("<Time>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        long now = (long)timeField.GetValue(Envir.Main);
        int step = (Math.Max(Settings.StoneHPDelay, Settings.StoneMPDelay) + 1) * 1000;
        timeField.SetValue(Envir.Main, now + step);

        object[] p = new object[] { 0, 0 };
        err = string.Empty;
        try
        {
            m.Invoke(bot, p);
        }
        catch (TargetInvocationException ex)
        {
            err = "  [异常] " + ex.InnerException?.Message;
        }
        mpGain = (int)p[1];
        return (int)p[0];
    }

    private static UserItem MakeStone(ItemInfo info, ulong id, ushort dura)
    {
        return new UserItem(info)
        {
            UniqueID = id,
            Count = 1,
            MaxDura = info.Durability,
            CurrentDura = dura,
        };
    }

    private static FakePlayerObject NewBot(int maxHp, int maxMp, int hp, int mp)
    {
        CharacterInfo info = new CharacterInfo
        {
            Index = 1,
            Name = "石头测试",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 45,
            HP = hp,
            MP = mp,
        };

        var bot = new FakePlayerObject(info, new BotConnection(1) { Account = new AccountInfo { Index = 1, AccountID = "stone_test" } });
        bot.Stats[Stat.HP] = maxHp;
        bot.Stats[Stat.MP] = maxMp;
        bot.HP = hp;
        bot.MP = mp;
        return bot;
    }
}
