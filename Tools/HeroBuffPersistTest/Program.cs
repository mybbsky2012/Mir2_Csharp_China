using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace HeroBuffPersistTest;

/// <summary>
/// 英雄 BUFF 存盘回归测试。
///
/// 覆盖四件事：
///   A 存档格式：HeroInfo.Save/Load 的 Buffs 段往返（含空表、多属性、带 Data、老档 110 兼容）
///   B 重放逻辑：SpawnHero 重放的是「英雄自己」的 BUFF，ReplayBuff 不再二次注入配置属性
///   C 端到端：拿线上库的副本 → 给某个英雄塞两条 BUFF → SaveAccounts → 新进程重新 LoadAccounts 校验
///   D 老档兼容：用改动前的 110 版库加载不崩、英雄 BUFF 为空、其它数据不错位
///
/// 用法（每个模式一个进程，Envir.Main 是单例）：
///   HeroBuffPersistTest.exe unit
///   HeroBuffPersistTest.exe prepare &lt;服务端目录&gt; &lt;临时目录&gt;
///   HeroBuffPersistTest.exe verify  &lt;临时目录&gt;
///   HeroBuffPersistTest.exe legacy  &lt;只读副本目录&gt;
/// </summary>
internal static class Program
{
    private const string SrcRoot = @"D:\mir2-20241027";

    private static int _ok, _fail;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string mode = args.Length > 0 ? args[0] : "unit";

        try
        {
            switch (mode)
            {
                case "prepare": return Prepare(args[1], args[2]);
                case "verify": return Verify(args[1]);
                case "resave": return Resave(args[1]);
                case "legacy": return Legacy(args[1]);
                default: return Unit();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("!! 异常：" + ex);
            return 3;
        }
    }

    // ══════════════════ 公共 ══════════════════

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("── " + title + " " + new string('─', Math.Max(0, 46 - title.Length * 2)));
    }

    private static bool Check(string name, bool pass, string detail = null)
    {
        if (pass) { _ok++; Console.WriteLine("  [OK]   " + name + (detail == null ? "" : "  (" + detail + ")")); }
        else { _fail++; Console.WriteLine("  [FAIL] " + name + (detail == null ? "" : "  (" + detail + ")")); }
        return pass;
    }

    private static void Summary()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 60));
        Console.WriteLine(_fail == 0
            ? $"全部通过：{_ok} 项"
            : $"有 {_fail} 项失败（通过 {_ok} 项）");
    }

    private static void FillBuffInfos(Envir envir)
    {
        envir.BuffInfoList.Clear();
        foreach (BuffInfo info in BuffInfo.Load())
            envir.BuffInfoList.Add(info);
    }

    /// <summary>把服务端跑起来所需的最小数据装进 Envir.Main（只读）。</summary>
    private static Envir Bootstrap()
    {
        Settings.Load();

        Envir envir = Envir.Main;
        if (!envir.LoadDB())
            throw new Exception("LoadDB 失败");

        FillBuffInfos(envir);
        envir.LoadAccounts();

        return envir;
    }

    // ══════════════════ A/B：存档格式（不碰文件） ══════════════════

    private static int Unit()
    {
        Console.WriteLine("英雄 BUFF 存盘回归测试 —— 内存格式 / 重放逻辑 / 源码门控");

        FillBuffInfos(Envir.Main);

        Section("A. 存档格式往返（HeroInfo.Save → HeroInfo.Load）");
        FormatTests();

        Section("B. 版本门控与常量");
        SourceTests();

        Section("C. 重放逻辑（IL）");
        IlTests();

        Summary();
        return _fail == 0 ? 0 : 1;
    }

    private static HeroInfo NewHero(string name = "测试英雄")
    {
        return new HeroInfo(new ClientPackets.NewHero
        {
            Name = name,
            Class = MirClass.战士,
            Gender = MirGender.男性
        });
    }

    private static Buff MakeBuff(BuffType type, int expire, params (Stat stat, int value)[] stats)
    {
        Buff b = new Buff(type)
        {
            ExpireTime = expire,
            Stats = new Stats(),
            Values = Array.Empty<int>()
        };

        foreach ((Stat stat, int value) in stats)
            b.Stats[stat] = value;

        return b;
    }

    private static void FormatTests()
    {
        // 1) 空表往返
        {
            HeroInfo hero = NewHero("空BUFF");
            (HeroInfo back, int left) = RoundTrip(hero, 111);

            Check("空 BUFF 表：往返后仍为 0 条", back.Buffs.Count == 0, "实际 " + back.Buffs.Count);
            Check("空 BUFF 表：字节流读干净（没有多读/少读）", left == 0, "剩余 " + left + " 字节");
        }

        // 2) 两条 BUFF（5 小时药剂 + 3 小时经验水）
        {
            HeroInfo hero = NewHero("两条BUFF");
            hero.Buffs.Add(MakeBuff(BuffType.攻击力提升, 18_000_000, (Stat.MaxDC, 7)));
            hero.Buffs.Add(MakeBuff(BuffType.获取经验提升, 10_800_000, (Stat.经验增长数率, 100)));

            (HeroInfo back, int left) = RoundTrip(hero, 111);

            Check("两条 BUFF：条数一致", back.Buffs.Count == 2, "实际 " + back.Buffs.Count);
            Check("两条 BUFF：类型一致",
                back.Buffs.Count == 2 && back.Buffs[0].Type == BuffType.攻击力提升 && back.Buffs[1].Type == BuffType.获取经验提升);

            if (back.Buffs.Count == 2)
            {
                Check("两条 BUFF：剩余时长一致（5 小时 = 18000000ms）",
                    back.Buffs[0].ExpireTime == 18_000_000, "实际 " + back.Buffs[0].ExpireTime);
                Check("两条 BUFF：属性一致（MaxDC=7）",
                    back.Buffs[0].Stats[Stat.MaxDC] == 7, "实际 " + back.Buffs[0].Stats[Stat.MaxDC]);
                Check("两条 BUFF：属性一致（经验增长数率=100）",
                    back.Buffs[1].Stats[Stat.经验增长数率] == 100, "实际 " + back.Buffs[1].Stats[Stat.经验增长数率]);
            }

            Check("两条 BUFF：字节流读干净", left == 0, "剩余 " + left + " 字节");
        }

        // 3) Values 数组 + Data 字典（脚本 GIVEBUFF 带值的情况）
        {
            HeroInfo hero = NewHero("带值BUFF");
            Buff b = MakeBuff(BuffType.准确命中提升, 3_600_000, (Stat.准确, 12));
            b.Values = new[] { 1, 2, 3 };
            b.Set("标记", 42);
            hero.Buffs.Add(b);

            (HeroInfo back, int left) = RoundTrip(hero, 111);

            Check("带值 BUFF：Values 数组往返", back.Buffs.Count == 1 && back.Buffs[0].Values.Length == 3 &&
                back.Buffs[0].Values[0] == 1 && back.Buffs[0].Values[2] == 3,
                back.Buffs.Count == 1 ? string.Join(",", back.Buffs[0].Values ?? Array.Empty<int>()) : "无");
            Check("带值 BUFF：Data 字典往返", back.Buffs.Count == 1 && back.Buffs[0].Get<int>("标记") == 42);
            Check("带值 BUFF：字节流读干净", left == 0, "剩余 " + left + " 字节");
        }

        // 4) Buffs 为 null（极端情况：老对象/被手动清空）不能炸
        {
            HeroInfo hero = NewHero("nullBUFF");
            hero.Buffs = null;

            bool ok;
            int left;
            try
            {
                (HeroInfo back, left) = RoundTrip(hero, 111);
                ok = back.Buffs.Count == 0;
            }
            catch (Exception ex)
            {
                ok = false;
                left = -1;
                Console.WriteLine("      · 异常：" + ex.GetType().Name + " " + ex.Message);
            }

            Check("Buffs=null：Save 不抛异常且落 0 条", ok);
            Check("Buffs=null：字节流读干净", left == 0, "剩余 " + left + " 字节");
        }

        // 5) 手工 new 出来的裸 Buff（Values/Stats 都是 null）：SaveAccounts 不能因此抛异常
        //    这类对象真实存在于「脚本/测试直接往 Buffs 里塞一条」的写法
        {
            HeroInfo hero = NewHero("裸BUFF");
            Buff raw = new Buff(BuffType.防御提升) { ExpireTime = 600_000 };
            raw.Values = null;
            raw.Stats = null;
            hero.Buffs.Add(raw);
            hero.Buffs.Add(null);                            // 列表里混进 null 也不能炸

            bool ok;
            HeroInfo back = null;
            int left = -1;
            try
            {
                (back, left) = RoundTrip(hero, 111);
                ok = true;
            }
            catch (Exception ex)
            {
                ok = false;
                Console.WriteLine("      · 异常：" + ex.GetType().Name + " " + ex.Message);
            }

            Check("裸 BUFF：Values/Stats 为 null 也不抛异常", ok);
            Check("裸 BUFF：条数按能写的算（null 条目被跳过、条数同步）", back != null && back.Buffs.Count == 1,
                back == null ? "null" : "实际 " + back.Buffs.Count);
            Check("裸 BUFF：字节流读干净", left == 0, "剩余 " + left + " 字节");
        }

        // 6) 老档（version 110，没有 BUFF 段）兼容：读完不越界、BUFF 为空
        {
            HeroInfo hero = NewHero("老档");
            hero.Level = 47;
            hero.HP = 1234;
            hero.MP = 567;
            hero.Experience = 987654321;

            byte[] newFormat = Save(hero, 111);
            byte[] legacy = new byte[newFormat.Length - 4];      // 砍掉末尾的「BUFF 条数(int)」
            Array.Copy(newFormat, legacy, legacy.Length);

            (HeroInfo back, int left) = Read(legacy, 110);

            Check("老档 110：能正常读出（不抛异常）", back != null);
            Check("老档 110：BUFF 为空", back != null && back.Buffs.Count == 0);
            Check("老档 110：等级/HP/MP/经验不错位",
                back != null && back.Level == 47 && back.HP == 1234 && back.MP == 567 && back.Experience == 987654321,
                back == null ? "null" : $"Lv{back.Level} HP{back.HP} MP{back.MP} Exp{back.Experience}");
            Check("老档 110：字节流读干净", left == 0, "剩余 " + left + " 字节");
        }

        // 6) 玩家角色的 BUFF 段不能被动到（端到端那段会拿真实角色核对条数）
        {
            string charInfo = Src("Server/MirDatabase/CharacterInfo.cs");
            Check("玩家角色存档仍保留 BUFF 段（Load 里 new Buff / Save 里 Buff.Save）",
                charInfo.Contains("new Buff(reader, version, customVersion)") &&
                System.Text.RegularExpressions.Regex.IsMatch(charInfo, @"Buffs\[i\]\.Save\(writer\)"));
        }
    }

    private static byte[] Save(HeroInfo hero, int version)
    {
        using MemoryStream ms = new MemoryStream();
        using (BinaryWriter w = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            w.Write(version);
            hero.Save(w);
        }
        return ms.ToArray();
    }

    private static (HeroInfo, int) Read(byte[] data, int version)
    {
        using MemoryStream ms = new MemoryStream(data);
        using BinaryReader r = new BinaryReader(ms);

        r.ReadInt32();                                   // 文件头版本
        HeroInfo hero = new HeroInfo(r, version, 0);
        return (hero, (int)(ms.Length - ms.Position));
    }

    private static (HeroInfo, int) RoundTrip(HeroInfo hero, int version)
    {
        return Read(Save(hero, version), version);
    }

    // ══════════════════ B：源码门控 ══════════════════

    private static void SourceTests()
    {
        Check("Envir.Version 已升到 111", Envir.Version == 111, "实际 " + Envir.Version);

        string heroInfo = Src("Server/MirDatabase/HeroInfo.cs");
        Check("HeroInfo.Load 有 version > 110 的 BUFF 段", heroInfo.Contains("if (version > 110)"));
        Check("HeroInfo.Save 写 BUFF 条数（Buffs?.Count ?? 0）", heroInfo.Contains("Buffs?.Count ?? 0"));
        Check("HeroInfo.Save 逐条调 Buff.Save",
            System.Text.RegularExpressions.Regex.IsMatch(heroInfo, @"buff\.Save\(writer\)"));

        string player = StripComments(Src("Server/MirObjects/PlayerObject.cs"));
        string spawnHero = ExtractMethod(player, "private void SpawnHero(HeroObject hero)");
        Check("SpawnHero 重放 hero.Buffs（不是 this.Buffs）",
            spawnHero.Contains("hero.Buffs") && !spawnHero.Contains("this.Buffs"),
            spawnHero.Length + " 字节");
        Check("SpawnHero 走 hero.ReplayBuff", spawnHero.Contains("hero.ReplayBuff"));
        Check("SpawnHero 里不再残留 player 的 AddBuff 调用", !spawnHero.Contains("AddBuff("));

        string startGame = ExtractMethod(player, "private void StartGameSuccess()");
        Check("StartGameSuccess 也改走 ReplayBuff", startGame.Contains("ReplayBuff("));

        string human = Src("Server/MirObjects/HumanObject.cs");
        Check("HumanObject.ReplayBuff 已存在", human.Contains("public void ReplayBuff(Buff buff)"));
    }

    // ══════════════════ C：IL ══════════════════

    private static void IlTests()
    {
        MethodInfo spawnHero = typeof(PlayerObject).GetMethod("SpawnHero", BindingFlags.NonPublic | BindingFlags.Instance);
        if (!Check("PlayerObject.SpawnHero 方法存在", spawnHero != null)) return;

        List<Ins> il = Parse(spawnHero);
        Check("SpawnHero：读取 Buffs（英雄自己的列表）", CallsMember(il, spawnHero, "get_Buffs"));
        Check("SpawnHero：调用 ReplayBuff", CallsMember(il, spawnHero, "ReplayBuff"));
        Check("SpawnHero：不再直接调 AddBuff", !CallsMember(il, spawnHero, "AddBuff"));

        MethodInfo replay = typeof(HumanObject).GetMethod("ReplayBuff", BindingFlags.Public | BindingFlags.Instance);
        if (Check("HumanObject.ReplayBuff 方法存在", replay != null))
        {
            List<Ins> ril = Parse(replay);
            Check("ReplayBuff：不调用 ApplyBuffConfigStats（否则重放会二次叠加配置属性）",
                !CallsMember(ril, replay, "ApplyBuffConfigStats"));
            Check("ReplayBuff：底层调 base.AddBuff", CallsMember(ril, replay, "AddBuff"));

            bool setObjectId = false, setLastTime = false;
            foreach (Ins ins in ril)
            {
                if (ins.Op != OpCodes.Stfld) continue;
                FieldInfo f = ResolveField(replay, ins.Operand);
                if (f == null) continue;
                if (f.Name == "ObjectID") setObjectId = true;
                if (f.Name == "LastTime") setLastTime = true;
            }

            Check("ReplayBuff：刷新 ObjectID（否则客户端认不到是哪个英雄的 BUFF）", setObjectId);
            Check("ReplayBuff：刷新 LastTime（剩余时长按重放那一刻重新起算）", setLastTime);
        }

        MethodInfo save = typeof(HeroInfo).GetMethod("Save", BindingFlags.Public | BindingFlags.Instance);
        if (save != null)
        {
            List<Ins> sil = Parse(save);
            Check("HeroInfo.Save：确实访问了 Buffs 字段", UsesField(sil, save, "Buffs"));
            Check("HeroInfo.Save：调用了 Buff.Save", CallsMember(sil, save, "Save"));
        }
    }

    // ══════════════════ 端到端（文件） ══════════════════

    private static int Prepare(string srcDir, string workDir)
    {
        Directory.CreateDirectory(workDir);

        foreach (string name in new[] { "Server.MirADB", "Server.MirDB" })
        {
            string from = Path.Combine(srcDir, name);
            if (!File.Exists(from))
            {
                Console.WriteLine("!! 找不到 " + from);
                return 2;
            }

            File.Copy(from, Path.Combine(workDir, name), true);
        }

        Directory.SetCurrentDirectory(workDir);

        Envir envir = Bootstrap();

        List<HeroInfo> heroes = envir.HeroList.Where(h => h != null).ToList();

        Console.WriteLine($"载入完成：账号 {envir.AccountList.Count} / 角色 {envir.CharacterList.Count} / 英雄 {heroes.Count}，LoadVersion={Envir.LoadVersion}");

        if (heroes.Count == 0)
        {
            Console.WriteLine("!! 库里没有英雄，无法测试");
            return 2;
        }

        // 找一个原本没有 BUFF 的英雄，避免和线上已有数据混淆
        HeroInfo target = heroes.FirstOrDefault(h => h.Buffs.Count == 0) ?? heroes[0];

        // 改前快照（用来对比「写回后其它数据有没有错位」）
        CharacterInfo sample = envir.CharacterList.FirstOrDefault(c => c != null && (c.Buffs?.Count ?? 0) > 0);

        List<string> snap = new List<string>
        {
            "version=" + Envir.LoadVersion,
            "accounts=" + envir.AccountList.Count,
            "chars=" + envir.CharacterList.Count,
            "heroes=" + heroes.Count,
            "target=" + target.Index,
            "targetName=" + target.Name,
            "targetLevel=" + target.Level,
            "targetHp=" + target.HP,
            "targetMp=" + target.MP,
            "targetExp=" + target.Experience,
            "targetInv=" + target.Inventory.Length,
            "targetMagics=" + target.Magics.Count,
            "targetEquip=" + target.Equipment.Count(e => e != null),
            "targetBuffsBefore=" + target.Buffs.Count,
            "otherHeroesWithBuffs=" + heroes.Count(h => h != target && h.Buffs.Count > 0),
            "playerBuffs=" + envir.CharacterList.Where(c => c != null).Sum(c => c.Buffs?.Count ?? 0),
            "playerSample=" + (sample == null
                ? "none"
                : sample.Index + "|" + sample.Buffs[0].Type + "|" + sample.Buffs[0].ExpireTime + "|" + sample.Buffs[0].Stats[Stat.MaxDC]),
        };

        // 塞两条典型药剂 BUFF：破坏强化水 5 小时 / 经验水 3 小时
        target.Buffs.Add(MakeBuff(BuffType.攻击力提升, 18_000_000, (Stat.MaxDC, 7)));
        target.Buffs.Add(MakeBuff(BuffType.获取经验提升, 10_800_000, (Stat.经验增长数率, 100)));

        Console.WriteLine($"注入：英雄 [{target.Index}] {target.Name} ← 攻击力提升(MaxDC=7,5h) + 获取经验提升(+100%,3h)");

        envir.SaveAccounts();

        File.WriteAllLines(Path.Combine(workDir, "snapshot.txt"), snap, Encoding.UTF8);

        Console.WriteLine("已写回（SaveAccounts）。");
        return 0;
    }

    private static int Verify(string workDir)
    {
        Directory.SetCurrentDirectory(workDir);

        string snapPath = Path.Combine(workDir, "snapshot.txt");
        if (!File.Exists(snapPath))
        {
            Console.WriteLine("!! 缺少 snapshot.txt，先跑 prepare");
            return 2;
        }

        Dictionary<string, string> snap = File.ReadAllLines(snapPath, Encoding.UTF8)
            .Where(l => l.Contains('='))
            .Select(l => l.Split('=', 2))
            .ToDictionary(a => a[0], a => a[1]);

        int fileVersion;
        using (FileStream fs = File.OpenRead(Envir.AccountPath))
        using (BinaryReader r = new BinaryReader(fs))
            fileVersion = r.ReadInt32();

        Envir envir = Bootstrap();

        List<HeroInfo> heroes = envir.HeroList.Where(h => h != null).ToList();
        int targetIndex = int.Parse(snap["target"]);
        HeroInfo target = heroes.FirstOrDefault(h => h.Index == targetIndex);

        Section("D. 落盘版本号");
        Check("账号库头版本已写成 111", fileVersion == 111, "实际 " + fileVersion);

        Section("E. 重新加载后英雄 BUFF 还在");
        if (!Check("找到目标英雄", target != null, "Index=" + targetIndex)) { Summary(); return 1; }

        Check("英雄 BUFF 条数 = 2", target.Buffs.Count == 2, "实际 " + target.Buffs.Count);

        Buff atk = target.Buffs.FirstOrDefault(b => b.Type == BuffType.攻击力提升);
        Buff exp = target.Buffs.FirstOrDefault(b => b.Type == BuffType.获取经验提升);

        Check("英雄 BUFF：攻击力提升 存在", atk != null);
        Check("英雄 BUFF：攻击力提升 剩余 5 小时", atk != null && atk.ExpireTime == 18_000_000,
            atk == null ? "无" : atk.ExpireTime + "ms");
        Check("英雄 BUFF：攻击力提升 MaxDC=7", atk != null && atk.Stats[Stat.MaxDC] == 7,
            atk == null ? "无" : atk.Stats[Stat.MaxDC].ToString());

        Check("英雄 BUFF：获取经验提升 存在", exp != null);
        Check("英雄 BUFF：获取经验提升 剩余 3 小时", exp != null && exp.ExpireTime == 10_800_000,
            exp == null ? "无" : exp.ExpireTime + "ms");
        Check("英雄 BUFF：获取经验提升 +100%", exp != null && exp.Stats[Stat.经验增长数率] == 100,
            exp == null ? "无" : exp.Stats[Stat.经验增长数率].ToString());

        Section("F. 写回没有把其它数据搞错位");
        Check("账号数一致", envir.AccountList.Count.ToString() == snap["accounts"],
            $"{snap["accounts"]} → {envir.AccountList.Count}");
        Check("角色数一致", envir.CharacterList.Count.ToString() == snap["chars"],
            $"{snap["chars"]} → {envir.CharacterList.Count}");
        Check("英雄数一致", heroes.Count.ToString() == snap["heroes"],
            $"{snap["heroes"]} → {heroes.Count}");
        Check("目标英雄 名字/等级/HP/MP/经验 一致",
            target.Name == snap["targetName"] && target.Level.ToString() == snap["targetLevel"] &&
            target.HP.ToString() == snap["targetHp"] && target.MP.ToString() == snap["targetMp"] &&
            target.Experience.ToString() == snap["targetExp"],
            $"{target.Name} Lv{target.Level} HP{target.HP} MP{target.MP} Exp{target.Experience}");
        Check("目标英雄 背包槽/技能数/装备数 一致",
            target.Inventory.Length.ToString() == snap["targetInv"] &&
            target.Magics.Count.ToString() == snap["targetMagics"] &&
            target.Equipment.Count(e => e != null).ToString() == snap["targetEquip"],
            $"背包{target.Inventory.Length} 技能{target.Magics.Count} 装备{target.Equipment.Count(e => e != null)}");
        Check("其它英雄没有被牵连（原本没有 BUFF 的仍为 0）",
            heroes.Count(h => h != target && h.Buffs.Count > 0).ToString() == snap["otherHeroesWithBuffs"],
            "实际 " + heroes.Count(h => h != target && h.Buffs.Count > 0));
        Check("玩家角色 BUFF 总数未变",
            envir.CharacterList.Where(c => c != null).Sum(c => c.Buffs?.Count ?? 0).ToString() == snap["playerBuffs"],
            snap["playerBuffs"] + " → " + envir.CharacterList.Where(c => c != null).Sum(c => c.Buffs?.Count ?? 0));

        // 抽一个带 BUFF 的玩家角色做细节比对（防止「英雄写多了把玩家那段挤歪」）
        if (snap["playerSample"] != "none")
        {
            string[] f = snap["playerSample"].Split('|');
            int idx = int.Parse(f[0]);
            CharacterInfo ch = envir.CharacterList.FirstOrDefault(c => c != null && c.Index == idx);

            string now = ch == null || ch.Buffs.Count == 0
                ? "none"
                : ch.Index + "|" + ch.Buffs[0].Type + "|" + ch.Buffs[0].ExpireTime + "|" + ch.Buffs[0].Stats[Stat.MaxDC];

            Check("玩家角色 BUFF 细节未变（类型/剩余时长/属性）", now == snap["playerSample"],
                snap["playerSample"] + " → " + now);
        }

        Console.WriteLine();
        Console.WriteLine($"目标英雄 [{target.Index}] {target.Name}：{target.Buffs.Count} 条 BUFF");
        foreach (Buff b in target.Buffs)
            Console.WriteLine($"   · {b.Type}  剩余={b.ExpireTime}ms  Stats={Describe(b)}");

        Summary();
        return _fail == 0 ? 0 : 1;
    }

    /// <summary>重复保存一次（不改任何数据）：验证多次 SaveAccounts 不会把 BUFF 写重或写丢。</summary>
    private static int Resave(string dir)
    {
        Directory.SetCurrentDirectory(dir);

        Envir envir = Bootstrap();

        int before = envir.HeroList.Where(h => h != null).Sum(h => h.Buffs.Count);
        envir.SaveAccounts();
        int after = envir.HeroList.Where(h => h != null).Sum(h => h.Buffs.Count);

        Section("H. 重复保存");
        Check("保存前后英雄 BUFF 总条数不变（不会越存越多）", before == after, before + " → " + after);
        Console.WriteLine($"   英雄 BUFF 合计 {after} 条");

        Summary();
        return _fail == 0 ? 0 : 1;
    }

    private static int Legacy(string dir)
    {
        Directory.SetCurrentDirectory(dir);

        Console.WriteLine("老档（改动前 110 版）兼容性检查：目录 " + dir);

        Envir envir = Bootstrap();

        List<HeroInfo> heroes = envir.HeroList.Where(h => h != null).ToList();

        Section("G. 老档能正常加载");
        Check("LoadVersion = 110（改动前写入的版本）", Envir.LoadVersion == 110, "实际 " + Envir.LoadVersion);
        Check("角色能读出来", envir.CharacterList.Count > 0, envir.CharacterList.Count + " 个");
        Check("英雄能读出来", heroes.Count > 0, heroes.Count + " 个");
        Check("老档英雄 BUFF 全为 0（历史数据本来就没存过）",
            heroes.All(h => h.Buffs.Count == 0),
            heroes.Sum(h => h.Buffs.Count) + " 条");

        bool dataOk = heroes.All(h => h.Equipment != null && h.Inventory != null && h.Magics != null);
        Check("老档英雄 背包/装备/技能 结构完整（没有因为多读一段而错位）", dataOk);

        Console.WriteLine();
        foreach (HeroInfo h in heroes.Take(5))
            Console.WriteLine($"   · [{h.Index}] {h.Name} Lv{h.Level} 背包{h.Inventory.Length} 装备{h.Equipment.Count(e => e != null)} 技能{h.Magics.Count} BUFF{h.Buffs.Count}");

        Summary();
        return _fail == 0 ? 0 : 1;
    }

    private static string Describe(Buff b)
    {
        if (b.Stats == null) return "null";
        return string.Join(",", b.Stats.Values.Select(kv => kv.Key + "=" + kv.Value));
    }

    private static string Src(string relative)
    {
        string p = Path.Combine(SrcRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(p) ? File.ReadAllText(p) : string.Empty;
    }

    /// <summary>去掉 // 注释（注释里常抄着旧代码，直接做文本断言会误判）。</summary>
    private static string StripComments(string source)
    {
        return System.Text.RegularExpressions.Regex.Replace(source, @"//[^\r\n]*", string.Empty);
    }

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return string.Empty;

        int depth = 0, i = source.IndexOf('{', start);
        if (i < 0) return string.Empty;

        for (int j = i; j < source.Length; j++)
        {
            if (source[j] == '{') depth++;
            else if (source[j] == '}')
            {
                depth--;
                if (depth == 0) return source.Substring(start, j - start + 1);
            }
        }

        return source.Substring(start);
    }

    // ───── IL 解析 ─────

    private readonly struct Ins
    {
        public readonly int Offset;
        public readonly OpCode Op;
        public readonly int Operand;

        public Ins(int offset, OpCode op, int operand) { Offset = offset; Op = op; Operand = operand; }
    }

    private static Dictionary<short, OpCode> _opcodes;

    private static Dictionary<short, OpCode> OpCodeMap()
    {
        if (_opcodes != null) return _opcodes;

        _opcodes = new Dictionary<short, OpCode>();
        foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is OpCode oc) _opcodes[oc.Value] = oc;

        return _opcodes;
    }

    private static List<Ins> Parse(MethodBase m)
    {
        List<Ins> list = new List<Ins>();
        byte[] il;
        try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch { return list; }
        if (il == null) return list;

        Dictionary<short, OpCode> map = OpCodeMap();
        int i = 0;

        while (i < il.Length)
        {
            int start = i;
            short code = il[i];

            if (code == 0xFE && i + 1 < il.Length)
            {
                code = (short)(0xFE00 | il[i + 1]);
                i += 2;
            }
            else i++;

            if (!map.TryGetValue(code, out OpCode op)) break;

            int operand = 0;
            int size = OperandSize(op.OperandType);

            if (op.OperandType == OperandType.InlineSwitch)
            {
                if (i + 4 > il.Length) break;
                int count = BitConverter.ToInt32(il, i);
                i += 4 + 4 * count;
            }
            else
            {
                if (i + size > il.Length) break;

                if (size == 1) operand = il[i];
                else if (size == 2) operand = BitConverter.ToUInt16(il, i);
                else if (size == 4) operand = BitConverter.ToInt32(il, i);

                i += size;
            }

            list.Add(new Ins(start, op, operand));
        }

        return list;
    }

    private static int OperandSize(OperandType t)
    {
        switch (t)
        {
            case OperandType.InlineNone: return 0;
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
            case OperandType.ShortInlineBrTarget: return 1;
            case OperandType.InlineVar: return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR: return 8;
            case OperandType.InlineSwitch: return 4;
            default: return 4;
        }
    }

    private static MethodBase ResolveMethod(MethodBase host, int token)
    {
        try { return host.Module.ResolveMethod(token); } catch { return null; }
    }

    private static FieldInfo ResolveField(MethodBase host, int token)
    {
        try { return host.Module.ResolveField(token); } catch { return null; }
    }

    /// <summary>方法里是否出现了某个成员调用（按名字匹配，名字唯一够用）。</summary>
    private static bool CallsMember(List<Ins> il, MethodBase host, string memberName)
    {
        foreach (Ins ins in il)
        {
            if (ins.Op != OpCodes.Call && ins.Op != OpCodes.Callvirt) continue;

            MethodBase target = ResolveMethod(host, ins.Operand);
            if (target == null) continue;
            if (target.Name == memberName) return true;
            if (target.Name == "get_" + memberName) return true;
        }

        return false;
    }

    private static bool UsesField(List<Ins> il, MethodBase host, string fieldName)
    {
        foreach (Ins ins in il)
        {
            if (ins.Op != OpCodes.Ldfld && ins.Op != OpCodes.Ldflda && ins.Op != OpCodes.Stfld) continue;

            FieldInfo f = ResolveField(host, ins.Operand);
            if (f != null && f.Name == fieldName) return true;
        }

        return false;
    }
}
