using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

// 羽化登仙装备爆率投放工具（只改 Envir\Drops\*.txt，不动 Server.MirDB）
//
// 投放方案（与用户确认于 2026-11-18）：
//   A. BOSS 档：60/90/100/110 档装备 → 对应档位 BOSS 的爆率文件，1/500 GROUP* 随机一件
//      （80 档候选只有麒麟，全服无刷怪点，跳过）
//   B. 70 档装备 → 70 级段地图（沙遗迹 morae / 货船帆船只片木岛 / 三清道场 SD 系列）
//      上所有刷怪：小怪 1/1500、精英（HP ≥ 1.5×本图怪物 HP 中位数）1/800
//   C. 120+130 档装备（合并池）→ 白龙潭 BOSS：八爪巨兽2 1/500、海触龙神 1/700
//
// 安全措施：改前备份到 Back Up\manual-<时间戳>-羽化爆率投放\；已写过（含标记）的文件跳过。
//
// 用法：工作目录 = 服务端目录；HumUpDropWriter --dryrun 只打印不改文件
try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

bool dry = args.Contains("--dryrun");
string marker = "[羽化登仙]";

Settings.Load();

var envir = Envir.Main;
if (!envir.LoadDB())
{
    Console.WriteLine("!! LoadDB 失败");
    return 2;
}

var monsters = envir.MonsterInfoList.Where(x => x != null).ToDictionary(x => x.Index, x => x);

string _backupDir = Path.Combine("Back Up", $"manual-{DateTime.Now:yyyyMMdd_HHmm}-羽化爆率投放");

// ---------- 装备池：按穿戴等级（RequiredType==Level）分档 ----------
var pools = new Dictionary<int, List<string>>();
foreach (var it in envir.ItemInfoList)
{
    if (it == null) continue;
    if (it.RequiredType != RequiredType.Level) continue;
    int lvl = it.RequiredAmount;
    if (lvl < 60 || lvl > 130) continue;
    if (!((it.Shape >= 1001 && it.Shape <= 1058) || (it.Shape >= 2011 && it.Shape <= 2058))) continue;
    if (!pools.TryGetValue(lvl, out var list)) pools[lvl] = list = new List<string>();
    list.Add(it.Name);
}

Console.WriteLine("==== 装备池 ====");
foreach (var kv in pools.OrderBy(x => x.Key))
    Console.WriteLine($"  {kv.Key} 档：{kv.Value.Count} 件");

// ---------- 掉落文件路径 ----------
string dropRoot = "Envir" + Path.DirectorySeparatorChar + "Drops";
string GetDropFile(MonsterInfo mi)
{
    string name = string.IsNullOrEmpty(mi.DropPath) ? mi.Name : mi.DropPath;
    return Path.Combine(dropRoot, name + ".txt");
}

// ---------- 写入函数 ----------
int _written = 0, _skipped = 0, _created = 0;
var _report = new List<string>();

void WriteBlock(string file, int chance, List<string> items, string label)
{
    if (File.Exists(file) && File.ReadAllText(file).Contains(marker))
    {
        _skipped++;
        _report.Add($"[跳过-已有] {Path.GetFileName(file)}");
        return;
    }

    if (dry)
    {
        _written++;
        _report.Add($"[OK {chance}] {Path.GetFileName(file)}  ←{items.Count}件 ({label})");
        return;
    }

    // 写前备份原文件（新文件不备份，但记录到清单）
    if (File.Exists(file))
    {
        Directory.CreateDirectory(_backupDir);
        string dst = Path.Combine(_backupDir, Path.GetFileName(file));
        if (!File.Exists(dst)) File.Copy(file, dst, true);
    }

    var sb = new StringBuilder();
    sb.AppendLine();
    sb.AppendLine($";{marker} {DateTime.Now:yyyy-MM-dd} 自动添加：{label}（平均杀 {chance} 个出一件，随机一件）");
    sb.AppendLine($"1/{chance} GROUP*");
    sb.AppendLine("{");
    foreach (var n in items) sb.AppendLine("1/1 " + n);
    sb.AppendLine("}");

    if (!File.Exists(file))
    {
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        _created++;
    }
    else
    {
        File.AppendAllText(file, sb.ToString(), new UTF8Encoding(false));
    }
    _written++;
    _report.Add($"[OK {chance}] {Path.GetFileName(file)}  ←{items.Count}件 ({label})");
}

// ---------- A. BOSS 档 ----------
var bossPlan = new (int tier, int chance, string[] names)[]
{
    ( 60, 500, new[]{ "沃玛教主","沃玛教主11","古代沃玛教主","祖玛教主","祖玛教主0","赤月恶魔","赤月恶魔1",
                     "骸骨盘王","古代骸骨盘王","古代骸骨盘王0","寒冰魔王","满月之牛魔王","邪牛天王",
                     "满月之魔神","凰天魔神","破凰魔神","瘟疫统领","瘟疫统领0" }),
    ( 90, 500, new[]{ "破天魔龙" }),
    (100, 500, new[]{ "混沌教主","阳龙王","阳龙王0","黑暗之王","黑暗之王0","雷帝1","雷帝2","雷帝3" }),
    (110, 500, new[]{ "狂暴鱼统领","狂暴鱼统领0" }),
};

Console.WriteLine("\n==== A. BOSS 档（1/500）====");
var monByName = monsters.Values.GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.First());
foreach (var (tier, chance, names) in bossPlan)
{
    if (!pools.TryGetValue(tier, out var pool)) { Console.WriteLine($"  {tier} 档无装备，跳过"); continue; }
    foreach (var n in names)
    {
        if (!monByName.TryGetValue(n, out var mi))
        {
            Console.WriteLine($"  !! 怪物表里没有 [{n}]，检查名字");
            continue;
        }
        WriteBlock(GetDropFile(mi), chance, pool, $"{tier} 档");
    }
}

// ---------- B. 70 档：70 级段地图全部刷怪 ----------
Console.WriteLine("\n==== B. 70 档（三片 70 级段地图）====");
if (!pools.TryGetValue(70, out var pool70)) { Console.WriteLine("  70 档无装备"); }

// 70 级段地图 = 刷有 70~79 级怪物的地图
var maps70 = new List<MapInfo>();
foreach (var map in envir.MapInfoList)
{
    if (map?.Respawns == null) continue;
    bool has70 = map.Respawns.Any(r => r != null && monsters.TryGetValue(r.MonsterIndex, out var mi) && mi.Level >= 70 && mi.Level <= 79);
    if (has70) maps70.Add(map);
}
Console.WriteLine("  地图：" + string.Join("、", maps70.Select(m => m.FileName)));

// 每张图的怪物 HP 中位数（排除箱子/卵/陷阱类：AI=971 或 HP≤100）
double Median(MapInfo map)
{
    var hps = new List<long>();
    foreach (var r in map.Respawns)
    {
        if (r == null || !monsters.TryGetValue(r.MonsterIndex, out var mi)) continue;
        if (mi.AI == 971) continue;
        long hp = 0; try { hp = mi.Stats == null ? 0 : mi.Stats[Stat.HP]; } catch { }
        if (hp <= 100) continue;
        hps.Add(hp);
    }
    if (hps.Count == 0) return 0;
    hps.Sort();
    return hps.Count % 2 == 1 ? hps[hps.Count / 2] : (hps[hps.Count / 2 - 1] + hps[hps.Count / 2]) / 2.0;
}

// 怪物 → 是否精英（在任一所在 70 级段地图上 HP ≥ 1.5×该图中位数）
var mon70 = new Dictionary<int, bool>();
var monCross = new HashSet<int>();   // 同时刷在 70 级段地图之外的（掉落文件是按怪名共用的，提示一下）
foreach (var map in envir.MapInfoList)
{
    if (map?.Respawns == null) continue;
    bool in70 = maps70.Contains(map);
    double med = in70 ? Median(map) : 0;

    foreach (var r in map.Respawns)
    {
        if (r == null || !monsters.TryGetValue(r.MonsterIndex, out var mi)) continue;
        if (mi.AI == 971) continue;
        long hp = 0; try { hp = mi.Stats == null ? 0 : mi.Stats[Stat.HP]; } catch { }
        if (hp <= 100) continue;

        if (in70)
        {
            bool elite = med > 0 && hp >= 1.5 * med;
            mon70[mi.Index] = mon70.TryGetValue(mi.Index, out var old) ? (old || elite) : elite;
        }
        else
        {
            if (mon70.ContainsKey(mi.Index)) monCross.Add(mi.Index);
        }
    }
}

foreach (var kv in mon70.OrderBy(x => monsters[x.Key].Name))
{
    var mi = monsters[kv.Key];
    int chance = kv.Value ? 800 : 1500;
    WriteBlock(GetDropFile(mi), chance, pool70, $"70档 {(kv.Value ? "精英" : "小怪")}");
}
if (monCross.Count > 0)
    Console.WriteLine("  !! 以下怪物同时刷在 70 级段地图之外（几率会全局生效），请留意：\n     "
        + string.Join("、", monCross.Select(i => monsters[i].Name).Distinct()));

// ---------- C. 白龙潭 BOSS ----------
Console.WriteLine("\n==== C. 白龙潭（120+130 档合并池）====");
var poolWD = new List<string>();
if (pools.TryGetValue(120, out var p120)) poolWD.AddRange(p120);
if (pools.TryGetValue(130, out var p130)) poolWD.AddRange(p130);
Console.WriteLine($"  合并池 {poolWD.Count} 件");

if (monByName.TryGetValue("海触龙神", out var boss1)) WriteBlock(GetDropFile(boss1), 700, poolWD, "白龙潭BOSS-海触龙神");
if (monByName.TryGetValue("八爪巨兽2", out var boss2)) WriteBlock(GetDropFile(boss2), 500, poolWD, "白龙潭BOSS-八爪巨兽2");

// ---------- --verify：用真实解析器复查 ----------
if (args.Contains("--verify"))
{
    Console.WriteLine("\n==== 解析器复查（DropInfo.FromLine）====");
    int files = 0, badLines = 0, groups = 0;
    foreach (var f in Directory.GetFiles(dropRoot, "*.txt"))
    {
        var text = File.ReadAllText(f);
        if (!text.Contains(marker)) continue;

        files++;
        bool groupOpen = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";")) continue;
            if (line == "{") { groupOpen = true; continue; }
            if (line == "}") { groupOpen = false; continue; }

            var d = DropInfo.FromLine(line);
            if (d == null) { badLines++; Console.WriteLine($"  !! 坏行 {Path.GetFileName(f)}: {line}"); continue; }
            if (d.GroupedDrop != null && !groupOpen) groups++;
        }
    }
    Console.WriteLine($"含羽化块的文件 {files} 个，GROUP 组 {groups} 个，坏行 {badLines} 行");
    Console.WriteLine(badLines == 0 && groups == 92 ? "✅ 全部通过" : "⚠️ 有问题，见上");
    return 0;
}

// ---------- 汇总 ----------
Console.WriteLine($"\n==== 汇总：写入 {_written} 个文件（新建 {_created}），跳过 {_skipped} 个 ====");
if (dry) Console.WriteLine("（--dryrun：以上未真正写盘）");
foreach (var line in _report) Console.WriteLine(line);

if (!dry && _written > 0)
{
    File.WriteAllLines(Path.Combine(_backupDir, "本次改动清单.txt"), _report, Encoding.UTF8);
    Console.WriteLine($"备份与改动清单已存：{_backupDir}");
}

return 0;
