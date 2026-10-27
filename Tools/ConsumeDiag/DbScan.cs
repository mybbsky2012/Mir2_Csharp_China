using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace ConsumeDiag;

/// <summary>
/// 只读：把线上库里「实际存在的」回城石/随机石/气血石/祝福油/传送戒指 逐个列出来，
/// 重点看 Count 与 CurrentDura/MaxDura —— 判断是不是「电量已经 0、物品却还留着」。
/// </summary>
internal static class DbScan
{
    private static readonly string[] Names =
    {
        "回城石", "随机石", "气血石", "魔血石", "幻血石",
        "祝福油", "精致祝福油", "修复油", "战神油",
        "传送戒指",
    };

    private static int _scanned;
    private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();

    public static void Run(Envir envir)
    {
        Console.WriteLine("===== 线上库实际物品（角色 + 英雄） =====");
        Console.WriteLine($"  账号 {envir.AccountList.Count}  角色 {envir.CharacterList.Count}  英雄 {envir.HeroList.Count}");

        int total = 0, zeroDura = 0, rings = 0, ringZero = 0;

        foreach (AccountInfo acc in envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;

                total += ScanGrid(ch.Name + "（角色）", "背包", ch.Inventory, ref zeroDura, ref rings, ref ringZero);
                total += ScanGrid(ch.Name + "（角色）", "装备", ch.Equipment, ref zeroDura, ref rings, ref ringZero);

                if (ch.Heroes == null) continue;
                foreach (HeroInfo h in ch.Heroes)
                {
                    if (h == null) continue;
                    total += ScanGrid(ch.Name + "→英雄 " + h.Name, "背包", h.Inventory, ref zeroDura, ref rings, ref ringZero);
                    total += ScanGrid(ch.Name + "→英雄 " + h.Name, "装备", h.Equipment, ref zeroDura, ref rings, ref ringZero);
                }
            }
        }

        Console.WriteLine($"  扫描到物品总数 = {_scanned} 件");
        Console.WriteLine($"  小计：命中 {total} 件；其中电量已 0 但物品仍在 = {zeroDura} 件");
        Console.WriteLine($"  传送戒指：{rings} 枚（持久 0 的 {ringZero} 枚 —— 持久 0 时 SpecialMode 不含 Teleport，@传送 会被静默拒绝）");

        Console.WriteLine("  ---- 各物品在库里的实际数量（只列含 石/油/戒指 的）----");
        foreach (var kv in Seen.OrderByDescending(x => x.Value).Take(40))
            Console.WriteLine($"      {kv.Key} × {kv.Value}");

        Console.WriteLine("  ---- 物品定义里名字含 祝福/油 的（全表搜索）----");
        foreach (ItemInfo ii in envir.ItemInfoList.Where(x => x?.Name != null && (x.Name.Contains("祝福") || x.Name.Contains("油"))))
            Console.WriteLine($"      [{ii.Index,5}] {ii.Name} Type={ii.Type}({(int)ii.Type}) Shape={ii.Shape} 持久={ii.Durability} 叠加={ii.StackSize}");

        int np = envir.MapInfoList.Count(m => m != null && m.NoPosition);
        Console.WriteLine($"  ---- 地图 NoPosition（禁用传送戒指）: {np}/{envir.MapInfoList.Count} 张 ----");
        foreach (MapInfo m in envir.MapInfoList.Where(x => x != null && x.NoPosition).Take(8))
            Console.WriteLine($"      [{m.Index}] {m.FileName} {m.Title}");

        Console.WriteLine("  ---- 最近新增的物品（Index >= 4230）----");
        foreach (ItemInfo ii in envir.ItemInfoList.Where(x => x != null && x.Index >= 4230).OrderBy(x => x.Index))
            Console.WriteLine($"      [{ii.Index,5}] {ii.Name} Type={ii.Type}({(int)ii.Type}) Shape={ii.Shape} 持久={ii.Durability} 叠加={ii.StackSize} Unique={(short)ii.Unique}");

        Console.WriteLine("  ---- 线上 超级祝福油 实例详情 ----");
        DumpName(envir, "超级祝福油");

        Console.WriteLine("  ---- 各角色 / 英雄 的武器幸运（判断祝福油是不是已经到顶）----");
        foreach (AccountInfo acc in envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;
                ShowWeapon(ch.Name + "（角色）", ch.Equipment);
                if (ch.Heroes == null) continue;
                foreach (HeroInfo h in ch.Heroes)
                {
                    if (h == null) continue;
                    ShowWeapon(ch.Name + "→英雄 " + h.Name, h.Equipment);
                }
            }
        }
        Console.WriteLine();
    }

    private static void ShowWeapon(string who, UserItem[] eq)
    {
        UserItem w = eq?[(int)EquipmentSlot.武器];
        if (w?.Info == null) { Console.WriteLine($"      {who}: 未装备武器"); return; }
        Console.WriteLine($"      {who}: {w.Info.Name} 幸运={w.AddedStats[Stat.幸运]} " +
                          $"Bind={w.Info.Bind} 持久={w.CurrentDura}/{w.MaxDura}" +
                          $"{(w.RentalInformation != null ? " ★租赁中" : "")}");
    }

    private static void DumpName(Envir envir, string target)
    {
        int n = 0;
        foreach (AccountInfo acc in envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;
                n += DumpNameIn(ch.Name + "（角色）", "背包", ch.Inventory, target);
                n += DumpNameIn(ch.Name + "（角色）", "装备", ch.Equipment, target);
                if (ch.Heroes == null) continue;
                foreach (HeroInfo h in ch.Heroes)
                {
                    if (h == null) continue;
                    n += DumpNameIn(ch.Name + "→英雄 " + h.Name, "背包", h.Inventory, target);
                    n += DumpNameIn(ch.Name + "→英雄 " + h.Name, "装备", h.Equipment, target);
                }
            }
        }
        if (n == 0) Console.WriteLine("      (无)");
    }

    private static int DumpNameIn(string who, string where, UserItem[] grid, string target)
    {
        if (grid == null) return 0;
        int hit = 0;
        for (int i = 0; i < grid.Length; i++)
        {
            UserItem it = grid[i];
            if (it?.Info == null || it.Info.Name != target) continue;
            hit++;
            Console.WriteLine($"      {who} [{where}#{i}] x{it.Count} 持久={it.CurrentDura}/{it.MaxDura} " +
                              $"定义Index={it.Info.Index} Type={it.Info.Type} Shape={it.Info.Shape} UniqueID={it.UniqueID}");
        }
        return hit;
    }

    private static int ScanGrid(string who, string where, UserItem[] grid, ref int zeroDura, ref int rings, ref int ringZero)
    {
        if (grid == null) return 0;
        int hit = 0;
        for (int i = 0; i < grid.Length; i++)
        {
            UserItem it = grid[i];
            if (it?.Info == null) continue;

            _scanned++;
            string name = it.Info.Name;
            if (name != null && (name.Contains('石') || name.Contains('油') || name.Contains("戒指") || name.Contains("祝福")))
            {
                Seen.TryGetValue(name, out int c);
                Seen[name] = c + 1;
            }

            bool match = false;
            foreach (string n in Names)
                if (name == n) { match = true; break; }
            if (!match) continue;

            hit++;
            bool isRing = name == "传送戒指";
            if (isRing) rings++;
            if (isRing && it.CurrentDura == 0) ringZero++;

            bool zero = it.CurrentDura == 0 && it.Info.Durability > 0;
            if (zero) zeroDura++;

            string luck = it.AddedStats[Stat.幸运] != 0 ? $" 幸运={it.AddedStats[Stat.幸运]}" : "";
            Console.WriteLine($"    {who} [{where}#{i}] {name} x{it.Count} 持久={it.CurrentDura}/{it.MaxDura}" +
                              $"（模板 {it.Info.Durability}）{(zero ? "  ★电量0仍在" : "")}{luck}");
        }
        return hit;
    }
}
