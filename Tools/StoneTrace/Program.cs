using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;

namespace StoneTrace;

/// <summary>
/// 只读诊断：模拟主循环连续推进时间，观察气血石/魔血石是否「持续」回复。
/// 用法：StoneTrace.exe "E:\...\Mir2Server"
/// 不写数据库、不启动服务端。
/// </summary>
internal static class Program
{
    private static MethodInfo _regen;
    private static FieldInfo _timeField;
    private static Envir _envir;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        Envir envir = Envir.Main;
        _envir = envir;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        _regen = typeof(HumanObject).GetMethod("ProcessRegen", BindingFlags.NonPublic | BindingFlags.Instance);
        _timeField = typeof(Envir).GetField("<Time>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (_regen == null || _timeField == null) { Console.WriteLine("!! 反射失败"); return 1; }

        // ---- 1) 石头类物品清单（只列带 HP/MP 的 + 气血石/魔血石）----
        Console.WriteLine("=== 带 HP/MP 数值的石头类物品 ===");
        foreach (ItemInfo info in envir.ItemInfoList
                     .Where(x => x.Type == ItemType.气血石 || x.Type == ItemType.魔血石
                                 || (x.Type == ItemType.守护石 && (x.Stats[Stat.HP] > 0 || x.Stats[Stat.MP] > 0))))
        {
            Console.WriteLine($"  [{info.Index,4}] {info.Name,-14} 类型={info.Type,-6} HP={info.Stats[Stat.HP],-4} MP={info.Stats[Stat.MP],-4} 电量={info.Durability,-6} Shape={info.Shape,-3} Image={info.Image}");
        }
        Console.WriteLine();

        ItemInfo hpStone = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.气血石);
        if (hpStone == null) { Console.WriteLine("没有气血石物品"); return 1; }

        // ---- 2) 气血石：10ms 步长跑 40 秒，看是否每 2 秒一跳、能否补满 ----
        Console.WriteLine($"=== A. 气血石 [{hpStone.Index}] {hpStone.Name}（每跳 HP+{hpStone.Stats[Stat.HP]} / MP+{hpStone.Stats[Stat.MP]}，{HumanObject.StoneDelay / 1000.0:F0} 秒一跳，每跳扣 {HumanObject.StoneDuraCost}），10ms 步长跑 60 秒 ===");
        Run(hpStone, 1000, 500, 100, 100, 60000, 10, true);

        // ---- 3) 守护石类型、但属性里填了 HP/MP 的物品（如 芷心之石）----
        ItemInfo guardWithStats = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.守护石 && x.Stats[Stat.HP] > 0);
        if (guardWithStats != null)
        {
            Console.WriteLine();
            Console.WriteLine($"=== B. 守护石类型 [{guardWithStats.Index}] {guardWithStats.Name}（HP={guardWithStats.Stats[Stat.HP]} MP={guardWithStats.Stats[Stat.MP]}）===");
            Run(guardWithStats, 1000, 500, 100, 100, 20000, 10, true);
        }

        // ---- 3b) 守护石类型物品：属性只并入最大 HP/MP（一次性），不产生回复 ----
        if (guardWithStats != null)
        {
            Console.WriteLine();
            Console.WriteLine($"=== B2. 把 [{guardWithStats.Index}] {guardWithStats.Name} 装进石头格后刷新属性 ===");
            FakePlayerObject b2 = NewBot();
            b2.RefreshStats();
            int hp0 = b2.Stats[Stat.HP], mp0 = b2.Stats[Stat.MP];
            b2.Info.Equipment[(int)EquipmentSlot.守护石] = new UserItem(guardWithStats) { UniqueID = 99002, Count = 1, MaxDura = guardWithStats.Durability, CurrentDura = guardWithStats.Durability };
            b2.RefreshStats();
            Console.WriteLine($"  最大 HP {hp0} → {b2.Stats[Stat.HP]}（差 {b2.Stats[Stat.HP] - hp0}）  最大 MP {mp0} → {b2.Stats[Stat.MP]}（差 {b2.Stats[Stat.MP] - mp0}）");
            Console.WriteLine("  → 这类物品的 HP/MP 是装备加成，装备瞬间上限涨一次，之后不会回复");
        }

        // ---- 4) 气血石电量只剩 15（不足一跳）----
        Console.WriteLine();
        Console.WriteLine($"=== C. 气血石电量只剩 15（每跳扣 {HumanObject.StoneDuraCost}）===");
        Run(hpStone, 1000, 500, 100, 100, 20000, 10, false, 15);

        return 0;
    }

    /// <summary>模拟：装石头 → 推进时间 → 调 ProcessRegen，打印每次真正发生变化的时刻</summary>
    private static void Run(ItemInfo info, int maxHp, int maxMp, int hp, int mp, int totalMs, int stepMs, bool verbose, int? dura = null)
    {
        FakePlayerObject bot = NewBot();
        bot.Stats[Stat.HP] = maxHp;
        bot.Stats[Stat.MP] = maxMp;
        bot.HP = hp;
        bot.MP = mp;

        int slot = (int)EquipmentSlot.守护石;
        var item = new UserItem(info)
        {
            UniqueID = 99001,
            Count = 1,
            MaxDura = info.Durability,
            CurrentDura = (ushort)(dura ?? info.Durability),
        };
        bot.Info.Equipment[slot] = item;

        long baseTime = 200000;
        _timeField.SetValue(_envir, baseTime);

        int jumps = 0;
        int lastHp = bot.HP, lastMp = bot.MP;
        ushort lastDura = item.CurrentDura;
        string firstErr = null;

        for (int ms = stepMs; ms <= totalMs; ms += stepMs)
        {
            _timeField.SetValue(_envir, baseTime + ms);
            try { _regen.Invoke(bot, null); }
            catch (TargetInvocationException ex)
            {
                firstErr ??= (ex.InnerException?.GetType().Name ?? "?") + ": " + ex.InnerException?.Message;
            }

            UserItem cur = bot.Info.Equipment[slot];
            ushort d = cur?.CurrentDura ?? 0;
            bool changed = bot.HP != lastHp || bot.MP != lastMp || d != lastDura || (cur == null);

            if (changed)
            {
                jumps++;
                if (verbose)
                    Console.WriteLine($"  t={ms / 1000.0,6:F2}s  HP {lastHp}→{bot.HP}  MP {lastMp}→{bot.MP}  电量 {lastDura}→{(cur == null ? "石头消失" : d.ToString())}");
                lastHp = bot.HP; lastMp = bot.MP; lastDura = d;
            }

            if (cur == null) break;
        }

        Console.WriteLine($"  合计 {jumps} 次变化，最终 HP {bot.HP}/{bot.Stats[Stat.HP]}  MP {bot.MP}/{bot.Stats[Stat.MP]}（人物上限 HP {bot.Stats[Stat.HP]} MP {bot.Stats[Stat.MP]}，开始设定 {maxHp}/{maxMp}）");
        if (firstErr != null) Console.WriteLine("  [异常] " + firstErr);
    }

    private static FakePlayerObject NewBot()
    {
        CharacterInfo info = new CharacterInfo
        {
            Index = 1,
            Name = "石头追踪",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 45,
            HP = 100,
            MP = 100,
        };

        return new FakePlayerObject(info, new BotConnection(1) { Account = new AccountInfo { Index = 1, AccountID = "stone_trace" } });
    }
}
