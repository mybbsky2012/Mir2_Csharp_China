using System.Text;
using Server;
using Server.MirEnvir;

namespace ConquestProbe;

/// <summary>
/// 只读探针：导出 MirDB 中全部攻城(Conquest)定义，核对城堡配置。
/// 用法：把本程序编译产物放到服务端目录（与 Server.MirDB 同级）运行 ConquestProbe.exe
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

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

        string MapName(int idx)
        {
            var m = envir.GetMapInfo(idx);
            return m == null ? "??#" + idx : $"{m.FileName}({m.Title})";
        }
        string MobName(int idx)
        {
            var m = envir.GetMonsterInfo(idx);
            return m == null ? "??#" + idx : $"{m.Name}[AI={m.AI}]";
        }

        Console.WriteLine($"=== 攻城定义共 {envir.ConquestInfoList.Count} 条 ===");
        foreach (var c in envir.ConquestInfoList)
        {
            Console.WriteLine();
            Console.WriteLine($"—— 城堡 #{c.Index} 「{c.Name}」 ——");
            Console.WriteLine($"  主地图={MapName(c.MapIndex)}  皇宫={MapName(c.PalaceIndex)}  FullMap={c.FullMap}");
            Console.WriteLine($"  区域中心=({c.Location.X},{c.Location.Y}) 范围={c.Size}");
            Console.WriteLine($"  开战整点={c.StartHour} 时长={c.WarLength}分钟 类型={c.Type} 玩法={c.Game}");
            Console.WriteLine($"  开战日=一二三四五六日 {c.Monday}{c.Tuesday}{c.Wednesday}{c.Thursday}{c.Friday}{c.Saturday}{c.Sunday} (1=开)");
            Console.WriteLine($"  附加地图=[{string.Join(',', c.ExtraMaps.Select(MapName))}]");
            Console.WriteLine($"  守卫弓手 {c.ConquestGuards.Count} 个:");
            foreach (var g in c.ConquestGuards)
                Console.WriteLine($"    #{g.Index} '{g.Name}' 怪={MobName(g.MobIndex)} 坐标=({g.Location.X},{g.Location.Y}) 修理费={g.RepairCost}");
            Console.WriteLine($"  城门 {c.ConquestGates.Count} 个:");
            foreach (var g in c.ConquestGates)
                Console.WriteLine($"    #{g.Index} '{g.Name}' 怪={MobName(g.MobIndex)} 坐标=({g.Location.X},{g.Location.Y}) 修理费={g.RepairCost}");
            Console.WriteLine($"  城墙 {c.ConquestWalls.Count} 个:");
            foreach (var w in c.ConquestWalls)
                Console.WriteLine($"    #{w.Index} '{w.Name}' 怪={MobName(w.MobIndex)} 坐标=({w.Location.X},{w.Location.Y}) 修理费={w.RepairCost}");
            Console.WriteLine($"  投石 {c.ConquestSieges.Count} 个:");
            foreach (var s in c.ConquestSieges)
                Console.WriteLine($"    #{s.Index} '{s.Name}' 怪={MobName(s.MobIndex)} 坐标=({s.Location.X},{s.Location.Y}) 修理费={s.RepairCost}");
            Console.WriteLine($"  旗帜 {c.ConquestFlags.Count} 个:");
            foreach (var f in c.ConquestFlags)
                Console.WriteLine($"    #{f.Index} '{f.Name}' 脚本={f.FileName} 坐标=({f.Location.X},{f.Location.Y})");
            Console.WriteLine($"  王点=({c.KingLocation.X},{c.KingLocation.Y})x{c.KingSize}  控制点 {c.ControlPoints.Count} 个");
        }

        return 0;
    }
}
