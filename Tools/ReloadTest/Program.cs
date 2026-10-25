using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

// ============================================================
// 热重载内部测试：直接驱动 Envir.Main / Envir.Edit 单例，
// 模拟「编辑器改动 → 点重载」验证物品/怪物/技能/商城同步逻辑。
// 不 LoadDB、不依赖地图，纯内存构造数据。
// ============================================================

int pass = 0, fail = 0;

void Check(string name, bool cond)
{
    if (cond) { pass++; Console.WriteLine($"  [PASS] {name}"); }
    else { fail++; Console.WriteLine($"  [FAIL] {name}"); }
}

// ---------- 1) 物品库 ReloadItems ----------
Console.WriteLine("== 1) ReloadItems ==");
{
    Envir main = Envir.Main, edit = Envir.Edit;

    // Main 现有：物品 1,2,4（4 将被编辑器删除）；商城条目指向物品2的旧实例
    ItemInfo m1 = new ItemInfo { Index = 1, Name = "旧木剑", Price = 100 };
    ItemInfo m2 = new ItemInfo { Index = 2, Name = "旧铁剑", Price = 200 };
    ItemInfo m4 = new ItemInfo { Index = 4, Name = "将删除", Price = 400 };
    main.ItemInfoList.Clear();
    main.ItemInfoList.AddRange(new[] { m1, m2, m4 });

    main.GameShopList.Clear();
    GameShopItem gs = new GameShopItem { ItemIndex = 2, GIndex = 7, Info = m2, GoldPrice = 999 };
    main.GameShopList.Add(gs);

    // 编辑器：物品 1 改名，2 原样，3 新增（4 已删除）
    ItemInfo e1 = new ItemInfo { Index = 1, Name = "新木剑", Price = 150 };
    ItemInfo e2 = new ItemInfo { Index = 2, Name = "旧铁剑", Price = 200 };
    ItemInfo e3 = new ItemInfo { Index = 3, Name = "新 added", Price = 300 };
    edit.ItemInfoList.Clear();
    edit.ItemInfoList.AddRange(new[] { e1, e2, e3 });

    main.ReloadItems();

    Check("条目数 = 3 (1,2,3，4已删)", main.ItemInfoList.Count == 3
        && main.ItemInfoList.Any(x => x.Index == 1)
        && main.ItemInfoList.Any(x => x.Index == 2)
        && main.ItemInfoList.Any(x => x.Index == 3)
        && !main.ItemInfoList.Any(x => x.Index == 4));
    Check("物品1 已替换为编辑器实例", main.ItemInfoList.First(x => x.Index == 1).Name == "新木剑"
        && ReferenceEquals(main.ItemInfoList.First(x => x.Index == 1), e1));
    Check("物品3 新增自编辑器", ReferenceEquals(main.ItemInfoList.First(x => x.Index == 3), e3));
    Check("商城条目 Info 重绑到新物品实例", ReferenceEquals(gs.Info, e2));

    // 清场
    main.ItemInfoList.Clear(); main.GameShopList.Clear(); edit.ItemInfoList.Clear();
}

// ---------- 2) 怪物库 ReloadMonsters ----------
Console.WriteLine("== 2) ReloadMonsters ==");
{
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Envir", "Drops")); // DropInfo.Load 需要目录存在
    Envir main = Envir.Main, edit = Envir.Edit;

    MonsterInfo m1 = new MonsterInfo { Index = 1, Name = "测试怪1", Image = Monster.Guard, AI = 1 };
    MonsterInfo m2 = new MonsterInfo { Index = 2, Name = "测试怪2", Image = Monster.ForestYeti };
    main.MonsterInfoList.Clear();
    main.MonsterInfoList.AddRange(new[] { m1, m2 });

    MonsterInfo e1 = new MonsterInfo { Index = 1, Name = "测试怪1", Image = Monster.Guard, AI = 1, Level = 99 }; // 改属性
    MonsterInfo e2 = new MonsterInfo { Index = 2, Name = "测试怪2", Image = Monster.ForestYeti };
    MonsterInfo e3 = new MonsterInfo { Index = 3, Name = "测试怪3", Image = Monster.TaoistGuard, DropPath = "" };
    edit.MonsterInfoList.Clear();
    edit.MonsterInfoList.AddRange(new[] { e1, e2, e3 });

    main.ReloadMonsters();

    Check("条目数 = 3 (1,2,3)", main.MonsterInfoList.Count == 3
        && main.MonsterInfoList.All(x => new[] { 1, 2, 3 }.Contains(x.Index)));
    Check("怪物1 替换为编辑器实例(Level=99)", ReferenceEquals(main.MonsterInfoList.First(x => x.Index == 1), e1) && e1.Level == 99);
    Check("怪物3 新增自编辑器", ReferenceEquals(main.MonsterInfoList.First(x => x.Index == 3), e3));
    Check("新条目 Drops 容器已就绪(ReloadDrops 不炸)", main.MonsterInfoList[0].Drops != null && main.MonsterInfoList[2].Drops != null);
    Check("旧实例 m2 不再在列表中", !main.MonsterInfoList.Contains(m2));

    main.MonsterInfoList.Clear(); edit.MonsterInfoList.Clear();
}

// ---------- 3) 技能库 ReloadMagics ----------
Console.WriteLine("== 3) ReloadMagics ==");
{
    Envir main = Envir.Main, edit = Envir.Edit;

    MagicInfo m1 = new MagicInfo { Name = "火球", Spell = Spell.FireBall, PowerBase = 10 };
    MagicInfo m2 = new MagicInfo { Name = "冰咆哮旧", Spell = Spell.GreatFireBall, PowerBase = 20 };
    MagicInfo m3 = new MagicInfo { Name = "将删除", Spell = Spell.GreatFireBallRare };
    main.MagicInfoList.Clear();
    main.MagicInfoList.AddRange(new[] { m1, m2, m3 });

    MagicInfo e1 = new MagicInfo { Name = "火球", Spell = Spell.FireBall, PowerBase = 99 };  // 改威力
    MagicInfo e2 = new MagicInfo { Name = "冰咆哮旧", Spell = Spell.GreatFireBall, PowerBase = 20 };
    MagicInfo e4 = new MagicInfo { Name = "新技能", Spell = Spell.SoulFireBall };             // 新增
    edit.MagicInfoList.Clear();
    edit.MagicInfoList.AddRange(new[] { e1, e2, e4 });

    main.ReloadMagics();

    Check("条目数 = 3 (FireBall/GreatFireBall/SoulFireBall，Rare已删)", main.MagicInfoList.Count == 3
        && main.MagicInfoList.Any(x => x.Spell == Spell.SoulFireBall)
        && !main.MagicInfoList.Any(x => x.Spell == Spell.GreatFireBallRare));
    Check("火球 已替换为编辑器实例(PowerBase=99)", ReferenceEquals(main.MagicInfoList.First(x => x.Spell == Spell.FireBall), e1)
        && e1.PowerBase == 99);
    Check("旧实例 m2/m3 不再在列表中", !main.MagicInfoList.Contains(m2) && !main.MagicInfoList.Contains(m3));

    main.MagicInfoList.Clear(); edit.MagicInfoList.Clear();
}

// ---------- 4) 商城 ReloadGameShop ----------
Console.WriteLine("== 4) ReloadGameShop ==");
{
    Envir main = Envir.Main, edit = Envir.Edit;

    // Main 物品表：1,3
    ItemInfo i1 = new ItemInfo { Index = 1, Name = "金创药", Price = 50 };
    ItemInfo i3 = new ItemInfo { Index = 3, Name = "魔法药", Price = 60 };
    main.ItemInfoList.Clear();
    main.ItemInfoList.AddRange(new[] { i1, i3 });

    // 编辑器商城：条目1(改价,Info指向Edit本地占位)、条目9(物品不存在→unbound跳过)
    ItemInfo fakeEditInfo = new ItemInfo { Index = 1, Name = "编辑器里的实例" };
    GameShopItem g1 = new GameShopItem { ItemIndex = 1, GIndex = 11, Info = fakeEditInfo, GoldPrice = 555, Count = 3 };
    GameShopItem g9 = new GameShopItem { ItemIndex = 9, GIndex = 19, Info = null, GoldPrice = 1 };
    edit.GameShopList.Clear();
    edit.GameShopList.AddRange(new[] { g1, g9 });

    main.ReloadGameShop();

    Check("商城条目数 = 1 (物品9不存在被跳过)", main.GameShopList.Count == 1);
    GameShopItem synced = main.GameShopList.FirstOrDefault();
    Check("条目字段完整拷贝(ItemIndex/GIndex/GoldPrice/Count)", synced != null
        && synced.ItemIndex == 1 && synced.GIndex == 11 && synced.GoldPrice == 555 && synced.Count == 3);
    Check("Info 重绑到 Main 的物品实例(而非编辑器实例)", synced != null && ReferenceEquals(synced.Info, i1));

    main.ItemInfoList.Clear(); main.GameShopList.Clear(); edit.GameShopList.Clear();
}

Console.WriteLine();
Console.WriteLine($"========== 结果: {pass} PASS / {fail} FAIL ==========");
Environment.Exit(fail == 0 ? 0 : 1);
