using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace QualityGradeProbe;

/// <summary>
/// 只读探针：核对「品质外观发光」链路的数据端。
///   1) 物品库里 Grade >= 2 的物品有多少个（样例）
///   2) 每个角色身上 盔甲/武器 槽的物品与 Grade（服务端发给客户端的字节就是它）
/// 用法：放到服务端目录（与 Server.MirDB 同级）运行。只读，不写库。
/// </summary>
internal static class Program
{
    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        // 角色数据在账号库（LoadAccounts）；读角色会解析 BUFF，必须先按服务端启动顺序加载 BuffSystem.ini
        envir.BuffInfoList.Clear();
        foreach (BuffInfo buff in BuffInfo.Load()) envir.BuffInfoList.Add(buff);
        envir.LoadAccounts();

        Console.WriteLine("=== 物品库 Grade 分布 ===");
        var byGrade = new Dictionary<int, int>();
        var samples = new Dictionary<int, List<string>>();
        foreach (var info in envir.ItemInfoList)
        {
            if (info == null) continue;
            int g = (int)info.Grade;
            byGrade.TryGetValue(g, out int n);
            byGrade[g] = n + 1;
            if (g >= 2)
            {
                if (!samples.TryGetValue(g, out var list)) samples[g] = list = new List<string>();
                if (list.Count < 8) list.Add(info.Name);
            }
        }
        foreach (var kv in byGrade.OrderBy(k => k.Key))
            Console.WriteLine($"  Grade={kv.Key} : {kv.Value} 个" + (samples.ContainsKey(kv.Key) ? "   样例: " + string.Join(", ", samples[kv.Key]) : ""));

        Console.WriteLine();
        Console.WriteLine("=== 角色身上 盔甲/武器 品质（这就是发给客户端的字节） ===");
        foreach (var c in envir.CharacterList)
        {
            if (c == null) continue;
            var armour = c.Equipment != null && c.Equipment.Length > (int)EquipmentSlot.盔甲 ? c.Equipment[(int)EquipmentSlot.盔甲] : null;
            var weapon = c.Equipment != null && c.Equipment.Length > (int)EquipmentSlot.武器 ? c.Equipment[(int)EquipmentSlot.武器] : null;
            string ag = armour != null ? $"{armour.Info.Name} Grade={(int)armour.Info.Grade}" : "(空)";
            string wg = weapon != null ? $"{weapon.Info.Name} Grade={(int)weapon.Info.Grade}" : "(空)";
            Console.WriteLine($"  [{c.Name}] Lv{c.Level}  盔甲: {ag}   武器: {wg}");
        }

        Console.WriteLine();
        Console.WriteLine($"统计: ItemInfo={envir.ItemInfoList.Count} Characters={envir.CharacterList.Count}");
        return 0;
    }
}
