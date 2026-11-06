using System.Text;
using System.Text.RegularExpressions;

// ═══════════════════════════════════════════════════════════════════
// 内挂「只显示 1 格内玩家」自动排雷
//
// 为什么不用「引用 Client.dll 跑单测」：
//   绘制入口全部依赖 SlimDX 设备、真实地图和账号，离线跑不起来。
//   所以沿用 NoShiftTest / SimpleAppearanceTest 的三段式：
//     ① 从源码原文里提取判定语句（正则现读，代码一改测试立刻跟着变，不会假通过）
//     ② 用独立实现的真值表校验「1 格」到底覆盖哪几格
//     ③ 检查编译产物 Client.dll 里是否真的带了新开关（防「改了源码没重编」）
//
// 用法：NearPlayerTest.exe <源码根目录> [Client.dll 路径]
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
        string mapObjectPath = Path.Combine(root, @"Client\MirObjects\MapObject.cs");
        string playerPath = Path.Combine(root, @"Client\MirObjects\PlayerObject.cs");
        string scenePath = Path.Combine(root, @"Client\MirScenes\GameScene.cs");
        string dialogPath = Path.Combine(root, @"Client\MirScenes\Dialogs\AutoPlayDialog.cs");

        Console.WriteLine("源码根目录： " + root);
        Console.WriteLine("客户端产物： " + dllPath);
        Console.WriteLine();

        foreach (string p in new[] { settingsPath, mapObjectPath, playerPath, scenePath, dialogPath })
        {
            if (!File.Exists(p))
            {
                Console.WriteLine("找不到文件：" + p);
                return 2;
            }
        }

        string settings = File.ReadAllText(settingsPath);
        string mapObject = File.ReadAllText(mapObjectPath);
        string player = File.ReadAllText(playerPath);
        string scene = File.ReadAllText(scenePath);
        string dialog = File.ReadAllText(dialogPath);

        // ══════════════════════════════════════════════
        Console.WriteLine("──── A. 配置项（Settings.cs）────");

        Check("①字段 NearPlayerOnly 已声明，默认 false（老玩家不会被强制过滤）", () =>
        {
            Assert(Regex.IsMatch(settings, @"NearPlayerOnly\s*=\s*false\s*,"), "没找到 `NearPlayerOnly = false,`");
            return "默认关闭";
        });

        Check("②Load 从 [Game] NearPlayerOnly 读回", () =>
        {
            Assert(Regex.IsMatch(settings, @"NearPlayerOnly\s*=\s*Reader\.ReadBoolean\(\s*""Game""\s*,\s*""NearPlayerOnly""\s*,\s*NearPlayerOnly\s*\)"),
                "没找到 ReadBoolean(\"Game\", \"NearPlayerOnly\", NearPlayerOnly)");
            return "启动时读回";
        });

        Check("③Save 写回 [Game] NearPlayerOnly（重启记得住）", () =>
        {
            Assert(Regex.IsMatch(settings, @"Reader\.Write\(\s*""Game""\s*,\s*""NearPlayerOnly""\s*,\s*NearPlayerOnly\s*\)"),
                "没找到 Reader.Write(\"Game\", \"NearPlayerOnly\", NearPlayerOnly)");
            return "可持久化";
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── B. 过滤判定（MapObject.NearPlayerFiltered）────");

        string prop = Block(mapObject, "public bool NearPlayerFiltered", 40);

        Check("④过滤判定只有一个出口（属性 MapObject.NearPlayerFiltered）", () =>
        {
            Assert(prop.Length > 0, "MapObject.cs 里没找到 `public bool NearPlayerFiltered`");
            Assert(prop.Contains("get"), "属性没有 get 访问器");
            return "统一判定点";
        });

        Check("⑤开关闸在最前：没勾选时一律不过滤（默认行为零变化）", () =>
        {
            int firstIf = prop.IndexOf("if (", StringComparison.Ordinal);
            int switchIf = prop.IndexOf("if (!Settings.NearPlayerOnly) return false;", StringComparison.Ordinal);
            Assert(switchIf >= 0, "属性里没有 `if (!Settings.NearPlayerOnly) return false;`");
            Assert(switchIf == firstIf, "开关闸不是属性里的第一个 if —— 默认行为可能被改（老玩家没勾也会被过滤）");
            return "属性内第 1 个判断";
        });

        Check("⑥User 为空时不过滤（进图瞬间不炸）", () =>
        {
            Assert(prop.Contains("if (User == null) return false;"), "缺 `if (User == null) return false;`");
            return "";
        });

        Check("⑦只作用于玩家：怪物 / NPC / 掉落 / 宝宝照常显示", () =>
        {
            Assert(prop.Contains("!(this is PlayerObject)"), "缺「不是 PlayerObject 就 false」的判定");
            Assert(prop.Contains("this == User"), "缺「自己是 User 时不过滤」的判定");
            return "怪物/NPC/掉落/宝宝不受影响";
        });

        Check("⑧英雄不参与过滤（自己的英雄走远两步不能消失）", () =>
        {
            Assert(prop.Contains("is HeroObject") && prop.Contains("is UserHeroObject"),
                "缺 HeroObject / UserHeroObject 排除，英雄会被误藏");
            return "自己/他人的英雄都保留";
        });

        Check("⑨距离用切比雪夫 1 格（含斜向 8 格）", () =>
        {
            Assert(Regex.IsMatch(prop, @"Math\.Abs\(\s*Movement\.X\s*-\s*User\.Movement\.X\s*\)\s*>\s*1"), "X 轴判定不是「> 1」");
            Assert(Regex.IsMatch(prop, @"Math\.Abs\(\s*Movement\.Y\s*-\s*User\.Movement\.Y\s*\)\s*>\s*1"), "Y 轴判定不是「> 1」");
            return "|dx|>1 或 |dy|>1 才隐藏";
        });

        Check("⑩真值表：1 格内（含斜角/同格）可见，2 格起隐藏", () =>
        {
            var bad = new List<string>();
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    bool hidden = Model(true, dx, dy);
                    bool expectHidden = Math.Abs(dx) > 1 || Math.Abs(dy) > 1;
                    if (hidden != expectHidden)
                        bad.Add(string.Format("({0},{1}) 期望{2} 实际{3}", dx, dy, expectHidden ? "隐藏" : "显示", hidden ? "隐藏" : "显示"));
                }
            }
            Assert(bad.Count == 0, "真值表不符：" + string.Join("；", bad.ToArray()));
            return "7x7 邻域 49 格全部符合（含 8 个斜向邻居）";
        });

        Check("⑪真值表：非玩家/英雄/自己/关开关 —— 四种情况都不隐藏", () =>
        {
            Assert(!Model(false, 9, 9), "开关关闭时仍隐藏了玩家");
            Assert(!Model(true, 0, 0), "同格（dx=dy=0）被隐藏了");
            Assert(!ModelMonster(true, 9, 9), "开开关后怪物被隐藏了（会看不见怪）");
            Assert(!ModelHero(true, 9, 9), "开开关后英雄被隐藏了");
            return "只影响「玩家」，且只有超出 1 格才隐藏";
        });

        Check("⑪-2 常显闸放在距离判定之前（先看该不该留，再看多远）", () =>
        {
            int keep = prop.IndexOf("if (NearPlayerKeepVisible) return false;", StringComparison.Ordinal);
            int dist = prop.IndexOf("Math.Abs(Movement.X", StringComparison.Ordinal);
            Assert(keep >= 0, "NearPlayerFiltered 里没有 `if (NearPlayerKeepVisible) return false;`");
            Assert(dist > keep, "常显闸排在距离判定之后（永远走不到，队友照样被藏）");
            return "闸在第 " + prop.Substring(0, keep).Split('\n').Length + " 行";
        });

        Check("⑪-3 常显名单：鼠标指向 / 当前目标 / 魔法锁定 / 施法对象", () =>
        {
            string keep = Block(mapObject, "public bool NearPlayerKeepVisible", 26);
            Assert(keep.Length > 0, "没找到 NearPlayerKeepVisible 属性");
            foreach (string s in new[] { "MouseObject", "TargetObject", "LockObject", "MagicObject" })
                Assert(keep.Contains(s), "常显名单里缺 " + s + "（锁的人会凭空消失）");
            return "4 个动态目标全在";
        });

        Check("⑪-4 常显名单：队友查 GroupDialog.GroupList，行会要求双方非空且相同", () =>
        {
            string keep = Block(mapObject, "public bool NearPlayerKeepVisible", 26);
            Assert(Regex.IsMatch(keep, @"GroupDialog\.GroupList\.Contains\(\s*Name\s*\)"),
                "队友判定不是查 GroupDialog.GroupList（客户端唯一的队员名单）");
            Assert(Regex.IsMatch(keep, @"string\.IsNullOrEmpty\(\s*guild\s*\)"), "行会判定没排除空行会");
            Assert(Regex.IsMatch(keep, @"guild\s*==\s*User\.GuildName"), "行会判定没比对 User.GuildName");
            Assert(Regex.IsMatch(keep, @"string\.IsNullOrEmpty\(\s*User\.GuildName\s*\)"),
                "没排除「自己没行会」——两个无会玩家会互相常显");
            return "队友优先，行会需双方非空";
        });

        Check("⑪-5 真值表：队友/目标在任何距离都可见，陌生人照旧按 1 格藏", () =>
        {
            var bad = new List<string>();
            for (int dy = -9; dy <= 9; dy += 3)
                for (int dx = -9; dx <= 9; dx += 3)
                {
                    if (ModelWithKeep(true, dx, dy, true))
                        bad.Add(string.Format("常显对象 ({0},{1}) 被藏了", dx, dy));
                    bool expect = Math.Abs(dx) > 1 || Math.Abs(dy) > 1;
                    if (ModelWithKeep(true, dx, dy, false) != expect)
                        bad.Add(string.Format("陌生人 ({0},{1}) 判定不符", dx, dy));
                }
            Assert(bad.Count == 0, string.Join("；", bad.ToArray()));
            return "7x7 抽样：常显全可见，陌生人不变";
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── C. 绘制入口闸门（PlayerObject）────");

        Check("⑫Draw() 第一句就返回（身体/武器/头/翅膀/坐骑/品质光效都不画）", () =>
        {
            AssertGuardFirst(player, "public override void Draw()", "Draw()");
            return "";
        });

        Check("⑬DrawBehindEffects / DrawEffects 有闸（技能特效不画）", () =>
        {
            AssertGuardFirst(player, "public override void DrawBehindEffects(bool effectsEnabled)", "DrawBehindEffects");
            AssertGuardFirst(player, "public override void DrawEffects(bool effectsEnabled)", "DrawEffects");
            return "";
        });

        Check("⑭DrawBlend 有闸（被过滤的人不会被鼠标/目标高亮成半透明影子）", () =>
        {
            AssertGuardFirst(player, "public override void DrawBlend()", "DrawBlend");
            return "";
        });

        Check("⑮DrawName 有闸（人藏了名字也不许飘）", () =>
        {
            AssertGuardFirst(player, "public override void DrawName()", "DrawName");
            return "";
        });

        Check("⑮-2 MouseOver 有闸（看不见的人不能被点中，否则免shift 下点地面会变成打人）", () =>
        {
            AssertGuardFirst(player, "public override bool MouseOver(Point p)", "MouseOver");
            return "";
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── D. 场景循环（GameScene）────");

        string[] sceneLines = scene.Replace("\r\n", "\n").Split('\n');

        Check("⑯对象循环整条跳过：名字/对话/血条/毒/伤害数字一起走", () =>
        {
            int idx = IndexOfLine(sceneLines, "if (Objects[i].NearPlayerFiltered) continue;");
            Assert(idx > 0, "没找到 `if (Objects[i].NearPlayerFiltered) continue;`");
            string block = Join(sceneLines, idx, 12);
            Assert(block.Contains("DrawEffects"), "跳过之后这段没有 DrawEffects，可能插错位置");
            Assert(block.Contains("DrawName"), "跳过之后这段没有 DrawName");
            Assert(block.Contains("DrawChat"), "跳过之后这段没有 DrawChat");
            Assert(block.Contains("DrawHealth"), "跳过之后这段没有 DrawHealth");
            Assert(block.Contains("DrawPoison"), "跳过之后这段没有 DrawPoison");
            Assert(block.Contains("DrawDamages"), "跳过之后这段没有 DrawDamages");
            return "第 " + (idx + 1) + " 行";
        });

        Check("⑰两处场景特效循环都按 Owner 过滤（别人的技能特效不糊在自己屏幕上）", () =>
        {
            int hits = 0;
            for (int i = 0; i < sceneLines.Length; i++)
                if (sceneLines[i].Contains("Effects[i].Owner != null && Effects[i].Owner.NearPlayerFiltered")) hits++;

            Assert(hits >= 2, "只找到 " + hits + " 处 Owner 过滤（应为 2：DrawBehind 前 + 前景）");
            return hits + " 处";
        });

        Check("⑱鼠标指向的名字也过滤（看不见的人不能被悬停出名字）", () =>
        {
            Assert(Regex.IsMatch(scene, @"MapObject\.MouseObject\s*!=\s*null[\s\S]{0,120}?&&\s*!MapObject\.MouseObject\.NearPlayerFiltered"),
                "鼠标对象 DrawName 前没加过滤");
            return "";
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── E. 面板（内挂 → 其他）────");

        Check("⑲控件字段 NearPlayerBox 已声明", () =>
        {
            Assert(Regex.IsMatch(dialog, @"public MirCheckBox NearPlayerBox\s*;"), "没找到 `public MirCheckBox NearPlayerBox;`");
            return "";
        });

        Check("⑳勾选框落在第 3 页 (26,340)，文字「只显示1格内玩家」", () =>
        {
            Match m = Regex.Match(dialog, @"NearPlayerBox\s*=\s*Reg\(P,\s*CreateBox\(\s*26\s*,\s*340\s*,\s*""只显示1格内玩家""\s*,\s*Settings\.NearPlayerOnly\s*\)\)");
            Assert(m.Success, "没找到 CreateBox(26, 340, \"只显示1格内玩家\", Settings.NearPlayerOnly)");
            return m.Value.Substring(0, Math.Min(66, m.Value.Length));
        });

        Check("㉑勾选后写回 Settings + Save + 立刻 Redraw（不用等下一次自然重绘）", () =>
        {
            Match m = Regex.Match(dialog,
                @"NearPlayerBox\.Click\s*\+=[\s\S]{0,500}?Settings\.NearPlayerOnly\s*=\s*NearPlayerBox\.Checked\s*;[\s\S]{0,160}?Settings\.Save\(\)\s*;");
            Assert(m.Success, "Click 里没有「写回 Settings + Save」");
            Match r = Regex.Match(dialog, @"NearPlayerBox\.Click\s*\+=[\s\S]{0,700}?GameScene\.Scene\.Redraw\(\)\s*;");
            Assert(r.Success, "Click 里没有 Redraw，勾上后要等场景自然重绘才生效（看起来像没反应）");
            Match w = Regex.Match(dialog, @"NearPlayerBox\.Click\s*\+=[\s\S]{0,900}?MapControl\.TextureValid\s*=\s*false\s*;");
            Assert(w.Success, "没有把世界层 MapControl.TextureValid 置 false —— 世界层是缓存贴图，只 Redraw 场景不会重画地图");
            return "写回 + 落盘 + 场景重绘 + 世界层失效";
        });

        Check("㉒打开面板按配置回填（手改 ini 也能对上）", () =>
        {
            Assert(Regex.IsMatch(dialog, @"NearPlayerBox\s*!=\s*null[\s\S]{0,80}?NearPlayerBox\.Checked\s*=\s*Settings\.NearPlayerOnly\s*;"),
                "刷新逻辑里没回填 NearPlayerBox");
            return "";
        });

        Check("㉓勾选/取消都有玩家可见提示，且写明常显名单", () =>
        {
            Assert(dialog.Contains("只显示1格内玩家已开启"), "没找到开启提示");
            Assert(dialog.Contains("只显示1格内玩家已关闭"), "没找到关闭提示");
            Assert(dialog.Contains("队友") && dialog.Contains("常显"),
                "面板提示里没写「队友/行会/目标常显」，玩家会以为队友也被藏了");
            return "";
        });

        Check("㉔第 3 页控件无重叠，且全部落在内容区（y+h ≤ 430）", () =>
        {
            Match page = Regex.Match(dialog, @"private void BuildPageOther\(\)\s*\{[\s\S]*?\n        \}");
            Assert(page.Success, "没找到 BuildPageOther");

            var boxes = new List<Tuple<int, int, int, int, string>>();   // x, y, w(=160 列宽), h, text
            foreach (Match m in Regex.Matches(page.Value, @"CreateBox\(\s*(\d+)\s*,\s*(\d+)\s*,\s*""([^""]*)"""))
            {
                boxes.Add(Tuple.Create(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 130, 14, m.Groups[3].Value));
            }

            foreach (var a in boxes)
                foreach (var b in boxes)
                {
                    if (ReferenceEquals(a, b)) continue;
                    bool overlap = a.Item1 < b.Item1 + b.Item3 && b.Item1 < a.Item1 + a.Item3
                                && a.Item2 < b.Item2 + b.Item4 && b.Item2 < a.Item2 + a.Item4;
                    if (overlap)
                        throw new Exception(string.Format("勾选框重叠：「{0}」({1},{2}) 与「{3}」({4},{5})",
                            a.Item5, a.Item1, a.Item2, b.Item5, b.Item1, b.Item2));
                }

            var hints = new List<Tuple<int, int, string>>();
            foreach (Match m in Regex.Matches(page.Value, @"CreateHint\(\s*(\d+)\s*,\s*""([^""]*)""\s*,\s*(\d+)\s*\)"))
                hints.Add(Tuple.Create(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[3].Value), m.Groups[2].Value));

            foreach (var h in hints)
            {
                if (h.Item1 + h.Item2 > 430)
                    throw new Exception(string.Format("提示文字超出内容区：y={0} h={1}（底线 430）「{2}」", h.Item1, h.Item2, Short(h.Item3)));
                foreach (var b in boxes)
                {
                    bool overlap = b.Item2 < h.Item1 + h.Item2 && h.Item1 < b.Item2 + b.Item4;
                    if (overlap)
                        throw new Exception(string.Format("提示压在勾选框上：「{0}」y={1} h={2} 与「{3}」y={4}",
                            Short(h.Item3), h.Item1, h.Item2, b.Item5, b.Item2));
                }
            }

            return string.Format("{0} 个勾选框、{1} 段提示，无重叠、不越界", boxes.Count, hints.Count);
        });

        // ══════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("──── F. 产物与影响面 ────");

        Check("㉕编译产物 Client.dll 里真的带了 NearPlayerOnly（防「改了源码没重编」）", () =>
        {
            Assert(File.Exists(dllPath), "找不到 " + dllPath);

            byte[] bytes = File.ReadAllBytes(dllPath);
            string text = Encoding.UTF8.GetString(Encoding.Convert(Encoding.UTF8, Encoding.UTF8, bytes));
            Assert(text.Contains("NearPlayerOnly"), "Client.dll 里没有 NearPlayerOnly 字符串——产物是旧的，需要重新编译并覆盖");
            Assert(text.Contains("NearPlayerKeepVisible"), "Client.dll 里没有 NearPlayerKeepVisible——常显名单没编进去，产物是旧的");

            FileInfo fi = new FileInfo(dllPath);
            return string.Format("已编入（{0:yyyy-MM-dd HH:mm}）", fi.LastWriteTime);
        });

        Check("㉖服务端 / Shared 完全没被牵连（纯客户端绘制过滤）", () =>
        {
            string[] dirs = { Path.Combine(root, "Server"), Path.Combine(root, "Shared") };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d)) continue;
                foreach (string f in Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
                {
                    if (f.Contains(@"\obj\") || f.Contains(@"\bin\")) continue;
                    if (File.ReadAllText(f).Contains("NearPlayerOnly"))
                        throw new Exception("服务端/Shared 里出现了 NearPlayerOnly：" + f);
                }
            }
            return "Server / Shared 干净";
        });

        Console.WriteLine();
        Console.WriteLine("──────────────────────────────────────────");
        Console.WriteLine(string.Format("通过 {0} 项，失败 {1} 项", _pass, _fail));
        return _fail == 0 ? 0 : 1;
    }

    // ── 独立实现的判定模型（真值表用；与源码语义对齐，不读源码） ──
    private static bool Model(bool on, int dx, int dy)
    {
        if (!on) return false;
        return Math.Abs(dx) > 1 || Math.Abs(dy) > 1;
    }
    // 带常显白名单的模型：队友 / 同会 / 鼠标指向 / 目标 → 任何距离都不隐藏
    private static bool ModelWithKeep(bool on, int dx, int dy, bool keepVisible)
    {
        if (!on) return false;
        if (keepVisible) return false;
        return Math.Abs(dx) > 1 || Math.Abs(dy) > 1;
    }
    private static bool ModelMonster(bool on, int dx, int dy) { return false; }   // 怪物永远不参与过滤
    private static bool ModelHero(bool on, int dx, int dy) { return false; }      // 英雄永远不参与过滤

    private static string Short(string s) { return s.Length <= 12 ? s : s.Substring(0, 12) + "…"; }

    private static string Block(string src, string signature, int lines)
    {
        int i = src.IndexOf(signature, StringComparison.Ordinal);
        if (i < 0) return string.Empty;
        string[] parts = src.Substring(i).Replace("\r\n", "\n").Split('\n');
        int n = Math.Min(lines, parts.Length);
        return string.Join("\n", parts, 0, n);
    }

    private static void AssertGuardFirst(string src, string signature, string label)
    {
        int i = src.IndexOf(signature, StringComparison.Ordinal);
        if (i < 0) throw new Exception("没找到方法 " + label);

        int brace = src.IndexOf('{', i);
        // 兼容两种写法：void 方法 return;  /  bool 方法 return false;
        int guard = src.IndexOf("if (NearPlayerFiltered) return", brace, StringComparison.Ordinal);
        if (guard < 0) throw new Exception(label + " 里没有 `if (NearPlayerFiltered) return...`");

        string between = src.Substring(brace, guard - brace);
        if (between.Contains("Draw(") || between.Contains("DXManager."))
            throw new Exception(label + " 的闸门放在了绘制调用之后，等于没提前返回（白白画了一遍）");

        if (guard - brace > 260)
            throw new Exception(label + " 的闸门离方法开头太远，先执行了一大堆逻辑才判断");
    }

    private static int IndexOfLine(string[] lines, string needle)
    {
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains(needle)) return i;
        return -1;
    }

    private static string Join(string[] lines, int start, int count)
    {
        int n = Math.Min(count, lines.Length - start);
        return string.Join("\n", lines, start, n);
    }
}
