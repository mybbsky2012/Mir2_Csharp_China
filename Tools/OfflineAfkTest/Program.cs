using System.Reflection;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;

namespace OfflineAfkTest;

/// <summary>
/// 离线挂机免损耗（10-26）只读验证：
///   规则：角色处于离线挂机（PlayerObject.OfflineAFK）时，
///         · 消耗类物品持久不扣：气血石/魔血石电量、照明物（火把/蜡烛）
///         · 装备持久不扣：被击不磨损、武器不磨损、坐骑忠诚不掉
///         · 回血/回蓝照常给（只是不耗电）
///         · 两个开关（Setup.ini [OfflineAFK] NoConsumableDura / NoEquipDura）可分别关掉
///         · 假人（FakePlayerObject）永远不享受免损耗
/// 用法：OfflineAfkTest.exe "E:\...\Mir2Server"，只读，不改数据库、不写配置。
/// </summary>
internal static class Program
{
    private static MethodInfo _processStones;
    private static MethodInfo _damageDura;
    private static FieldInfo _timeField;
    private static ItemInfo _stoneInfo;
    private static ItemInfo _armorInfo;
    private static ItemInfo _weaponInfo;
    private static int _fail;
    private static int _uid = 70000;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));

        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        _stoneInfo = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.气血石)
                  ?? envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.魔血石);
        _armorInfo = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.盔甲 && x.Durability > 1000);
        _weaponInfo = envir.ItemInfoList.FirstOrDefault(x => x.Type == ItemType.武器 && x.Durability > 1000);

        if (_stoneInfo == null) { Console.WriteLine("没有气血石/魔血石物品"); return 2; }
        if (_armorInfo == null) { Console.WriteLine("没有耐久 >1000 的盔甲（测试需要真实物品）"); return 2; }
        if (_weaponInfo == null) { Console.WriteLine("没有耐久 >1000 的武器"); return 2; }

        _processStones = typeof(HumanObject).GetMethod("ProcessStones", BindingFlags.NonPublic | BindingFlags.Instance);
        _damageDura = typeof(HumanObject).GetMethod("DamageDura", BindingFlags.NonPublic | BindingFlags.Instance);
        _timeField = typeof(Envir).GetField("<Time>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (_processStones == null || _damageDura == null || _timeField == null)
        {
            Console.WriteLine("!! 反射拿不到 ProcessStones / DamageDura / Envir.Time");
            return 2;
        }

        _timeField.SetValue(envir, 1_000_000L);

        // 固定守护石参数，期望值才可算
        Settings.StoneHPTriggerPercent = 50;
        Settings.StoneHPDelay = 4;
        Settings.StoneHPGainPercent = 1;      // 每跳回血 = 满持久 × 1%
        Settings.StoneHPDuraCost = 100;       // 每跳扣电 100
        Settings.StoneMPGainPercent = 0;
        Settings.StoneAllowBagUse = false;
        Settings.DuraLossRate = 100;          // 战斗磨损 100% 触发，避免随机掩盖问题

        short savedShape = _stoneInfo.Shape;
        _stoneInfo.Shape = 1;                 // 只回血，用例更干净

        Console.WriteLine($"[物品] 石头=[{_stoneInfo.Index}] {_stoneInfo.Name} 满持久{_stoneInfo.Durability} | 盔甲=[{_armorInfo.Index}] {_armorInfo.Name} | 武器=[{_weaponInfo.Index}] {_weaponInfo.Name}");
        Console.WriteLine();

        // ── 1/2/3：守护石电量 ──────────────────────────────
        Settings.OfflineAfkNoConsumableDura = true;
        Settings.OfflineAfkNoEquipDura = true;

        // 1) 在线（不挂机）：回血 + 扣电（回归基线）
        {
            var p = NewPlayer(false);
            UserItem stone = MakeStone(60000);
            p.Info.Equipment[(int)EquipmentSlot.守护石] = stone;
            p.Stats[Stat.HP] = 1000; p.HP = 400;
            int hp = Stones(p, out string err);
            Check("1) 在线：正常扣电", hp > 0 && stone.CurrentDura == 59900,
                  $"回血={hp} 电量={stone.CurrentDura}（期望 59900）{err}");
        }

        // 2) 离线挂机 + 开关开：回血但电量不动
        {
            var p = NewPlayer(true);
            UserItem stone = MakeStone(60000);
            p.Info.Equipment[(int)EquipmentSlot.守护石] = stone;
            p.Stats[Stat.HP] = 1000; p.HP = 400;
            int hp = Stones(p, out string err);
            Check("2) 离线挂机：回血但不扣电", hp > 0 && stone.CurrentDura == 60000,
                  $"回血={hp} 电量={stone.CurrentDura}（期望 60000）{err}");
        }

        // 3) 离线挂机但开关关：回到旧行为（扣电）
        {
            Settings.OfflineAfkNoConsumableDura = false;
            var p = NewPlayer(true);
            UserItem stone = MakeStone(60000);
            p.Info.Equipment[(int)EquipmentSlot.守护石] = stone;
            p.Stats[Stat.HP] = 1000; p.HP = 400;
            int hp = Stones(p, out string err);
            Check("3) 离线挂机 + 开关关：照旧扣电", hp > 0 && stone.CurrentDura == 59900,
                  $"回血={hp} 电量={stone.CurrentDura}（期望 59900）{err}");
            Settings.OfflineAfkNoConsumableDura = true;
        }

        // 4) 假人即使被标成离线挂机，也不免扣电
        {
            var bot = NewFake();
            bot.OfflineAFK = true;
            UserItem stone = MakeStone(60000);
            bot.Info.Equipment[(int)EquipmentSlot.守护石] = stone;
            bot.Stats[Stat.HP] = 1000; bot.HP = 400;

            bool flag = bot.AfkFreeConsumableDura;
            int hp = Stones(bot, out string err);
            Check("4) 假人豁免保护（假人不享受免扣）", !flag && stone.CurrentDura == 59900,
                  $"AfkFreeConsumableDura={flag}（期望 False）电量={stone.CurrentDura}（期望 59900）回血={hp}{err}");
        }

        // ── 5/6/7/8：装备磨损 ──────────────────────────────
        Settings.OfflineAfkNoEquipDura = true;

        // 5) 离线挂机：被击不磨损盔甲
        {
            var p = NewPlayer(true);
            UserItem armor = MakeArmor(5000);
            p.Info.Equipment[(int)EquipmentSlot.盔甲] = armor;
            DamageDura(p, out string err);
            Check("5) 离线挂机：被击不磨损装备", armor.CurrentDura == 5000,
                  $"盔甲持久={armor.CurrentDura}（期望 5000）{err}");
        }

        // 6) 离线挂机：武器不磨损
        {
            var p = NewPlayer(true);
            UserItem weapon = MakeWeapon(5000);
            p.Info.Equipment[(int)EquipmentSlot.武器] = weapon;
            p.DamageWeapon();
            Check("6) 离线挂机：武器不磨损", weapon.CurrentDura == 5000,
                  $"武器持久={weapon.CurrentDura}（期望 5000）");
        }

        // 7) 在线：被击照常磨损（回归基线）
        {
            Settings.OfflineAfkNoEquipDura = true;   // 开关开着，但在线不该受影响
            var p = NewPlayer(false);
            UserItem armor = MakeArmor(5000);
            p.Info.Equipment[(int)EquipmentSlot.盔甲] = armor;
            DamageDura(p, out string err);
            Check("7) 在线：照常磨损装备", armor.CurrentDura < 5000,
                  $"盔甲持久={armor.CurrentDura}（期望 <5000）{err}");
        }

        // 8) 离线挂机但开关关：照旧磨损
        {
            Settings.OfflineAfkNoEquipDura = false;
            var p = NewPlayer(true);
            UserItem armor = MakeArmor(5000);
            p.Info.Equipment[(int)EquipmentSlot.盔甲] = armor;
            DamageDura(p, out string err);
            Check("8) 离线挂机 + 开关关：照旧磨损", armor.CurrentDura < 5000,
                  $"盔甲持久={armor.CurrentDura}（期望 <5000）{err}");
            Settings.OfflineAfkNoEquipDura = true;
        }

        // 9) 属性口径：PlayerObject 挂机为 true，普通在线为 false
        {
            var online = NewPlayer(false);
            var afk = NewPlayer(true);
            Check("9) 免扣开关口径正确",
                  !online.AfkFreeConsumableDura && !online.AfkFreeEquipDura &&
                  afk.AfkFreeConsumableDura && afk.AfkFreeEquipDura,
                  $"在线=({online.AfkFreeConsumableDura},{online.AfkFreeEquipDura}) 挂机=({afk.AfkFreeConsumableDura},{afk.AfkFreeEquipDura})");
        }

        // 10) 离线挂机死亡不爆装：连续 200 次死亡结算，装备一件都不能少
        //     （保护失效时会真的走掉落逻辑，测试地图为空 → DropItem 抛异常，同样能被检出）
        {
            MapInfo mi = envir.MapInfoList.FirstOrDefault();
            if (mi == null)
            {
                Check("10) 离线挂机死亡不爆装", false, "数据库里没有地图配置，跳过（视为失败）");
            }
            else
            {
                mi.NoDropPlayer = false;

                var dropMethod = typeof(HumanObject).GetMethod("DeathDrop", BindingFlags.NonPublic | BindingFlags.Instance);
                int lost = 0, errs = 0;
                string firstErr = string.Empty;

                for (int n = 0; n < 200; n++)
                {
                    var p = NewPlayer(true);
                    p.CurrentMap = new Map(mi);

                    UserItem armor = MakeArmor(5000);
                    UserItem weapon = MakeWeapon(5000);
                    p.Info.Equipment[(int)EquipmentSlot.盔甲] = armor;
                    p.Info.Equipment[(int)EquipmentSlot.武器] = weapon;

                    try
                    {
                        dropMethod.Invoke(p, new object[] { null });
                    }
                    catch (TargetInvocationException ex)
                    {
                        errs++;
                        if (firstErr.Length == 0) firstErr = ex.InnerException?.Message ?? ex.Message;
                    }

                    if (p.Info.Equipment[(int)EquipmentSlot.盔甲] != armor ||
                        p.Info.Equipment[(int)EquipmentSlot.武器] != weapon) lost++;
                }

                Check("10) 离线挂机死亡不爆装", lost == 0 && errs == 0,
                      $"200 次死亡结算：掉装 {lost} 次、异常 {errs} 次" + (firstErr.Length > 0 ? "  首个异常：" + firstErr : ""));
            }
        }

        // 11) 对照实验（不靠随机）：把盔甲临时改成「死亡即消失」，
        //     在线应该被清空、挂机应该原封不动 —— 证明 10 的通过不是「方法根本没跑」
        {
            MapInfo mi = envir.MapInfoList.FirstOrDefault();
            var dropMethod = typeof(HumanObject).GetMethod("DeathDrop", BindingFlags.NonPublic | BindingFlags.Instance);
            BindMode savedBind = _armorInfo.Bind;

            if (mi == null || dropMethod == null)
            {
                Check("11) 对照组：在线会爆、挂机不爆", false, "缺地图配置或拿不到 DeathDrop");
            }
            else
            {
                _armorInfo.Bind = BindMode.BreakOnDeath;

                // 在线：应进入掉落逻辑，装备被清空
                var online = NewPlayer(false);
                online.CurrentMap = new Map(mi);
                UserItem armorOnline = MakeArmor(5000);
                online.Info.Equipment[(int)EquipmentSlot.盔甲] = armorOnline;
                try { dropMethod.Invoke(online, new object[] { null }); } catch (TargetInvocationException) { /* 空地图掉落会抛，不影响判据 */ }
                bool onlineCleared = online.Info.Equipment[(int)EquipmentSlot.盔甲] == null;

                // 挂机：应早退，装备原封不动
                var afk = NewPlayer(true);
                afk.CurrentMap = new Map(mi);
                UserItem armorAfk = MakeArmor(5000);
                afk.Info.Equipment[(int)EquipmentSlot.盔甲] = armorAfk;
                string err = string.Empty;
                try { dropMethod.Invoke(afk, new object[] { null }); }
                catch (TargetInvocationException ex) { err = "  [异常] " + (ex.InnerException?.Message ?? ex.Message); }
                bool afkIntact = afk.Info.Equipment[(int)EquipmentSlot.盔甲] == armorAfk;

                _armorInfo.Bind = savedBind;

                Check("11) 对照组：在线会爆、挂机不爆", onlineCleared && afkIntact,
                      $"在线装备被清空={onlineCleared}（期望 True）挂机装备保留={afkIntact}（期望 True）{err}");
            }

            _armorInfo.Bind = savedBind;
        }

        // ── 12) 总开关关闭：即使 OfflineAFK 被别的路径误设，免扣也一律不生效 ──
        {
            bool savedEnabled = Settings.OfflineAfkEnabled;
            bool savedCons = Settings.OfflineAfkNoConsumableDura;
            bool savedEquip = Settings.OfflineAfkNoEquipDura;

            Settings.OfflineAfkNoConsumableDura = true;
            Settings.OfflineAfkNoEquipDura = true;

            Settings.OfflineAfkEnabled = true;
            var probe = NewPlayer(true);
            bool onValue = probe.AfkFreeConsumableDura && probe.AfkFreeEquipDura;

            Settings.OfflineAfkEnabled = false;
            bool offValue = probe.AfkFreeConsumableDura || probe.AfkFreeEquipDura;

            Settings.OfflineAfkEnabled = savedEnabled;
            Settings.OfflineAfkNoConsumableDura = savedCons;
            Settings.OfflineAfkNoEquipDura = savedEquip;

            Check("12) 关掉「允许离线挂机」后，免扣一律不生效",
                  onValue && !offValue,
                  $"总开关开=({onValue} 期望 True) 总开关关=({offValue} 期望 False)");
        }

        _stoneInfo.Shape = savedShape;

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过 ✓" : $"失败 {_fail} 项 ✗");
        return _fail == 0 ? 0 : 1;
    }

    private static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + "  →  " + detail);
        if (!ok) _fail++;
    }

    /// <summary>推进时钟到下一跳后调 ProcessStones，返回本跳回血量</summary>
    private static int Stones(PlayerObject p, out string err)
    {
        long now = (long)_timeField.GetValue(Envir.Main);
        _timeField.SetValue(Envir.Main, now + (Settings.StoneHPDelay + 1) * 1000L);

        object[] args = { 0, 0 };
        err = string.Empty;
        try
        {
            _processStones.Invoke(p, args);
        }
        catch (TargetInvocationException ex)
        {
            err = "  [异常] " + (ex.InnerException?.Message ?? ex.Message);
        }
        return (int)args[0];
    }

    private static void DamageDura(PlayerObject p, out string err)
    {
        err = string.Empty;
        try
        {
            _damageDura.Invoke(p, null);
        }
        catch (TargetInvocationException ex)
        {
            err = "  [异常] " + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    private static UserItem MakeStone(ushort dura)
    {
        return new UserItem(_stoneInfo)
        {
            UniqueID = (ulong)(++_uid),
            Count = 1,
            MaxDura = _stoneInfo.Durability,
            CurrentDura = dura,
        };
    }

    private static UserItem MakeArmor(ushort dura)
    {
        return new UserItem(_armorInfo)
        {
            UniqueID = (ulong)(++_uid),
            Count = 1,
            MaxDura = _armorInfo.Durability,
            CurrentDura = dura,
        };
    }

    private static UserItem MakeWeapon(ushort dura)
    {
        return new UserItem(_weaponInfo)
        {
            UniqueID = (ulong)(++_uid),
            Count = 1,
            MaxDura = _weaponInfo.Durability,
            CurrentDura = dura,
        };
    }

    private static PlayerObject NewPlayer(bool offlineAfk)
    {
        CharacterInfo info = new CharacterInfo
        {
            Index = 1,
            Name = "挂机测试",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 45,
            HP = 400,
            MP = 100,
        };

        var conn = new BotConnection(1) { Account = new AccountInfo { Index = 1, AccountID = "afk_test" } };
        var p = new PlayerObject(info, conn);

        p.Stats[Stat.HP] = 1000;
        p.Stats[Stat.MP] = 200;
        p.HP = 400;
        p.MP = 100;
        p.OfflineAFK = offlineAfk;
        return p;
    }

    private static FakePlayerObject NewFake()
    {
        CharacterInfo info = new CharacterInfo
        {
            Index = 2,
            Name = "假人测试",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 45,
            HP = 400,
            MP = 100,
        };

        var conn = new BotConnection(2) { Account = new AccountInfo { Index = 2, AccountID = "afk_fake" } };
        var bot = new FakePlayerObject(info, conn);
        bot.Stats[Stat.HP] = 1000;
        bot.Stats[Stat.MP] = 200;
        bot.HP = 400;
        bot.MP = 100;
        return bot;
    }
}
