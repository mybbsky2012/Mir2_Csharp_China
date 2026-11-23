using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace HeroHumCmdTest;

/// <summary>
/// 英雄经验/等级/登仙 NPC + GM 命令模拟测试（只读，不写库）。
///
/// 用法：HeroHumCmdTest.exe "D:\Mirserver"
///
/// 覆盖四块：
///   A. NPC 脚本命令解析：CHECKHEROHUMUP / HEROEXP / SETHEROLEVEL / HEROHUMUP / HEROHUMDOWN / RESETHEROHUMUP
///   B. 英雄羽化重数：SetHumUpStage 升/降/封顶/清位/装备门槛
///   C. 英雄经验/等级：ApplyExpDelta（正=加可连升、负=只扣经验条绝不掉级）/ ApplySetLevel
///   D. 自动脱装：TryUnequipHeroHumUpEquipment（背包空格不足=整单失败+红字）+ GM 目标解析 ResolveHeroGmTarget
/// </summary>
internal static class Program
{
    private static Envir _envir;
    private static int _uid = 800001;
    private static int _pass, _fail;

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

        ParseTests();
        Console.WriteLine();
        var (p, hero, conn) = BuildPlayerAndHero("命令测试主角", "英雄测试甲");
        RuntimeTests(p, hero, conn);
        Console.WriteLine();
        GmTargetTests(p, hero);

        Console.WriteLine();
        Console.WriteLine($"==== 结果：通过 {_pass} / 失败 {_fail} ====");
        return _fail == 0 ? 0 : 1;
    }

    // ---------- A. 解析测试 ----------

    private static void ParseTests()
    {
        Console.WriteLine("==== A. NPC 脚本命令解析 ====");
        var page = new NPCPage("@herotest");
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

        CheckChk("CHECKHEROHUMUP", CheckType.CheckHeroHumUp, ">=", "1");
        CheckChk("CHECKHEROHUMUP 3", CheckType.CheckHeroHumUp, ">=", "3");
        CheckChk("CHECKHEROHUMUP >= 5", CheckType.CheckHeroHumUp, ">=", "5");
        CheckChk("CHECKHEROHUMUP != 10", CheckType.CheckHeroHumUp, "!=", "10");
        CheckAct("HEROEXP 100000", ActionType.HeroExp, "100000");
        CheckAct("HEROEXP -50000", ActionType.HeroExp, "-50000");
        CheckAct("SETHEROLEVEL 55", ActionType.HeroSetLevel, "55");
        CheckAct("HEROHUMUP", ActionType.HeroHumUpUp, "1");
        CheckAct("HEROHUMUP 3", ActionType.HeroHumUpUp, "3");
        CheckAct("HEROHUMDOWN", ActionType.HeroHumUpDown, "1");
        CheckAct("HEROHUMDOWN 2", ActionType.HeroHumUpDown, "2");
        CheckAct("RESETHEROHUMUP", ActionType.HeroHumUpReset);
        CheckAct("RESETHEROHUMUP 1", ActionType.HeroHumUpReset, "1");

        // 老命令不受影响（回归）
        CheckAct("HUMUPUP 2", ActionType.HumUpUp, "2");
        CheckAct("RESETHUMUP 1", ActionType.HumUpReset, "1");
        CheckChk("CHECKHUMUP >= 3", CheckType.CheckHumUp, ">=", "3");
    }

    // ---------- B/C/D. 运行时测试 ----------

    private static void RuntimeTests(PlayerObject p, HeroObject hero, RecConnection conn)
    {
        Console.WriteLine("==== B. 英雄羽化重数（SetHumUpStage/GetHumUpStage）====");
        Report("初始未登仙", hero.GetHumUpStage() == 0, $"stage={hero.GetHumUpStage()}");

        SetStage(hero, 3, p);
        Report("升到三重", hero.GetHumUpStage() == 3, $"stage={hero.GetHumUpStage()}");
        Report("挂着 羽化登仙 标记", hero.HasBuff(BuffType.羽化登仙), "");
        Report("挂着 羽化三重 属性位", hero.HasBuff(BuffType.羽化三重), "");

        SetStage(hero, 5, p);
        Report("换重不叠加（三重位已清）", hero.GetHumUpStage() == 5 && !hero.HasBuff(BuffType.羽化三重) && hero.HasBuff(BuffType.羽化五重),
            $"stage={hero.GetHumUpStage()} 三重残留={hero.HasBuff(BuffType.羽化三重)}");

        SetStage(hero, 11, p);
        Report("封顶十重", hero.GetHumUpStage() == 10, $"stage={hero.GetHumUpStage()}");
        SetStage(hero, -3, p);
        Report("负数钳到 0", hero.GetHumUpStage() == 0, $"stage={hero.GetHumUpStage()}");

        Console.WriteLine();
        Console.WriteLine("==== C. 英雄经验 / 等级（ApplyExpDelta/ApplySetLevel）====");

        if (hero.MaxExperience <= 0)
        {
            Console.WriteLine("!! ExperienceList 为空，经验类测试跳过（不影响功能判定，但请人工确认）");
            _fail++;
        }
        else
        {
            long beforeExp = hero.Experience;
            ushort beforeLevel = hero.Level;

            string fb = hero.ApplyExpDelta(100_000_000);
            Report("加经验生效", fb != null && hero.Experience > beforeExp, fb);

            long bigExp = hero.Experience;
            ushort bigLevel = hero.Level;
            hero.ApplyExpDelta(-500);
            Report("扣经验只扣当前条", hero.Experience == Math.Max(0, bigExp - 500), $"exp={hero.Experience}（期望 {Math.Max(0, bigExp - 500)}）");
            Report("扣经验不掉级", hero.Level == bigLevel, $"level={hero.Level}（期望 {bigLevel}）");

            hero.ApplyExpDelta(long.MinValue / 2);
            Report("大额扣到地板 0", hero.Experience == 0, $"exp={hero.Experience}");
            Report("地板 0 也不掉级", hero.Level == bigLevel, $"level={hero.Level}（期望 {bigLevel}）");

            string fbNull = hero.ApplyExpDelta(0);
            Report("delta=0 返回 null（无提示）", fbNull == null, "");

            string fbLevel = hero.ApplySetLevel(55);
            Report("SETHEROLEVEL 55 生效", hero.Level == 55 && fbLevel != null, fbLevel);
            Report("升级后 MaxExperience 重算", hero.MaxExperience > 0, $"maxExp={hero.MaxExperience}");
            Report("ApplySetLevel(0) 返回 null", hero.ApplySetLevel(0) == null, "");
        }

        Console.WriteLine();
        Console.WriteLine("==== D. 自动脱装（TryUnequipHeroHumUpEquipment）====");

        ItemInfo fakeArmour = new ItemInfo { Index = 990001, Name = "测试仙甲", Shape = 1001, Type = ItemType.盔甲, Weight = 1 };
        ItemInfo fakeWeapon = new ItemInfo { Index = 990002, Name = "测试仙兵", Shape = 2001, Type = ItemType.武器, Weight = 1 };
        _envir.ItemInfoList.Add(fakeArmour);
        _envir.ItemInfoList.Add(fakeWeapon);

        hero.SetHumUpStage(2, p); FlushRemovedBuffs(hero);
        conn.Clear();
        hero.HInfo.Equipment[(int)EquipmentSlot.盔甲] = MakeItem(fakeArmour);
        hero.HInfo.Equipment[(int)EquipmentSlot.武器] = MakeItem(fakeWeapon);

        // 把英雄背包填满（留 1 格，装备有 2 件 → 空格不足必须整单拒绝）
        var inv = hero.HInfo.Inventory;
        for (int i = 1; i < inv.Length; i++) inv[i] = MakeItem(fakeArmour);   // 只留第 0 格

        bool blocked = p.TryUnequipHeroHumUpEquipment();
        Report("背包空格不足 → 整单拒绝", !blocked, $"返回={blocked}");
        Report("拒绝时装备一件没动", hero.HasHumUpEquipmentEquipped() &&
            hero.HInfo.Equipment[(int)EquipmentSlot.盔甲] != null &&
            hero.HInfo.Equipment[(int)EquipmentSlot.武器] != null, "装备仍在身上");
        Report("拒绝时发了红字提示", conn.Sent.OfType<S.Chat>().Any(c => c.Message.Contains("英雄背包空格不足")),
            string.Join(" | ", conn.Sent.OfType<S.Chat>().Select(c => c.Message)));

        // 腾空背包 → 自动脱装成功
        for (int i = 1; i < inv.Length; i++) inv[i] = null;
        conn.Clear();
        bool ok = p.TryUnequipHeroHumUpEquipment();
        Report("空格足够 → 自动脱装成功", ok, $"返回={ok}");
        Report("英雄身上已无羽化装备", !hero.HasHumUpEquipmentEquipped(), "");
        Report("两件装备都进了英雄背包",
            inv.Count(x => x != null && (x.Info.Shape == 1001 || x.Info.Shape == 2001)) == 2,
            $"背包羽化件数={inv.Count(x => x != null && (x.Info.Shape == 1001 || x.Info.Shape == 2001))}");

        bool resetOk = hero.SetHumUpStage(0, p); FlushRemovedBuffs(hero);
        Report("脱装后可以还原为未登仙", resetOk && hero.GetHumUpStage() == 0, $"stage={hero.GetHumUpStage()}");

        // 无装备时 RESETHEROHUMUP（不带 1）不应被装备门槛拦住
        hero.SetHumUpStage(1, p); FlushRemovedBuffs(hero);
        bool reset2 = hero.SetHumUpStage(0, p); FlushRemovedBuffs(hero);
        Report("未穿羽化装可直接还原", reset2 && hero.GetHumUpStage() == 0, $"stage={hero.GetHumUpStage()}");
    }

    private static void GmTargetTests(PlayerObject p, HeroObject hero)
    {
        Console.WriteLine("==== E. GM 目标解析（ResolveHeroGmTarget）====");
        var mi = typeof(PlayerObject).GetMethod("ResolveHeroGmTarget",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi == null)
        {
            Report("找到 ResolveHeroGmTarget", false, "反射未命中");
            return;
        }

        HeroObject Invoke(PlayerObject caller, string[] parts, bool hasParam, out string param)
        {
            var args = new object[] { parts, hasParam, null };
            var r = (HeroObject)mi.Invoke(caller, args);
            param = (string)args[2];
            return r;
        }

        var r1 = Invoke(p, new[] { "@HEROHUMUPUP", "5" }, true, out string s1);
        Report("@HEROHUMUPUP 5 → 自己的英雄 + 参数 5", r1 == hero && s1 == "5", $"param={s1}");

        var r2 = Invoke(p, new[] { "@HEROEXP", "-100000" }, true, out string s2);
        Report("@HEROEXP -100000 → 自己的英雄 + 参数 -100000", r2 == hero && s2 == "-100000", $"param={s2}");

        var r3 = Invoke(p, new[] { "@HEROEXP", "玩家不存在", "100" }, true, out string s3);
        Report("@HEROEXP 玩家不存在 100 → 返回 null（已提示）", r3 == null, $"param={s3}");

        // 跨玩家：二号玩家带英雄
        var (p2, hero2, _) = BuildPlayerAndHero("命令测试二号", "英雄测试乙");
        _envir.Players.Add(p2);

        var r4 = Invoke(p, new[] { "@HEROHUMUPRESET", "命令测试二号" }, false, out string s4);
        Report("@HEROHUMUPRESET 二号 → 二号的英雄", r4 == hero2, $"param={s4}");

        var r5 = Invoke(p, new[] { "@HEROHUMUPDOWN", "命令测试二号", "2" }, true, out string s5);
        Report("@HEROHUMUPDOWN 二号 2 → 二号的英雄 + 参数 2", r5 == hero2 && s5 == "2", $"param={s5}");

        // 二号英雄撤下后应拒绝
        p2.Hero = null;
        var r6 = Invoke(p, new[] { "@HEROHUMUPINFO", "命令测试二号" }, false, out _);
        Report("英雄未召唤 → 返回 null（已提示）", r6 == null, "");

        _envir.Players.Remove(p2);
    }

    // ---------- 公共骨架 ----------

    private static (PlayerObject, HeroObject, RecConnection) BuildPlayerAndHero(string playerName, string heroName)
    {
        var conn = new RecConnection(_uid) { Account = new AccountInfo { Index = _uid, AccountID = "herohumtest" } };

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

    /// <summary>测试专用：SetHumUpStage + 模拟 buff tick（见 FlushRemovedBuffs）。</summary>
    private static bool SetStage(HeroObject hero, int stage, PlayerObject p)
    {
        bool ok = hero.SetHumUpStage(stage, p);
        FlushRemovedBuffs(hero);
        return ok;
    }

    /// <summary>
    /// 测试专用：模拟服务端 buff tick（HumanObject.ProcessBuffs 每秒把 FlagForRemoval 的 buff 真正移除）。
    /// RemoveBuff 是懒删除，线上由 tick 清理；测试没有 tick 循环，必须手工刷一遍。
    /// </summary>
    private static void FlushRemovedBuffs(HumanObject h)
    {
        for (int i = h.Buffs.Count - 1; i >= 0; i--)
        {
            Buff b = h.Buffs[i];
            bool alive = (b.ExpireTime > 0 || b.StackType == BuffStackType.Infinite) && !b.FlagForRemoval;
            if (alive) continue;
            h.Buffs.RemoveAt(i);
        }
    }

    private static void Report(string name, bool ok, string detail)
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
