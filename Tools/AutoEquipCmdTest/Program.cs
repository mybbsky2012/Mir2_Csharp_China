using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace AutoEquipCmdTest;

/// <summary>
/// 自动穿戴/脱下/背包空格 NPC 命令模拟测试（只读，不写库）。
///
/// 用法：AutoEquipCmdTest.exe "D:\Mirserver"
///
/// 覆盖：
///   A. 解析：GIVEW / GIVEWHERO / TAKEW / TAKEWHERO / CHECKBAG [HERO] / CHECKHEROBAG（含老命令回归）
///   B. 参数解析：TryResolveSlotOrType（槽位名/数字/类型名/无效名）
///   C. 穿戴核心：NpcGiveW（无参穿全部 / 类型过滤 / 槽位过滤 / 槽位被占拒绝）
///   D. 脱下核心：NpcTakeW（全身脱光 / 类型 / 槽位 / 背包不足整单拒绝 / 诅咒与结婚戒指跳过）
///   E. 英雄版：NpcGiveW+NpcTakeW 打在英雄身上（英雄背包进出）
///   F. CHECKBAG / CHECKHEROBAG 执行链路（seg.Check(player) 真跑）
/// </summary>
internal static class Program
{
    private static Envir _envir;
    private static int _uid = 900001;
    private static int _pass, _fail;

    private static ItemInfo _iArmour, _iWeapon, _iBracelet, _iRing, _iMisc;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                     ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));
        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        _envir = Envir.Main;
        if (!_envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        // BUFF 注册表：正常由 StartEnvir 灌，测试里手工补（GetBuffInfo 找不到会抛异常）
        _envir.BuffInfoList.Clear();
        foreach (var info in BuffInfo.Load()) _envir.BuffInfoList.Add(info);

        RegisterTestItems();

        ParseTests();
        Console.WriteLine();
        var (p, hero, conn) = BuildPlayerAndHero("穿脱测试主角", "穿脱测试英雄");
        ResolveTests();
        Console.WriteLine();
        EquipTests(p);
        Console.WriteLine();
        UnequipTests(p, conn);
        Console.WriteLine();
        HeroTests(p, hero);
        Console.WriteLine();
        CheckExecTests(p);

        Console.WriteLine();
        Console.WriteLine($"==== 结果：通过 {_pass} / 失败 {_fail} ====");
        return _fail == 0 ? 0 : 1;
    }

    private static void RegisterTestItems()
    {
        // 全部按战士/男性/等级1 设计——60 级战士男角色与 30 级战士男英雄都能穿
        _iArmour = new ItemInfo { Index = 991001, Name = "测试布衣(男战)", Type = ItemType.盔甲, Weight = 1, RequiredType = RequiredType.Level, RequiredAmount = 1, RequiredClass = RequiredClass.战士, RequiredGender = RequiredGender.男性, Durability = 1000 };
        _iWeapon = new ItemInfo { Index = 991002, Name = "测试木剑", Type = ItemType.武器, Weight = 1, RequiredType = RequiredType.Level, RequiredAmount = 1, RequiredClass = RequiredClass.战士, RequiredGender = RequiredGender.男性, Durability = 1000 };
        _iBracelet = new ItemInfo { Index = 991003, Name = "测试手镯", Type = ItemType.手镯, Weight = 1, RequiredType = RequiredType.Level, RequiredAmount = 1, RequiredClass = RequiredClass.战士, RequiredGender = RequiredGender.男性, Durability = 1000 };
        _iRing = new ItemInfo { Index = 991004, Name = "测试戒指", Type = ItemType.戒指, Weight = 1, RequiredType = RequiredType.Level, RequiredAmount = 1, RequiredClass = RequiredClass.战士, RequiredGender = RequiredGender.男性, Durability = 1000 };
        _iMisc = new ItemInfo { Index = 991005, Name = "测试杂物(不可穿)", Type = ItemType.杂物, Weight = 1, Durability = 100 };
        _envir.ItemInfoList.Add(_iArmour);
        _envir.ItemInfoList.Add(_iWeapon);
        _envir.ItemInfoList.Add(_iBracelet);
        _envir.ItemInfoList.Add(_iRing);
        _envir.ItemInfoList.Add(_iMisc);
    }

    // ---------- A. 解析测试 ----------

    private static void ParseTests()
    {
        Console.WriteLine("==== A. NPC 脚本命令解析 ====");
        var page = new NPCPage("@equiptest");
        var seg = new NPCSegment(page, new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());

        void CheckAct(string line, ActionType type, params string[] ps)
        {
            seg.ActList.Clear();
            seg.ParseAct(seg.ActList, line);
            var a = seg.ActList.FirstOrDefault();
            bool ok = a != null && a.Type == type && a.Params.Count == ps.Length;
            if (ok) for (int i = 0; i < ps.Length; i++) ok &= a.Params[i] == ps[i];
            Report($"解析 {line}", ok, a == null ? "(无动作)" : $"{a.Type}[{string.Join(",", a.Params)}]");
        }

        void CheckChk(string line, CheckType type, params string[] ps)
        {
            seg.CheckList.Clear();
            seg.ParseCheck(line);
            var c = seg.CheckList.FirstOrDefault();
            bool ok = c != null && c.Type == type && c.Params.Count == ps.Length;
            if (ok) for (int i = 0; i < ps.Length; i++) ok &= c.Params[i] == ps[i];
            Report($"解析 {line}", ok, c == null ? "(无条件)" : $"{c.Type}[{string.Join(",", c.Params)}]");
        }

        CheckChk("CHECKBAG", CheckType.CheckBagSpace, ">=", "1", "");
        CheckChk("CHECKBAG 5", CheckType.CheckBagSpace, ">=", "5", "");
        CheckChk("CHECKBAG >= 3", CheckType.CheckBagSpace, ">=", "3", "");
        CheckChk("CHECKBAG < 2", CheckType.CheckBagSpace, "<", "2", "");
        CheckChk("CHECKBAG HERO", CheckType.CheckBagSpace, ">=", "1", "HERO");
        CheckChk("CHECKBAG HERO 2", CheckType.CheckBagSpace, ">=", "2", "HERO");
        CheckChk("CHECKBAG HERO <= 10", CheckType.CheckBagSpace, "<=", "10", "HERO");
        CheckChk("CHECKHEROBAG", CheckType.CheckHeroBagSpace, ">=", "1");
        CheckChk("CHECKHEROBAG 4", CheckType.CheckHeroBagSpace, ">=", "4");
        CheckChk("CHECKHEROBAG < 3", CheckType.CheckHeroBagSpace, "<", "3");

        CheckAct("GIVEW", ActionType.GiveW, "");
        CheckAct("GIVEW 武器", ActionType.GiveW, "武器");
        CheckAct("GIVEW 0", ActionType.GiveW, "0");
        CheckAct("GIVEWHERO 手镯", ActionType.GiveWHero, "手镯");
        CheckAct("TAKEW", ActionType.TakeW, "");
        CheckAct("TAKEW 戒指", ActionType.TakeW, "戒指");
        CheckAct("TAKEWHERO 盔甲", ActionType.TakeWHero, "盔甲");

        // 老命令回归（不受影响）
        CheckAct("EQUIPITEM 测试木剑", ActionType.EquipItem, "测试木剑", "");
        CheckAct("UNEQUIPITEM 武器", ActionType.UnequipItem, "武器");
        CheckAct("RESETHUMUP 1", ActionType.HumUpReset, "1");
        CheckChk("CHECKHUMUP >= 3", CheckType.CheckHumUp, ">=", "3");
        CheckChk("CHECKEMPTYEQUIP", CheckType.CheckEmptyEquip);
    }

    // ---------- B. 参数解析 ----------

    private static void ResolveTests()
    {
        Console.WriteLine("==== B. 参数解析（TryResolveSlotOrType）====");
        var mi = typeof(NPCSegment).GetMethod("TryResolveSlotOrType", BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null) { Report("找到 TryResolveSlotOrType", false, "反射未命中"); return; }

        object[] Invoke(string text)
        {
            var args = new object[] { text, (int)-1, (ItemType?)null };
            mi.Invoke(null, args);
            return args;
        }

        var r1 = Invoke("");
        Report("空参 = 全部（槽位-1 类型null）", (int)r1[1] == -1 && r1[2] == null, "");
        var r2 = Invoke("武器");
        Report("「武器」→ 槽位0", (int)r2[1] == 0 && r2[2] == null, $"slot={r2[1]}");
        var r3 = Invoke("0");
        Report("「0」→ 槽位0（数字）", (int)r3[1] == 0, $"slot={r3[1]}");
        var r4 = Invoke("13");
        Report("「13」→ 槽位13（坐骑）", (int)r4[1] == 13, $"slot={r4[1]}");
        var r5 = Invoke("手镯");
        Report("「手镯」→ 类型手镯（槽位-1）", r5[2] is ItemType it && it == ItemType.手镯 && (int)r5[1] == -1, $"type={r5[2]}");
        var r6 = Invoke("戒指");
        Report("「戒指」→ 类型戒指", r6[2] is ItemType it2 && it2 == ItemType.戒指, $"type={r6[2]}");
        var r7 = Invoke("垃圾名");
        Report("「垃圾名」→ 解析失败", (int)r7[1] == -1 && r7[2] == null, "");
    }

    // ---------- C. 穿戴核心 ----------

    private static void ClearAll(PlayerObject p)
    {
        for (int i = 0; i < p.Info.Equipment.Length; i++) p.Info.Equipment[i] = null;
        for (int i = 0; i < p.Info.Inventory.Length; i++) p.Info.Inventory[i] = null;
    }

    private static void EquipTests(PlayerObject p)
    {
        Console.WriteLine("==== C. 穿戴核心（NpcGiveW）====");

        // C1 无参穿全部
        ClearAll(p);
        p.Info.Inventory[0] = MakeItem(_iArmour);
        p.Info.Inventory[1] = MakeItem(_iWeapon);
        p.Info.Inventory[2] = MakeItem(_iMisc);
        int c1 = NPCSegment.NpcGiveW(p, p, MirGridType.Inventory, null, -1, out string f1);
        Report("无参穿全部：穿上 2 件（杂物不穿）", c1 == 2, $"count={c1} fail={f1}");
        Report("武器进槽0、盔甲进槽1", p.Info.Equipment[0] != null && p.Info.Equipment[0].Info.Type == ItemType.武器
            && p.Info.Equipment[1] != null && p.Info.Equipment[1].Info.Type == ItemType.盔甲, "");
        Report("杂物留在背包", p.Info.Inventory.ContainsItem(_iMisc), "");
        Report("穿过的背包格已清空", p.Info.Inventory[0] == null && p.Info.Inventory[1] == null, "");

        // C2 类型过滤（手镯自动分配左右槽）
        ClearAll(p);
        p.Info.Inventory[0] = MakeItem(_iBracelet);
        p.Info.Inventory[1] = MakeItem(_iBracelet);
        int c2 = NPCSegment.NpcGiveW(p, p, MirGridType.Inventory, ItemType.手镯, -1, out _);
        Report("类型过滤手镯：穿上 2 件", c2 == 2, $"count={c2}");
        Report("自动分配槽5(左)槽6(右)", p.Info.Equipment[5] != null && p.Info.Equipment[6] != null, "");

        // C3 戒指进左戒指槽
        ClearAll(p);
        p.Info.Inventory[3] = MakeItem(_iRing);
        int c3 = NPCSegment.NpcGiveW(p, p, MirGridType.Inventory, ItemType.戒指, -1, out _);
        Report("类型过滤戒指：进槽7(左戒指)", c3 == 1 && p.Info.Equipment[7] != null, $"count={c3}");

        // C4 指定槽位被占 → 拒绝
        ClearAll(p);
        p.Info.Equipment[1] = MakeItem(_iArmour);
        p.Info.Inventory[0] = MakeItem(_iArmour);
        int c4 = NPCSegment.NpcGiveW(p, p, MirGridType.Inventory, null, 1, out string f4);
        Report("指定槽位被占 → 拒绝", c4 == 0 && f4.Contains("已有装备"), $"count={c4} fail={f4}");
        Report("拒绝时背包物品没动", p.Info.Inventory[0] != null, "");

        // C5 指定槽位成功（脱下旧的后）
        p.Info.Equipment[1] = null;
        int c5 = NPCSegment.NpcGiveW(p, p, MirGridType.Inventory, null, 1, out _);
        Report("指定槽位1（盔甲）→ 穿上", c5 == 1 && p.Info.Equipment[1] != null, $"count={c5}");

        // C6 职业不符的装备不穿（给战士塞 RequiredClass=法师 的武器）
        var mageWeapon = new ItemInfo { Index = 991006, Name = "法师测试剑", Type = ItemType.武器, Weight = 1, RequiredType = RequiredType.Level, RequiredAmount = 1, RequiredClass = RequiredClass.法师, RequiredGender = RequiredGender.男性, Durability = 1000 };
        _envir.ItemInfoList.Add(mageWeapon);
        ClearAll(p);
        p.Info.Inventory[0] = MakeItem(mageWeapon);
        int c6 = NPCSegment.NpcGiveW(p, p, MirGridType.Inventory, null, -1, out string f6);
        Report("职业不符 → 不穿", c6 == 0 && p.Info.Equipment[0] == null, $"count={c6} fail={f6}");
    }

    // ---------- D. 脱下核心 ----------

    private static void UnequipTests(PlayerObject p, RecConnection conn)
    {
        Console.WriteLine("==== D. 脱下核心（NpcTakeW）====");

        // D1 全身脱光
        ClearAll(p);
        p.Info.Equipment[0] = MakeItem(_iWeapon);
        p.Info.Equipment[1] = MakeItem(_iArmour);
        p.Info.Equipment[5] = MakeItem(_iBracelet);
        p.Info.Equipment[6] = MakeItem(_iBracelet);
        p.Info.Equipment[7] = MakeItem(_iRing);
        int d1 = NPCSegment.NpcTakeW(p, p, MirGridType.Inventory, null, -1, new List<string>(), out _);
        Report("全身脱光：脱下 5 件", d1 == 5, $"count={d1}");
        Report("装备栏全空", p.Info.Equipment.All(x => x == null), "");
        Report("5 件都进了背包", p.Info.Inventory.Count(x => x != null) == 5, $"bag={p.Info.Inventory.Count(x => x != null)}");

        // D2 背包不足整单拒绝
        ClearAll(p);
        p.Info.Equipment[0] = MakeItem(_iWeapon);
        p.Info.Equipment[1] = MakeItem(_iArmour);
        for (int i = 1; i < p.Info.Inventory.Length; i++) p.Info.Inventory[i] = MakeItem(_iMisc); // 只留第 0 格
        int d2 = NPCSegment.NpcTakeW(p, p, MirGridType.Inventory, null, -1, new List<string>(), out string f2);
        Report("背包空格不足 → 整单拒绝", d2 == 0 && f2.Contains("背包空格不足"), $"count={d2} fail={f2}");
        Report("拒绝时装备一件没动", p.Info.Equipment[0] != null && p.Info.Equipment[1] != null, "");
        Report("拒绝时背包没变", p.Info.Inventory.Count(x => x != null) == p.Info.Inventory.Length - 1, "");
        for (int i = 1; i < p.Info.Inventory.Length; i++) p.Info.Inventory[i] = null;

        // D3 类型过滤脱下（手镯两槽都脱，盔甲不动）
        ClearAll(p);
        p.Info.Equipment[1] = MakeItem(_iArmour);
        p.Info.Equipment[5] = MakeItem(_iBracelet);
        p.Info.Equipment[6] = MakeItem(_iBracelet);
        int d3 = NPCSegment.NpcTakeW(p, p, MirGridType.Inventory, ItemType.手镯, -1, new List<string>(), out _);
        Report("类型过滤手镯：脱下 2 件", d3 == 2, $"count={d3}");
        Report("盔甲还在身上", p.Info.Equipment[1] != null, "");
        Report("手镯两槽已空", p.Info.Equipment[5] == null && p.Info.Equipment[6] == null, "");

        // D4 槽位过滤脱下
        ClearAll(p);
        p.Info.Equipment[0] = MakeItem(_iWeapon);
        p.Info.Equipment[1] = MakeItem(_iArmour);
        int d4 = NPCSegment.NpcTakeW(p, p, MirGridType.Inventory, null, 0, new List<string>(), out _);
        Report("槽位过滤武器：只脱武器", d4 == 1 && p.Info.Equipment[0] == null && p.Info.Equipment[1] != null, $"count={d4}");

        // D5 诅咒跳过
        ClearAll(p);
        var cursed = MakeItem(_iWeapon);
        cursed.Cursed = true;
        p.Info.Equipment[0] = cursed;
        p.Info.Equipment[1] = MakeItem(_iArmour);
        var skip = new List<string>();
        int d5 = NPCSegment.NpcTakeW(p, p, MirGridType.Inventory, null, -1, skip, out _);
        Report("诅咒武器跳过，其余照脱", d5 == 1 && p.Info.Equipment[0] != null && p.Info.Equipment[1] == null, $"count={d5}");
        Report("发了诅咒提示", skip.Any(s => s.Contains("诅咒")), string.Join(" | ", skip));

        // D6 结婚戒指跳过
        ClearAll(p);
        var wedRing = MakeItem(_iRing);
        wedRing.WeddingRing = 12345;
        p.Info.Equipment[7] = wedRing;
        skip = new List<string>();
        int d6 = NPCSegment.NpcTakeW(p, p, MirGridType.Inventory, null, -1, skip, out string f6);
        Report("结婚戒指跳过（其它没东西可脱 → 0）", d6 == 0 && skip.Any(s => s.Contains("结婚戒指")), $"count={d6} skip={skip.Count} fail={f6}");
    }

    // ---------- E. 英雄版 ----------

    private static void HeroTests(PlayerObject p, HeroObject hero)
    {
        Console.WriteLine("==== E. 英雄版（GIVEWHERO/TAKEWHERO 核心）====");

        // 清英雄
        for (int i = 0; i < hero.HInfo.Equipment.Length; i++) hero.HInfo.Equipment[i] = null;
        for (int i = 0; i < hero.HInfo.Inventory.Length; i++) hero.HInfo.Inventory[i] = null;

        hero.HInfo.Inventory[0] = MakeItem(_iArmour);
        hero.HInfo.Inventory[1] = MakeItem(_iWeapon);

        int e1 = NPCSegment.NpcGiveW(hero, p, MirGridType.HeroInventory, null, -1, out _);
        Report("英雄无参穿全部：穿上 2 件", e1 == 2, $"count={e1}");
        Report("英雄武器槽0/盔甲槽1", hero.HInfo.Equipment[0] != null && hero.HInfo.Equipment[1] != null, "");
        Report("英雄背包已清空对应格", hero.HInfo.Inventory[0] == null && hero.HInfo.Inventory[1] == null, "");

        int e2 = NPCSegment.NpcTakeW(hero, p, MirGridType.HeroInventory, null, -1, new List<string>(), out _);
        Report("英雄全身脱光：脱下 2 件", e2 == 2 && hero.HInfo.Equipment.All(x => x == null), $"count={e2}");
        Report("英雄装备回英雄背包", hero.HInfo.Inventory.Count(x => x != null) == 2, $"bag={hero.HInfo.Inventory.Count(x => x != null)}");

        // 英雄背包不足整单拒绝：身上 2 件装备，背包只留 1 格空（0、1 已被脱下的装备占住）
        hero.HInfo.Equipment[0] = MakeItem(_iWeapon);
        hero.HInfo.Equipment[1] = MakeItem(_iArmour);
        int hLen2 = hero.HInfo.Inventory.Length;
        for (int i = 2; i < hLen2 - 1; i++) hero.HInfo.Inventory[i] = MakeItem(_iMisc); // 留最后 1 格空
        int e3 = NPCSegment.NpcTakeW(hero, p, MirGridType.HeroInventory, null, -1, new List<string>(), out string f3);
        Report("英雄背包不足 → 整单拒绝", e3 == 0 && f3.Contains("背包空格不足"), $"count={e3} fail={f3}");
        Report("英雄装备一件没动", hero.HInfo.Equipment[0] != null && hero.HInfo.Equipment[1] != null, "");
        for (int i = 2; i < hLen2 - 1; i++) hero.HInfo.Inventory[i] = null;
    }

    // ---------- F. CHECKBAG 执行链路 ----------

    private static void CheckExecTests(PlayerObject p)
    {
        Console.WriteLine("==== F. CHECKBAG / CHECKHEROBAG 执行（seg.Check 真跑）====");
        var page = new NPCPage("@bagtest");
        var seg = new NPCSegment(page, new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());

        bool RunCheck(string line)
        {
            seg.CheckList.Clear();
            seg.ParseCheck(line);
            return seg.Check(p);
        }

        ClearAll(p);

        Report("CHECKBAG（背包全空 → 成立）", RunCheck("CHECKBAG"));
        Report("CHECKBAG 5（空格>=5 → 成立）", RunCheck("CHECKBAG 5"));

        int bagLen = p.Info.Inventory.Length;
        for (int i = 6; i < bagLen; i++) p.Info.Inventory[i] = MakeItem(_iMisc); // 只留前 6 格
        int manualFree = p.Info.Inventory.Count(x => x == null);
        Console.WriteLine($"[诊断] 背包长度 {bagLen}，填满后手工数空格 = {manualFree}");
        Report("占满后 CHECKBAG >= 10 → 不成立", !RunCheck("CHECKBAG >= 10"), $"free={manualFree}");
        Report("CHECKBAG >= 6 → 成立", RunCheck("CHECKBAG >= 6"), "");
        Report("CHECKBAG == 6 → 成立", RunCheck("CHECKBAG == 6"), "");
        Report("CHECKBAG HERO（英雄背包全空 → 成立）", RunCheck("CHECKBAG HERO"), "");
        Report("CHECKHEROBAG（同上，独立写法）", RunCheck("CHECKHEROBAG"), "");

        // 英雄背包占格（自适应长度，只留前 6 格）
        var hero = p.Hero;
        int hLen = hero.HInfo.Inventory.Length;
        for (int i = 6; i < hLen; i++) hero.HInfo.Inventory[i] = MakeItem(_iMisc);
        int heroManualFree = hero.HInfo.Inventory.Count(x => x == null);
        Console.WriteLine($"[诊断] 英雄背包长度 {hLen}，填满后手工数空格 = {heroManualFree}");
        Report("英雄占满后 CHECKBAG HERO >= 10 → 不成立", !RunCheck("CHECKBAG HERO >= 10"), $"free={heroManualFree}");
        Report("CHECKHEROBAG >= 10 → 不成立", !RunCheck("CHECKHEROBAG >= 10"), "");
        Report($"CHECKHEROBAG {heroManualFree} → 成立（自由格数）", RunCheck($"CHECKHEROBAG {heroManualFree}"), "");

        // 英雄未召唤 → 不成立
        var savedHero = p.Hero;
        p.Hero = null;
        Report("英雄未召唤 CHECKBAG HERO → 不成立", !RunCheck("CHECKBAG HERO"), "");
        Report("英雄未召唤 CHECKHEROBAG → 不成立", !RunCheck("CHECKHEROBAG"), "");
        p.Hero = savedHero;
    }

    // ---------- 公共骨架 ----------

    private static (PlayerObject, HeroObject, RecConnection) BuildPlayerAndHero(string playerName, string heroName)
    {
        var conn = new RecConnection(_uid) { Account = new AccountInfo { Index = _uid, AccountID = "autoequiptest" } };

        var ch = new CharacterInfo
        {
            Index = _uid,
            Name = playerName,
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 60,
            HP = 1000,
            MP = 300,
        };

        var p = new PlayerObject(ch, conn);
        if (p.Info.Inventory == null || p.Info.Inventory.Length < 5) p.Info.Inventory = new UserItem[46];
        if (p.Info.Equipment == null || p.Info.Equipment.Length < 15) p.Info.Equipment = new UserItem[14];

        var hi = new HeroInfo(new ClientPackets.NewHero { Name = heroName, Class = MirClass.战士, Gender = MirGender.男性 })
        {
            Index = _uid + 5000,
            Name = heroName,
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 30,
            HP = 500,
            MP = 100,
        };
        if (hi.Inventory == null || hi.Inventory.Length < 5) hi.Inventory = new UserItem[46];
        if (hi.Equipment == null || hi.Equipment.Length < 15) hi.Equipment = new UserItem[14];
        if (hi.Buffs == null) hi.Buffs = new List<Buff>();

        // CurrentHero setter 会遍历 Info.Heroes 找英雄索引，先把英雄挂进角色档案
        ch.Heroes = new HeroInfo[] { hi };
        p.CurrentHero = hi;

        var hero = new HeroObject(hi, p);
        p.Hero = hero;

        _uid++;
        return (p, hero, conn);
    }

    private static UserItem MakeItem(ItemInfo info)
    {
        return new UserItem(info)
        {
            UniqueID = (ulong)(++_uid),
            Count = 1,
            MaxDura = info.Durability,
            CurrentDura = info.Durability,
        };
    }

    private static bool ContainsItem(this UserItem[] bag, ItemInfo info)
        => bag.Any(x => x != null && x.Info != null && x.Info.Index == info.Index);

    private static void Report(string name, bool ok, string detail = "")
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "OK    " : "FAIL  ")}{name}{(string.IsNullOrEmpty(detail) ? "" : "   → " + detail)}");
    }
}

/// <summary>把服务端下发的包收进列表（不涉及网络）。</summary>
internal sealed class RecConnection : MirConnection
{
    public readonly List<Packet> Sent = new List<Packet>();
    public RecConnection(int sessionID) : base(sessionID, null) { Stage = GameStage.Game; }
    public override void Enqueue(Packet p) { lock (Sent) Sent.Add(p); }
    public override void Process() { }
    public void Clear() => Sent.Clear();
}
