using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace RelationProbe;

/// <summary>
/// 社交关系 × 归档 的只读体检工具。
///
///   RelationProbe            盘点现网账号库：好友/夫妻/师徒里指向「已不在角色列表」的悬空引用
///   RelationProbe --corrupt  内存里验证「好友写少一条」是否真的把存档写错位（不落盘，绝对安全）
///
/// ★默认模式只读账号库，不写任何文件；--corrupt 只在内存流里做，也不写。
///   建议对服务端目录副本运行（需要 Server.MirADB / Server.MirDB / Configs）。
/// </summary>
internal static class Program
{
    private static Envir E;
    private static int _pass, _fail;

    private static void Check(bool ok, string name, string detail = null)
    {
        if (ok) { _pass++; Console.WriteLine($"  [PASS] {name}"); }
        else { _fail++; Console.WriteLine($"  [FAIL] {name}" + (detail == null ? "" : $"  -> {detail}")); }
    }

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.AccountPath) || !File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录缺 {Envir.AccountPath} 或 {Envir.DatabasePath}。");
            Console.WriteLine("★请把服务端目录的 Server.MirADB / Server.MirDB / Configs 拷一份出来再跑。");
            return 2;
        }

        Settings.Load();

        // 阻断加载期自动归档，我们要看的是「已经归档过的」和「关系是否悬空」
        Settings.ArchiveInactiveCharacterAfterMonths = 9999;
        Settings.ArchiveDeletedCharacterAfterMonths = 9999;

        E = Envir.Main;
        if (!E.LoadDB()) { Console.WriteLine("!! LoadDB 失败"); return 2; }

        E.BuffInfoList.Clear();
        foreach (var buff in BuffInfo.Load()) E.BuffInfoList.Add(buff);

        // --bak：先把账号库写成垃圾，验证 LoadAccounts 会不会自动回退到 .bak
        if (args.Any(a => a.Equals("--bak", StringComparison.OrdinalIgnoreCase)))
            return FallbackTest();

        E.LoadAccounts();

        Console.WriteLine("== 社交关系 × 归档 · 只读体检 ==");
        Console.WriteLine($"账号 {E.AccountList.Count} 个 / 角色 {E.CharacterList.Count} 个");

        if (args.Any(a => a.Equals("--corrupt", StringComparison.OrdinalIgnoreCase)))
            return CorruptTest();

        if (args.Any(a => a.Equals("--mail", StringComparison.OrdinalIgnoreCase)))
            return MailTest();

        if (args.Any(a => a.Equals("--heroes", StringComparison.OrdinalIgnoreCase)))
            return HeroSpellDump();

        return Scan();
    }

    // ─────────────────────────────────────────────── 英雄技能清单（只读）

    /// <summary>
    /// 列出每个英雄「主力输出档」的候选顺序，用来确认某个英雄到底学了哪些技能、
    /// 以及同档轮换会不会真的生效（前两名评分差距 &lt; 10% 才轮换）。
    /// 战士的单体输出是平砍，没有 Single 技能，所以看的是群攻（Aoe）这一档。
    /// </summary>
    private static int HeroSpellDump()
    {
        var total = 0;

        foreach (var account in E.AccountList)
        {
            foreach (var hero in account.Characters.SelectMany(c => c.Heroes ?? new HeroInfo[0]))
            {
                if (hero == null || hero.Magics == null) continue;

                string jobName;
                HeroSpellKind kind;
                bool warrior = hero.Class == MirClass.战士;

                switch (hero.Class)
                {
                    case MirClass.战士: jobName = "战士"; kind = HeroSpellKind.Aoe; break;
                    case MirClass.法师: jobName = "法师"; kind = HeroSpellKind.Single; break;
                    case MirClass.道士: jobName = "道士"; kind = HeroSpellKind.Single; break;
                    case MirClass.刺客: jobName = "刺客"; kind = HeroSpellKind.Single; break;
                    default: jobName = "弓手"; kind = HeroSpellKind.Single; break;
                }

                // 群攻要按「被 2 个贴身」的场景过滤；单体按目标 5 格
                int surround = kind == HeroSpellKind.Aoe ? 2 : 0;
                int distance = kind == HeroSpellKind.Aoe ? 2 : 5;

                List<UserMagic> ranked = HeroSpellBook.Rank(hero.Magics, kind, surround, distance,
                    b => false, s => false, Settings.HeroAutoSkill);

                total++;

                Console.WriteLine();
                Console.WriteLine($"【{jobName}英雄】{hero.Name}  Lv{hero.Level}  主人 {account.AccountID}");
                Console.WriteLine($"  {(kind == HeroSpellKind.Aoe ? "群攻" : "单体")}候选 : " +
                    (ranked.Count == 0 ? "（无）" : string.Join(" > ",
                        ranked.Select(x => $"{x.Info?.Name ?? x.Spell.ToString()}[S{HeroSpellBook.Score(x):0.00}]"))));

                if (ranked.Count >= 2)
                {
                    double first = HeroSpellBook.Score(ranked[0]);
                    double second = HeroSpellBook.Score(ranked[1]);
                    bool rotate = first > 0 && second / first >= HeroSpellBook.RotationBand
                                           && second / first <= 1 / HeroSpellBook.RotationBand;

                    var next = new List<UserMagic>(ranked);
                    HeroSpellBook.ApplyRotation(next, ranked[0].Spell);
                    bool moved = next[0].Spell != ranked[0].Spell;

                    Console.WriteLine($"  轮换判定 : 前两名差距 {(second / first):P1} → " +
                        (rotate && moved ? $"会轮换，放了 {ranked[0].Info?.Name} 之后下一发是 {next[0].Info?.Name}"
                                        : "不轮换（第二本和第一本不同档）"));
                }
                else
                {
                    Console.WriteLine($"  轮换判定 : 只学了一本{(kind == HeroSpellKind.Aoe ? "群攻" : "单体")}，轮换无从谈起");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"英雄共 {total} 个（战士看群攻档，其余看单体档）");

        return 0;
    }

    // ─────────────────────────────────────────────── 现网盘点

    private static int Scan()
    {
        var byIndex = new Dictionary<int, CharacterInfo>();
        foreach (var c in E.CharacterList) byIndex[c.Index] = c;

        // 归档目录里的角色名（用来判断「悬空的是不是被归档了」而不是真删了）
        var archived = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(Envir.ArchivePath))
        {
            foreach (var f in Directory.GetFiles(Envir.ArchivePath, "*.MirCA"))
            {
                var stem = Path.GetFileNameWithoutExtension(f);
                int u = stem.LastIndexOf('_');
                if (u > 0) stem = stem.Substring(0, u);
                u = stem.LastIndexOf('_');
                if (u > 0) stem = stem.Substring(0, u);
                archived.Add(stem);
            }
        }

        int danglingFriend = 0, danglingLover = 0, danglingMentor = 0;
        int pagesHit = 0;                 // 至少有一个悬空好友的角色（保存时会被写坏）
        var detail = new List<string>();

        foreach (var c in E.CharacterList)
        {
            bool hitSaveRisk = false;

            foreach (var f in c.Friends)
            {
                if (byIndex.ContainsKey(f.Index) || f.Index == 0) continue;
                danglingFriend++;
                hitSaveRisk = true;
                detail.Add($"  好友悬空  {c.Name}(idx {c.Index}) -> idx {f.Index}{ArchivedHint(archived, f.Index)}");
            }

            if (c.Married != 0 && !byIndex.ContainsKey(c.Married))
            {
                danglingLover++;
                detail.Add($"  配偶悬空  {c.Name}(idx {c.Index}) -> idx {c.Married}{ArchivedHint(archived, c.Married)}");
            }

            if (c.Mentor != 0 && !byIndex.ContainsKey(c.Mentor))
            {
                danglingMentor++;
                detail.Add($"  师徒悬空  {c.Name}(idx {c.Index}) -> idx {c.Mentor}  IsMentor={c.IsMentor}");
            }

            if (hitSaveRisk) pagesHit++;
        }

        Console.WriteLine();
        Console.WriteLine("── 悬空引用统计 ──");
        Console.WriteLine($"  好友条目悬空      : {danglingFriend}");
        Console.WriteLine($"  配偶指向已不存在  : {danglingLover}");
        Console.WriteLine($"  师徒指向已不存在  : {danglingMentor}");
        Console.WriteLine($"  ★保存会写出错位档的角色 : {pagesHit} / {E.CharacterList.Count}");

        if (detail.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("── 明细（前 40 条）──");
            foreach (var d in detail.Take(40)) Console.WriteLine(d);
            if (detail.Count > 40) Console.WriteLine($"  ... 其余 {detail.Count - 40} 条");
        }

        // 归档角色是否还被别人引用
        Console.WriteLine();
        Console.WriteLine("── 归档角色被引用情况 ──");
        foreach (var name in archived)
        {
            var refs = new List<string>();
            foreach (var c in E.CharacterList)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var f in c.Friends)
                    if (byIndex.TryGetValue(f.Index, out var t) && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                        refs.Add($"{c.Name} 的好友");

                if (byIndex.TryGetValue(c.Married, out var l) && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                    refs.Add($"{c.Name} 的配偶");

                if (byIndex.TryGetValue(c.Mentor, out var m) && string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                    refs.Add($"{c.Name} 的{(c.IsMentor ? "徒弟" : "师父")}");
            }
            Console.WriteLine($"  {name,-14} 被引用 {refs.Count} 处" + (refs.Count > 0 ? "  <- " + string.Join("、", refs) : ""));
        }
        if (archived.Count == 0) Console.WriteLine("  （Archive 目录为空）");

        Console.WriteLine();
        Console.WriteLine(danglingFriend + danglingLover + danglingMentor == 0
            ? "== 结论：现网没有悬空引用，暂时安全 =="
            : "== 结论：发现悬空引用，见上 ==");
        return 0;
    }

    private static string ArchivedHint(HashSet<string> archived, int index)
    {
        // Index 已经查不到人了，只能提示「可能被归档」
        return archived.Count > 0 ? "（指向的角色已不在列表，可能已归档/删除）" : "";
    }

    // ─────────────────────────────────────────────── 邮件投递实验

    private static int MailTest()
    {
        Console.WriteLine();
        Console.WriteLine("== 邮件 × 归档投递实验（全内存）==");

        var target = E.CharacterList.FirstOrDefault(c => c.Mail.Count == 0) ?? E.CharacterList.FirstOrDefault();
        if (target == null) { Console.WriteLine("!! 没有角色可用于实验"); return 2; }
        Console.WriteLine($"  收件人：{target.Name}（ID {target.Index}，现有邮件 {target.Mail.Count} 封）");

        // 1) 收件人已归档（Index 查不到人）
        var lost = new MailInfo(99999999) { Sender = "测试", Message = "寄给归档角色", Gold = 100 };
        bool threw = false, sent = true;
        try { sent = lost.Send(); }
        catch (Exception ex) { threw = true; Console.WriteLine($"    抛出：{ex.GetType().Name}: {ex.Message}"); }

        Check(!threw && !sent, "★收件人已归档时，Send() 返回 false，不再抛 NullReferenceException",
            threw ? "还是抛了空引用" : "居然返回 true");

        // 2) 正常收件人照常投递
        int before = target.Mail.Count;
        var ok = new MailInfo(target.Index) { Sender = "测试", Message = "正常邮件" };
        bool okSent = ok.Send();

        Check(okSent && target.Mail.Count == before + 1, "正常收件人照常投递",
            $"sent={okSent} count {before}->{target.Mail.Count}");

        // 3) 已发送的邮件再次 Send 不应重复进箱
        ok.Send();
        Check(target.Mail.Count == before + 1, "已发送的邮件再 Send 不会重复进收件箱",
            $"count={target.Mail.Count}");

        // 4) Sender / Message 为 null 也要能存（否则 ReadString 抛 IOException，整库读不完）
        var nulls = new MailInfo(target.Index) { Sender = null, Message = null };
        var back = RoundTripMail(nulls, out var err);
        Check(back != null && back.Sender == string.Empty && back.Message == string.Empty,
            "Sender/Message 为 null 时存档不再写 -1（读回空串）", err ?? $"Sender={back?.Sender ?? "<null>"}");

        // 还原，别影响后面的盘点
        target.Mail.Remove(ok);

        Console.WriteLine();
        Console.WriteLine($"== 结果：{_pass} 通过 / {_fail} 失败 ==");
        return _fail > 0 ? 1 : 0;
    }

    private static MailInfo RoundTripMail(MailInfo src, out string err)
    {
        err = null;
        try
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
                src.Save(w);

            ms.Position = 0;
            using var r = new BinaryReader(ms);
            return new MailInfo(r, Envir.Version, Envir.CustomVersion);
        }
        catch (Exception ex)
        {
            err = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    // ─────────────────────────────────────────────── 坏档回退实验

    private static int FallbackTest()
    {
        string bak = Envir.AccountPath + ".bak";

        Console.WriteLine();
        Console.WriteLine("== 坏档回退实验 ==");

        if (!File.Exists(bak))
        {
            Console.WriteLine($"!! 当前目录没有 {Path.GetFileName(bak)}。先 cp Server.MirADB Server.MirADB.bak 再跑。");
            return 2;
        }

        string original = Envir.AccountPath + ".original";
        File.Copy(Envir.AccountPath, original, true);

        try
        {
            // 12 字节 = 前 3 个 Int32，第 4 个 ReadInt32 必然 EndOfStream
            File.WriteAllBytes(Envir.AccountPath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
            Console.WriteLine($"  已把 {Path.GetFileName(Envir.AccountPath)} 写成 12 字节垃圾，开始加载…");

            E.LoadAccounts();

            Check(E.AccountList.Count > 0 && E.CharacterList.Count > 0,
                "★账号库是垃圾时，自动回退到 .bak 并成功读入",
                $"账号 {E.AccountList.Count} / 角色 {E.CharacterList.Count}");

            var dir = Path.GetDirectoryName(Path.GetFullPath(Envir.AccountPath));
            var prefix = Path.GetFileName(Envir.AccountPath) + ".corrupt-";
            var corrupt = Directory.GetFiles(dir, prefix + "*");
            Check(corrupt.Length > 0, "坏档已另存为 .corrupt-时间戳（留证据）",
                corrupt.Length == 0 ? "没找到备份文件" : string.Join(", ", corrupt.Select(Path.GetFileName)));
        }
        catch (Exception ex)
        {
            Check(false, "★账号库是垃圾时，自动回退到 .bak 并成功读入", ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            try { File.Copy(original, Envir.AccountPath, true); File.Delete(original); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine($"== 结果：{_pass} 通过 / {_fail} 失败 ==");
        return _fail > 0 ? 1 : 0;
    }

    // ─────────────────────────────────────────────── 内存错位实验

    private static int CorruptTest()
    {
        Console.WriteLine();
        Console.WriteLine("== 存档错位实验（全内存，不落盘）==");

        var src = E.CharacterList.FirstOrDefault(c => c.Friends.Count > 0) ?? E.CharacterList.FirstOrDefault();
        if (src == null) { Console.WriteLine("!! 没有角色可用于实验"); return 2; }
        Console.WriteLine($"  取材角色：{src.Name} Lv{src.Level} 好友{src.Friends.Count} 配偶{src.Married} 师徒{src.Mentor}");

        // 1) 对照组：干净角色，往返必须一致
        var clean = Clone(src, 900001, "对照_干净");
        clean.Friends.Clear();
        var back1 = RoundTrip(clean, out var err1);
        Check(back1 != null && back1.Name == clean.Name && back1.Level == clean.Level && back1.Married == clean.Married,
            "对照组：好友列表干净时，往返完全一致", err1 ?? "字段不一致");

        // 2) 实验组：好友列表里塞一条指向不存在角色的记录（＝好友被归档）
        var dirty = Clone(src, 900002, "实验_悬空好友");
        dirty.Friends.Clear();
        dirty.Friends.Add(new FriendInfo(src, false) { Index = 999999 });
        dirty.Married = 123456;              // 用来验证错位后这个字段会不会读成别的值
        dirty.Mentor = 0;
        var back2 = RoundTrip(dirty, out var err2);

        bool aligned = back2 != null
                       && back2.Name == dirty.Name
                       && back2.Level == dirty.Level
                       && back2.Married == dirty.Married
                       && back2.Friends.Count == dirty.Friends.Count
                       && back2.Friends[0].Index == 999999;

        Check(aligned, "★修复后：好友悬空也能完整往返，条目原样保留（等 @RESTOREPLAYER 自动接回）",
            err2 ?? "字段错位或好友条目丢失");

        if (back2 != null)
        {
            Console.WriteLine($"    写: Name={dirty.Name} Lv{dirty.Level} Married={dirty.Married} Friends={dirty.Friends.Count}");
            Console.WriteLine($"    读: Name={back2.Name} Lv{back2.Level} Married={back2.Married} Friends={back2.Friends.Count}");
        }
        else
        {
            Console.WriteLine($"    读取直接抛异常：{err2}");
        }

        // 3) 传染实验：同一账号里第 1 个角色坏，第 2 个角色会不会跟着坏
        var a = Clone(src, 900003, "传染_A坏");
        a.Friends.Clear();
        a.Friends.Add(new FriendInfo(src, false) { Index = 999999 });
        var b = Clone(src, 900004, "传染_B好");
        b.Friends.Clear();
        b.Married = 777;

        byte[] blob;
        using (var ms = new MemoryStream())
        {
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(2);
                w.Write(Envir.Version); w.Write(Envir.CustomVersion); a.Save(w);
                w.Write(Envir.Version); w.Write(Envir.CustomVersion); b.Save(w);
            }
            blob = ms.ToArray();
        }

        CharacterInfo ra = null, rb = null;
        string cErr = null;
        try
        {
            using var ms = new MemoryStream(blob);
            using var r = new BinaryReader(ms);
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int ver = r.ReadInt32(), cver = r.ReadInt32();
                var c = new CharacterInfo(r, ver, cver);
                if (i == 0) ra = c; else rb = c;
            }
        }
        catch (Exception ex) { cErr = ex.GetType().Name + ": " + ex.Message; }

        Check(ra != null && ra.Name == "传染_A坏" && rb != null && rb.Name == "传染_B好" && rb.Married == 777,
            "★不再传染：A 有悬空好友，同账号的 B 照常读回",
            cErr ?? $"A={ra?.Name} B={rb?.Name} B.Married={rb?.Married}");

        // 4) 边界：好友全部悬空（数量必须归零，不能多算）
        var allGone = Clone(src, 900005, "边界_全悬空");
        allGone.Friends.Clear();
        for (int i = 0; i < 5; i++) allGone.Friends.Add(new FriendInfo(src, false) { Index = 880000 + i });
        var back3 = RoundTrip(allGone, out var err3);
        Check(back3 != null && back3.Friends.Count == 5 && back3.Friends.All(f => f.Index >= 880000) && back3.Level == allGone.Level,
            "边界：5 条好友全部悬空时，数量与内容都不丢", err3 ?? $"读到 {back3?.Friends.Count} 条");

        Console.WriteLine();
        Console.WriteLine($"== 结果：{_pass} 通过 / {_fail} 失败 ==");
        return _fail > 0 ? 1 : 0;
    }

    private static CharacterInfo RoundTrip(CharacterInfo src, out string err)
    {
        err = null;
        try
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
            {
                w.Write(Envir.Version);
                w.Write(Envir.CustomVersion);
                src.Save(w);
            }

            ms.Position = 0;
            using var r = new BinaryReader(ms);
            int ver = r.ReadInt32(), cver = r.ReadInt32();
            return new CharacterInfo(r, ver, cver);
        }
        catch (Exception ex)
        {
            err = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    private static CharacterInfo Clone(CharacterInfo src, int newIndex, string newName)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            w.Write(Envir.Version);
            w.Write(Envir.CustomVersion);
            src.Save(w);
        }

        ms.Position = 0;
        using var r = new BinaryReader(ms);
        int ver = r.ReadInt32(), cver = r.ReadInt32();
        var c = new CharacterInfo(r, ver, cver);
        c.Index = newIndex;
        c.Name = newName;
        return c;
    }
}
