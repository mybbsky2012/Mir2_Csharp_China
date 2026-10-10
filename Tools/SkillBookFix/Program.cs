using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace SkillBookFix;

/// <summary>
/// 只在本机服务端目录运行（Server.MirDB 所在处）。
///
///   SkillBookFix.exe --verify          只读：列出全部技能书的 Shape 与应有技能
///   SkillBookFix.exe --apply           修正技能书 Shape + 补齐新增 MagicInfo，写回数据库（自动备份）
///   SkillBookFix.exe --roundtrip 输出   只读往返测试：LoadDB 后立刻 SaveDB 到别的文件，用于校验读写是否对称
/// </summary>
internal static class Program
{
    // 技能书物品名 → 应授予的技能（对照官网 mir2db 技能列表 49~53）
    private static readonly (string Name, Spell Spell)[] BookMap =
    {
        // 法师
        ("强击-秘籍",        Spell.ThunderBoltRare),
        ("吸魔炎风",         Spell.AbsorbFlame),
        ("吸魔炎风-秘籍",     Spell.AbsorbFlameRare),
        // 道士
        ("万效符",           Spell.GreatTalisman),
        ("万效符-秘籍",       Spell.GreatTalismanRare),
        ("血龙兽",           Spell.PetEnhancer),
        ("血龙兽-秘籍",       Spell.PetEnhancerRare),
        // 刺客
        ("影华连击",         Spell.ShadowCombo),
        ("影华连击-秘籍",     Spell.ShadowComboRare),
        ("月华乱舞-秘籍",     Spell.CrescentSlashRare),
        ("拔刀术-秘籍",       Spell.FlashDashRare),
        ("月影雾-秘籍",       Spell.MoonMistRare),
        // 弓手
        ("落雷杀",           Spell.SkyThunderShot),
        ("落雷杀-秘籍",       Spell.SkyThunderShotRare),
        ("爆闪-秘籍",         Spell.DelayedExplosionRare),
        ("气流术-秘籍",       Spell.ConcentrationRare),
    };

    // 新增技能：按官网 krmir.com「2024 年新武功」说明写入的等级成长倍率（MultiplierBase + Level * MultiplierBonus）
    private static readonly (Spell Spell, float Base, float Bonus)[] MultiplierMap =
    {
        (Spell.ThunderBoltRare,     1f, 0.150f),  //官网 1~3级：对怪物 +15% / +30% / +50%
        (Spell.AbsorbFlame,         1f, 0.250f),  //官网 1~3级：对 BOSS +25% / +50% / +100%
        (Spell.AbsorbFlameRare,     2f, 0.500f),  //官网 0级 +100% / 1级 +150%
        (Spell.CrescentSlashRare,   1f, 2.000f),  //月下乱舞-秘籍（官网未给倍率，沿用原设计）
        (Spell.ShadowCombo,         1f, 0.250f),  //官网 1~3级：对怪物 +25% / +50% / +100%
        (Spell.ShadowComboRare,     3f, 1.000f),  //官网 0级 +200% / 1级 +300% / 2级 +400%
        (Spell.SkyThunderShot,      1f, 0.250f),  //官网 1~4级：对怪物 +25% / +50% / +75% / +100%
        (Spell.SkyThunderShotRare,  2f, 0.125f),  //官网 0级 +100% / 2级 +125% / 3级 +150%
        (Spell.DeathBlow,           2f, 0.250f),  //灭杀（落雷杀-秘籍的追加落雷）
    };

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请把本程序放到服务端目录（与 Server.exe 同级）再运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine($"物品 {envir.ItemInfoList.Count} 个 / 技能 {envir.MagicInfoList.Count} 个 / 版本 {Envir.Version}");

        if (args.Contains("--roundtrip"))
        {
            int idx = Array.IndexOf(args, "--roundtrip");
            string outPath = args.Length > idx + 1 ? args[idx + 1] : "RoundTrip.MirDB";
            envir.SaveDB();
            File.Move(Envir.DatabasePath, outPath, true);
            Console.WriteLine($"往返写出 {Path.GetFullPath(outPath)}");
            return 0;
        }

        bool apply = args.Contains("--apply");

        // ---------- 1. 技能书 Shape ----------
        Console.WriteLine();
        Console.WriteLine("===== 技能书 Shape 核对 =====");

        var bookKeys = BookMap.ToDictionary(x => x.Name, x => x.Spell);
        int changed = 0, total = 0;

        foreach (var it in envir.ItemInfoList)
        {
            if (it == null || it.Type != ItemType.技能书) continue;
            total++;

            string wantName = null;
            foreach (var kv in bookKeys)
            {
                if (kv.Key != it.Name) continue;
                wantName = kv.Key;
                break;
            }

            if (wantName == null) continue;

            short want = (short)bookKeys[wantName];
            if (it.Shape == want) continue;

            changed++;
            Console.WriteLine($"  [{it.Index,5}] {it.Name,-16} Shape {it.Shape,4} -> {want,4}  ({bookKeys[wantName]})");

            if (apply) it.Shape = want;
        }

        Console.WriteLine($"技能书共 {total} 本，需修正 {changed} 本。");

        // ---------- 2. 新增技能是否已进库 ----------
        Console.WriteLine();
        Console.WriteLine("===== 新增技能 MagicInfo =====");

        var need = new[]
        {
            Spell.ThunderBoltRare, Spell.AbsorbFlame, Spell.AbsorbFlameRare,
            Spell.GreatTalisman, Spell.GreatTalismanRare, Spell.SoulFireBallPlus, Spell.SoulFireBallPlusRare,
            Spell.PetEnhancerRare, Spell.CrescentSlashRare, Spell.ShadowCombo, Spell.ShadowComboRare,
            Spell.SkyThunderShot, Spell.SkyThunderShotRare, Spell.DeathBlow,
            Spell.FlashDashRare, Spell.MoonMistRare, Spell.DelayedExplosionRare, Spell.ConcentrationRare,
        };

        int missing = 0;
        foreach (var sp in need)
        {
            MagicInfo found = null;
            for (int i = 0; i < envir.MagicInfoList.Count; i++)
                if (envir.MagicInfoList[i].Spell == sp) { found = envir.MagicInfoList[i]; break; }

            if (found == null)
            {
                missing++;
                Console.WriteLine($"  !! 缺失 {sp}");
                continue;
            }
            Console.WriteLine($"  OK  {found.Name,-18} Spell={(byte)sp,4} Icon={found.Icon,4} Lv={found.Level1}/{found.Level2}/{found.Level3} Need={found.Need1}/{found.Need2}/{found.Need3} Range={found.Range}");
        }

        if (missing > 0)
        {
            Console.WriteLine($"  —— 共缺失 {missing} 条（会由 Envir.FillMagicInfoList 在 LoadDB 时自动补进内存，保存后落到库里）");
        }

        // ---------- 3. 等级成长倍率（对照官网 krmir.com 新武功说明） ----------
        Console.WriteLine();
        Console.WriteLine("===== 新技能等级成长倍率 =====");

        int mChanged = 0;
        foreach (var (sp, wantBase, wantBonus) in MultiplierMap)
        {
            MagicInfo found = null;
            for (int i = 0; i < envir.MagicInfoList.Count; i++)
                if (envir.MagicInfoList[i].Spell == sp) { found = envir.MagicInfoList[i]; break; }

            if (found == null)
            {
                Console.WriteLine($"  !! 缺失 {sp}，无法设置倍率");
                continue;
            }

            bool same = Math.Abs(found.MultiplierBase - wantBase) < 0.0001f &&
                        Math.Abs(found.MultiplierBonus - wantBonus) < 0.0001f;

            Console.WriteLine($"  {(same ? "OK  " : "修改")} {found.Name,-18} 倍率 {found.MultiplierBase:0.###}+Lv*{found.MultiplierBonus:0.###} -> {wantBase:0.###}+Lv*{wantBonus:0.###}");

            if (same) continue;

            mChanged++;
            if (apply)
            {
                found.MultiplierBase = wantBase;
                found.MultiplierBonus = wantBonus;
            }
        }
        Console.WriteLine($"需调整 {mChanged} 条。");

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("[预览模式] 加 --apply 才写回数据库。");
            return 0;
        }

        string bak = Envir.DatabasePath + "." + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".bak";
        File.Copy(Envir.DatabasePath, bak, true);
        envir.SaveDB();

        Console.WriteLine();
        Console.WriteLine($"已写回 {Path.GetFullPath(Envir.DatabasePath)}");
        Console.WriteLine($"备份   {Path.GetFullPath(bak)}");
        return 0;
    }
}
