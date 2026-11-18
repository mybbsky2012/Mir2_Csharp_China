using Server;
using Server.MirDatabase;
using Server.MirObjects;

// BUFF 配置解析冒烟：确认羽化 11 个节能被识别（Shared.dll 必须含 159~169）
if (args.Length > 0 && args[0] == "--buff")
{
    Settings.Load();
    BuffConfig.Load();

    var types = new List<BuffType> { BuffType.羽化登仙 };
    for (int i = 0; i < 10; i++) types.Add((BuffType)((int)BuffType.羽化一重 + i));

    int ok = 0;
    foreach (var t in types)
    {
        var def = BuffConfig.Get(t);
        if (def == null || def.Stats.Count == 0)
        {
            Console.WriteLine($"MISS  {t}  def={(def != null)}  stats={(def == null ? -1 : def.Stats.Count)}");
            continue;
        }
        ok++;
        Console.WriteLine($"OK    {t,-8} {def.Name}  属性 {def.Stats.Count} 条  Stack={def.Stack}");
    }
    Console.WriteLine($"识别成功 {ok}/{types.Count}   配置目录 {Settings.ConfigPath}");
    Environment.Exit(ok == types.Count ? 0 : 1);
}

// NPC 脚本命令解析冒烟测试：只验证「命令名 → ActionType/CheckType + 参数」的映射，
// 不启动服务端、不碰存档。用于 CHECKHUMUP / HUMP / HUMDOWN / RESETHUMUP 的语法自检。
var page = new NPCPage("@test");
var seg = new NPCSegment(page, new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());

void ParseAct(string line)
{
    seg.ActList.Clear();
    seg.ParseAct(seg.ActList, line);
    if (seg.ActList.Count == 0)
    {
        Console.WriteLine($"ACT  {line,-22} -> (无动作)");
        return;
    }
    var a = seg.ActList[0];
    Console.WriteLine($"ACT  {line,-22} -> {a.Type}  params=[{string.Join(",", a.Params)}]");
}

void ParseCheck(string line)
{
    seg.CheckList.Clear();
    seg.ParseCheck(line);
    if (seg.CheckList.Count == 0)
    {
        Console.WriteLine($"CHK  {line,-22} -> (无条件)");
        return;
    }
    var c = seg.CheckList[0];
    Console.WriteLine($"CHK  {line,-22} -> {c.Type}  params=[{string.Join(",", c.Params)}]");
}

Console.WriteLine("=== 条件：检测登仙状态 ===");
ParseCheck("CHECKHUMUP");
ParseCheck("CHECKHUMUP 3");
ParseCheck("CHECKHUMUP >= 3");
ParseCheck("CHECKHUMUP < 10");

Console.WriteLine();
Console.WriteLine("=== 动作：升重 / 降重 / 还原 ===");
ParseAct("HUMP");
ParseAct("HUMP 3");
ParseAct("HUMDOWN");
ParseAct("HUMDOWN 2");
ParseAct("RESETHUMUP");
ParseAct("RESETHUMUP 1");

Console.WriteLine();
Console.WriteLine("=== 旧名字仍可用（向后兼容）===");
ParseAct("HUMUPUP 2");
ParseAct("HUMUPDOWN 1");
ParseAct("HUMUPRESET");
