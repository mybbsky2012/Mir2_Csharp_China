using Server.MirDatabase;

// 冒烟测试：只想验证 BuffConfig 的解析/保存/槽位映射是否自洽。
// 会在当前工作目录下建 Configs\BuffSystem.ini，请在临时目录里运行。
internal static class Program
{
    private static int _failed;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  [OK] " : "  [FAIL] ") + what);
        if (!ok) _failed++;
    }

    private static void Main(string[] args)
    {
        // 只读检查模式：BuffCfgTest inspect <服务器目录>
        // 解析该目录下 Configs\BuffSystem.ini 并打印结果，不写任何文件。
        if (args.Length > 0 && args[0] == "inspect")
        {
            string dir = args.Length > 1 ? args[1] : ".";

            Directory.SetCurrentDirectory(dir);

            Console.WriteLine("工作目录  : " + Directory.GetCurrentDirectory());
            Console.WriteLine("配置文件  : " + Path.GetFullPath(BuffConfig.FilePath));
            Console.WriteLine("文件存在  : " + File.Exists(BuffConfig.FilePath));

            BuffConfig.Load();

            Console.WriteLine("解析错误  : " + (string.IsNullOrEmpty(BuffConfig.LastError) ? "无" : BuffConfig.LastError));
            Console.WriteLine("解析条数  : " + BuffConfig.Defs.Count);
            Console.WriteLine();

            foreach (BuffDef def in BuffConfig.Defs)
            {
                Console.WriteLine(string.Format("[{0}] {1}  Icon={2} Visible={3} Stack={4} Property={5}",
                    def.Section, def.Name, def.Icon, def.Visible, def.Stack, def.Property));

                foreach (BuffStatDef stat in def.Stats)
                    Console.WriteLine(string.Format("      {0} = {1}", stat.Stat, stat.ToText()));

                Stats at1 = def.BuildStats(1);
                Stats at30 = def.BuildStats(30);

                Console.WriteLine(string.Format("      等级1 属性项 {0} / 等级30 属性项 {1}", at1.Values.Count, at30.Values.Count));
            }

            Environment.Exit(0);
        }

        Console.WriteLine("=== 1. 写入一份测试配置 ===");

        Directory.CreateDirectory(Path.Combine(".", "Configs"));

        string ini = string.Join("\n", new[]
        {
            "; 测试用配置",
            "[1]",
            "Name=羽化登仙",
            "Icon=30034",
            "Visible=True",
            "Stack=ResetDuration",
            "Desc=测试",
            "Stat.MaxAC=5+2",
            "Stat.HP=100",
            "",
            "[2]",
            "Name=减益示例",
            "Visible=True",
            "Property=Debuff|RemoveOnDeath",
            "Stat.MaxDC=-10",
            "Stat.暴击伤害=3+1",
            "",
            "[隐身术]",
            "Stat.攻击速度=3",
            "",
            "[99]",
            "Name=越界槽位应被忽略",
            "",
            "[不存在的BUFF名]",
            "Stat.MaxAC=1",
            "",
            "[3]",
            "Stat.不存在的属性=5",
            "Stat.MaxAC=abc",
            ""
        });

        File.WriteAllText(Path.Combine(".", "Configs", "BuffSystem.ini"), ini);

        Console.WriteLine("=== 2. 加载 ===");

        BuffConfig.Load();

        // 期望 4 条：槽位1、槽位2、槽位3（属性全非法 → 空属性但仍注册）、隐身术
        // 被忽略的：[99] 越界槽位、[不存在的BUFF名] 枚举名不存在
        Console.WriteLine($"  解析出 {BuffConfig.Defs.Count} 条定义（期望 4）");
        Check(BuffConfig.Defs.Count == 4, "越界槽位 [99] 与不存在的节名被忽略");

        BuffDef three = BuffConfig.Get(BuffConfig.SlotToType(3));
        Check(three != null && three.Stats.Count == 0, "槽位3 的非法属性行被拦掉且不影响加载（空属性）");

        BuffDef one = BuffConfig.Get(BuffConfig.SlotToType(1));
        BuffDef two = BuffConfig.Get(BuffConfig.SlotToType(2));
        BuffDef hide = BuffConfig.Get(BuffType.隐身术);

        Check(one != null && one.Name == "羽化登仙" && one.Icon == 30034, "槽位1 名称/图标解析");
        Check(one != null && one.Stats.Count == 2, "槽位1 属性条数 = 2（坏的 Stat.不存在 / 非数字已被拦掉）");
        Check(one != null && one.Stats[0].Stat == Stat.MaxAC && one.Stats[0].Base == 5 && one.Stats[0].PerLevel == 2,
            "槽位1 MaxAC = 5+2");

        Check(two != null && two.Property.HasFlag(BuffProperty.Debuff) && two.Property.HasFlag(BuffProperty.RemoveOnDeath),
            "槽位2 多标记 Debuff|RemoveOnDeath");
        Check(two != null && two.Stats.Count == 2 && two.Stats[0].Base == -10, "槽位2 支持负数");

        Check(hide != null && !hide.IsCustom && hide.Stats.Count == 1 && hide.Stats[0].Stat == Stat.攻击速度,
            "已有 BUFF「隐身术」按枚举名挂上");
        Check(hide != null && !hide.HasVisible, "隐身术没写 Visible → 不覆盖（关键：不能把原有行为改坏）");

        Console.WriteLine("=== 3. 槽位 ↔ 枚举 映射 ===");

        Check((int)BuffConfig.SlotToType(1) == 230, "槽位1 → 枚举值 230");
        Check((int)BuffConfig.SlotToType(24) == 253, "槽位24 → 枚举值 253（byte 上限内）");
        Check(BuffConfig.TypeToSlot(BuffConfig.SlotToType(7)) == 7, "槽位7 往返一致");
        Check(BuffConfig.TypeToSlot(BuffType.隐身术) == 0, "已有 BUFF 的槽位号为 0");

        Console.WriteLine("=== 4. 按等级算属性（等级 10）===");

        Stats stats = one.BuildStats(10);
        Console.WriteLine($"  MaxAC={stats[Stat.MaxAC]}（期望 25 = 5 + 2×10）  HP={stats[Stat.HP]}（期望 100）");
        Check(stats[Stat.MaxAC] == 25, "基础 + 每级递增 计算正确");
        Check(stats[Stat.HP] == 100, "只有基础值时不随等级变化");

        Console.WriteLine("=== 5. 保存回文件 ===");

        BuffConfig.Save();

        string saved = File.ReadAllText(Path.Combine(".", "Configs", "BuffSystem.ini"));
        Check(saved.Contains("[1]") && saved.Contains("Stat.MaxAC=5+2"), "保存后能写回 Stat=基础+每级");
        Check(saved.Contains("[隐身术]"), "保存后保留挂已有 BUFF 的节");

        BuffConfig.Load();
        Check(BuffConfig.Defs.Count == 4, "保存 → 重新加载，条数一致（往返无损）");

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "★ 全部通过" : $"★ 有 {_failed} 项失败");
        Console.WriteLine();
        Console.WriteLine("=== 生成的配置文件 ===");
        Console.WriteLine(saved);

        Environment.Exit(_failed == 0 ? 0 : 1);
    }
}
