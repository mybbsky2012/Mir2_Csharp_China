using System.Text.RegularExpressions;

namespace HighlightGhostTest;

/// <summary>
/// 目标高亮「黄色残影」修复自测（2026-11-06）
///   1) 源码约束：高亮只能由 CellInfo.DrawObjects 紧跟本体调用，旧的后置补画必须已移除
///   2) 行为仿真：可见/已死/视野外/被过滤 四类对象的绘制顺序与遮挡关系
///   3) 产物校验：Client.dll 比源文件新，且含新方法
/// </summary>
internal static class Program
{
    private static int _pass, _fail;
    private static string _root = "";

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) { _pass++; Console.WriteLine($"  [PASS] {name}"); }
        else { _fail++; Console.WriteLine($"  [FAIL] {name}  {detail}"); }
    }

    private static string Read(string rel) => File.ReadAllText(Path.Combine(_root, rel));

    private static int Main()
    {
        _root = FindRoot();
        Console.WriteLine($"工程根目录：{_root}\n");

        Console.WriteLine("== 1. 源码约束 ==");
        SourceRules();

        Console.WriteLine("\n== 2. 绘制顺序仿真 ==");
        BehaviourSim();

        Console.WriteLine("\n== 3. 产物校验 ==");
        BinaryCheck();

        Console.WriteLine($"\n结果：{_pass} 通过 / {_fail} 失败");
        return _fail == 0 ? 0 : 1;
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Client", "MirObjects"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new Exception("找不到工程根目录（应包含 Client/MirObjects）");
    }

    // ---------------- 1. 源码约束 ----------------
    private static void SourceRules()
    {
        var gameScene = Read("Client/MirScenes/GameScene.cs");
        var mapObject = Read("Client/MirObjects/MapObject.cs");
        var player    = Read("Client/MirObjects/PlayerObject.cs");
        var mapCode   = Read("Client/MirObjects/MapCode.cs");

        // 1.1 旧的后置补画必须消失：全客户端除 MapObject/PlayerObject 的定义外，不该再有 DrawBlend() 调用
        var calls = Regex.Matches(gameScene, @"\.DrawBlend\(\)");
        Check("GameScene 里不再有 .DrawBlend() 调用（旧的高亮补画已移除）", calls.Count == 0,
              $"实际 {calls.Count} 处");

        // 1.2 高亮唯一入口：CellInfo.DrawObjects 里紧跟 Draw() 之后
        //     先剥掉 // 注释再比，断言的是「两条语句之间没有别的语句」，注释允许有
        string StripComments(string s) => Regex.Replace(s, @"//[^\n]*", "");
        var m = Regex.Match(StripComments(mapCode),
            @"if\s*\(!CellObjects\[i\]\.Dead\)\s*\{\s*CellObjects\[i\]\.Draw\(\);\s*CellObjects\[i\]\.DrawHighlight\(\);\s*continue;",
            RegexOptions.Singleline);
        Check("CellInfo.DrawObjects：Draw() 之后立刻 DrawHighlight()，中间无其它语句", m.Success);

        // 1.3 死对象分支不得高亮（DrawDeadObjects 里不能出现 DrawHighlight）
        var dead = Regex.Match(mapCode, @"public void DrawDeadObjects\(\)(.*?)\n        \}", RegexOptions.Singleline);
        Check("DrawDeadObjects 不调用 DrawHighlight（尸体不再描边）",
              dead.Success && !dead.Groups[1].Value.Contains("DrawHighlight"));

        // 1.4 DrawHighlight 的守卫齐全
        var hl = Regex.Match(mapObject, @"public void DrawHighlight\(\)(.*?)\n        \}", RegexOptions.Singleline);
        string body = hl.Success ? hl.Groups[1].Value : "";
        Check("DrawHighlight 存在", hl.Success);
        Check("守卫：Settings.HighlightTarget", body.Contains("Settings.HighlightTarget"));
        Check("守卫：Blend 标记", body.Contains("if (!Blend) return;"));
        Check("守卫：跳过玩家自己", body.Contains("this == User"));
        Check("守卫：跳过 1 格过滤对象", body.Contains("NearPlayerFiltered"));
        Check("守卫：锁定分支仍受 Settings.MagicLock 控制", body.Contains("Settings.MagicLock && this == LockObject"));
        Check("只判定 鼠标/目标/锁定 三个来源",
              body.Contains("this == MouseObject") && body.Contains("this == TargetObject"));

        // 1.5 高亮强度统一走 HighlightRate，不再有 0.3F 魔法数
        Check("MapObject 定义 HighlightRate", Regex.IsMatch(mapObject, @"const float HighlightRate\s*=\s*[\d.]+F"));
        var mb = Regex.Match(mapObject, @"public virtual void DrawBlend\(\)(.*?)\n        \}", RegexOptions.Singleline);
        Check("MapObject.DrawBlend 用 HighlightRate", mb.Success && mb.Groups[1].Value.Contains("HighlightRate"));
        Check("MapObject.DrawBlend 不再写死 0.3F", mb.Success && !mb.Groups[1].Value.Contains("0.3F"));
        var pb = Regex.Match(player, @"public override void DrawBlend\(\)(.*?)\n        \}", RegexOptions.Singleline);
        Check("PlayerObject.DrawBlend 用 HighlightRate", pb.Success && pb.Groups[1].Value.Contains("MapObject.HighlightRate"));

        // 1.6 混合状态防护仍在（上一轮的补丁不能被这次改动弄丢）
        Check("MapObject.DrawBlend 有 try/finally 复位混合",
              mb.Success && mb.Groups[1].Value.Contains("finally") && mb.Groups[1].Value.Contains("SetBlend(false)"));
        Check("PlayerObject.DrawBlend 有 try/finally 复位混合",
              pb.Success && pb.Groups[1].Value.Contains("finally") && pb.Groups[1].Value.Contains("SetBlend(false)"));
        Check("CreateTexture 入口仍有混合泄漏护栏",
              gameScene.Contains("if (DXManager.Blending)"));
    }

    // ---------------- 2. 绘制顺序仿真 ----------------
    private class Obj
    {
        public string Name = "";
        public bool Dead, InView, Filtered, IsUser;
        public bool IsMouse, IsTarget, IsLock;
        public int OccluderIndex;      // 该对象之后画的前景层/树在总序列里的位置（-1 表示后面没有遮挡）
    }

    /// <summary>复刻 CellInfo.DrawObjects + DrawHighlight 的判定与顺序</summary>
    private static List<(string op, Obj? owner, bool highlight)> Simulate(List<Obj> objs, bool magicLock, bool highlightOn)
    {
        var seq = new List<(string, Obj?, bool)>();
        foreach (var o in objs)
        {
            if (!o.InView) continue;                 // 视野格子之外的不会进入绘制循环
            if (o.Dead) continue;                    // 走 DrawDeadObjects，不参与高亮
            seq.Add((o.Name + ".Draw", o, false));

            bool hl = highlightOn && !o.IsUser && !o.Filtered &&
                      (o.IsMouse || o.IsTarget || (magicLock && o.IsLock));
            if (hl) seq.Add((o.Name + ".Highlight", o, true));

            if (o.OccluderIndex >= 0) seq.Add((o.Name + ".FrontTile", o, false));
        }
        return seq;
    }

    private static void BehaviourSim()
    {
        var list = new List<Obj>
        {
            new Obj { Name="活怪_被锁", InView=true,  IsLock=true,  OccluderIndex=-1 },
            new Obj { Name="活怪_被树挡", InView=true, IsLock=true, OccluderIndex=0 },
            new Obj { Name="已死怪_被锁", InView=true, IsLock=true, Dead=true },
            new Obj { Name="视野外怪_被锁", InView=false, IsLock=true },
            new Obj { Name="被过滤玩家_目标", InView=true, IsTarget=true, Filtered=true },
            new Obj { Name="玩家自己", InView=true, IsTarget=true, IsUser=true },
            new Obj { Name="路人甲", InView=true },
        };

        var seq = Simulate(list, magicLock: true, highlightOn: true);
        var hlNames = seq.Where(x => x.highlight).Select(x => x.owner!.Name).ToList();

        Check("只高亮：可见 + 存活 + 未被过滤 + 命中三来源之一",
              hlNames.SequenceEqual(new[] { "活怪_被锁", "活怪_被树挡" }),
              "实际=" + string.Join(",", hlNames));
        Check("已死对象不再高亮", !hlNames.Contains("已死怪_被锁"));
        Check("视野外对象不再凭空冒影子", !hlNames.Contains("视野外怪_被锁"));
        Check("被 1 格过滤的玩家不再高亮", !hlNames.Contains("被过滤玩家_目标"));
        Check("玩家自己不重复绘制", !hlNames.Contains("玩家自己"));

        // 顺序：高亮必须紧跟本体（同一个 owner 相邻）
        bool adjacent = true;
        for (int i = 0; i < seq.Count; i++)
            if (seq[i].highlight && (i == 0 || seq[i - 1].owner != seq[i].owner || seq[i - 1].highlight))
                adjacent = false;
        Check("每个高亮都紧跟在它自己的本体后面（同深度）", adjacent);

        // 遮挡：树/前景层画在高亮之后 → 能盖住高亮
        int hi = seq.FindIndex(x => x.highlight && x.owner!.Name == "活怪_被树挡");
        int oc = seq.FindIndex(x => !x.highlight && x.op == "活怪_被树挡.FrontTile");
        Check("树后对象：遮挡层排在高亮之后（高亮会被盖住，不再浮在树上）",
              hi >= 0 && oc > hi, $"高亮={hi} 遮挡={oc}");

        // 同一个对象同时是鼠标/目标/锁定，只能画一次
        var one = new List<Obj> { new Obj { Name="三合一", InView=true, IsMouse=true, IsTarget=true, IsLock=true } };
        var seq2 = Simulate(one, true, true);
        Check("同一对象同时命中三种来源时只高亮一次", seq2.Count(x => x.highlight) == 1,
              $"实际 {seq2.Count(x => x.highlight)} 次");

        // 关闭开关时彻底不画
        var seq3 = Simulate(list, magicLock: true, highlightOn: false);
        Check("HighlightTarget=False 时一次都不画", seq3.All(x => !x.highlight));

        // 关掉魔法锁定时，锁来源不再生效
        var lockOnly = new List<Obj> { new Obj { Name="仅锁定", InView=true, IsLock=true } };
        Check("MagicLock=False 时仅锁定对象不高亮",
              Simulate(lockOnly, magicLock: false, highlightOn: true).All(x => !x.highlight));
    }

    // ---------------- 3. 产物校验 ----------------
    private static void BinaryCheck()
    {
        var dll = Path.Combine(_root, "Build", "Client", "Release", "Client.dll");
        if (!File.Exists(dll)) { Check("Client.dll 存在", false); return; }

        var dllTime = File.GetLastWriteTime(dll);
        string[] sources = { "Client/MirObjects/MapObject.cs", "Client/MirObjects/MapCode.cs",
                             "Client/MirScenes/GameScene.cs", "Client/MirObjects/PlayerObject.cs" };
        foreach (var s in sources)
        {
            var t = File.GetLastWriteTime(Path.Combine(_root, s));
            Check($"{Path.GetFileName(s)} 早于 Client.dll（产物包含本次改动）", t <= dllTime,
                  $"{t:HH:mm:ss} > {dllTime:HH:mm:ss}");
        }

        var bytes = File.ReadAllBytes(dll);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Check("Client.dll 元数据中含 DrawHighlight（新方法已编进去）", text.Contains("DrawHighlight"));
        Check("Client.dll 元数据中含 DrawHighlight 的注释说明不需要，但含 HighlightRate 常量名",
              text.Contains("HighlightRate"));
    }
}
