using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;

namespace BotAmuletTest;

/// <summary>
/// 只读验证：假人道士的护身符 / 毒粉补给逻辑（不改数据库，不启动服务端）。
///
/// 验证三件事：
///   1. 装备方案里写了「护身符=护身符(特)」→ 穿上时数量是满叠（600），不是 1；
///   2. 打怪消耗到只剩 5 张 → 回城补给 RefillPotions 能补回满叠；
///   3. 道士完全没有配护身符 → 上线自动补一叠最好的符（不再有光杆道士）。
/// 用法：在服务端目录（与 Server.MirDB 同级）运行。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

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

        ItemInfo amulet = null;
        for (int i = 0; i < envir.ItemInfoList.Count; i++)
        {
            ItemInfo info = envir.ItemInfoList[i];
            if (info == null || info.Type != ItemType.护身符 || info.Shape != 0) continue;
            if (amulet == null || info.StackSize > amulet.StackSize) amulet = info;
        }

        if (amulet == null)
        {
            Console.WriteLine("!! 物品库里没有护身符(Shape=0)，无法测试");
            return 2;
        }

        Console.WriteLine($"最好的符：{amulet.Name}  StackSize={amulet.StackSize}");
        Console.WriteLine();

        int fail = 0;

        // ---- 1) 装备方案穿上来的数量 ----
        FakePlayerObject bot = NewBot(MirClass.道士);

        var equipNamed = typeof(FakePlayerManager).GetMethod("EquipNamed", BindingFlags.NonPublic | BindingFlags.Static);
        equipNamed.Invoke(null, new object[] { bot, EquipmentSlot.护身符, amulet });

        UserItem worn = bot.Info.Equipment[(int)EquipmentSlot.护身符];
        Console.WriteLine($"[1] 方案配符 → 槽里：{worn?.Info.Name} x{worn?.Count}");
        if (worn == null || worn.Count != amulet.StackSize) { Console.WriteLine("    !! 期望满叠，实际不是"); fail++; }

        // ---- 2) 消耗后补给 ----
        worn.Count = 5;
        var refillPotions = typeof(FakePlayerManager).GetMethod("RefillPotions", BindingFlags.NonPublic | BindingFlags.Static);
        refillPotions.Invoke(null, new object[] { bot });

        Console.WriteLine($"[2] 消耗到 5 张后补给 → x{worn.Count}");
        if (worn.Count != amulet.StackSize) { Console.WriteLine("    !! 期望补满，实际不是"); fail++; }

        // ---- 3) 没配符的道士自动补 ----
        FakePlayerObject fresh = NewBot(MirClass.道士);
        refillPotions.Invoke(null, new object[] { fresh });

        UserItem auto = fresh.Info.Equipment[(int)EquipmentSlot.护身符];
        Console.WriteLine($"[3] 没配符的道士上线 → {auto?.Info.Name} x{auto?.Count}");
        if (auto == null || auto.Count != amulet.StackSize) { Console.WriteLine("    !! 期望自动配满叠，实际不是"); fail++; }

        // ---- 4) 毒粉补满（走的是同一条逻辑，认类型不认名字） ----
        ItemInfo poison = null;
        for (int i = 0; i < envir.ItemInfoList.Count; i++)
        {
            ItemInfo info = envir.ItemInfoList[i];
            if (info == null || info.Type != ItemType.护身符) continue;
            if (info.Shape != 1 && info.Shape != 2) continue;   // 只看毒粉；气功石/苏生符籍不算
            if (poison == null || info.StackSize > poison.StackSize) poison = info;
        }

        if (poison != null)
        {
            FakePlayerObject pbot = NewBot(MirClass.道士);
            equipNamed.Invoke(null, new object[] { pbot, EquipmentSlot.护身符, poison });
            UserItem pd = pbot.Info.Equipment[(int)EquipmentSlot.护身符];

            pd.Count = 1;
            refillPotions.Invoke(null, new object[] { pbot });

            Console.WriteLine($"[4] 毒粉配装/补给 → {pd.Info.Name} x{pd.Count}（满叠 {poison.StackSize}）");
            if (pd.Count != poison.StackSize) { Console.WriteLine("    !! 期望补满，实际不是"); fail++; }
        }

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过 ✓" : $"失败 {fail} 项 ✗");
        return fail == 0 ? 0 : 1;
    }

    private static FakePlayerObject NewBot(MirClass cls)
    {
        CharacterInfo info = new CharacterInfo
        {
            Index = 1,
            Name = "测试假人",
            Class = cls,
            Gender = MirGender.男性,
            Level = 45,
            HP = 500,
            MP = 500,
        };

        return new FakePlayerObject(info, new BotConnection(1) { Account = new AccountInfo { Index = 1, AccountID = "bot_test" } });
    }
}
