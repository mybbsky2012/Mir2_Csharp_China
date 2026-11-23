using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

// 只读探针②：地图刷怪视图
//   HumUpDropProbe2 --maps70      70~79 级怪物 → 出现在哪些地图
//   HumUpDropProbe2 --map 关键词  指定地图（FileName 或 Title 含关键词）的刷怪明细
try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

Settings.Load();

var envir = Envir.Main;
if (!envir.LoadDB())
{
    Console.WriteLine("!! LoadDB 失败");
    return 2;
}

var monsters = envir.MonsterInfoList.Where(x => x != null).ToDictionary(x => x.Index, x => x);

string keyword = null;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--map") keyword = args[i + 1];

bool maps70 = args.Contains("--maps70");

string monName = null;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--monster") monName = args[i + 1];

if (monName != null)
{
    Console.WriteLine($"\n==== 怪物「{monName}」刷在哪些地图 ====");
    var hits = monsters.Values.Where(m => m.Name.Contains(monName)).ToList();
    foreach (var map in envir.MapInfoList)
    {
        if (map?.Respawns == null) continue;
        var rows = map.Respawns.Where(r => r != null && hits.Any(h => h.Index == r.MonsterIndex)).ToList();
        if (rows.Count == 0) continue;
        Console.WriteLine($"[{map.FileName}] {map.Title}  " +
            string.Join("、", rows.Select(r => $"{hits.First(h => h.Index == r.MonsterIndex).Name}x{r.Count}")));
    }
}

if (maps70)
{
    Console.WriteLine("==== 70~79 级怪物分布（怪物 → 所在地图）====");
    var byMon = new Dictionary<int, List<string>>();
    foreach (var map in envir.MapInfoList)
    {
        if (map?.Respawns == null) continue;
        foreach (var r in map.Respawns)
        {
            if (r == null || !monsters.TryGetValue(r.MonsterIndex, out var mi)) continue;
            if (mi.Level < 70 || mi.Level > 79) continue;
            if (!byMon.TryGetValue(mi.Index, out var list)) byMon[mi.Index] = list = new List<string>();
            list.Add($"{map.FileName}({map.Title})x{r.Count}");
        }
    }
    foreach (var kv in byMon.OrderBy(x => monsters[x.Key].Level).ThenBy(x => x.Key))
    {
        var mi = monsters[kv.Key];
        long hp = 0; try { hp = mi.Stats == null ? 0 : mi.Stats[Stat.HP]; } catch { }
        Console.WriteLine($"[{mi.Index,5}] {mi.Name,-14} 等级={mi.Level,-3} AI={mi.AI,-4} HP={hp,-8} 经验={mi.Experience,-9} 地图={string.Join("、", kv.Value.Distinct())}");
    }
}

if (keyword != null)
{
    Console.WriteLine($"\n==== 地图「{keyword}」刷怪明细 ====");
    var maps = envir.MapInfoList.Where(m => m != null &&
        ((m.FileName ?? "").Contains(keyword) || (m.Title ?? "").Contains(keyword))).ToList();
    if (maps.Count == 0) { Console.WriteLine("(找不到)"); return 0; }

    foreach (var map in maps)
    {
        Console.WriteLine($"\n[{map.Index}] {map.FileName}  {map.Title}");
        if (map.Respawns == null || map.Respawns.Count == 0) { Console.WriteLine("   (无刷怪)"); continue; }
        foreach (var r in map.Respawns.Where(r => r != null))
        {
            if (!monsters.TryGetValue(r.MonsterIndex, out var mi)) { Console.WriteLine($"   [未知怪物 {r.MonsterIndex}] x{r.Count}"); continue; }
            long hp = 0; try { hp = mi.Stats == null ? 0 : mi.Stats[Stat.HP]; } catch { }
            Console.WriteLine($"   {mi.Name,-14} 等级={mi.Level,-3} AI={mi.AI,-4} HP={hp,-8} 经验={mi.Experience,-9} 数量={r.Count} 坐标={r.Location.X},{r.Location.Y}");
        }
    }
}

return 0;
