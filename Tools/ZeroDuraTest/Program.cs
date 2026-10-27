using System.Drawing;
using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace ZeroDuraTest;

/// <summary>
/// 10-27 三项修复的回归测试（1 提示 / 2 0持久清理 / 3 客户端英雄格子）：
///
///   A) 0 持久判定矩阵：守护石/回城石/随机石 → 安全；祝福油/修复油 → 只列不删；药水 → 根本不算残留
///   B) 登录自动清理：只删安全项、notify=false 不发包、幂等、开关能关、日志落盘
///   C) 英雄背包清理：DeleteItem 转发给主人 + 带「[英雄: xxx]」前缀
///   D) 祝福油失败提示：不可升级 / 幸运已满 / 没武器；英雄版提示发给主人
///   E) @传送 提示：没戒指 / 戒指在包里 / 戒指没电 / 坐标不可站立（且只锁 1 秒）/ 正常传送 / 随机传送
///
/// 只读：加载数据库、构造对象、调用真实业务方法，**不写库**。唯一写盘是 Logs\ZeroDuraClean.log
/// 用法：ZeroDuraTest.exe "E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server"
/// </summary>
internal static class Program
{
    private static Envir _envir;
    private static Map _map;
    private static MapInfo _mapInfo;
    private static int _pass, _fail;

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

        try
        {
            _envir.BuffInfoList.Clear();
            foreach (BuffInfo bi in BuffInfo.Load()) _envir.BuffInfoList.Add(bi);
        }
        catch (Exception ex) { Console.WriteLine("[提示] BuffInfo.Load: " + ex.Message); }
        try { _envir.LoadAccounts(); } catch (Exception ex) { Console.WriteLine("[提示] LoadAccounts: " + ex.GetType().Name + ": " + ex.Message); }

        LoadOneMap();

        TestMatrix();
        TestAutoClean();
        TestHeroClean();
        TestLuckMessages();
        TestTeleportHints();
        TestOilThenRing();
        TestSortBag();
        DumpRings();
        ScanDuplicateUids();
        TestRealCharacter();

        Console.WriteLine();
        Console.WriteLine($"===== 结果：{_pass} 通过 / {_fail} 失败 =====");
        return _fail == 0 ? 0 : 1;
    }

    // ───────────────────────── A. 判定矩阵 ─────────────────────────
    private static void TestMatrix()
    {
        Section("A. 0 持久判定矩阵");

        Check("幻血石 0 持久 = 安全可清", () => Expect(Find("幻血石"), 0, true, true));
        Check("气血石 0 持久 = 安全可清", () => Expect(Find("气血石"), 0, true, true));
        Check("回城石 0 持久 = 安全可清", () => Expect(Find("回城石"), 0, true, true));
        Check("随机石 0 持久 = 安全可清", () => Expect(Find("随机石"), 0, true, true));

        Check("超级祝福油 0 持久 = 只列不删", () => Expect(Find("超级祝福油"), 0, true, false));
        // 普通祝福油 / 修复油 定义持久就是 0（不吃持久），谈不上「残留」
        Check("祝福油（定义无持久）= 不算残留", () => Expect(Find("祝福油"), 0, false, false));
        Check("修复油（定义无持久）= 不算残留", () => Expect(Find("修复油"), 0, false, false));

        Check("满持久的气血石 = 不算残留", () => Expect(Find("气血石"), (ushort)60000, false, false));
        Check("药水（定义无持久）= 不算残留", () => Expect(AnyItem(ItemType.药水), 0, false, false));
    }

    private static void Expect(ItemInfo info, ushort currentDura, bool expectZeroDura, bool expectSafe)
    {
        if (info == null) throw new Exception("库里找不到该物品");
        UserItem item = Fresh(info, currentDura);
        bool zero = ZeroDuraCleaner.IsZeroDura(item);
        bool safe = ZeroDuraCleaner.IsSafeZeroDura(item);

        if (zero != expectZeroDura || safe != expectSafe)
            throw new Exception($"判定不符：IsZeroDura={zero}(期望 {expectZeroDura}) IsSafe={safe}(期望 {expectSafe}) " +
                                $"定义持久={info.Durability} 类型={info.Type} Shape={info.Shape}");
    }

    // ───────────────────────── B. 登录自动清理 ─────────────────────────
    private static void TestAutoClean()
    {
        Section("B. 登录自动清理（玩家背包）");

        RecConnection conn = NewConn(11);
        PlayerObject p = NewPlayer(conn);

        UserItem deadStone = PutBag(p, 0, Find("幻血石"), 0);      // 安全
        UserItem deadTown = PutBag(p, 1, Find("回城石"), 0);       // 安全
        UserItem deadSuperOil = PutBag(p, 2, Find("超级祝福油"), 0); // 不安全：油照样能用
        UserItem liveSuperOil = PutBag(p, 3, Find("超级祝福油"), 1);
        UserItem potion = PutBag(p, 4, AnyItem(ItemType.药水), 1);
        UserItem weapon = PutBag(p, 5, AnyWeapon(), 1);

        conn.Sent.Clear();
        Settings.CleanZeroDuraOnLogin = true;
        int removed = ZeroDuraCleaner.AutoClean(p, false, false);

        Check("自动清理删掉 2 件（幻血石+回城石）", () => Assert(removed == 2, "实际 " + removed));
        Check("幻血石格子已清空", () => Assert(p.Info.Inventory[0] == null, "仍然占位"));
        Check("回城石格子已清空", () => Assert(p.Info.Inventory[1] == null, "仍然占位"));
        Check("0 持久超级祝福油被保留", () => Assert(ReferenceEquals(p.Info.Inventory[2], deadSuperOil), "被误删"));
        Check("正常物品全部保留", () => Assert(ReferenceEquals(p.Info.Inventory[3], liveSuperOil) &&
                                             ReferenceEquals(p.Info.Inventory[4], potion) &&
                                             ReferenceEquals(p.Info.Inventory[5], weapon), "有正常物品被误删"));

        int delPackets = conn.Sent.OfType<S.DeleteItem>().Count();
        Check("登录清理不发 DeleteItem（背包快照在清理后下发）", () => Assert(delPackets == 0, "发了 " + delPackets + " 个"));

        string chats = Chats(conn.Sent);
        Check("给玩家一句「清理了 2 件」提示", () => Assert(chats.Contains("清理了 2 件"), "提示：" + chats));

        // 幂等
        conn.Sent.Clear();
        int again = ZeroDuraCleaner.AutoClean(p, false, false);
        Check("再跑一次不再删（幂等）", () => Assert(again == 0, "又删了 " + again));

        // 开关
        Settings.CleanZeroDuraOnLogin = false;
        PutBag(p, 6, Find("气血石"), 0);
        int off = ZeroDuraCleaner.AutoClean(p, false, false);
        Check("关掉开关后不自动清理", () => Assert(off == 0 && p.Info.Inventory[6] != null, "开关失效"));
        Settings.CleanZeroDuraOnLogin = true;

        // 日志
        string logPath = Path.Combine(".", "Logs", "ZeroDuraClean.log");
        Check("日志文件已写入且含角色名", () =>
            Assert(File.Exists(logPath) && File.ReadAllText(logPath).Contains("诊断角色"), "日志缺失"));

        _ = deadStone; _ = deadTown;
    }

    // ───────────────────────── C. 英雄背包 ─────────────────────────
    private static void TestHeroClean()
    {
        Section("C. 英雄背包清理");

        RecConnection conn = NewConn(12);
        PlayerObject owner = NewPlayer(conn);
        HeroObject hero = NewHero(owner);

        if (hero.Info.Inventory == null) { Console.WriteLine("  !! 英雄没有背包，跳过"); return; }

        hero.Info.Inventory[0] = Fresh(Find("气血石"), 0);
        hero.Info.Inventory[1] = Fresh(Find("超级祝福油"), 0);

        conn.Sent.Clear();
        int removed = ZeroDuraCleaner.AutoClean(hero, true, true);

        Check("英雄包里只删气血石 1 件", () => Assert(removed == 1, "实际 " + removed));
        Check("英雄包格子已清空", () => Assert(hero.Info.Inventory[0] == null, "仍然占位"));
        Check("英雄包里 0 持久祝福油保留", () => Assert(hero.Info.Inventory[1] != null, "被误删"));

        int delPackets = conn.Sent.OfType<S.DeleteItem>().Count();
        Check("DeleteItem 已转发给主人", () => Assert(delPackets == 1, "主人收到 " + delPackets + " 个"));

        string chats = Chats(conn.Sent);
        Check("提示带「[英雄: 」前缀", () => Assert(chats.Contains("[英雄: "), "提示：" + chats));
    }

    // ───────────────────────── D. 祝福油失败提示 ─────────────────────────
    private static void TestLuckMessages()
    {
        Section("D. 祝福油「用不了」的提示");

        // D1 武器不可升级
        ItemInfo wInfo = AnyWeapon();
        if (wInfo == null) { Console.WriteLine("  !! 库里没有可用武器，跳过"); return; }
        BindMode oldBind = wInfo.Bind;
        try
        {
            wInfo.Bind = BindMode.DontUpgrade;
            RecConnection conn = NewConn(21);
            PlayerObject p = NewPlayer(conn);
            UserItem weapon = Fresh(wInfo, wInfo.Durability);
            p.Info.Equipment[(int)EquipmentSlot.武器] = weapon;
            UserItem oil = PutBag(p, 0, Find("超级祝福油"), 1);
            conn.Sent.Clear();

            Run(() => p.UseItem(oil.UniqueID));
            string chats = Chats(conn.Sent);

            Check("DontUpgrade 武器：提示「不可升级」", () => Assert(chats.Contains("不可升级"), "提示：" + chats));
            Check("DontUpgrade 武器：油不被消耗", () => Assert(p.Info.Inventory[0] != null && p.Info.Inventory[0].Count == 1, "油被吃掉了"));
        }
        finally { wInfo.Bind = oldBind; }

        // D2 幸运已满
        {
            RecConnection conn = NewConn(22);
            PlayerObject p = NewPlayer(conn);
            UserItem weapon = Fresh(wInfo, wInfo.Durability);
            weapon.AddedStats[Stat.幸运] = 10;
            p.Info.Equipment[(int)EquipmentSlot.武器] = weapon;
            UserItem oil = PutBag(p, 0, Find("超级祝福油"), 1);
            conn.Sent.Clear();

            Run(() => p.UseItem(oil.UniqueID));
            string chats = Chats(conn.Sent);
            Check("幸运已满：提示「上限」", () => Assert(chats.Contains("上限"), "提示：" + chats));
            Check("幸运已满：油不被消耗", () => Assert(p.Info.Inventory[0] != null, "油被吃掉了"));
        }

        // D3 没装备武器
        {
            RecConnection conn = NewConn(23);
            PlayerObject p = NewPlayer(conn);
            UserItem oil = PutBag(p, 0, Find("超级祝福油"), 1);
            conn.Sent.Clear();

            Run(() => p.UseItem(oil.UniqueID));
            string chats = Chats(conn.Sent);
            Check("没武器：提示「没有装备武器」", () => Assert(chats.Contains("没有装备武器"), "提示：" + chats));
        }

        // D4 英雄武器不可升级 → 提示发给主人
        {
            wInfo.Bind = BindMode.DontUpgrade;
            try
            {
                RecConnection conn = NewConn(24);
                PlayerObject owner = NewPlayer(conn);
                HeroObject hero = NewHero(owner);
                UserItem weapon = Fresh(wInfo, wInfo.Durability);
                hero.Info.Equipment[(int)EquipmentSlot.武器] = weapon;
                hero.Info.Inventory[0] = Fresh(Find("超级祝福油"), 1);
                conn.Sent.Clear();

                Run(() => hero.UseItem(hero.Info.Inventory[0].UniqueID));
                string chats = Chats(conn.Sent);
                Check("英雄武器不可升级：主人收到「[英雄: ... 不可升级」",
                    () => Assert(chats.Contains("[英雄: ") && chats.Contains("不可升级"), "提示：" + chats));
                Check("英雄喝了无效：油不被消耗", () => Assert(hero.Info.Inventory[0] != null, "油被吃掉了"));
            }
            finally { wInfo.Bind = oldBind; }
        }
    }

    // ───────────────────────── E. 传送提示 ─────────────────────────
    private static void TestTeleportHints()
    {
        Section("E. @传送 的提示与冷却");

        if (_map == null) { Console.WriteLine("  !! 没有可用地图，跳过"); return; }

        ItemInfo ringInfo = Find("传送戒指");
        if (ringInfo == null) { Console.WriteLine("  !! 库里没有传送戒指，跳过"); return; }

        // E1 没戒指
        {
            RecConnection conn = NewConn(31);
            PlayerObject p = NewPlayer(conn);
            p.CurrentMap = _map; p.CurrentLocation = RandomWalkable();
            conn.Sent.Clear();
            Run(() => p.Chat("@传送"));
            string chats = Chats(conn.Sent);
            Check("没戒指：提示「没有传送戒指」", () => Assert(chats.Contains("没有传送戒指"), "提示：" + chats));
        }

        // E2 戒指在背包
        {
            RecConnection conn = NewConn(32);
            PlayerObject p = NewPlayer(conn);
            p.CurrentMap = _map; p.CurrentLocation = RandomWalkable();
            p.Info.Inventory[0] = Fresh(ringInfo, ringInfo.Durability);
            conn.Sent.Clear();
            Run(() => p.Chat("@传送"));
            string chats = Chats(conn.Sent);
            Check("戒指在背包：提示「请先装备」", () => Assert(chats.Contains("背包里") && chats.Contains("装备"), "提示：" + chats));
        }

        // E3 戒指没电
        {
            RecConnection conn = NewConn(33);
            PlayerObject p = NewPlayer(conn);
            p.CurrentMap = _map; p.CurrentLocation = RandomWalkable();
            p.Info.Equipment[(int)EquipmentSlot.左戒指] = Fresh(ringInfo, 0);
            Run(() => p.RefreshStats());
            conn.Sent.Clear();
            Run(() => p.Chat("@传送"));
            string chats = Chats(conn.Sent);
            Check("戒指 0 持久：提示「电量已耗尽」", () => Assert(chats.Contains("电量已耗尽"), "提示：" + chats));
        }

        // E4 坐标不可站立：有提示、人不走、只锁 1 秒
        {
            RecConnection conn = NewConn(34);
            PlayerObject p = NewPlayer(conn);
            p.CurrentMap = _map; p.CurrentLocation = RandomWalkable();
            p.Info.Equipment[(int)EquipmentSlot.左戒指] = Fresh(ringInfo, ringInfo.Durability);
            Run(() => p.RefreshStats());
            Point at = p.CurrentLocation;
            Point bad = new Point(_map.Width - 2, _map.Height - 2);
            // 若该格恰好可站立，换一个肯定越界的坐标
            if (_map.ValidPoint(bad)) bad = new Point(_map.Width + 50, _map.Height + 50);
            conn.Sent.Clear();

            Run(() => p.Chat($"@传送 {bad.X} {bad.Y}"));
            string chats = Chats(conn.Sent);
            Check("坐标不可站立：提示「传送失败」", () => Assert(chats.Contains("传送失败"), "提示：" + chats));
            Check("坐标不可站立：人没被移动", () => Assert(p.CurrentLocation == at, "位置变了"));

            long now = EnvirTime();
            long lockMs = p.LastTeleportTime - now;
            Check("坐标不可站立：只锁 ~1 秒（不吃 10 秒冷却）", () => Assert(lockMs > 0 && lockMs <= 1500, "锁了 " + lockMs + "ms"));
        }

        // E5 正常传送
        {
            RecConnection conn = NewConn(35);
            PlayerObject p = NewPlayer(conn);
            p.CurrentMap = _map;
            p.CurrentLocation = RandomWalkable();
            PrepCell(p.CurrentLocation);
            p.Info.Equipment[(int)EquipmentSlot.左戒指] = Fresh(ringInfo, ringInfo.Durability);
            Run(() => p.RefreshStats());
            Point target = FarWalkable(p.CurrentLocation);
            PrepCell(target);
            conn.Sent.Clear();

            Run(() => p.Chat($"@传送 {target.X} {target.Y}"));
            Check("正常坐标：真的传过去了", () => Assert(p.CurrentLocation == target,
                $"位置 {p.CurrentLocation}，目标 {target}，提示：{Chats(conn.Sent)}"));

            long lockMs = p.LastTeleportTime - EnvirTime();
            Check("正常传送：锁满 10 秒", () => Assert(lockMs > 9000, "只锁了 " + lockMs + "ms"));
        }

        // E6 随机传送
        {
            RecConnection conn = NewConn(36);
            PlayerObject p = NewPlayer(conn);
            p.CurrentMap = _map;
            p.CurrentLocation = RandomWalkable();
            PrepCell(p.CurrentLocation);
            p.Info.Equipment[(int)EquipmentSlot.左戒指] = Fresh(ringInfo, ringInfo.Durability);
            Run(() => p.RefreshStats());
            Point at = p.CurrentLocation;
            conn.Sent.Clear();

            Run(() => p.Chat("@传送"));
            Check("不带坐标：随机传送成功", () => Assert(p.CurrentLocation != at, "人没动：" + Chats(conn.Sent)));
        }
    }

    // ───────────────────────── F. 喝油之后传送戒指 ─────────────────────────
    private static void TestOilThenRing()
    {
        Section("F. 使用超级祝福油之后，传送戒指是否还有效");

        ItemInfo ringInfo = Find("传送戒指");
        ItemInfo oilInfo = Find("超级祝福油");
        ItemInfo wInfo = AnyWeapon();
        if (ringInfo == null || oilInfo == null || wInfo == null || _map == null)
        {
            Console.WriteLine("  !! 缺少传送戒指/超级祝福油/武器/地图，跳过");
            return;
        }

        RecConnection conn = NewConn(41);
        PlayerObject p = NewPlayer(conn);
        p.CurrentMap = _map;
        p.CurrentLocation = RandomWalkable();
        PrepCell(p.CurrentLocation);

        UserItem ring = Fresh(ringInfo, ringInfo.Durability);
        p.Info.Equipment[(int)EquipmentSlot.左戒指] = ring;
        p.Info.Equipment[(int)EquipmentSlot.武器] = Fresh(wInfo, wInfo.Durability);
        UserItem oil = PutBag(p, 0, oilInfo, 1);
        Run(() => p.RefreshStats());

        Check("喝油前：SpecialMode 含 Teleport", () => Assert(p.SpecialMode.HasFlag(SpecialItemMode.Teleport), "没戒指效果"));

        conn.Sent.Clear();
        Run(() => p.UseItem(oil.UniqueID));

        Check("喝油后：戒指还在原槽位", () => Assert(ReferenceEquals(p.Info.Equipment[(int)EquipmentSlot.左戒指], ring),
            "戒指被换掉了：" + (p.Info.Equipment[(int)EquipmentSlot.左戒指]?.Info?.Name ?? "null")));
        Check("喝油后：戒指持久没变", () => Assert(ring.CurrentDura == ringInfo.Durability,
            "持久变成 " + ring.CurrentDura));
        Check("喝油后：SpecialMode 仍含 Teleport", () => Assert(p.SpecialMode.HasFlag(SpecialItemMode.Teleport),
            "SpecialMode = " + p.SpecialMode));

        // 真的还能传送吗
        Point target = FarWalkable(p.CurrentLocation);
        PrepCell(target);
        conn.Sent.Clear();
        Run(() => p.Chat($"@传送 {target.X} {target.Y}"));
        Check("喝油后：@传送 仍然能走", () => Assert(p.CurrentLocation == target,
            $"位置 {p.CurrentLocation} 目标 {target}，提示：{Chats(conn.Sent)}"));

        // 英雄喝油：玩家的戒指不能被牵连
        {
            RecConnection c2 = NewConn(42);
            PlayerObject owner = NewPlayer(c2);
            owner.CurrentMap = _map;
            owner.CurrentLocation = RandomWalkable();
            PrepCell(owner.CurrentLocation);
            UserItem ring2 = Fresh(ringInfo, ringInfo.Durability);
            owner.Info.Equipment[(int)EquipmentSlot.左戒指] = ring2;
            Run(() => owner.RefreshStats());

            HeroObject hero = NewHero(owner);
            hero.Info.Equipment[(int)EquipmentSlot.武器] = Fresh(wInfo, wInfo.Durability);
            hero.Info.Inventory[0] = Fresh(oilInfo, 1);
            c2.Sent.Clear();
            Run(() => hero.UseItem(hero.Info.Inventory[0].UniqueID));

            Check("英雄喝油：玩家戒指没被顶掉", () => Assert(ReferenceEquals(owner.Info.Equipment[(int)EquipmentSlot.左戒指], ring2),
                "戒指变成 " + (owner.Info.Equipment[(int)EquipmentSlot.左戒指]?.Info?.Name ?? "null")));
            Check("英雄喝油：玩家 SpecialMode 仍含 Teleport",
                () => Assert(owner.SpecialMode.HasFlag(SpecialItemMode.Teleport), "SpecialMode = " + owner.SpecialMode));
        }

        // 用真实角色 + 真实武器（带 DontUpgrade 的那种）再走一遍：喝油失败后戒指必须还在
        foreach (AccountInfo acc in _envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;
                UserItem realRing = ch.Equipment?[(int)EquipmentSlot.左戒指];
                if (realRing?.Info == null || !realRing.Info.Unique.HasFlag(SpecialItemMode.Teleport)) continue;

                RecConnection c3 = NewConn(43);
                PlayerObject rp;
                try { rp = new PlayerObject(ch, c3); } catch { ch.Player = null; continue; }
                rp.CurrentMap = _map;
                rp.CurrentLocation = RandomWalkable();
                PrepCell(rp.CurrentLocation);
                Run(() => rp.RefreshStats());

                int slot = Array.FindIndex(rp.Info.Inventory, x => x == null);
                if (slot < 0) continue;
                rp.Info.Inventory[slot] = Fresh(oilInfo, 1);
                c3.Sent.Clear();
                Run(() => rp.UseItem(rp.Info.Inventory[slot].UniqueID));

                Check($"{ch.Name}（真实角色）喝油后戒指仍在原位",
                    () => Assert(ReferenceEquals(rp.Info.Equipment[(int)EquipmentSlot.左戒指], realRing), "戒指被换掉"));
                Check($"{ch.Name}（真实角色）喝油后 SpecialMode 仍含 Teleport",
                    () => Assert(rp.SpecialMode.HasFlag(SpecialItemMode.Teleport), "SpecialMode = " + rp.SpecialMode));

                Point t3 = FarWalkable(rp.CurrentLocation);
                PrepCell(t3);
                c3.Sent.Clear();
                Run(() => rp.Chat($"@传送 {t3.X} {t3.Y}"));
                Check($"{ch.Name}（真实角色）喝油后 @传送 仍然能走",
                    () => Assert(rp.CurrentLocation == t3, $"没走，提示：{Chats(c3.Sent)}"));

                ch.Player = null;   // 测试用：拆掉引用，免得后面再构造同一个角色时抛「Player.Info 不能为空」
                return;   // 查一个就够
            }
        }
    }

    // ───────────────────────── G. 整理背包 ─────────────────────────
    private static void TestSortBag()
    {
        Section("G. 整理背包：0 持久物品当场消失 + 刷新");

        RecConnection conn = NewConn(51);
        PlayerObject p = NewPlayer(conn);

        // 腰带（0~5）与背包区（6+）各放一件 0 持久，验证两处都清
        p.Info.Inventory[0] = Fresh(Find("超级祝福油"), 0);
        p.Info.Inventory[6] = Fresh(Find("回城石"), 0);
        p.Info.Inventory[7] = Fresh(Find("幻血石"), 0);
        UserItem keepA = PutBag(p, 8, AnyWeapon(), 1);
        UserItem keepB = PutBag(p, 9, AnyItem(ItemType.药水), 1);
        UserItem keepC = PutBag(p, 30, Find("气血石"), 60000);
        UserItem brokenWeapon = PutBag(p, 10, AnyWeapon(), 0);   // 坏掉的武器：只能修，绝不能删

        int before = p.Info.Inventory.Count(x => x != null);
        conn.Sent.Clear();
        Run(() => p.SortBag());

        Check("腰带里的 0 持久物品被清掉", () => Assert(p.Info.Inventory[0] == null, "还在"));
        Check("背包里的 0 持久物品被清掉（回城石/幻血石）",
            () => Assert(!p.Info.Inventory.Any(x => x != null && x.CurrentDura == 0 && x.Info.Durability > 0 &&
                                                     (x.Info.Type == ItemType.卷轴 || x.Info.Type == ItemType.守护石)),
                "还有残留"));

        int del = conn.Sent.OfType<S.DeleteItem>().Count();
        Check("删了 3 件并发了 3 个 DeleteItem", () => Assert(del == 3, "实际 " + del));

        Check("★ 坏掉的武器（0 持久装备）没有被删掉", () => Assert(p.Info.Inventory.Any(x => ReferenceEquals(x, brokenWeapon)),
            "装备被误删了"));

        Check("正常物品一件没少", () => Assert(
            p.Info.Inventory.Count(x => x != null) == before - 3 &&
            p.Info.Inventory.Any(x => ReferenceEquals(x, keepA)) &&
            p.Info.Inventory.Any(x => ReferenceEquals(x, keepB)) &&
            p.Info.Inventory.Any(x => ReferenceEquals(x, keepC)), "物品数量不对"));

        Check("背包区已压实（6 起连续无空格）", () =>
        {
            bool seenEmpty = false;
            for (int i = 6; i < p.Info.Inventory.Length; i++)
            {
                if (p.Info.Inventory[i] == null) seenEmpty = true;
                else if (seenEmpty) throw new Exception($"第 {i} 格前面有空格");
            }
        });

        int refresh = conn.Sent.OfType<S.RefreshItem>().Count();
        Check("整理后给每件物品补了 RefreshItem（客户端刷新）", () => Assert(refresh >= 3, "只发了 " + refresh + " 个"));
    }

    // ───────────────────────── H. 线上库里的传送戒指 ─────────────────────────
    private static void DumpRings()
    {
        Section("H. 线上库里每一枚传送戒指的实际状态（只读）");

        int n = 0;
        foreach (AccountInfo acc in _envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;
                n += DumpRingIn(ch.Name + "（角色）", "背包", ch.Inventory);
                n += DumpRingIn(ch.Name + "（角色）", "装备", ch.Equipment);
                if (ch.Heroes == null) continue;
                foreach (HeroInfo h in ch.Heroes)
                {
                    if (h == null) continue;
                    n += DumpRingIn(ch.Name + "→英雄 " + h.Name, "背包", h.Inventory);
                    n += DumpRingIn(ch.Name + "→英雄 " + h.Name, "装备", h.Equipment);
                }
            }
        }
        if (n == 0) Console.WriteLine("  (库里没有任何传送戒指)");
        _ = n;
    }

    private static int DumpRingIn(string who, string where, UserItem[] grid)
    {
        if (grid == null) return 0;
        int hit = 0;
        for (int i = 0; i < grid.Length; i++)
        {
            UserItem it = grid[i];
            if (it?.Info == null) continue;
            if (!it.Info.Unique.HasFlag(SpecialItemMode.Teleport)) continue;

            hit++;
            bool equipped = where == "装备" && (i == (int)EquipmentSlot.左戒指 || i == (int)EquipmentSlot.右戒指);
            string state = it.CurrentDura == 0 && it.Info.Durability > 0
                ? "★持久 0（失效）"
                : equipped ? "✓戴着（生效）" : "背包里（未戴，不生效）";
            Console.WriteLine($"  {who} [{where}#{i}] {it.Info.Name} 持久={it.CurrentDura}/{it.MaxDura} uid={it.UniqueID} → {state}");
        }
        return hit;
    }

    // ───────────────────────── J. UniqueID 冲突扫描 ─────────────────────────
    /// <summary>
    /// 客户端 RefreshItem 按 UniqueID 在「玩家背包→玩家装备→英雄背包→英雄装备」里找第一件替换，
    /// 所以同一个角色名下 UniqueID 撞车，就会把别的物品顶掉（比如英雄武器把玩家的戒指顶了）。
    /// </summary>
    private static void ScanDuplicateUids()
    {
        Section("J. 同一角色名下 UniqueID 是否撞车（撞了会让客户端的 RefreshItem 顶错物品）");

        int dupChars = 0, dupItems = 0;

        foreach (AccountInfo acc in _envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;

                var map = new Dictionary<ulong, string>();
                var dups = new List<string>();

                Collect(ch.Name + "·背包", ch.Inventory, map, dups);
                Collect(ch.Name + "·装备", ch.Equipment, map, dups);
                if (ch.Heroes != null)
                {
                    foreach (HeroInfo h in ch.Heroes)
                    {
                        if (h == null) continue;
                        Collect(ch.Name + "→英雄" + h.Name + "·背包", h.Inventory, map, dups);
                        Collect(ch.Name + "→英雄" + h.Name + "·装备", h.Equipment, map, dups);
                    }
                }

                if (dups.Count == 0) continue;
                dupChars++;
                dupItems += dups.Count;
                Console.WriteLine($"  ★ {ch.Name}：{dups.Count} 处撞车");
                foreach (string s in dups.Take(10)) Console.WriteLine("      " + s);
            }
        }

        Console.WriteLine(dupChars == 0 ? "  ✓ 没有撞车" : $"  合计 {dupChars} 个角色、{dupItems} 件物品撞车");
    }

    private static void Collect(string who, UserItem[] grid, Dictionary<ulong, string> map, List<string> dups)
    {
        if (grid == null) return;
        for (int i = 0; i < grid.Length; i++)
        {
            UserItem it = grid[i];
            if (it == null) continue;

            string key = $"{who}#{i} {(it.Info?.Name ?? "?")}";
            if (map.TryGetValue(it.UniqueID, out string other))
                dups.Add($"uid={it.UniqueID} 同时出现在 [{other}] 和 [{key}]");
            else
                map[it.UniqueID] = key;
        }
    }

    // ───────────────────────── I. 真实角色 ─────────────────────────
    /// <summary>拿线上库里的真实角色做一次 SpecialMode 检查：戴着传送戒指却 @传送 不了，到底卡在哪。</summary>
    private static void TestRealCharacter()
    {
        Section("I. 真实角色：戴着传送戒指时 SpecialMode / @传送 到底是什么状态");

        foreach (AccountInfo acc in _envir.AccountList)
        {
            if (acc?.Characters == null) continue;
            foreach (CharacterInfo ch in acc.Characters)
            {
                if (ch == null) continue;

                UserItem ring = ch.Equipment?[(int)EquipmentSlot.左戒指];
                UserItem ring2 = ch.Equipment?[(int)EquipmentSlot.右戒指];
                bool hasRing = (ring?.Info != null && ring.Info.Unique.HasFlag(SpecialItemMode.Teleport)) ||
                               (ring2?.Info != null && ring2.Info.Unique.HasFlag(SpecialItemMode.Teleport));
                if (!hasRing) continue;

                RecConnection conn = NewConn(90 + (int)(ch.Index % 100));
                PlayerObject p;
                try { p = new PlayerObject(ch, conn); }
                catch (Exception ex) { Console.WriteLine($"  {ch.Name}: 构造失败 {ex.GetType().Name}: {ex.Message}"); continue; }

                p.CurrentMap = _map;
                p.CurrentLocation = RandomWalkable();
                PrepCell(p.CurrentLocation);
                Run(() => p.RefreshStats());

                Console.WriteLine($"  {ch.Name}: SpecialMode = {p.SpecialMode}（Teleport={p.SpecialMode.HasFlag(SpecialItemMode.Teleport)}）");
                Console.WriteLine($"     左戒指={DescribeItem(ring)}  右戒指={DescribeItem(ring2)}");

                if (!SpecialMode(p)) continue;

                Point target = FarWalkable(p.CurrentLocation);
                PrepCell(target);
                conn.Sent.Clear();
                Run(() => p.Chat($"@传送 {target.X} {target.Y}"));
                Console.WriteLine($"     @传送 {target.X},{target.Y} → 位置 {p.CurrentLocation}{(p.CurrentLocation == target ? " ✓成功" : " ★没走")}；提示：{Chats(conn.Sent)}");
            }
        }
    }

    private static bool SpecialMode(PlayerObject p) => p.SpecialMode.HasFlag(SpecialItemMode.Teleport);

    private static string DescribeItem(UserItem it)
    {
        if (it?.Info == null) return "空";
        return $"{it.Info.Name} 持久={it.CurrentDura}/{it.MaxDura} Unique={(short)it.Info.Unique} 定义持久={it.Info.Durability}";
    }

    // ───────────────────────── 工具方法 ─────────────────────────
    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("===== " + title + " =====");
    }

    private static void Check(string name, Action body)
    {
        try
        {
            body();
            _pass++;
            Console.WriteLine("  ✓ " + name);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine("  ✗ " + name + " → " + ex.Message);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
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

    private static void PrepCell(Point at)
    {
        if (_map == null) return;
        Cell c = _map.GetCell(at);
        if (c != null && c.Objects == null) c.Objects = new List<MapObject>();
    }

    private static string Run(Action a)
    {
        try { a(); return string.Empty; }
        catch (Exception ex) { Console.WriteLine("      [异常] " + ex.Message); return ex.Message; }
    }

    private static ItemInfo Find(string name) =>
        _envir.ItemInfoList.FirstOrDefault(x => x != null && x.Name == name);

    private static ItemInfo AnyItem(ItemType type) =>
        _envir.ItemInfoList.FirstOrDefault(x => x != null && x.Type == type);

    /// <summary>随便找一把有持久的武器（用来试祝福油）。</summary>
    private static ItemInfo AnyWeapon() =>
        _envir.ItemInfoList.FirstOrDefault(x => x != null && x.Type == ItemType.武器 && x.Durability > 0);

    private static int _uid = 900000;

    private static UserItem Fresh(ItemInfo info, ushort dura)
    {
        if (info == null) throw new Exception("物品定义为空（库里找不到）");
        return new UserItem(info)
        {
            UniqueID = (ulong)(++_uid),
            Count = 1,
            MaxDura = info.Durability,
            CurrentDura = dura,
        };
    }

    /// <summary>往背包指定格放一件物品。</summary>
    private static UserItem PutBag(PlayerObject p, int slot, ItemInfo info, ushort currentDura)
    {
        UserItem item = Fresh(info, currentDura);
        p.Info.Inventory[slot] = item;
        return item;
    }

    private static long EnvirTime()
    {
        FieldInfo tf = typeof(Envir).GetField("<Time>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        return (long)tf.GetValue(_envir);
    }

    private static RecConnection NewConn(int id) =>
        new RecConnection(id) { Account = new AccountInfo { Index = id, AccountID = "zdt_" + id } };

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
        if (hinfo.Inventory == null) hinfo.Inventory = new UserItem[40];
        hinfo.Magics = new List<UserMagic>();
        return new HeroObject(hinfo, owner);
    }

    private static string Chats(List<Packet> list)
    {
        if (list == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (Packet p in list)
            if (p is S.Chat c) sb.Append(c.Message).Append(" | ");
        return sb.ToString().TrimEnd(' ', '|');
    }
}

/// <summary>把服务端下发的包收进列表，方便断言（不涉及网络）。</summary>
internal sealed class RecConnection : MirConnection
{
    public readonly List<Packet> Sent = new List<Packet>();
    public RecConnection(int sessionID) : base(sessionID, null) { Stage = GameStage.Game; }
    public override void Enqueue(Packet p) { lock (Sent) Sent.Add(p); }
    public override void Process() { }
    public void Clear() => Sent.Clear();
}
