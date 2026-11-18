using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

// 只读探针：羽化登仙装备清单 + 60~130 级怪物一览（识别 BOSS 候选用），不写库不写文件
// 用法（工作目录 = 服务端目录，即 Server.MirDB 所在处）：
//   HumUpDropProbe --items            列羽化装备（Shape 1001~1058 / 2011~2058）
//   HumUpDropProbe --monsters         列 60~130 级全部怪物（名称/等级/AI/经验/HP）

try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

bool items = args.Contains("--items");
bool monsters = args.Contains("--monsters");
if (!items && !monsters) { items = monsters = true; }

Settings.Load();

var envir = Envir.Main;
if (!envir.LoadDB())
{
    Console.WriteLine("!! LoadDB 失败");
    return 2;
}

if (items)
{
    Console.WriteLine("==== 羽化登仙装备（Shape 1001~1058 / 2011~2058）====");
    var list = envir.ItemInfoList
        .Where(x => x != null && ((x.Shape >= 1001 && x.Shape <= 1058) || (x.Shape >= 2011 && x.Shape <= 2058)))
        .OrderBy(x => x.Shape).ThenBy(x => x.Index)
        .ToList();

    Console.WriteLine($"共 {list.Count} 件\n");
    foreach (var it in list)
    {
        string lvl = it.RequiredType == RequiredType.Level ? it.RequiredAmount.ToString() : $"{it.RequiredType}:{it.RequiredAmount}";
        Console.WriteLine($"[{it.Index,5}] {it.Name,-20} Shape={it.Shape,-5} 类型={it.Type} 等级={lvl} 职业={it.RequiredClass}");
    }
}

if (monsters)
{
    Console.WriteLine("\n==== 60~130 级怪物（名称/等级/AI/经验/HP）====");
    var list = envir.MonsterInfoList
        .Where(x => x != null && x.Level >= 60 && x.Level <= 130)
        .OrderBy(x => x.Level).ThenBy(x => x.Experience)
        .ToList();

    Console.WriteLine($"共 {list.Count} 只\n");
    foreach (var m in list)
    {
        long hp = 0;
        try { hp = m.Stats == null ? 0 : m.Stats[Stat.HP]; } catch { }
        Console.WriteLine($"[{m.Index,5}] {m.Name,-16} 等级={m.Level,-4} AI={m.AI,-4} 经验={m.Experience,-12} HP={hp,-10} 掉落={m.DropPath}");
    }
}

return 0;
