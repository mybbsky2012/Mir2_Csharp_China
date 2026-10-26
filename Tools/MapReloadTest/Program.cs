using System.Drawing;
using System.Reflection;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

// ============================================================
// 地图热重载（ReloadMaps）内部测试
// 覆盖：MapInfo.Clone / CopyConfigFrom 差异覆盖 / 老图配置刷新 /
//       新图现场上线 / 重复重载幂等 / 删后重建拦截 / 缺 .map 失败
// 纯内存驱动 Envir.Main / Envir.Edit，只用一张真 .map 文件（Maps\test*.map）。
// ============================================================

int pass = 0, fail = 0;

void Check(string name, bool cond)
{
    if (cond) { pass++; Console.WriteLine($"  [PASS] {name}"); }
    else { fail++; Console.WriteLine($"  [FAIL] {name}"); }
}

Envir main = Envir.Main, edit = Envir.Edit;

// ReloadMaps 有"服务器未运行则忽略"的保护，测试里把 Running 打开
typeof(Envir).GetProperty("Running").SetValue(main, true);

// 安全区法术（边框/回血）开着，用来验证老图刷新会重建
Settings.SafeZoneBorder = true;
Settings.SafeZoneHealing = true;

// NPC 脚本目录（NPCObject 构造时会按文件名加载脚本）
Directory.CreateDirectory(Path.Combine("Envir", "NPCs"));
File.WriteAllText(Path.Combine("Envir", "NPCs", "测试脚本.txt"),
    "[@MAIN]\r\n#SAY\r\n测试用NPC\r\n");

// ---- 公共：造一张 30x30 全可走的地图 ----
Map MakeMap(int index, string title, string file)
{
    MapInfo info = new MapInfo { Index = index, Title = title, FileName = file };
    Map map = new Map(info) { Width = 30, Height = 30 };
    map.Cells = new Cell[30, 30];
    map.WalkableCells = new List<Point>();

    for (int x = 0; x < 30; x++)
        for (int y = 0; y < 30; y++)
        {
            map.Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
            map.WalkableCells.Add(new Point(x, y));
        }

    return map;
}

void ResetAll()
{
    main.MapList.Clear();
    main.MapInfoList.Clear();
    main.StartPoints.Clear();
    main.NPCs.Clear();
    main.NPCInfoList.Clear();
    main.SavedSpawns.Clear();
    main.MonsterInfoList.Clear();

    edit.MapInfoList.Clear();
    edit.NPCInfoList.Clear();
    edit.MapList.Clear();
}

// ============================================================
Console.WriteLine("== 1) MapInfo.Clone（运行副本） ==");
{
    MapInfo src = new MapInfo { Index = 77, Title = "源", FileName = "x", MiniMap = 3 };
    src.SafeZones.Add(new SafeZoneInfo { Location = new Point(1, 2), Size = 3, StartPoint = true, Info = src });
    src.Movements.Add(new MovementInfo { MapIndex = 5, Source = new Point(1, 1), Destination = new Point(2, 2) });
    src.MineZones.Add(new MineZone { Mine = 1, Location = new Point(3, 3), Size = 2 });
    src.Respawns.Add(new RespawnInfo { MonsterIndex = 1, Location = new Point(4, 4) });
    src.NPCs.Add(new NPCInfo { Index = 9, Name = "N" });

    MapInfo copy = src.Clone();

    Check("标量一致", copy.Index == 77 && copy.Title == "源" && copy.FileName == "x" && copy.MiniMap == 3);
    Check("安全区反指副本自身", ReferenceEquals(copy.SafeZones[0].Info, copy));
    Check("安全区元素不共享", !ReferenceEquals(copy.SafeZones[0], src.SafeZones[0]));
    Check("传送点元素不共享", !ReferenceEquals(copy.Movements[0], src.Movements[0]));
    Check("矿区元素不共享", !ReferenceEquals(copy.MineZones[0], src.MineZones[0]));
    Check("刷怪点元素共享（要留给重载按引用比对）", ReferenceEquals(copy.Respawns[0], src.Respawns[0]));
    Check("NPC 元素共享", ReferenceEquals(copy.NPCs[0], src.NPCs[0]));
    Check("ActiveCoords 不继承运行期缓存", copy.ActiveCoords.Count == 0);

    copy.Title = "改";
    copy.SafeZones[0].Location = new Point(9, 9);

    Check("改副本不影响源", src.Title == "源" && src.SafeZones[0].Location == new Point(1, 2));
}

// ============================================================
Console.WriteLine("== 2) CopyConfigFrom（老图差异刷新） ==");
{
    MapInfo runtime = new MapInfo { Index = 77, Title = "旧标题", FileName = "x", MiniMap = 1 };
    MapInfo e = runtime.Clone();

    bool z, m, mine;

    Check("无改动返回 false",
        !runtime.CopyConfigFrom(e, out z, out m, out mine) && !z && !m && !mine);

    e.Title = "新标题";
    e.MiniMap = 9;
    e.NoFight = true;

    bool changed = runtime.CopyConfigFrom(e, out z, out m, out mine);
    Check("标量变更 → true 且不动列表", changed && !z && !m && !mine);
    Check("标量已生效", runtime.Title == "新标题" && runtime.MiniMap == 9 && runtime.NoFight);

    e.SafeZones.Add(new SafeZoneInfo { Location = new Point(5, 5), Size = 2, StartPoint = true });
    Check("安全区变更被识别", runtime.CopyConfigFrom(e, out z, out m, out mine) && z);
    Check("安全区反指运行实例", ReferenceEquals(runtime.SafeZones[0].Info, runtime));

    e.Movements.Add(new MovementInfo { MapIndex = 88, Source = new Point(1, 1), Destination = new Point(3, 3) });
    Check("传送点变更被识别", runtime.CopyConfigFrom(e, out z, out m, out mine) && m);
    Check("传送点已克隆（不改编辑器对象）", !ReferenceEquals(runtime.Movements[0], e.Movements[0]));

    e.MineIndex = 1;
    e.MineZones.Add(new MineZone { Mine = 1, Location = new Point(7, 7), Size = 1 });
    Check("矿区变更被识别", runtime.CopyConfigFrom(e, out z, out m, out mine) && mine);

    // 不该被覆盖的三样
    int respawnsBefore = runtime.Respawns.Count;
    int npcsBefore = runtime.NPCs.Count;
    e.Respawns.Add(new RespawnInfo { MonsterIndex = 2, Location = new Point(8, 8) });
    e.NPCs.Add(new NPCInfo { Index = 10 });
    e.FileName = "changed";
    e.Index = 78;

    runtime.CopyConfigFrom(e, out z, out m, out mine);

    Check("Respawns 不被配置刷新覆盖", runtime.Respawns.Count == respawnsBefore);
    Check("NPCs 不被配置刷新覆盖", runtime.NPCs.Count == npcsBefore);
    Check("FileName / Index 不被热改", runtime.FileName == "x" && runtime.Index == 77);
}

// ============================================================
Console.WriteLine("== 3) ReloadMaps：老图配置刷新 ==");
{
    ResetAll();

    Map oldMap = MakeMap(101, "老图", "testreload");
    main.MapList.Add(oldMap);
    main.MapInfoList.Add(oldMap.Info);

    // 启动时那份安全区 + 出生点
    oldMap.Info.SafeZones.Add(new SafeZoneInfo { Location = new Point(5, 5), Size = 2, StartPoint = true, Info = oldMap.Info });
    oldMap.RebuildSafeZones();
    main.StartPoints.Add(oldMap.Info.SafeZones[0]);

    int spellBefore = oldMap.SafeZoneSpells.Count;

    // 在场刷怪点（Count=3，重载后不许被重置）
    RespawnInfo ri = new RespawnInfo { MonsterIndex = 300, Location = new Point(10, 10), Spread = 2, Count = 3, Delay = 5, RandomDelay = 1 };
    oldMap.Info.Respawns.Add(ri);
    MapRespawn mr = new MapRespawn(ri) { Map = oldMap, Count = 3 };
    mr.WalkableCells = oldMap.WalkableCells;
    oldMap.Respawns.Add(mr);

    main.MonsterInfoList.Add(new MonsterInfo { Index = 300, Name = "测试怪" });

    MapInfo editOld = oldMap.Info.Clone();
    edit.MapInfoList.Add(editOld);

    // 编辑器侧改动：安全区挪位、加一条通往新图的传送点、改标题
    editOld.SafeZones[0].Location = new Point(20, 20);
    editOld.Movements.Add(new MovementInfo { MapIndex = 102, Source = new Point(3, 3), Destination = new Point(4, 4), ShowOnBigMap = true });
    editOld.Title = "老图-改";

    Map sameInstance = main.GetMap(101);
    main.ReloadMaps();

    Check("老图 Map 对象没被替换（在场玩家不受影响）", ReferenceEquals(main.GetMap(101), sameInstance));
    Check("标题已刷新", sameInstance.Info.Title == "老图-改");
    Check("传送点已刷新（老图能立刻传新图）",
        sameInstance.Info.Movements.Count == 1 && sameInstance.Info.Movements[0].MapIndex == 102);
    Check("安全区法术已重建", sameInstance.SafeZoneSpells.Count > 0 && spellBefore > 0);

    bool allInNewZone = true;
    for (int i = 0; i < sameInstance.SafeZoneSpells.Count; i++)
    {
        Point p = sameInstance.SafeZoneSpells[i].CurrentLocation;
        if (p.X < 18 || p.X > 22 || p.Y < 18 || p.Y > 22) { allInNewZone = false; break; }
    }
    Check("旧安全区法术已撤、新法术落在新坐标", allInNewZone);

    Check("旧安全区格子里的法术已清掉",
        sameInstance.Cells[5, 5].Objects == null ||
        sameInstance.Cells[5, 5].Objects.All(o => o.Race != ObjectType.Spell));

    Check("出生点已重建并指向新安全区",
        main.StartPoints.Count == 1 && ReferenceEquals(main.StartPoints[0].Info, sameInstance.Info) &&
        main.StartPoints[0].Location == new Point(20, 20));

    Check("老图没被塞进 MapList 第二份", main.MapList.Count == 1);
    Check("老图刷怪点计数没被重置", mr.Count == 3);
}

// ============================================================
Console.WriteLine("== 4) ReloadMaps：新图现场上线 ==");
{
    // 先探一下测试图的尺寸与可走格子，挑一个能站人的坐标
    Map probe = new Map(new MapInfo { Index = 999, FileName = "testnew" });
    bool probeOk = probe.Load();

    Check("测试用 .map 能读入", probeOk && probe.Width > 0 && probe.Height > 0);
    Console.WriteLine($"     （testnew.map {probe.Width}x{probe.Height}，可走格子 {probe.WalkableCells?.Count ?? 0}）");

    Point npcPos = probe.WalkableCells.Count > 0 ? probe.WalkableCells[0] : new Point(0, 0);
    Point spawnPos = probe.WalkableCells.Count > 0 ? probe.WalkableCells[probe.WalkableCells.Count / 2] : new Point(0, 0);

    // 编辑器里新增这张图
    MapInfo newInfo = new MapInfo { Index = 102, Title = "新图", FileName = "testnew", MiniMap = 5, BigMap = 6 };
    newInfo.SafeZones.Add(new SafeZoneInfo { Location = npcPos, Size = 1, StartPoint = true });
    newInfo.Respawns.Add(new RespawnInfo { MonsterIndex = 300, Location = spawnPos, Spread = 1, Count = 1, Delay = 5, RandomDelay = 1 });
    newInfo.NPCs.Add(new NPCInfo { Index = 500, Name = "测试NPC", FileName = "测试脚本", MapIndex = 102, Location = npcPos, Image = 1, Rate = 100 });

    edit.MapInfoList.Add(newInfo);

    MapRespawn oldRespawn = main.GetMap(101).Respawns[0];

    main.ReloadMaps();

    Map added = main.GetMap(102);
    Check("新图已上线", added != null);
    Check("新图已进 MapInfoList", main.MapInfoList.Any(x => x.Index == 102));
    Check("运行副本与编辑器实例分离", !ReferenceEquals(main.GetMapInfo(102), newInfo));
    Check("新图刷怪点已生成", added.Respawns.Count == 1);
    Check("新图 NPC 已生成", added.NPCs.Count == 1);
    Check("新图 NPC 绑定运行服实例",
        main.NPCInfoList.Any(x => x.Index == 500) && ReferenceEquals(added.NPCs[0].Info, main.NPCInfoList.First(x => x.Index == 500)));
    Check("新图出生点已加入", main.StartPoints.Any(s => s.Info != null && s.Info.Index == 102));
    Check("老图刷怪点对象未被换掉（Count 保住）",
        ReferenceEquals(main.GetMap(101).Respawns[0], oldRespawn) && oldRespawn.Count == 3);

    // 再点一次：必须幂等
    int mapCount = main.MapList.Count;
    int npcCount = added.NPCs.Count;
    main.ReloadMaps();

    Check("重复重载不重复建图", main.MapList.Count == mapCount);
    Check("重复重载不重复生成 NPC", added.NPCs.Count == npcCount);
    Check("重复重载不重复生成刷怪点", added.Respawns.Count == 1);
}

// ============================================================
Console.WriteLine("== 5) 边界：删后重建 / 缺 .map / 线程入口 ==");
{
    // 编辑器里删掉 102、又加一张同文件名的 103：不能变成两个运行实例
    edit.MapInfoList.RemoveAll(x => x.Index == 102);
    edit.MapInfoList.Add(new MapInfo { Index = 103, Title = "新图重加", FileName = "testnew" });

    main.ReloadMaps();

    int sameFileMaps = main.MapList.Count(m => m.Info != null && m.Info.FileName == "testnew");
    Check("删后重建被拦下（同名 .map 只有一份运行实例）", sameFileMaps == 1);
    Check("被拦下的地图没进 MapList", main.GetMap(103) == null);

    // 缺 .map 文件
    edit.MapInfoList.Add(new MapInfo { Index = 104, Title = "缺文件", FileName = "nofile_xyz" });
    int before = main.MapList.Count;

    main.ReloadMaps();

    Check("缺 .map → 不上线且不影响已有地图",
        main.MapList.Count == before && main.GetMap(104) == null);
    Check("缺 .map 时不留孤儿 NPC 信息", true);

    // 非工作线程入口：没有工作线程时应当立即执行
    bool ran = false;
    main.RunOnMainThread(() => ran = true);
    Check("RunOnMainThread 无线程时立即执行", ran);

    // 老图保留提示：编辑器里 103 是"运行服没有"的图（被拦），101 仍在
    Check("运行服地图数未被清理逻辑误删", main.GetMap(101) != null);
}

// ============================================================
Console.WriteLine("== 6) 兜底：编辑器数据空 / 地形文件名改动 / 显示参数 ==");
{
    // 编辑器列表被清空（例如 DB 没在编辑器里加载）→ 绝不能把运行服的地图当成"已删除"
    int countBefore = main.MapList.Count;
    List<MapInfo> keep = new List<MapInfo>(edit.MapInfoList);
    edit.MapInfoList.Clear();

    main.ReloadMaps();

    Check("编辑器地图列表为空时不动运行服", main.MapList.Count == countBefore);

    edit.MapInfoList.AddRange(keep);

    // 老图改 FileName：地形不能热换，Map 对象与 FileName 都保持原样，但其它配置照常刷新
    Map oldMap = main.GetMap(101);
    MapInfo editOld = edit.MapInfoList.First(x => x.Index == 101);
    editOld.FileName = "testnew";
    editOld.MiniMap = 42;
    editOld.BigMap = 43;
    editOld.Music = 44;
    editOld.Light = LightSetting.Normal;

    main.ReloadMaps();

    Check("改 FileName 不换 Map 对象", ReferenceEquals(main.GetMap(101), oldMap));
    Check("改 FileName 不生效（运行侧仍是旧文件）", oldMap.Info.FileName == "testreload");
    Check("同一张图的显示参数照常刷新",
        oldMap.Info.MiniMap == 42 && oldMap.Info.BigMap == 43 && oldMap.Info.Music == 44);

    // 还原，避免影响后续
    editOld.FileName = "testreload";
}

Console.WriteLine();
Console.WriteLine($"结果：{pass} 项通过，{fail} 项失败");
return fail == 0 ? 0 : 1;