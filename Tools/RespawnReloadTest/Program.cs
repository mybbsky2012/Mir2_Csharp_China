using System.Drawing;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

// ============================================================
// 刷怪配置重载 / NPC 全量重载 内部测试
// 直接驱动 Envir.Main / Envir.Edit，纯内存构造地图与配置，不读 DB。
// ============================================================

int pass = 0, fail = 0;

void Check(string name, bool cond)
{
    if (cond) { pass++; Console.WriteLine($"  [PASS] {name}"); }
    else { fail++; Console.WriteLine($"  [FAIL] {name}"); }
}

Envir main = Envir.Main, edit = Envir.Edit;

// ---- 公共：造一张 50x50 全可走的地图 ----
Map MakeMap(int index, string title)
{
    MapInfo info = new MapInfo { Index = index, Title = title, FileName = "testmap" };
    Map map = new Map(info) { Width = 50, Height = 50 };
    map.Cells = new Cell[50, 50];
    map.WalkableCells = new List<Point>();
    for (int x = 0; x < 50; x++)
        for (int y = 0; y < 50; y++)
        {
            map.Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
            map.WalkableCells.Add(new Point(x, y));
        }
    return map;
}

RespawnInfo MakeRespawn(int mon, int x, int y, int spread, int count)
{
    return new RespawnInfo
    {
        MonsterIndex = mon,
        Location = new Point(x, y),
        Spread = (ushort)spread,
        Count = (ushort)count,
        Delay = 5,
        RandomDelay = 2,
    };
}

// ---------- 1) 刷怪配置重载：合并式 ----------
Console.WriteLine("== 1) ReloadRespawns ==");
{
    main.MonsterInfoList.Clear();
    main.MonsterInfoList.Add(new MonsterInfo { Index = 10, Name = "鸡" });

    Map map = MakeMap(1, "测试图1");
    main.MapList.Clear();
    main.MapList.Add(map);
    main.NPCs.Clear();
    main.SavedSpawns.Clear();

    // 运行中已有的两个刷怪点：A(10,10) 保留、B(20,20) 将被删除
    RespawnInfo oldA = MakeRespawn(10, 10, 10, 2, 3);
    RespawnInfo oldB = MakeRespawn(10, 20, 20, 2, 3);

    MapRespawn rA = new MapRespawn(oldA) { Map = map, Count = 3 };
    rA.WalkableCells = map.WalkableCells;
    MapRespawn rB = new MapRespawn(oldB) { Map = map, Count = 2 };
    rB.WalkableCells = map.WalkableCells;

    map.Respawns.Clear();
    map.Respawns.Add(rA);
    map.Respawns.Add(rB);
    map.Info.Respawns.Clear();
    map.Info.Respawns.Add(oldA);
    map.Info.Respawns.Add(oldB);

    // 编辑器侧：A 数量 3→5（同一点，应沿用对象保住 Count=3）、新增 C(30,30)、B 已删除、
    // 再加一个怪物编号不存在的 D(40,40) 应被跳过
    MapInfo editMap = new MapInfo { Index = 1, Title = "测试图1", FileName = "testmap" };
    RespawnInfo newA = MakeRespawn(10, 10, 10, 2, 5);
    RespawnInfo newC = MakeRespawn(10, 30, 30, 3, 4);
    RespawnInfo badD = MakeRespawn(999, 40, 40, 2, 1);
    editMap.Respawns.Clear();
    editMap.Respawns.Add(newA);
    editMap.Respawns.Add(newC);
    editMap.Respawns.Add(badD);

    edit.MapInfoList.Clear();
    edit.MapInfoList.Add(editMap);

    main.ReloadRespawns();

    Check("刷怪点数量 = 2（A 沿用 + C 新增；B 删除、非法怪物跳过）", map.Respawns.Count == 2);
    Check("A 沿用的是原 MapRespawn 对象（计数不丢）", map.Respawns.Any(x => ReferenceEquals(x, rA)));
    Check("A 的 Count 保持场上计数 3", rA.Count == 3);
    Check("A 的配置已更新（数量 5）", rA.Info == newA && rA.Info.Count == 5);
    Check("B 已从列表移除（但对象未被销毁）", !map.Respawns.Contains(rB) && rB.Count == 2);

    MapRespawn rC = map.Respawns.FirstOrDefault(x => x.Info == newC);
    Check("C 新建且 Count 从 0 开始", rC != null && rC.Count == 0);
    Check("C 的可走格子已按坐标+范围算好", rC != null && rC.WalkableCells != null && rC.WalkableCells.Count > 0);
    Check("C 的 WalkableCells 落在 (30,30) 范围 3 内",
        rC != null && rC.WalkableCells.All(p => Math.Abs(p.X - 30) <= 3 && Math.Abs(p.Y - 30) <= 3));
    Check("非法怪物编号 999 未进入列表", !map.Respawns.Any(x => x.Info != null && x.Info.MonsterIndex == 999));
    Check("map.Info.Respawns 已换成编辑器配置（3 条，供 DB 保存）",
        map.Info.Respawns.Count == 3 && map.Info.Respawns.Contains(newA) && map.Info.Respawns.Contains(badD));

    // 再跑一次幂等性：不应该新增/丢失
    int before = map.Respawns.Count;
    main.ReloadRespawns();
    Check("重复重载幂等（数量不变、对象不变）", map.Respawns.Count == before && map.Respawns.Contains(rA));
}

// ---------- 2) NPC 全量重载：全局新增 + 地图级条目 ----------
Console.WriteLine("== 2) ReloadAllNPCs ==");
{
    Map map = main.MapList[0];
    main.NPCInfoList.Clear();
    main.NPCs.Clear();
    map.NPCs.Clear();
    map.Info.NPCs.Clear();

    // 编辑器：全局列表有一个新 NPC（Index 5），地图级列表另有一个 Index=0 的 NPC
    NPCInfo globalNpc = new NPCInfo
    {
        Index = 5, Name = "新NPC", MapIndex = 1, Location = new Point(5, 5),
        Image = 10, FileName = "攻城信息",
    };
    NPCInfo mapNpc = new NPCInfo
    {
        Index = 0, Name = "地图级NPC", MapIndex = 1, Location = new Point(6, 6),
        Image = 11, FileName = "攻城信息",
    };

    edit.NPCInfoList.Clear();
    edit.NPCInfoList.Add(globalNpc);

    MapInfo editMap = edit.MapInfoList[0];
    editMap.NPCs.Clear();
    editMap.NPCs.Add(globalNpc);
    editMap.NPCs.Add(mapNpc);

    try
    {
        main.ReloadAllNPCs();
    }
    catch (Exception ex)
    {
        Console.WriteLine("  [INFO] ReloadAllNPCs 抛异常：" + ex.GetType().Name + " - " + ex.Message);
    }

    Check("全局新 NPC 进入运行服信息表", main.NPCInfoList.Any(x => x.Index == 5));
    Check("全局新 NPC 已生成对象", main.NPCs.Any(x => x.Info != null && x.Info.Index == 5));
    Check("地图级 NPC（Index 0）也被生成", main.NPCs.Any(x => x.Info != null && x.Info.Index == 0 && x.Info.Name == "地图级NPC"));
    Check("地图级 NPC 未被塞进全局信息表", !main.NPCInfoList.Contains(mapNpc));

    // 从编辑器删掉地图级 NPC，再重载 → 应撤下
    editMap.NPCs.Remove(mapNpc);
    try { main.ReloadAllNPCs(); }
    catch (Exception ex) { Console.WriteLine("  [INFO] 二次 ReloadAllNPCs 抛异常：" + ex.GetType().Name); }

    Check("地图级 NPC 删除后已撤下", !main.NPCs.Any(x => x.Info != null && x.Info.Name == "地图级NPC"));
    Check("全局 NPC 仍在场（配置没变不重建）", main.NPCs.Count(x => x.Info != null && x.Info.Index == 5) == 1);

    // 坐标不可站立 → 跳过且不崩
    NPCInfo badNpc = new NPCInfo
    {
        Index = 6, Name = "墙上NPC", MapIndex = 1, Location = new Point(49, 49),
        Image = 12, FileName = "攻城信息",
    };
    map.Cells[49, 49] = new Cell { Attribute = CellAttribute.HighWall };
    edit.NPCInfoList.Add(badNpc);
    try { main.ReloadAllNPCs(); }
    catch (Exception ex) { Console.WriteLine("  [INFO] 三次 ReloadAllNPCs 抛异常：" + ex.GetType().Name); }

    Check("坐标不可站立的 NPC 未生成", !main.NPCs.Any(x => x.Info != null && x.Info.Index == 6));
}

Console.WriteLine();
Console.WriteLine($"== 结果：{pass} 通过，{fail} 失败 ==");
return fail == 0 ? 0 : 1;
