using System.Drawing;
using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace ConsumeDiag;

/// <summary>
/// 只读诊断（10-27 三问排查）：
///   1. 回城石 / 随机石 / 气血石 持久用完之后是否真的从背包或宝石位消失
///   2. 英雄背包里的祝福油（卷轴 Shape=3）能不能给英雄武器加幸运（含幸运已满的情形）
///   3. 传送戒指装上后 SpecialMode 里有没有 Teleport，@传送 命令能不能真的走通
///
/// 只读：加载数据库、构造对象、调用真实业务方法，**不写库、不改配置、不发网络包**。
/// 用法：ConsumeDiag.exe "E:\...\Mir2Server"
/// </summary>
internal static class Program
{
    private static Envir _envir;
    private static Map _map;
    private static MapInfo _mapInfo;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                     ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));
        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        _envir = Envir.Main;
        if (!_envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        // 读账号存档前必须先把 BUFF 定义灌进去，否则读到带 BUFF 的角色会抛「尚未实施」
        try
        {
            _envir.BuffInfoList.Clear();
            foreach (BuffInfo bi in BuffInfo.Load()) _envir.BuffInfoList.Add(bi);
            Console.WriteLine($"BUFF 定义 {_envir.BuffInfoList.Count} 条已装载");
        }
        catch (Exception ex) { Console.WriteLine("[提示] BuffInfo.Load: " + ex.Message); }
        try { _envir.LoadAccounts(); } catch (Exception ex) { Console.WriteLine("[提示] LoadAccounts: " + ex.GetType().Name + ": " + ex.Message); }

        Console.WriteLine("===== 关键配置 =====");
        Console.WriteLine($"  气血石: 触发{Settings.StoneHPTriggerPercent}% 每跳回{Settings.StoneHPGainPercent}% 扣电{Settings.StoneHPDuraCost} 间隔{Settings.StoneHPDelay}s | 背包内也生效={Settings.StoneAllowBagUse}");
        Console.WriteLine($"  MaxLuck（普通祝福油幸运上限） = {Settings.MaxLuck}  |  TestServer = {Settings.TestServer}");
        Console.WriteLine();

        Console.WriteLine("===== 物品定义 =====");
        DumpItems("回城石", "随机石", "气血石", "魔血石", "幻血石", "祝福油", "精致祝福油", "超级祝福油", "传送戒指");
        Console.WriteLine();

        LoadOneMap();
        Console.WriteLine();

        DbScan.Run(_envir);

        TestTownStone();
        TestRandomStone();
        TestHpStone("气血石");
        TestHpStone("幻血石");
        TestHeroOil();
        TestTeleportRing();

        Console.WriteLine();
        Console.WriteLine("诊断结束（未修改任何文件）");
        return 0;
    }

    private static void LoadOneMap()
    {
        foreach (MapInfo mi in _envir.MapInfoList)
        {
            if (mi == null || mi.NoPosition) continue;
            try
            {
                var m = new Map(mi);
                if (!m.Load()) continue;
                _map = m; _mapInfo = mi;
                _envir.MapList.Add(m);
                Console.WriteLine($"已载入测试地图: [{mi.Index}] {mi.FileName} {m.Width}x{m.Height}");
                return;
            }
            catch { }
        }
        Console.WriteLine("!! 没有可用地图（传送类测试只能看提示语）");
    }

    private static Point FarWalkable(Point from)
    {
        if (_map == null) return from;
        for (int y = _map.Height - 2; y >= 0; y--)
            for (int x = _map.Width - 2; x >= 0; x--)
            {
                Cell c = _map.GetCell(x, y);
                if (c == null || !c.Valid) continue;
                if (Math.Abs(x - from.X) + Math.Abs(y - from.Y) < 50) continue;
                return new Point(x, y);
            }
        return from;
    }

    private static Point RandomWalkable()
    {
        if (_map == null) return new Point(100, 100);
        for (int y = 0; y < _map.Height; y++)
            for (int x = 0; x < _map.Width; x++)
            {
                Cell c = _map.GetCell(x, y);
                if (c != null && c.Valid) return new Point(x, y);
            }
        return new Point(_map.Width / 2, _map.Height / 2);
    }

    // ───────────────────────── 1a. 回城石 ─────────────────────────
    private static void TestTownStone()
    {
        Console.WriteLine("===== 1a. 回城石 [卷轴 Shape=20] 消耗与销毁 =====");
        ItemInfo info = Find("回城石");
        if (info == null) { Console.WriteLine("  !! 库里没有回城石"); return; }

        RunTownCase(info, 2000, "电量 2000（应扣到 1000，物品保留）");
        RunTownCase(info, 1000, "电量 1000（应扣到 0，物品消失）");
        RunTownCase(info, 0, "电量 0（观察是否只提示、物品留着）");
        Console.WriteLine();
    }

    private static void RunTownCase(ItemInfo info, ushort dura, string label)
    {
        RecConnection conn = NewConn(1);
        PlayerObject p = NewPlayer(conn);
        Point at = RandomWalkable();
        p.CurrentMap = _map;
        p.CurrentLocation = at;
        PrepCell(at);
        if (_mapInfo != null) p.BindMapIndex = _mapInfo.Index;

        UserItem item = Fresh(info, dura);
        p.Info.Inventory[0] = item;
        p.BindLocation = at;
        conn.Sent.Clear();

        string err = Run(() => p.UseItem(item.UniqueID));
        Console.WriteLine($"  · {label}");
        Console.WriteLine($"      电量 {dura} -> {item.CurrentDura}；背包格子已空 = {p.Info.Inventory[0] == null}；位置 {at} -> {p.CurrentLocation}");
        Console.WriteLine($"      发包: {Describe(conn.Sent)}{err}");
    }

    // ───────────────────────── 1b. 随机石 ─────────────────────────
    private static void TestRandomStone()
    {
        Console.WriteLine("===== 1b. 随机石 [卷轴 Shape=21] 消耗与销毁 =====");
        ItemInfo info = Find("随机石");
        if (info == null) { Console.WriteLine("  !! 库里没有随机石"); return; }

        foreach (ushort dura in new ushort[] { 2000, 1000, 0 })
        {
            RecConnection conn = NewConn(2);
            PlayerObject p = NewPlayer(conn);
            Point at = RandomWalkable();
            p.CurrentMap = _map;
            p.CurrentLocation = at;
            PrepCell(at);

            UserItem item = Fresh(info, dura);
            p.Info.Inventory[0] = item;
            conn.Sent.Clear();

            string err = Run(() => p.UseItem(item.UniqueID));
            Console.WriteLine($"  · 电量 {dura}: -> {item.CurrentDura}；背包格子已空 = {p.Info.Inventory[0] == null}；位置 {at} -> {p.CurrentLocation}");
            Console.WriteLine($"      发包: {Describe(conn.Sent)}{err}");
        }
        Console.WriteLine();
    }

    // ───────────────────────── 1c. 气血石 / 幻血石 ─────────────────────────
    private static void TestHpStone(string name)
    {
        Console.WriteLine($"===== 1c. {name} 电量耗尽是否从宝石位消失 =====");
        ItemInfo info = Find(name);
        if (info == null) { Console.WriteLine($"  !! 库里没有{name}"); return; }

        foreach (ushort dura in new ushort[] { 300, 100 })
        {
            RecConnection conn = NewConn(3);
            PlayerObject p = NewPlayer(conn);
            UserItem stone = Fresh(info, dura);
            p.Info.Equipment[(int)EquipmentSlot.守护石] = stone;
            p.Stats[Stat.HP] = 1000;
            p.HP = 400;
            conn.Sent.Clear();

            int heal = 0;
            string err = Run(() => heal = Stones(p));
            bool gone = p.Info.Equipment[(int)EquipmentSlot.守护石] == null;
            Console.WriteLine($"  · 本次电量 {dura}: 回血={heal} 剩余电量={stone.CurrentDura} 宝石位已空={gone}");
            Console.WriteLine($"      发包: {Describe(conn.Sent)}{err}");
        }
        Console.WriteLine();
    }

    // ───────────────────────── 2. 英雄祝福油 ─────────────────────────
    private static void TestHeroOil()
    {
        Console.WriteLine("===== 2. 英雄背包里的祝福油 [卷轴 Shape=3] =====");
        ItemInfo oil = Find("祝福油");
        if (oil == null) { Console.WriteLine("  !! 库里没有祝福油"); return; }

        ItemInfo weaponInfo = _envir.ItemInfoList.FirstOrDefault(x => x != null && x.Type == ItemType.武器
                                                                   && x.Durability > 0
                                                                   && !x.Bind.HasFlag(BindMode.DontUpgrade));
        if (weaponInfo == null) { Console.WriteLine("  !! 没有合适的武器物品"); return; }

        Console.WriteLine($"  普通祝福油幸运上限 Settings.MaxLuck = {Settings.MaxLuck}");

        foreach (int luck in new[] { 0, 3, Settings.MaxLuck })
            RunHeroOilCase(oil, weaponInfo, luck, $"英雄武器幸运 {luck}");

        RunPlayerOilCase(oil, weaponInfo, Settings.MaxLuck);

        // 复刻线上英雄武器的情形：武器带 DontUpgrade（不可升级）
        {
            BindMode saved = weaponInfo.Bind;
            weaponInfo.Bind |= BindMode.DontUpgrade;
            Console.WriteLine($"  ---- 复刻线上情形：武器 Bind 加 DontUpgrade（{weaponInfo.Name}）----");
            RunHeroOilCase(oil, weaponInfo, 0, "【不可升级武器】普通祝福油");
            ItemInfo sup = Find("超级祝福油");
            if (sup != null) RunHeroOilCase(sup, weaponInfo, 0, "【不可升级武器】超级祝福油");
            weaponInfo.Bind = saved;
        }

        // 超级祝福油（Shape=22，必成、上限 10）—— 线上真实存在的物品
        ItemInfo super = Find("超级祝福油");
        if (super == null) Console.WriteLine("  （库里没有超级祝福油，跳过）");
        else
        {
            Console.WriteLine($"  ---- 超级祝福油 [{super.Index}] Shape={super.Shape} 持久={super.Durability} ----");
            foreach (int luck in new[] { 0, 5, 9, 10 })
                RunHeroOilCase(super, weaponInfo, luck, $"【超级】英雄武器幸运 {luck}");
        }
        Console.WriteLine();
    }

    private static HeroObject NewHero(PlayerObject owner)
    {
        var hinfo = new HeroInfo(new ClientPackets.NewHero
        {
            Name = "诊断英雄",
            Class = MirClass.战士,
            Gender = MirGender.男性,
        });
        hinfo.Level = 45;
        hinfo.Equipment = new UserItem[14];
        hinfo.Magics = new List<UserMagic>();
        return new HeroObject(hinfo, owner);
    }

    private static void RunHeroOilCase(ItemInfo oil, ItemInfo weaponInfo, int luck, string label)
    {
        RecConnection conn = NewConn(4);
        PlayerObject owner = NewPlayer(conn);
        HeroObject hero;
        try { hero = NewHero(owner); }
        catch (Exception ex) { Console.WriteLine("  !! 构造英雄失败: " + ex.Message); return; }

        UserItem weapon = Fresh(weaponInfo, weaponInfo.Durability);
        weapon.AddedStats[Stat.幸运] = luck;
        hero.Info.Equipment[(int)EquipmentSlot.武器] = weapon;

        UserItem oilItem = Fresh(oil, oil.Durability);
        oilItem.Count = 1;
        hero.Info.Inventory[0] = oilItem;
        conn.Sent.Clear();

        string err = Run(() => hero.UseItem(oilItem.UniqueID));
        int left = hero.Info.Inventory[0] == null ? 0 : hero.Info.Inventory[0].Count;
        Console.WriteLine($"  · {label} → 幸运 {luck} => {weapon.AddedStats[Stat.幸运]}；油剩 {left}");
        Console.WriteLine($"      发包: {Describe(conn.Sent)}{err}");
    }

    private static void RunPlayerOilCase(ItemInfo oil, ItemInfo weaponInfo, int luck)
    {
        RecConnection conn = NewConn(15);
        PlayerObject p = NewPlayer(conn);
        UserItem weapon = Fresh(weaponInfo, weaponInfo.Durability);
        weapon.AddedStats[Stat.幸运] = luck;
        p.Info.Equipment[(int)EquipmentSlot.武器] = weapon;
        UserItem oilItem = Fresh(oil, oil.Durability);
        oilItem.Count = 1;
        p.Info.Inventory[0] = oilItem;
        conn.Sent.Clear();

        string err = Run(() => p.UseItem(oilItem.UniqueID));
        int left = p.Info.Inventory[0] == null ? 0 : p.Info.Inventory[0].Count;
        Console.WriteLine($"  · 对照：玩家自己喝（幸运 {luck}）→ {weapon.AddedStats[Stat.幸运]}；油剩 {left}");
        Console.WriteLine($"      发包: {Describe(conn.Sent)}{err}");
    }

    // ───────────────────────── 3. 传送戒指 ─────────────────────────
    private static void TestTeleportRing()
    {
        Console.WriteLine("===== 3. 传送戒指 [ItemInfo.Unique=Teleport] =====");
        ItemInfo ring = Find("传送戒指");
        if (ring == null) { Console.WriteLine("  !! 库里没有传送戒指"); return; }

        RecConnection conn = NewConn(6);
        PlayerObject p = NewPlayer(conn);
        UserItem r = Fresh(ring, ring.Durability);
        p.Info.Equipment[(int)EquipmentSlot.左戒指] = r;
        Run(() => p.RefreshStats());
        Console.WriteLine($"  持久 {r.CurrentDura}/{ring.Durability} → SpecialMode = {p.SpecialMode}（含 Teleport = {p.SpecialMode.HasFlag(SpecialItemMode.Teleport)}）");

        UserItem r0 = Fresh(ring, 0);
        p.Info.Equipment[(int)EquipmentSlot.左戒指] = r0;
        Run(() => p.RefreshStats());
        Console.WriteLine($"  持久 0 的戒指 → SpecialMode = {p.SpecialMode}（含 Teleport = {p.SpecialMode.HasFlag(SpecialItemMode.Teleport)}）");

        p.Info.Equipment[(int)EquipmentSlot.左戒指] = r;
        Run(() => p.RefreshStats());

        if (_map == null) { Console.WriteLine("  !! 没有可用地图，跳过命令测试"); return; }
        p.CurrentMap = _map;
        Point at = RandomWalkable();
        p.CurrentLocation = at;
        conn.Sent.Clear();

        Point good = FarWalkable(at);
        TpCase(61, ring, "A) 传到可站立格 " + good, good, false);
        TpCase(62, ring, "B) 传到不可站立格 (" + (_map.Width - 2) + "," + (_map.Height - 2) + ")", new Point(_map.Width - 2, _map.Height - 2), false);
        TpCase(63, ring, "C) 不带坐标（随机传送）", Point.Empty, true);
        Console.WriteLine();
    }

    private static void TpCase(int connId, ItemInfo ring, string label, Point target, bool noCoords)
    {
        RecConnection conn = NewConn(connId);
        PlayerObject p = NewPlayer(conn);
        UserItem r = Fresh(ring, ring.Durability);
        p.Info.Equipment[(int)EquipmentSlot.左戒指] = r;
        Run(() => p.RefreshStats());

        p.CurrentMap = _map;
        Point at = RandomWalkable();
        p.CurrentLocation = at;
        PrepCell(at);
        PrepCell(target);
        conn.Sent.Clear();

        string cmd = noCoords ? "@传送" : $"@传送 {target.X} {target.Y}";
        string err = Run(() => p.Chat(cmd));
        Console.WriteLine($"  {label}: {at} -> {p.CurrentLocation}{(p.CurrentLocation == at ? "  ★没动" : "  ✓已传送")}");
        Console.WriteLine($"      指令 [{cmd}] 发包: {Describe(conn.Sent)}{err}");
    }

    // ───────────────────────── 工具方法 ─────────────────────────
    /// <summary>把目标格子上的 Objects 列表准备好，避免 Teleport→Cell.Remove 的空引用（只是让测试能跑通）。</summary>
    private static void PrepCell(Point at)
    {
        if (_map == null) return;
        Cell c = _map.GetCell(at);
        if (c != null && c.Objects == null) c.Objects = new List<MapObject>();
    }

    private static string Run(Action a)
    {
        try { a(); return string.Empty; }
        catch (Exception ex) { return "\r\n      [异常] " + ex.ToString().Replace("\n", "\r\n      "); }
    }

    private static ItemInfo Find(string name) =>
        _envir.ItemInfoList.FirstOrDefault(x => x != null && x.Name == name);

    private static void DumpItems(params string[] names)
    {
        foreach (string n in names)
        {
            ItemInfo it = Find(n);
            if (it == null) { Console.WriteLine($"  [{n}] 不存在"); continue; }
            Console.WriteLine($"  [{it.Index,5}] {it.Name,-8} Type={it.Type}({(int)it.Type}) Shape={it.Shape} " +
                              $"持久={it.Durability} 叠加={it.StackSize} Unique={(short)it.Unique} " +
                              $"需求={it.RequiredType}{it.RequiredAmount} 性别={it.RequiredGender} 职业={it.RequiredClass} Bind={it.Bind}");
            var sb = new StringBuilder();
            for (int si = 0; si < 80; si++)
            {
                int v = 0;
                try { v = it.Stats[(Stat)si]; } catch { break; }
                if (v != 0) sb.Append($"{(Stat)si}={v} ");
            }
            if (sb.Length > 0) Console.WriteLine($"        Stats: {sb}");
        }
    }

    private static int _uid = 900000;

    private static UserItem Fresh(ItemInfo info, ushort dura) => new UserItem(info)
    {
        UniqueID = (ulong)(++_uid),
        Count = 1,
        MaxDura = info.Durability,
        CurrentDura = dura,
    };

    private static RecConnection NewConn(int id) =>
        new RecConnection(id) { Account = new AccountInfo { Index = id, AccountID = "diag_" + id } };

    private static PlayerObject NewPlayer(MirConnection conn)
    {
        var info = new CharacterInfo
        {
            Index = 1,
            Name = "诊断角色",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 45,
            HP = 400,
            MP = 100,
        };
        var p = new PlayerObject(info, conn);
        p.Stats[Stat.HP] = 1000;
        p.Stats[Stat.MP] = 200;
        p.HP = 400;
        p.MP = 100;
        return p;
    }

    private static int Stones(PlayerObject p)
    {
        MethodInfo m = typeof(HumanObject).GetMethod("ProcessStones", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo tf = typeof(Envir).GetField("<Time>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        long now = (long)tf.GetValue(_envir);
        tf.SetValue(_envir, now + (Settings.StoneHPDelay + 1) * 1000L);
        object[] args = { 0, 0 };
        m.Invoke(p, args);
        return (int)args[0];
    }

    private static string Describe(List<Packet> list)
    {
        if (list == null || list.Count == 0) return "(无)";
        var sb = new StringBuilder();
        foreach (Packet p in list)
        {
            switch (p)
            {
                case S.Chat c: sb.Append($"[说:\"{c.Message}\"] "); break;
                case S.DeleteItem d: sb.Append($"DeleteItem(id={d.UniqueID},n={d.Count}) "); break;
                case S.DuraChanged dc: sb.Append($"DuraChanged(id={dc.UniqueID},{dc.CurrentDura}) "); break;
                case S.UseItem u: sb.Append($"UseItem(id={u.UniqueID},grid={u.Grid},ok={u.Success}) "); break;
                case S.RefreshItem ri: sb.Append($"RefreshItem({ri.Item?.Info?.Name}) "); break;
                default: sb.Append(p.GetType().Name + " "); break;
            }
        }
        return sb.ToString().Trim();
    }
}

/// <summary>把服务端下发的包收进列表，方便诊断（不涉及网络）。</summary>
internal sealed class RecConnection : MirConnection
{
    public readonly List<Packet> Sent = new List<Packet>();
    public RecConnection(int sessionID) : base(sessionID, null) { Stage = GameStage.Game; }
    public override void Enqueue(Packet p) { lock (Sent) Sent.Add(p); }
    public override void Process() { }
    public void Clear() => Sent.Clear();
}
