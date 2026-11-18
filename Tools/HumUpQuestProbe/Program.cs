using Server;
using Server.MirDatabase;
using Server.MirEnvir;

// 只读探针：看羽化登仙任务链的定义 + 每个角色的任务状态（不动 DB，不 Save）
// 用法（工作目录 = 服务端目录，即 Server.MirDB 所在处）：
//   HumUpQuestProbe                打印 humup* 任务定义 + 所有角色里与羽化任务相关的状态
//   HumUpQuestProbe --quest 179    只打印指定任务 + 拥有该任务状态的角色

try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

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

var humup = envir.QuestInfoList.Where(x => x != null && (x.FileName ?? "").StartsWith("humup", StringComparison.OrdinalIgnoreCase))
                              .OrderBy(x => x.Index)
                              .ToList();

Console.WriteLine($"任务总数 {envir.QuestInfoList.Count}，羽化链条目 {humup.Count}");
Console.WriteLine();

int onlyIndex = -1;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--quest") int.TryParse(args[i + 1], out onlyIndex);

Console.WriteLine("==== 羽化任务定义 ====");
foreach (var q in humup)
{
    if (onlyIndex > 0 && q.Index != onlyIndex) continue;

    int tasks = q.KillTasks.Count + q.ItemTasks.Count + q.FlagTasks.Count;

    Console.WriteLine($"[{q.Index,4}] {q.FileName,-12} {q.Name}");
    Console.WriteLine($"       类型={q.Type}  等级≥{q.RequiredMinLevel}  前置={q.RequiredQuest}  起始NPC={q.NpcIndex}  交任务NPC={q.FinishNpcIndex}  限时={q.TimeLimitInSeconds}s");
    Console.WriteLine($"       条件数={tasks}（杀{q.KillTasks.Count}/物{q.ItemTasks.Count}/标记{q.FlagTasks.Count}）" +
                      (tasks == 0 ? "  ← 跑腿任务，接完即 Completed" : ""));
}

Console.WriteLine();
Console.WriteLine("==== 角色任务状态（只列与羽化任务有关的）====");

var humupIndexes = new HashSet<int>(humup.Select(x => x.Index));
int hit = 0;

foreach (var acc in envir.AccountList)
{
    if (acc == null) continue;

    foreach (var ch in acc.Characters)
    {
        if (ch == null) continue;

        var done = ch.CompletedQuests.Where(humupIndexes.Contains).ToList();
        var cur = ch.CurrentQuests.Where(x => x != null && humupIndexes.Contains(x.Index)).ToList();

        if (done.Count == 0 && cur.Count == 0) continue;

        hit++;
        Console.WriteLine($"{acc.AccountID,-16} {ch.Name,-14} 等级{ch.Level,-3} " +
                          $"完成=[{string.Join(",", done)}]  进行中=[{string.Join(",", cur.Select(x => x.Index))}]");

        foreach (var c in cur)
            Console.WriteLine($"      进行中 {c.Index} {c.Info?.FileName}  Start={c.StartDateTime:yyyy-MM-dd HH:mm}  End={c.EndDateTime:yyyy-MM-dd HH:mm}  已完成={c.Completed}");

        if (done.Count > 0)
            foreach (var d in done)
            {
                var qi = envir.QuestInfoList.FirstOrDefault(x => x.Index == d);
                Console.WriteLine($"      已完成 {d} {qi?.FileName} {qi?.Name}");
            }
    }
}

Console.WriteLine();
Console.WriteLine(hit == 0 ? "没有任何角色带羽化任务状态。" : $"共 {hit} 个角色带羽化任务状态。");
return 0;
