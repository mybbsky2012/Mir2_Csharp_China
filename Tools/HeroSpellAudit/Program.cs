using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace HeroSpellAudit;

/// <summary>
/// 只读审计：把库里所有英雄的已学技能挨个对照 HeroSpellBook，
/// 找出「会被 AI 挑中、但服务端 Magic() 放不出来」的技能。
/// 用法：放到服务端目录（Server.MirDB 同级）运行。
/// </summary>
internal static class Program
{
    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("请在服务端目录运行"); return 2; }

        Settings.Load();
        Envir envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("!! LoadDB 失败"); return 2; }

        // 服务端是在 StartGame 里把 BuffInfo.Load() 灌进 BuffInfoList 的，读档前必须手工补上，
        // 否则 HeroInfo.Load 里 Buff 反序列化会抛「尚未实施」
        envir.BuffInfoList.Clear();
        envir.BuffInfoList.AddRange(BuffInfo.Load());

        envir.LoadAccounts();

        Console.WriteLine($"英雄总数: {envir.HeroList.Count}");
        Console.WriteLine();

        int bad = 0;

        foreach (HeroInfo hero in envir.HeroList)
        {
            if (hero == null) continue;

            var hits = new List<string>();
            var notes = new List<string>();
            var all = new List<string>();

            for (int i = 0; i < hero.Magics.Count; i++)
            {
                UserMagic m = hero.Magics[i];
                if (m?.Info == null) continue;

                all.Add($"{m.Info.Name}[L{m.Level}]");

                if (CastableViaMagic(m.Spell)) continue;

                // 危险：技能表里登记了 + 服务端放不出来 → AI 会一直挑它，英雄卡成「站着不打」
                // 提示：没有登记 → AI 根本不会挑，不影响
                if (HeroSpellBook.Get(m.Spell) != null)
                    hits.Add($"{m.Info.Name}({m.Spell}) 放不出来");
                else
                    notes.Add(m.Info.Name);
            }

            if (notes.Count > 0)
            {
                Console.WriteLine($"  (提示) {hero.Name} {hero.Class} Lv{hero.Level} 学了放不出来的技能: {string.Join(",", notes)} —— 未登记，AI 不会挑，无影响");
            }

            if (hero.Class == MirClass.弓箭)
            {
                Console.WriteLine($"  {hero.Name} Lv{hero.Level} 技能: {string.Join(" ", all)}");
                DumpPicks(hero);
            }

            if (hits.Count == 0) continue;

            bad++;
            Console.WriteLine($"[!] {hero.Name}  {hero.Class}  Lv{hero.Level}");
            Console.WriteLine($"     会被 AI 挑中但放不出来: {string.Join(", ", hits)}");
            Console.WriteLine();
        }

        Console.WriteLine(bad == 0 ? "✔ 没有英雄会被「排得出却放不出」的技能卡死" : $"✘ {bad} 个英雄存在会被卡死的技能");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>用这个英雄真实学过的技能，跑一遍 AI 排序：看它现在到底会放什么。</summary>
    private static void DumpPicks(HeroInfo hero)
    {
        var scenarios = new (string Name, HeroSpellKind Kind, int Surround, int Distance)[]
        {
            ("增益(自身)", HeroSpellKind.Buff, 0, 0),
            ("单体 5 格", HeroSpellKind.Single, 0, 5),
            ("单体 2 格", HeroSpellKind.Single, 0, 2),
            ("被围 3 个", HeroSpellKind.Aoe, 3, 1),
            ("控制", HeroSpellKind.Control, 3, 1),
        };

        foreach (var sc in scenarios)
        {
            List<UserMagic> ranked = HeroSpellBook.Rank(hero.Magics, sc.Kind, sc.Surround, sc.Distance, _ => false, _ => false, true);
            string text = ranked.Count == 0
                ? "（没有可用技能 → 走普通平射）"
                : string.Join(" > ", ranked.Take(3).Select(m => $"{m.Info.Name}[S{HeroSpellBook.Score(m):0}]"));
            Console.WriteLine($"      {sc.Name,-10} → {text}");
        }
    }

    /// <summary>
    /// Magic() 里有分支的技能白名单（由 HeroBrainTest 的静态检查和这里的实现保持一致）。
    /// 这里只为了在运行时给出告警，真正的判据是 HumanObject.Magic 的 switch。
    /// </summary>
    private static bool CastableViaMagic(Spell spell)
    {
        // 已知不能通过 Magic() 施放的技能（走 SpellToggle 或被动触发）
        switch (spell)
        {
            case Spell.MentalState:   // 只在 SpellToggle 处理
            case Spell.Hemorrhage:    // 普通攻击时被动触发
                return false;
        }

        return true;
    }
}
