using System;
using System.Collections.Generic;
using System.Diagnostics;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

/// <summary>
/// 物品库热重载（ItemInfoReload.Sync）的离线验证。
/// 目标：证明「重载后存量物品立刻看到新数据」这件事真的成立，而不是又换了个对象。
/// 运行：dotnet ItemReloadTest.dll   退出码 0 = 全绿，1 = 有失败项。
/// </summary>
internal static class Program
{
    private static int _fail;

    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "   " + detail));
        if (!ok) _fail++;
    }

    private static ItemInfo MakeItem(int index, string name, int minDc, int maxDc, int price = 100)
    {
        ItemInfo info = new ItemInfo
        {
            Index = index,
            Name = name,
            Type = ItemType.武器,
            Grade = ItemGrade.普通,
            Shape = (short)index,
            Image = (ushort)index,
            Price = (uint)price,
            Durability = 10000,
            StackSize = 1,
        };

        info.Stats[Stat.MinDC] = minDc;
        info.Stats[Stat.MaxDC] = maxDc;
        info.RandomStats = new RandomItemStat(ItemType.武器);
        info.RandomStats.MaxDcChance = 7;

        return info;
    }

    /// <summary>模拟「DB 重新读入」：造一份字段相同但实例不同的副本。</summary>
    private static ItemInfo Clone(ItemInfo src)
    {
        ItemInfo copy = new ItemInfo();
        copy.CopyFrom(src);
        copy.Index = src.Index;
        return copy;
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0 && args[0] == "--db") return RunDbTest();

        Console.WriteLine("=== 物品库热重载 就地同步验证 ===\n");

        // ---------- 1. 基本场景：3 件存量物品，改 1 件、加 1 件、删 1 件 ----------
        ItemInfo sword = MakeItem(1, "木剑", 2, 5);
        ItemInfo potion = MakeItem(2, "金创药", 0, 0);
        ItemInfo oldRing = MakeItem(3, "旧戒指", 1, 1);

        List<ItemInfo> runtime = new List<ItemInfo> { sword, potion, oldRing };

        // 模拟运行期的 UserItem.Info：直接持有实例引用（这正是旧实现修不好的原因）
        ItemInfo heldByPlayer = sword;

        // DB 新读入：木剑属性被改强、金创药改价、新增一件、旧戒指被删
        ItemInfo srcSword = Clone(sword);
        srcSword.Name = "木剑·改";
        srcSword.Stats[Stat.MaxDC] = 30;
        srcSword.Price = 999;

        ItemInfo srcPotion = Clone(potion);
        srcPotion.Price = 250;

        ItemInfo srcNew = MakeItem(4, "新圣剑", 10, 40);

        List<ItemInfo> source = new List<ItemInfo> { srcSword, srcPotion, srcNew };

        List<ItemInfo> changed = new List<ItemInfo>();
        int added, removed, updated;

        ItemInfoReload.Sync(runtime, source, changed, out added, out removed, out updated);

        Check("计数正确（新增1/删除1/修改2）",
              added == 1 && removed == 1 && updated == 2,
              string.Format("add={0} rm={1} upd={2}", added, removed, updated));

        Check("玩家手里的实例没被换掉（引用不变）", ReferenceEquals(heldByPlayer, sword));

        Check("玩家手里的物品立刻看到新名称", heldByPlayer.Name == "木剑·改", "实际=" + heldByPlayer.Name);
        Check("玩家手里的物品立刻看到新属性 MaxDC=30", heldByPlayer.Stats[Stat.MaxDC] == 30,
              "实际=" + heldByPlayer.Stats[Stat.MaxDC]);
        Check("玩家手里的物品立刻看到新价格", heldByPlayer.Price == 999, "实际=" + heldByPlayer.Price);

        Check("运行期列表里仍是原实例（不是被替换）",
              runtime.Count == 3 && ReferenceEquals(runtime[0], sword));

        Check("变更清单命中改了的那两条（2 条）", changed.Count == 2,
              "实际=" + changed.Count);
        Check("变更清单包含玩家持有的实例", changed.Contains(heldByPlayer));

        ItemInfo runtimeNew = runtime[2];   // 新增的那件（列表末尾）
        Check("新增条目进运行期列表", runtimeNew != null && runtimeNew.Name == "新圣剑",
              runtimeNew == null ? "缺失" : runtimeNew.Name);
        Check("新增条目不与编辑器共享实例（改 DB 不隔空改线上）", !ReferenceEquals(runtimeNew, srcNew));

        // ---------- 2. 深拷贝：改源数据不能影响运行期 ----------
        srcSword.Stats[Stat.MaxDC] = 999;
        srcSword.Name = "在编辑器里又改了一次";
        srcSword.RandomStats.MaxDcChance = 99;

        Check("Stats 是深拷贝（改 DB 侧不影响线上）", heldByPlayer.Stats[Stat.MaxDC] == 30,
              "实际=" + heldByPlayer.Stats[Stat.MaxDC]);
        Check("名称是值拷贝（改 DB 侧不影响线上）", heldByPlayer.Name == "木剑·改",
              "实际=" + heldByPlayer.Name);
        Check("RandomStats 是深拷贝", heldByPlayer.RandomStats.MaxDcChance == 7,
              "实际=" + heldByPlayer.RandomStats.MaxDcChance);

        // ---------- 3. 幂等：内容没变（只是新实例）时不该产生"变更"，否则每次重载都全服重推 ----------
        ItemInfo idemItem = MakeItem(20, "幂等剑", 5, 9);
        List<ItemInfo> idemRuntime = new List<ItemInfo> { idemItem };
        List<ItemInfo> idemDb = new List<ItemInfo> { Clone(idemItem) };   // 同内容、不同实例

        List<ItemInfo> changed2 = new List<ItemInfo>();
        ItemInfoReload.Sync(idemRuntime, idemDb, changed2, out added, out removed, out updated);

        Check("同内容不同实例：不计修改（不会重复推包）",
              added == 0 && removed == 0 && updated == 0,
              string.Format("add={0} rm={1} upd={2}", added, removed, updated));
        Check("同内容不同实例：变更清单为空", changed2.Count == 0, "实际=" + changed2.Count);

        // 真改一个字段 → 必须报修改，且只在这一次报
        idemDb[0].Stats[Stat.MaxDC] = 88;
        ItemInfoReload.Sync(idemRuntime, idemDb, changed2, out added, out removed, out updated);
        Check("改一个字段：报 1 件修改", updated == 1 && changed2.Count == 1,
              string.Format("upd={0} changed={1}", updated, changed2.Count));
        Check("改一个字段：原地生效", idemItem.Stats[Stat.MaxDC] == 88,
              "实际=" + idemItem.Stats[Stat.MaxDC]);

        // ---------- 4. Index 身份字段不允许被覆盖 ----------
        ItemInfo a = MakeItem(10, "A", 1, 1);
        ItemInfo b = MakeItem(10, "B", 2, 2);
        b.Index = 77;                    // 恶意/异常数据
        bool changedFlag = a.CopyFrom(b);

        Check("CopyFrom 不改 Index", a.Index == 10, "实际=" + a.Index);
        Check("CopyFrom 正常返回有变化", changedFlag && a.Name == "B");

        // ---------- 5. 空值容错 ----------
        ItemInfo c = MakeItem(11, "C", 3, 4);
        bool okNull = true;
        try
        {
            c.CopyFrom(null);
            ItemInfoReload.Sync(null, null, null, out added, out removed, out updated);
            List<ItemInfo> rt = new List<ItemInfo> { null, c };
            ItemInfoReload.Sync(rt, new List<ItemInfo> { c }, null, out added, out removed, out updated);
        }
        catch (Exception ex) { okNull = false; Console.WriteLine("   异常：" + ex.Message); }

        Check("空列表/空条目不抛异常", okNull);

        // ---------- 6. 性能：整库规模（5000 条）计时 ----------
        List<ItemInfo> bigRuntime = new List<ItemInfo>();
        for (int i = 0; i < 5000; i++) bigRuntime.Add(MakeItem(1000 + i, "物品" + i, 1, 2));

        List<ItemInfo> bigSource = new List<ItemInfo>();
        for (int i = 0; i < 5000; i++)
        {
            ItemInfo copy = Clone(bigRuntime[i]);
            if (i % 100 == 0) copy.Stats[Stat.MaxDC] = 50;    // 每 100 件改 1 件
            bigSource.Add(copy);
        }

        Stopwatch sw = Stopwatch.StartNew();
        ItemInfoReload.Sync(bigRuntime, bigSource, new List<ItemInfo>(), out added, out removed, out updated);
        sw.Stop();

        Check("5000 条整库同步 < 500ms（修改 50 件）",
              sw.ElapsedMilliseconds < 500 && updated == 50,
              string.Format("{0}ms，upd={1}", sw.ElapsedMilliseconds, updated));

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过。" : string.Format("失败 {0} 项。", _fail));
        return _fail == 0 ? 0 : 1;
    }

    /// <summary>
    /// 真实库集成验证：在服务端目录运行（Server.MirDB 所在处），
    /// 用真库的 4000+ 条物品跑一遍「DB 重新读入 → 就地同步」，确认不炸、计数正确、
    /// 玩家手里那件物品的实例被就地改写（而不是被换成新对象）。
    /// 只读 + 内存操作，不写回数据库。
    /// </summary>
    private static int RunDbTest()
    {
        Console.WriteLine("=== 真实库集成验证（" + Envir.DatabasePath + "）===\n");

        if (!System.IO.File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine("当前目录找不到 " + Envir.DatabasePath + "，请在服务端目录运行。");
            return 2;
        }

        Settings.Load();

        Envir envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("LoadDB 失败");
            return 2;
        }

        List<ItemInfo> runtime = envir.ItemInfoList;
        Console.WriteLine("运行期物品条目：" + runtime.Count);

        if (runtime.Count == 0) { Console.WriteLine("物品库为空，无法验证。"); return 2; }

        // 模拟玩家背包/身上的一件物品：直接持有 ItemInfo 实例引用（旧的实现修不好的地方）
        ItemInfo held = runtime.Find(x => x != null && x.Type == ItemType.武器) ?? runtime[0];
        string oldName = held.Name;
        int oldMaxDc = held.Stats[Stat.MaxDC];
        int heldIndex = held.Index;

        // 模拟编辑器/DB 重新读入的整份列表（全新实例），并把 held 那件改掉
        List<ItemInfo> source = new List<ItemInfo>(runtime.Count);
        for (int i = 0; i < runtime.Count; i++)
        {
            ItemInfo origin = runtime[i];
            ItemInfo copy = new ItemInfo();
            copy.CopyFrom(origin);
            copy.Index = origin.Index;

            if (origin.Index == heldIndex)
            {
                copy.Name = oldName + "·热改";
                copy.Stats[Stat.MaxDC] = oldMaxDc + 5;
            }

            source.Add(copy);
        }

        Stopwatch sw = Stopwatch.StartNew();
        List<ItemInfo> changed = new List<ItemInfo>();
        int added, removed, updated;
        ItemInfoReload.Sync(runtime, source, changed, out added, out removed, out updated);
        sw.Stop();

        Check("真实库：条目数不变", runtime.Count == source.Count,
              string.Format("runtime={0} source={1}", runtime.Count, source.Count));
        Check("真实库：只报 1 件修改、无增删",
              updated == 1 && added == 0 && removed == 0 && changed.Count == 1,
              string.Format("add={0} rm={1} upd={2} changed={3}", added, removed, updated, changed.Count));
        Check("真实库：玩家持有实例未被替换", ReferenceEquals(runtime.Find(x => x.Index == heldIndex), held));
        Check("真实库：名称就地更新", held.Name == oldName + "·热改", "实际=" + held.Name);
        Check("真实库：属性就地更新", held.Stats[Stat.MaxDC] == oldMaxDc + 5,
              string.Format("{0} → {1}", oldMaxDc, held.Stats[Stat.MaxDC]));
        Check("真实库：变更清单指向玩家持有的实例", changed.Contains(held));
        Check("真实库：4000+ 条同步耗时 < 2000ms", sw.ElapsedMilliseconds < 2000,
              sw.ElapsedMilliseconds + "ms");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过。" : string.Format("失败 {0} 项。", _fail));
        return _fail == 0 ? 0 : 1;
    }
}
