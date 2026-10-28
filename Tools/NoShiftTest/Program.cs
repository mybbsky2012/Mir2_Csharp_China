using System.Text;
using System.Text.RegularExpressions;

// ═══════════════════════════════════════════════════════════════════
// 免shift（内挂第 3 页）自动排雷
//
// 为什么不用「引用 Client.dll 跑单测」：
//   客户端的 CheckInput 是 MapControl 的私有方法，依赖 SlimDX / WinForms / 全局静态状态，
//   离线跑不起来（要真设备、真地图、真账号）。所以沿用 SimpleAppearanceTest 的做法：
//     ① 从**源码原文**里提取判定语句（正则现读，代码一改测试立刻跟着变，不会假通过）
//     ② 用独立实现的真值表校验判定逻辑是否符合设计意图
//     ③ 直接检查编译产物 Client.dll 里是否真的带了 NoShift
//
// 用法：NoShiftTest.exe <源码根目录> [Client.dll 路径]
// ═══════════════════════════════════════════════════════════════════

internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, Func<string> body)
    {
        try
        {
            string detail = body();
            _pass++;
            Console.WriteLine("  [OK]   " + name + (string.IsNullOrEmpty(detail) ? "" : "  → " + detail));
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine("  [FAIL] " + name + "\n         " + ex.Message);
        }
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string root = args.Length > 0 ? args[0] : @"D:\mir2-20241027";
        string dllPath = args.Length > 1 ? args[1] : Path.Combine(root, @"Build\Client\Release\Client.dll");

        string settingsPath = Path.Combine(root, @"Client\Settings.cs");
        string scenePath = Path.Combine(root, @"Client\MirScenes\GameScene.cs");
        string dialogPath = Path.Combine(root, @"Client\MirScenes\Dialogs\AutoPlayDialog.cs");

        Console.WriteLine("源码根目录： " + root);
        Console.WriteLine("客户端产物： " + dllPath);
        Console.WriteLine();

        foreach (string p in new[] { settingsPath, scenePath, dialogPath })
        {
            if (!File.Exists(p))
            {
                Console.WriteLine("找不到文件：" + p);
                return 2;
            }
        }

        string settings = File.ReadAllText(settingsPath);
        string scene = File.ReadAllText(scenePath);
        string dialog = File.ReadAllText(dialogPath);

        string[] sceneLines = scene.Replace("\r\n", "\n").Split('\n');

        Console.WriteLine("──── A. 配置项（Settings.cs）────");
        Check("字段 NoShift 已声明（默认 false）", () =>
        {
            Assert(Regex.IsMatch(settings, @"NoShift\s*=\s*false\s*,"), "没找到 `NoShift = false,`");
            return "默认关闭";
        });
        Check("Load 从 [Game] NoShift 读取", () =>
        {
            Assert(Regex.IsMatch(settings, @"NoShift\s*=\s*Reader\.ReadBoolean\(\s*""Game""\s*,\s*""NoShift"""), "没找到 ReadBoolean(\"Game\", \"NoShift\")");
            return "启动时读回";
        });
        Check("Save 写回 [Game] NoShift", () =>
        {
            Assert(Regex.IsMatch(settings, @"Reader\.Write\(\s*""Game""\s*,\s*""NoShift""\s*,\s*NoShift\s*\)"), "没找到 Reader.Write(\"Game\", \"NoShift\", NoShift)");
            return "开关能持久化";
        });

        Console.WriteLine();
        Console.WriteLine("──── B. 判定点（GameScene.CheckInput）────");

        int idxNpcBreak = IndexOfLine(sceneLines, "if (MapObject.MouseObject is NPCObject) break;");
        int idxPlayerBreak = IndexOfLine(sceneLines, "MapObject.MouseObject is PlayerObject && MapObject.MouseObject != User && !Settings.NoShift");
        int idxAltMine = IndexOfLine(sceneLines, "if (CMain.Alt && !User.RidingMount)");
        int idxNoShiftTarget = IndexOfLine(sceneLines, "bool noShiftTarget = Settings.NoShift && MapObject.MouseObject != null");
        int idxForceBranch = IndexOfLine(sceneLines, "if (CMain.Shift || noShiftTarget)");
        int idxHoldAttack = IndexOfLine(sceneLines, "MapObject.TargetObject.Name.EndsWith(\")\") || MapObject.TargetObject is PlayerObject) &&");
        int idxChaseGuard = IndexOfLine(sceneLines, "(!CMain.Shift && !(Settings.NoShift && MapObject.TargetObject != User))");
        int idxCursor = IndexOfLine(sceneLines, "if (CMain.Shift || Settings.NoShift)");

        Check("①点玩家不再无条件 break：改为「未勾免shift 才 break」", () =>
        {
            Assert(idxPlayerBreak > 0, "没找到 `... && !Settings.NoShift) break;`");
            return "第 " + (idxPlayerBreak + 1) + " 行";
        });
        Check("②点 NPC 仍然直接 break（免shift 不能把 NPC 变成可打目标）", () =>
        {
            Assert(idxNpcBreak > 0, "没找到 `if (MapObject.MouseObject is NPCObject) break;`");
            Assert(!sceneLines[idxNpcBreak].Contains("NoShift"), "NPC 那行被加了 NoShift 条件，会让点 NPC 走进攻击分支");
            return "第 " + (idxNpcBreak + 1) + " 行（无 NoShift）";
        });
        Check("③免shift 目标判定：非空 + 别的玩家 + 宝宝（名字带 \")\"）", () =>
        {
            Assert(idxNoShiftTarget > 0, "没找到 `bool noShiftTarget = ...`");
            string block = Join(sceneLines, idxNoShiftTarget, 5);
            Assert(block.Contains("MapObject.MouseObject != null"), "缺 null 判断 → 点空地会 NRE");
            Assert(block.Contains("MapObject.MouseObject is PlayerObject && MapObject.MouseObject != User"), "缺「别的玩家」判定（不能把自己算进去）");
            Assert(block.Contains("MapObject.MouseObject is MonsterObject"), "缺「宝宝（MonsterObject）」判定");
            Assert(block.Contains("Name != null"), "缺 Name 的空判断 → 无名对象会 NRE");
            Assert(block.Contains("Name.EndsWith(\")\")"), "缺宝宝名字后缀判定");
            return "第 " + (idxNoShiftTarget + 1) + " 行起 5 行";
        });
        Check("④强制攻击分支 = Shift 或 免shift 目标", () =>
        {
            Assert(idxForceBranch > 0, "没找到 `if (CMain.Shift || noShiftTarget)`");
            return "第 " + (idxForceBranch + 1) + " 行";
        });
        Check("⑤按住左键连打（TargetObject）也认免shift，且排除自己", () =>
        {
            Assert(idxHoldAttack > 0, "持续攻击条件里没找到免shift 兜底");
            string block = Join(sceneLines, idxHoldAttack, 3);
            Assert(block.Contains("(CMain.Shift || (Settings.NoShift && MapObject.TargetObject != User))"),
                "持续攻击的免shift 分支缺「排除自己」（点自己会一直空挥）");
            return "第 " + (idxHoldAttack + 1) + " 行";
        });
        Check("⑥追击守卫（走过去打）也认免shift，且排除自己", () =>
        {
            Assert(idxChaseGuard > 0, "没找到 `(!CMain.Shift && !(Settings.NoShift && MapObject.TargetObject != User))`");
            return "第 " + (idxChaseGuard + 1) + " 行";
        });
        Check("⑦悬停玩家的红剑光标也认免shift", () =>
        {
            Assert(idxCursor > 0, "没找到 `if (CMain.Shift || Settings.NoShift)`");
            return "第 " + (idxCursor + 1) + " 行";
        });
        Check("⑧免shift 判定必须排在 Alt 挖矿之后（Alt 点人仍是挖矿，不误伤）", () =>
        {
            Assert(idxAltMine > 0 && idxAltMine < idxNoShiftTarget,
                string.Format("顺序不对：Alt 挖矿第 {0} 行、免shift 第 {1} 行", idxAltMine + 1, idxNoShiftTarget + 1));
            return string.Format("Alt 第 {0} 行 < 免shift 第 {1} 行", idxAltMine + 1, idxNoShiftTarget + 1);
        });
        Check("⑨服务端 / Shared 没有被牵连（纯客户端改动）", () =>
        {
            string[] dirs = { Path.Combine(root, "Server"), Path.Combine(root, "Shared") };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                foreach (string f in Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
                {
                    if (f.Contains(@"\obj\") || f.Contains(@"\bin\")) continue;
                    if (File.ReadAllText(f).Contains("NoShift"))
                        throw new Exception("服务端/Shared 里出现了 NoShift：" + f);
                }
            }
            return "Server / Shared 干净";
        });

        Console.WriteLine();
        Console.WriteLine("──── C. 面板控件（AutoPlayDialog 第 3 页）────");
        Check("控件字段 NoShiftBox 已声明", () =>
        {
            Assert(Regex.IsMatch(dialog, @"public MirCheckBox NoShiftBox\s*;"), "没找到 `public MirCheckBox NoShiftBox;`");
            return "";
        });
        Check("勾选框落在第 3 页 (162, 260)，文字「免shift」", () =>
        {
            Match m = Regex.Match(dialog, @"NoShiftBox\s*=\s*Reg\(P,\s*CreateBox\(\s*162\s*,\s*260\s*,\s*""免shift""\s*,\s*Settings\.NoShift\s*\)\)");
            Assert(m.Success, "没找到 CreateBox(162, 260, \"免shift\", Settings.NoShift)");
            return m.Value.Substring(0, Math.Min(60, m.Value.Length));
        });
        Check("勾选后写回 Settings 并立刻 Save（重启也记得）", () =>
        {
            Match m = Regex.Match(dialog, @"NoShiftBox\.Click\s*\+=[\s\S]{0,400}?Settings\.NoShift\s*=\s*NoShiftBox\.Checked\s*;[\s\S]{0,120}?Settings\.Save\(\)\s*;");
            Assert(m.Success, "Click 里没有「写回 Settings + Save」");
            return "已保存";
        });
        Check("勾选/取消都有玩家可见的提示（不自闭）", () =>
        {
            Assert(dialog.Contains("免shift已开启"), "没找到开启提示");
            Assert(dialog.Contains("免shift已关闭"), "没找到关闭提示");
            return "";
        });
        Check("第 3 页控件无重叠（免shift 落在右列 y=260 的空位）", () =>
        {
            Match page = Regex.Match(dialog, @"private void BuildPageOther\(\)\s*\{[\s\S]*?\n        \}");
            Assert(page.Success, "没找到 BuildPageOther");

            var boxes = new List<(int X, int Y, string Text)>();
            foreach (Match m in Regex.Matches(page.Value, @"CreateBox\(\s*(\d+)\s*,\s*(\d+)\s*,\s*""([^""]*)"""))
                boxes.Add((int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Value));

            Assert(boxes.Count >= 12, "第 3 页控件数量异常：" + boxes.Count);
            Assert(boxes.Any(b => b.Text == "免shift" && b.X == 162 && b.Y == 260), "免shift 不在 (162,260)");

            // 同一行两列之间至少留 120px（右列 x=162、左列 x=26，差 136）
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    var a = boxes[i];
                    var b = boxes[j];
                    if (Math.Abs(a.Y - b.Y) < 20 && Math.Abs(a.X - b.X) < 120)
                        throw new Exception(string.Format("控件重叠：\"{0}\"({1},{2}) 与 \"{3}\"({4},{5})", a.Text, a.X, a.Y, b.Text, b.X, b.Y));
                }
            }

            return boxes.Count + " 个勾选框，无重叠";
        });
        Check("第 3 页所有提示文字都在面板高度内（483）", () =>
        {
            Match page = Regex.Match(dialog, @"private void BuildPageOther\(\)\s*\{[\s\S]*?\n        \}");
            int maxBottom = 0;
            foreach (Match m in Regex.Matches(page.Value, @"CreateHint\(\s*(\d+)\s*,\s*""[^""]*""\s*(?:,\s*(\d+)\s*)?\)"))
            {
                int y = int.Parse(m.Groups[1].Value);
                int h = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 32;
                maxBottom = Math.Max(maxBottom, y + h);
            }
            Assert(maxBottom <= 483, "提示文字超出面板底部：" + maxBottom);
            return "最高到 y=" + maxBottom;
        });

        Console.WriteLine();
        Console.WriteLine("──── D. 判定真值表（设计意图校验）────");
        RunTruthTable();

        Console.WriteLine();
        Console.WriteLine("──── E. 产物校验（Client.dll）────");
        Check("Client.dll 存在", () =>
        {
            Assert(File.Exists(dllPath), "找不到 " + dllPath + "（先编译客户端）");
            return new FileInfo(dllPath).Length / 1024 + " KB";
        });
        Check("Client.dll 内含 NoShift 字段/字符串", () =>
        {
            byte[] bytes = File.ReadAllBytes(dllPath);
            string text = Encoding.UTF8.GetString(bytes);
            Assert(text.Contains("NoShift"), "产物里没有 NoShift —— 编译没带上这次的改动？");
            return "命中 " + CountOccurrences(text, "NoShift") + " 处";
        });
        Check("Client.dll 内含「免shift」界面文字", () =>
        {
            byte[] bytes = File.ReadAllBytes(dllPath);
            // .NET 元数据里：标识符在 #Strings 堆（UTF-8），而代码里的中文字面量在 #US 堆（UTF-16LE）。
            // 所以中文必须按 UTF-16 小端去找，用 UTF8.GetString 是找不到的（这里踩过一次）。
            Assert(ContainsUtf16(bytes, "免shift"), "产物里没有「免shift」—— 面板没编译进去？");
            return "UTF-16 命中 " + CountOccurrencesUtf16(bytes, "免shift") + " 处";
        });
        Check("Client.dll 内含免shift 的开关提示语", () =>
        {
            byte[] bytes = File.ReadAllBytes(dllPath);
            Assert(ContainsUtf16(bytes, "免shift已开启"), "产物里没有「免shift已开启」提示语");
            return "";
        });

        Console.WriteLine();
        Console.WriteLine("════════════════════════════════════════");
        Console.WriteLine(string.Format("结果：{0} 项通过，{1} 项失败", _pass, _fail));
        return _fail == 0 ? 0 : 1;
    }

    // ───────────────────────── 真值表 ─────────────────────────

    private enum MouseTarget
    {
        None,          // 空地（MouseObject == null）
        Npc,
        Self,          // 自己
        OtherPlayer,   // 别的玩家
        Monster,       // 普通怪物
        Pet,           // 宝宝（MonsterObject，名字以 ")" 结尾）
    }

    /// <summary>与源码 noShiftTarget 同构：只有「别的玩家」和「宝宝」算免shift 目标。</summary>
    private static bool IsNoShiftTarget(MouseTarget t) => t == MouseTarget.OtherPlayer || t == MouseTarget.Pet;

    /// <summary>是否走「强制攻击」分支（源码：if (CMain.Shift || noShiftTarget)）。</summary>
    private static bool WillForceAttack(bool shift, bool noShift, MouseTarget t) => shift || (noShift && IsNoShiftTarget(t));

    /// <summary>点这一下最终会不会打出去（含普通怪物那条不需要 Shift 的老路）。</summary>
    private static bool WillAttack(bool shift, bool noShift, MouseTarget t)
    {
        if (t == MouseTarget.None || t == MouseTarget.Npc) return false;
        // 自己：原版按 Shift 点自己照样会排一次近距攻击（服务端拒绝），保持一致；
        //      免shift 侧源码已显式排除自己，不会因为常开而误触空挥。
        if (t == MouseTarget.Self) return shift;
        if (t == MouseTarget.Monster) return true;                  // 老逻辑：怪物本来就不用 Shift
        return WillForceAttack(shift, noShift, t);                  // 玩家 / 宝宝
    }

    /// <summary>「按住左键持续攻击」条件（源码 15078 行同构）。</summary>
    private static bool ContinuesAttack(bool shift, bool noShift, MouseTarget t)
    {
        if (t == MouseTarget.Self) return shift;
        bool petOrPlayer = t == MouseTarget.OtherPlayer || t == MouseTarget.Pet;
        bool plainMonster = t == MouseTarget.Monster;
        return (petOrPlayer && (shift || noShift)) || plainMonster;
    }

    private static void RunTruthTable()
    {
        // 1) 关着免shift 时，行为必须和原版完全一致
        Check("关着免shift：点玩家不打 / Shift 点玩家才打", () =>
        {
            Assert(!WillAttack(false, false, MouseTarget.OtherPlayer), "没按 Shift 就打人了 —— 原版行为被改坏");
            Assert(WillAttack(true, false, MouseTarget.OtherPlayer), "Shift 点玩家打不出去");
            Assert(!WillAttack(false, false, MouseTarget.Pet), "没按 Shift 就打宝宝");
            Assert(WillAttack(true, false, MouseTarget.Pet), "Shift 点宝宝打不出去");
            return "与原版一致";
        });

        // 2) 开了免shift：玩家 / 宝宝直接打
        Check("开着免shift：点玩家、点宝宝直接打（不用 Shift）", () =>
        {
            Assert(WillAttack(false, true, MouseTarget.OtherPlayer), "免shift 点玩家不攻击");
            Assert(WillAttack(false, true, MouseTarget.Pet), "免shift 点宝宝不攻击");
            return "玩家 / 宝宝 ✓";
        });

        // 3) 开着免shift，绝不能误伤到「不该打」的目标
        Check("开着免shift：点空地 / NPC / 自己 一律不打", () =>
        {
            Assert(!WillAttack(false, true, MouseTarget.None) && !ContinuesAttack(false, true, MouseTarget.None), "点空地会被当成攻击目标（会原地乱挥，且不能走路）");
            Assert(!WillAttack(false, true, MouseTarget.Npc), "点 NPC 也攻击了（会打不到 NPC 还挡掉对话）");
            Assert(!WillAttack(false, true, MouseTarget.Self), "点自己也算攻击目标");
            return "空地 / NPC / 自己 全部安全";
        });

        // 4) 普通怪物不受影响
        Check("点自己：免shift 常开也不会一直空挥（Shift 老行为保留）", () =>
        {
            Assert(!WillAttack(false, true, MouseTarget.Self) && !ContinuesAttack(false, true, MouseTarget.Self),
                "免shift 下点自己会排队攻击（常开时一点自己就空挥，很烦）");
            Assert(WillAttack(true, false, MouseTarget.Self), "Shift 点自己的老行为被改掉了（本不该动）");
            return "免shift 不算，Shift 照旧";
        });

        // 5) 普通怪物不受影响
        Check("普通怪物：开不开免shift 都照常打（走原来那条路）", () =>
        {
            Assert(WillAttack(false, false, MouseTarget.Monster), "关着免shift 打不了怪");
            Assert(WillAttack(false, true, MouseTarget.Monster), "开着免shift 打不了怪");
            Assert(!WillForceAttack(false, true, MouseTarget.Monster), "怪物误入强制攻击分支（会绕过走位/射程判定）");
            return "不受影响";
        });

        // 5) 持续攻击（按住左键）与单击一致
        Check("按住左键连打：玩家/宝宝的判定与单击完全一致", () =>
        {
            foreach (MouseTarget t in new[] { MouseTarget.OtherPlayer, MouseTarget.Pet })
            {
                foreach (bool shift in new[] { false, true })
                {
                    foreach (bool noShift in new[] { false, true })
                    {
                        bool single = WillAttack(shift, noShift, t);
                        bool hold = ContinuesAttack(shift, noShift, t);
                        Assert(single == hold,
                            string.Format("目标 {0}、Shift={1}、免shift={2}：单击 {3} / 连打 {4} 不一致",
                                t, shift, noShift, single, hold));
                    }
                }
            }
            return "8 种组合一致";
        });

        // 6) 全组合打印（人工可核对）
        Console.WriteLine();
        Console.WriteLine("         目标        Shift  免shift   会攻击   连打");
        foreach (MouseTarget t in Enum.GetValues<MouseTarget>())
        {
            foreach (bool shift in new[] { false, true })
            {
                foreach (bool noShift in new[] { false, true })
                {
                    Console.WriteLine(string.Format("         {0,-11} {1,-6} {2,-8} {3,-8} {4}",
                        t, shift ? "是" : "否", noShift ? "是" : "否",
                        WillAttack(shift, noShift, t) ? "打" : "不打",
                        ContinuesAttack(shift, noShift, t) ? "打" : "不打"));
                }
            }
        }
    }

    // ───────────────────────── 工具 ─────────────────────────

    private static int IndexOfLine(string[] lines, string needle)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(needle, StringComparison.Ordinal)) return i;
        }
        return -1;
    }

    private static string Join(string[] lines, int start, int count)
    {
        var sb = new StringBuilder();
        for (int i = start; i < Math.Min(lines.Length, start + count); i++)
            sb.AppendLine(lines[i]);
        return sb.ToString();
    }

    private static int CountOccurrences(string text, string needle)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    /// <summary>在字节流里按 UTF-16LE 找字符串（#US 堆里的中文字面量就是这个编码）。</summary>
    private static bool ContainsUtf16(byte[] haystack, string needle) => CountOccurrencesUtf16(haystack, needle) > 0;

    private static int CountOccurrencesUtf16(byte[] haystack, string needle)
    {
        byte[] pattern = Encoding.Unicode.GetBytes(needle);
        int count = 0;

        for (int i = 0; i + pattern.Length <= haystack.Length; i++)
        {
            bool match = true;
            for (int k = 0; k < pattern.Length; k++)
            {
                if (haystack[i + k] != pattern[k]) { match = false; break; }
            }
            if (match) count++;
        }

        return count;
    }
}
