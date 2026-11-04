using System.Diagnostics;
using System.Drawing;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

// 测服务端地图的常驻内存 / 加载耗时。
// 工作目录必须是服务端目录（Server.MirDB 与 Maps 都在那里）。
//
//   dotnet MapMem.dll db           → 完全按服务端启动流程：LoadDB 后加载 MapInfoList 里的全部地图（推荐）
//   dotnet MapMem.dll 40           → 按文件大小取前 40 张
//   dotnet MapMem.dll all          → Maps 目录下全部 .map

if (args.Length == 0)
{
    Console.WriteLine("用法: MapMem db | MapMem <张数> | MapMem all");
    return 1;
}

if (args[0].Equals("db", StringComparison.OrdinalIgnoreCase))
    return RunDbMode();

if (args[0].Equals("detail", StringComparison.OrdinalIgnoreCase))
    return RunDetailMode(args);

if (args[0].Equals("verify", StringComparison.OrdinalIgnoreCase))
    return RunVerifyMode();

return RunFileMode();


static int RunDbMode()
{
    if (!File.Exists(Envir.DatabasePath))
    {
        Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
        return 2;
    }

    Settings.Load();

    var envir = Envir.Main;

    var swDb = Stopwatch.StartNew();
    if (!envir.LoadDB())
    {
        Console.WriteLine("LoadDB 失败");
        return 2;
    }
    swDb.Stop();

    long afterDb = GC.GetTotalMemory(true);
    Console.WriteLine($"LoadDB 用时 {swDb.ElapsedMilliseconds} ms，基础内存 {afterDb / 1048576.0:F1} MB，地图表 {envir.MapInfoList.Count} 张");
    Console.WriteLine();

    var maps = new List<Map>();
    long cells = 0, walk = 0, totalMs = 0, respawnPoints = 0, safeSpells = 0, npcCount = 0, mineMaps = 0;
    int fail = 0;
    long doorEntries = 0;
    var rows = new List<(string name, long cells, long walk, long respawn, long spell, long ms, long bytes)>();

    var swAll = Stopwatch.StartNew();
    foreach (MapInfo info in envir.MapInfoList)
    {
        long before = GC.GetTotalMemory(false);
        var sw = Stopwatch.StartNew();
        Map map;
        try
        {
            map = new Map(info);
            if (!map.Load()) { fail++; continue; }
        }
        catch (Exception ex)
        {
            fail++;
            Console.WriteLine($"  失败 {info.FileName}: {ex.GetType().Name} {ex.Message}");
            continue;
        }
        sw.Stop();
        long after = GC.GetTotalMemory(false);

        maps.Add(map);
        totalMs += sw.ElapsedMilliseconds;

        long c = (long)map.Width * map.Height;
        long wk = map.WalkableCells?.Count ?? 0;
        long rp = 0;   // 11-04 阶段一：刷怪点可走点副本已移除，恒为 0
        doorEntries += map.DoorIndex.Count;

        cells += c; walk += wk; respawnPoints += rp;
        safeSpells += map.SafeZoneSpells.Count;
        npcCount += map.NPCs.Count;
        if (map.Mine != null) mineMaps++;

        rows.Add((info.FileName, c, wk, rp, map.SafeZoneSpells.Count, sw.ElapsedMilliseconds, Math.Max(0, after - before)));
    }
    swAll.Stop();

    long total = GC.GetTotalMemory(true) - afterDb;

    Console.WriteLine("------ 加载耗时最长的 10 张 ------");
    foreach (var r in rows.OrderByDescending(x => x.ms).Take(10))
        Console.WriteLine($"  {r.name,-22} {r.cells,10:N0} 格  刷怪副本 {r.respawn,10:N0} 点  {r.ms,6} ms");

    Console.WriteLine();
    Console.WriteLine("------ 刷怪副本点数最多的 10 张 ------");
    foreach (var r in rows.OrderByDescending(x => x.respawn).Take(10))
        Console.WriteLine($"  {r.name,-22} 刷怪点副本 {r.respawn,10:N0} 点 (≈{r.respawn * 8 / 1048576.0:F1} MB)  耗时 {r.ms} ms");

    Console.WriteLine();
    Console.WriteLine("================ 汇总 ================");
    Console.WriteLine($"成功 {maps.Count} 张，失败 {fail} 张，墙钟 {swAll.ElapsedMilliseconds / 1000.0:F1} s，加载 CPU {totalMs / 1000.0:F1} s");
    Console.WriteLine($"总格子        {cells,15:N0}");
    Console.WriteLine($"可走格子      {walk,15:N0}  ({100.0 * walk / Math.Max(1, cells):F1}%)");
    Console.WriteLine($"刷怪点副本点  {respawnPoints,15:N0}  (≈{respawnPoints * 8.0 / 1048576:F0} MB，未计 List 扩容余量)");
    Console.WriteLine($"安全区法术    {safeSpells,15:N0} 个对象");
    Console.WriteLine($"NPC {npcCount}，含矿区图 {mineMaps} 张");
    Console.WriteLine();
    Console.WriteLine($"地图常驻      {total / 1048576.0,15:F1} MB");

    double arrCells = cells * 8.0 / 1048576;
    double arrDoor = doorEntries * 40.0 / 1048576;   // 稀疏字典：int 键 + 桶 + 节点 ≈ 40 B/条
    double walkList = walk * 8.0 * 1.33 / 1048576;
    double respList = respawnPoints * 8.0 * 1.33 / 1048576;
    double rest = total / 1048576.0 - arrCells - arrDoor - walkList - respList;

    Console.WriteLine();
    Console.WriteLine("---------------- 各项拆解 ----------------");
    Console.WriteLine($"  Cells[x,y]     {arrCells,10:F1} MB");
    Console.WriteLine($"  DoorIndex 字典 {arrDoor,10:F1} MB   <- 只有 {doorEntries:N0} 条（原先是全图 8B/格 = 242 MB）");
    Console.WriteLine($"  WalkableCells  {walkList,10:F1} MB   <- 每图一份全图可走点表");
    Console.WriteLine($"  刷怪点副本     {respList,10:F1} MB   <- 每个刷怪点再复制一片可走点");
    Console.WriteLine($"  Cell 对象及其它{rest,10:F1} MB   <- 含 {(long)cells} 个 Cell 对象（墙也没共享）");
    Console.WriteLine($"  Cell 对象单价  {rest * 1048576 / Math.Max(1.0, cells),10:F1} B/格");

    GC.KeepAlive(maps);
    return 0;
}


int RunFileMode()
{
    string mapDir = Path.GetFullPath(Settings.MapPath);
    if (!Directory.Exists(mapDir))
    {
        Console.WriteLine("找不到地图目录: " + mapDir);
        return 2;
    }

    string[] files = Directory.GetFiles(mapDir, "*.map");
    Array.Sort(files, (a, b) => new FileInfo(b).Length.CompareTo(new FileInfo(a).Length));

    int take = 40;
    if (args.Length > 0)
    {
        if (args[0].Equals("all", StringComparison.OrdinalIgnoreCase)) take = files.Length;
        else if (int.TryParse(args[0], out int n)) take = Math.Min(n, files.Length);
    }
    if (take > files.Length) take = files.Length;

    Console.WriteLine($"地图目录 {mapDir}");
    Console.WriteLine($"文件 {files.Length} 张，本次 {take} 张");
    Console.WriteLine();

    long baseMem = GC.GetTotalMemory(true);
    long prevMem = baseMem;

    var all = new List<Map>();
    long totalMs = 0, cells = 0, walk = 0, sumMem = 0;

    for (int i = 0; i < take; i++)
    {
        string name = Path.GetFileNameWithoutExtension(files[i]);
        var info = new MapInfo { FileName = name, Title = name };

        var sw = Stopwatch.StartNew();
        Map map;
        try
        {
            map = new Map(info);
            if (!map.Load()) { Console.WriteLine($"  {name} 加载失败"); continue; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {name} 异常 {ex.GetType().Name} {ex.Message}");
            continue;
        }
        sw.Stop();

        all.Add(map);
        totalMs += sw.ElapsedMilliseconds;

        long mem = GC.GetTotalMemory(true);
        long delta = mem - prevMem;
        prevMem = mem;
        sumMem += delta;

        cells += (long)map.Width * map.Height;
        walk += map.WalkableCells?.Count ?? 0;

        Console.WriteLine($"[{i + 1}/{take}] {name,-22} {map.Width}x{map.Height} 格子 {(long)map.Width * map.Height,10:N0} 可走 {(map.WalkableCells?.Count ?? 0),9:N0} 常驻 {delta / 1048576.0,7:F1} MB 用时 {sw.ElapsedMilliseconds,5} ms");
    }

    long total = GC.GetTotalMemory(true) - baseMem;
    Console.WriteLine();
    Console.WriteLine($"加载 {all.Count} 张 / 格子 {cells:N0} / 可走 {walk:N0}");
    Console.WriteLine($"常驻 {total / 1048576.0:F1} MB（累加 {sumMem / 1048576.0:F1} MB），耗时 {totalMs:N0} ms，平均 {(double)totalMs / Math.Max(1, all.Count):F1} ms/张");
    Console.WriteLine($"平均 {(double)total / Math.Max(1, cells):F0} B/格");

    GC.KeepAlive(all);
    return 0;
}

static int RunDetailMode(string[] a)
{
    if (a.Length < 2) { Console.WriteLine("用法: MapMem detail <地图名> [更多名字...]"); return 1; }
    if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("请在服务端目录运行。"); return 2; }

    Settings.Load();
    var envir = Envir.Main;
    if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

    foreach (string name in a.Skip(1))
    {
        MapInfo info = envir.MapInfoList.FirstOrDefault(x => string.Equals(x.FileName, name, StringComparison.OrdinalIgnoreCase));
        if (info == null) { Console.WriteLine($"{name}: 地图表里没有这张图"); continue; }

        long before = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var map = new Map(info);
        bool ok = map.Load();
        sw.Stop();
        long after = GC.GetTotalMemory(true);

        if (!ok) { Console.WriteLine($"{name}: 加载失败"); continue; }

        int total = map.Width * map.Height;
        int nonNull = 0;
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
                if (map.Cells[x, y] != null) nonNull++;

        long respawnCells = 0;   // 11-04 阶段一后已无副本

        Console.WriteLine($"{name}  {map.Width}x{map.Height} 格子 {total:N0}");
        Console.WriteLine($"  常驻增量      {(after - before) / 1048576.0:F1} MB   用时 {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"  Cells 非空     {nonNull:N0} ({100.0 * nonNull / total:F1}%)   可走 {map.WalkableCells?.Count ?? 0:N0}");
        Console.WriteLine($"  刷怪点 {map.Respawns.Count}   副本可走点 {respawnCells:N0}（阶段一后恒为 0）");
        Console.WriteLine($"  安全区 {info.SafeZones.Count}   安全区法术对象 {map.SafeZoneSpells.Count}");
        Console.WriteLine($"  NPC {map.NPCs.Count}   Mine {(map.Mine == null ? "无" : "有")}");
        Console.WriteLine();
    }
    return 0;
}

static int RunVerifyMode()
{
    if (!File.Exists(Envir.DatabasePath))
    {
        Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
        return 2;
    }

    Settings.Load();
    var envir = Envir.Main;
    if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

    int mapOk = 0, mapFail = 0;
    int respawnTotal = 0, respawnFail = 0, respawnEmptyBox = 0, sampleChecks = 0;
    int doorTotal = 0, doorMiss = 0, doorMismatch = 0, doorCheckFail = 0, plainCellFail = 0;
    int indexKeys = 0;
    var problems = new List<string>();

    void Bad(string msg)
    {
        if (problems.Count < 40) problems.Add(msg);
    }

    foreach (MapInfo info in envir.MapInfoList)
    {
        Map map;
        try
        {
            map = new Map(info);
            if (!map.Load()) { mapFail++; Bad(info.FileName + ": 加载失败"); continue; }
        }
        catch (Exception ex) { mapFail++; Bad(info.FileName + ": " + ex.GetType().Name + " " + ex.Message); continue; }

        mapOk++;
        indexKeys += map.DoorIndex.Count;

        for (int i = 0; i < map.Respawns.Count; i++)
        {
            MapRespawn r = map.Respawns[i];
            respawnTotal++;

            int cx = r.Info.Location.X, cy = r.Info.Location.Y, sp = r.Info.Spread;

            bool anyWalkable = false;
            int y0 = Math.Max(0, cy - sp), y1 = Math.Min(map.Height - 1, cy + sp);
            int x0 = Math.Max(0, cx - sp), x1 = Math.Min(map.Width - 1, cx + sp);

            for (int y = y0; y <= y1 && !anyWalkable; y++)
                for (int x = x0; x <= x1; x++)
                    if (map.Cells[x, y] != null && map.Cells[x, y].Valid) { anyWalkable = true; break; }

            if (!anyWalkable) { respawnEmptyBox++; continue; }

            for (int k = 0; k < 5; k++)
            {
                sampleChecks++;
                Point pt = r.GetSpawnPoint(map);

                bool bad = pt.X < 0 || pt.Y < 0 || pt.X >= map.Width || pt.Y >= map.Height
                           || map.Cells[pt.X, pt.Y] == null || !map.Cells[pt.X, pt.Y].Valid
                           || Math.Abs(pt.X - cx) > sp || Math.Abs(pt.Y - cy) > sp;

                if (bad)
                {
                    respawnFail++;
                    Bad(string.Format("{0}: 刷怪点({1}) 中心({2},{3}) 范围{4} 取到非法点({5},{6})",
                        info.FileName, i, cx, cy, sp, pt.X, pt.Y));
                    break;
                }
            }
        }

        foreach (Door d in map.Doors)
        {
            doorTotal++;
            int key = d.Location.Y * map.Width + d.Location.X;

            if (!map.DoorIndex.TryGetValue(key, out Door got))
            {
                doorMiss++;
                Bad(string.Format("{0}: 门{1} 位于({2},{3}) 未登记进 DoorIndex", info.FileName, d.index, d.Location.X, d.Location.Y));
                continue;
            }
            if (!ReferenceEquals(got, d))
            {
                doorMismatch++;
                Bad(string.Format("{0}: 门{1} ({2},{3}) 索引指向了别的 Door 对象", info.FileName, d.index, d.Location.X, d.Location.Y));
            }
        }

        for (int i = 0; i < Math.Min(3, map.Doors.Count); i++)
        {
            Door d = map.Doors[i];
            DoorState old = d.DoorState;

            d.DoorState = DoorState.Open;
            if (!map.CheckDoorOpen(d.Location)) { doorCheckFail++; Bad(info.FileName + ": 门开着却判定为不可通过"); }

            d.DoorState = (DoorState)0;
            if (map.CheckDoorOpen(d.Location)) { doorCheckFail++; Bad(info.FileName + ": 门关着却判定为可通过"); }

            d.DoorState = old;
        }

        int plainTried = 0;
        for (int x = 0; x < map.Width && plainTried < 3; x += 7)
            for (int y = 0; y < map.Height && plainTried < 3; y += 7)
            {
                if (map.Cells[x, y] == null || !map.Cells[x, y].Valid) continue;
                if (map.DoorIndex.ContainsKey(y * map.Width + x)) continue;

                plainTried++;
                if (!map.CheckDoorOpen(new Point(x, y)))
                {
                    plainCellFail++;
                    Bad(string.Format("{0}: 普通格({1},{2}) 被误判为有门", info.FileName, x, y));
                }
            }
    }

    Console.WriteLine("================ 功能校验 ================");
    Console.WriteLine($"地图 成功 {mapOk} / 失败 {mapFail}");
    Console.WriteLine($"刷怪点 {respawnTotal}（方框内无可走格 {respawnEmptyBox}），取点校验 {sampleChecks} 次，非法 {respawnFail}");
    Console.WriteLine($"门 {doorTotal} 个，索引缺失 {doorMiss}，指向错误 {doorMismatch}，DoorIndex 条目 {indexKeys}");
    Console.WriteLine($"开关门判定失败 {doorCheckFail}，普通格误判 {plainCellFail}");

    if (problems.Count == 0)
    {
        Console.WriteLine("结论: 全部通过");
        return 0;
    }

    Console.WriteLine("---- 前 40 条问题 ----");
    foreach (string p in problems) Console.WriteLine("  " + p);
    return 1;
}
