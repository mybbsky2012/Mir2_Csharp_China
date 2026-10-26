using System.Text;
using Server.MirObjects;

namespace RepairAllTest;

/// <summary>
/// 只读单元测试：全身修复（卷轴 Shape=23 / 特殊消耗品 Shape=84 共用）的纯逻辑核心。
/// 直接反射/调用真实的 PlayerObject.RepairAllEquipmentCore，不连服务器、不写库。
/// 用法：dotnet RepairAllTest.dll
/// </summary>
internal static class Program
{
    private static int _pass, _fail;

    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Console.WriteLine("=== 全身修复 RepairAllEquipmentCore 单元测试 ===\n");

        Case1_普通装备修满();
        Case2_已满持久不重复修();
        Case3_禁止修理的装备跳过();
        Case4_只允许普修的装备跳过();
        Case5_空槽与空Info不崩();
        Case6_空数组与null不崩();
        Case7_混装场景();

        Console.WriteLine($"\n合计：通过 {_pass} / 失败 {_fail}");
        return _fail == 0 ? 0 : 1;
    }

    // ── 工具 ──────────────────────────────────────────────
    private static UserItem MakeItem(string name, ushort maxDura, ushort curDura, BindMode bind = BindMode.None)
    {
        ItemInfo info = new ItemInfo
        {
            Name = name,
            Type = ItemType.武器,
            Bind = bind,
        };

        return new UserItem(info)
        {
            MaxDura = maxDura,
            CurrentDura = curDura,
            DuraChanged = true,
        };
    }

    private static void Check(string title, bool ok, string detail = "")
    {
        if (ok) { _pass++; Console.WriteLine($"[PASS] {title}"); }
        else { _fail++; Console.WriteLine($"[FAIL] {title}   {detail}"); }
    }

    // ── 用例 ──────────────────────────────────────────────

    private static void Case1_普通装备修满()
    {
        UserItem[] eq = new UserItem[4];
        eq[0] = MakeItem("屠龙", 30000, 120);
        eq[1] = MakeItem("战神盔甲", 25000, 24999);
        eq[2] = MakeItem("头盔", 8000, 0);

        int n = PlayerObject.RepairAllEquipmentCore(eq);

        Check("普通装备：3 件都修满", n == 3
              && eq[0].CurrentDura == 30000 && eq[1].CurrentDura == 25000 && eq[2].CurrentDura == 8000,
              $"n={n} dura={eq[0].CurrentDura},{eq[1].CurrentDura},{eq[2].CurrentDura}");

        Check("普通装备：修满后 DuraChanged 清零",
              !eq[0].DuraChanged && !eq[1].DuraChanged && !eq[2].DuraChanged);
    }

    private static void Case2_已满持久不重复修()
    {
        UserItem[] eq = new UserItem[2];
        eq[0] = MakeItem("满耐久武器", 20000, 20000);
        eq[1] = MakeItem("满耐久衣服", 15000, 15000);

        int n = PlayerObject.RepairAllEquipmentCore(eq);

        Check("已满持久：返回 0（调用方据此不消耗物品）", n == 0, $"n={n}");
    }

    private static void Case3_禁止修理的装备跳过()
    {
        UserItem[] eq = new UserItem[3];
        eq[0] = MakeItem("绑定的武器", 20000, 100, BindMode.DontRepair);
        eq[1] = MakeItem("正常武器", 20000, 100);

        int n = PlayerObject.RepairAllEquipmentCore(eq);

        Check("DontRepair：跳过不修，只修其余", n == 1 && eq[0].CurrentDura == 100 && eq[1].CurrentDura == 20000,
              $"n={n} dura0={eq[0].CurrentDura} dura1={eq[1].CurrentDura}");
    }

    private static void Case4_只允许普修的装备跳过()
    {
        UserItem[] eq = new UserItem[2];
        eq[0] = MakeItem("禁特修武器", 20000, 100, BindMode.NoSRepair);

        int n = PlayerObject.RepairAllEquipmentCore(eq);

        Check("NoSRepair：按特修处理 → 跳过", n == 0 && eq[0].CurrentDura == 100, $"n={n} dura={eq[0].CurrentDura}");
    }

    private static void Case5_空槽与空Info不崩()
    {
        // 说明：UserItem 的构造函数强制需要 ItemInfo，运行期不会出现 Info==null 的实例，
        // 这里只测「空槽（null）」以及核心方法对 Info 的防御性判空不会崩。
        UserItem[] eq = new UserItem[5];
        eq[0] = null;
        eq[2] = null;
        eq[4] = MakeItem("正常", 10000, 1);

        int n = 0;
        bool threw = false;

        try { n = PlayerObject.RepairAllEquipmentCore(eq); }
        catch (Exception ex) { threw = true; Console.WriteLine("      异常: " + ex.Message); }

        Check("空槽：不抛异常且只修有效那件", !threw && n == 1 && eq[4].CurrentDura == 10000, $"n={n}");
    }

    private static void Case6_空数组与null不崩()
    {
        bool ok = true;

        try
        {
            ok &= PlayerObject.RepairAllEquipmentCore(null) == 0;
            ok &= PlayerObject.RepairAllEquipmentCore(new UserItem[0]) == 0;
        }
        catch (Exception ex)
        {
            ok = false;
            Console.WriteLine("      异常: " + ex.Message);
        }

        Check("null / 空数组：返回 0 不抛异常", ok);
    }

    private static void Case7_混装场景()
    {
        UserItem[] eq = new UserItem[14];         // 客户端装备槽 14 格
        eq[0] = MakeItem("武器", 30000, 1);        // 快碎 → 必须修
        eq[1] = MakeItem("衣服", 25000, 24999);    // 掉一点 → 修
        eq[2] = null;
        eq[3] = MakeItem("头盔", 6000, 6000);      // 满 → 不动
        eq[4] = MakeItem("禁修戒指", 5000, 10, BindMode.DontRepair);

        List<UserItem> repaired = new List<UserItem>();
        int n = PlayerObject.RepairAllEquipmentCore(eq, repaired);

        Check("混装：修 2 件、名单 2 条、满耐久与禁修保持原样",
              n == 2 && repaired.Count == 2
              && eq[0].CurrentDura == 30000 && eq[1].CurrentDura == 25000
              && eq[3].CurrentDura == 6000 && eq[4].CurrentDura == 10,
              $"n={n} list={repaired.Count}");
    }
}
