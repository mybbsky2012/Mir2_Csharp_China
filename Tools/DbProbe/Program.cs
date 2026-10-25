using System.Text;
using Server;
using Server.MirEnvir;
using Server.MirObjects;

namespace DbProbe;

/// <summary>
/// 只读探针：导出地图刷怪配置与怪物定义，用来核对 NPC 脚本里写的怪名/地图名是否真的会刷出来。
/// 用法：把本程序放到服务端目录（与 Server.MirDB 同级）运行 DbProbe.exe。
///   DbProbe.exe            —— 原有默认行为（InDun_01 地图 + 雷帝怪物）
///   DbProbe.exe 关键词     —— 按关键词查怪物：记录、掉落文件是否存在、刷怪点
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 重定向时可能失败 */ }

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

        string keyword = args.Length > 0 ? args[0] : null;
        if (!string.IsNullOrWhiteSpace(keyword))
            return ProbeMonster(envir, keyword);

        Console.WriteLine("=== 地图：FileName 含 InDun_01 ===");
        foreach (var m in envir.MapInfoList)
        {
            if (m == null) continue;
            if (!m.FileName.Contains("InDun_01", StringComparison.OrdinalIgnoreCase)) continue;

            Console.WriteLine($"[{m.Index}] FileName='{m.FileName}' Title='{m.Title}' 刷怪组={m.Respawns.Count} NPC={m.NPCs.Count}");
            foreach (var r in m.Respawns)
            {
                string mn = (r.MonsterIndex >= 0 && r.MonsterIndex < envir.MonsterInfoList.Count
                             && envir.MonsterInfoList[r.MonsterIndex] != null)
                    ? envir.MonsterInfoList[r.MonsterIndex].Name
                    : "??#" + r.MonsterIndex;
                string gn = (r.MonsterIndex >= 0 && r.MonsterIndex < envir.MonsterInfoList.Count
                             && envir.MonsterInfoList[r.MonsterIndex] != null)
                    ? envir.MonsterInfoList[r.MonsterIndex].GameName
                    : "??";
                Console.WriteLine($"     怪 Name='{mn}' GameName='{gn}' 坐标={r.Location.X},{r.Location.Y} 数量={r.Count} 范围={r.Spread}");
            }
            foreach (var n in m.NPCs)
                Console.WriteLine($"     NPC='{n.Name}' FileName={n.FileName} 坐标={n.Location.X},{n.Location.Y}");
        }

        Console.WriteLine();
        Console.WriteLine("=== 怪物表：Name 或 GameName 含 雷帝 ===");
        for (int i = 0; i < envir.MonsterInfoList.Count; i++)
        {
            var mi = envir.MonsterInfoList[i];
            if (mi == null) continue;
            bool hit = (mi.Name != null && mi.Name.Contains("雷帝"))
                    || (mi.GameName != null && mi.GameName.Contains("雷帝"));
            if (!hit) continue;

            string dropFile = string.IsNullOrEmpty(mi.DropPath) ? mi.Name + ".txt" : mi.DropPath + ".txt";
            Console.WriteLine($"[{i}] Name='{mi.Name}' | GameName='{mi.GameName}' | 相同={(mi.Name == mi.GameName)}");
            Console.WriteLine($"      AI={mi.AI} Lv={mi.Level} 掉落文件=Drops\\{dropFile} (DropPath='{mi.DropPath}')");
            Console.WriteLine($"      HP={mi.Stats[Stat.HP]} DC={mi.Stats[Stat.MinDC]}-{mi.Stats[Stat.MaxDC]} EXP={mi.Experience}");
        }

        Console.WriteLine();
        Console.WriteLine("=== 抽查：Name != GameName 的怪物数量 / 样例 ===");
        int diff = 0, same = 0;
        for (int i = 0; i < envir.MonsterInfoList.Count; i++)
        {
            var mi = envir.MonsterInfoList[i];
            if (mi == null) continue;
            if (mi.Name == mi.GameName) { same++; continue; }
            diff++;
            if (diff <= 15)
                Console.WriteLine($"[{i}] Name='{mi.Name}'  <-->  GameName='{mi.GameName}'");
        }
        Console.WriteLine($"统计：Name==GameName {same} 个，不同的 {diff} 个");

        Console.WriteLine();
        Console.WriteLine("=== Respawn 里指向 雷帝* 的地图（按 Name 或 GameName 匹配） ===");
        foreach (var m in envir.MapInfoList)
        {
            if (m == null) continue;
            foreach (var r in m.Respawns)
            {
                if (r.MonsterIndex < 0 || r.MonsterIndex >= envir.MonsterInfoList.Count) continue;
                var mi = envir.MonsterInfoList[r.MonsterIndex];
                if (mi == null) continue;
                if (!(mi.Name != null && mi.Name.Contains("雷帝")) && !(mi.GameName != null && mi.GameName.Contains("雷帝"))) continue;
                Console.WriteLine($"   {m.FileName} <- Name='{mi.Name}' GameName='{mi.GameName}' x{r.Count} @ {r.Location.X},{r.Location.Y}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"统计：MapInfo={envir.MapInfoList.Count} MonsterInfo={envir.MonsterInfoList.Count} MapList={envir.MapList.Count}");
        return 0;
    }

    /// <summary>按关键词查怪物：记录、掉落文件、刷新点。只读，不改任何数据。</summary>
    private static int ProbeMonster(Envir envir, string keyword)
    {
        Console.WriteLine($"=== 怪物表：Name 或 GameName 含 {keyword} ===");
        var hits = new List<Server.MirDatabase.MonsterInfo>();
        for (int i = 0; i < envir.MonsterInfoList.Count; i++)
        {
            var mi = envir.MonsterInfoList[i];
            if (mi == null) continue;
            if (!((mi.Name != null && mi.Name.Contains(keyword)) ||
                  (mi.GameName != null && mi.GameName.Contains(keyword)))) continue;
            hits.Add(mi);

            string dropFile = string.IsNullOrEmpty(mi.DropPath) ? mi.Name + ".txt" : mi.DropPath + ".txt";
            string dropPath = Path.Combine("Envir", "Drops", dropFile);
            bool dropExists = File.Exists(dropPath);
            long dropSize = dropExists ? new FileInfo(dropPath).Length : -1;

            Console.WriteLine($"[{i}] Name='{mi.Name}' | GameName='{mi.GameName}'");
            Console.WriteLine($"      AI={mi.AI} Lv={mi.Level} HP={mi.Stats[Stat.HP]} DC={mi.Stats[Stat.MinDC]}-{mi.Stats[Stat.MaxDC]} EXP={mi.Experience}");
            Console.WriteLine($"      掉落文件=Drops\\{dropFile} 存在={dropExists} 大小={dropSize}B (DropPath='{mi.DropPath}')");
        }
        if (hits.Count == 0)
        {
            Console.WriteLine("（无匹配记录）");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("=== 刷怪点：指向上述怪的地图 ===");
        HashSet<int> hitIdx = new(hits.Select(h => h.Index));
        int respTotal = 0;
        foreach (var m in envir.MapInfoList)
        {
            if (m == null) continue;
            foreach (var r in m.Respawns)
            {
                if (r.MonsterIndex < 0 || r.MonsterIndex >= envir.MonsterInfoList.Count) continue;
                var mi = envir.MonsterInfoList[r.MonsterIndex];
                if (mi == null || !hitIdx.Contains(mi.Index)) continue;
                respTotal++;
                Console.WriteLine($"   {m.FileName}({m.Title}) <- Name='{mi.Name}' x{r.Count} @ {r.Location.X},{r.Location.Y}");
            }
        }
        if (respTotal == 0)
            Console.WriteLine("   （没有任何地图刷怪组指向它 —— 若游戏里还能遇到，多半是脚本/事件刷的）");

        Console.WriteLine();
        Console.WriteLine($"统计：命中 {hits.Count} 条记录，刷怪点 {respTotal} 处");
        return 0;
    }
}
