using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

// ============================================================================
//  假人取名逻辑自测（20261106）
//  复刻 FakePlayerManager.RandomBotName / MakeName / LoadBotNames 的算法，
//  验证 2 万假人规模下：不重名、不与真实角色撞名、配置名优先、性能可用。
//  注意：池子与算法改动后要同步 FakePlayerManager.cs，这里只是离线验证。
// ============================================================================
namespace BotNameSelfTest
{
    internal static class Program
    {
        private static readonly Random Rng = new Random(20261106);   // 固定种子，结果可复现

        // ---- 以下与 FakePlayerManager.RandomBotName 保持一致 ----
        private static readonly string[] First =
        {
            "风", "云", "雷", "龙", "战", "天", "海", "刀", "剑", "影",
            "玄", "冰", "火", "狂", "醉", "紫", "青", "银", "血", "铁",
            "孤", "长", "无", "夜", "月", "雪", "小", "老", "大", "圣",
            "魔", "神", "幽", "冷", "傲", "疾", "破", "断", "追", "逐",
            "啸", "鸣", "荒", "澜", "尘", "汐", "焰", "霜", "曜", "冥",
            "燃", "苍", "澈", "凛", "晴", "岚", "曦", "楚",
        };

        private static readonly string[] Second =
        {
            "星辰", "之痕", "无双", "天下", "追风", "逐月", "惊雷", "若尘", "清歌", "一刀",
            "屠龙", "破军", "逍遥", "忘忧", "流年", "拾光", "不悔", "长歌", "微凉", "半夏",
            "千寻", "万古", "如故", "归尘", "逐鹿", "听雨", "观澜", "问天", "朝歌", "暮雪",
            "孤影", "断念", "傲雪", "冷月", "疾风", "落霞", "残阳", "惊鸿", "踏月", "凌霄",
            "沧海", "桑田", "浮生", "烟雨", "疏影", "暗香", "平生", "故人", "旧梦", "山河",
            "万里", "未央",
        };

        private static readonly string[] Third =
        {
            "者", "客", "仙", "魔", "神", "圣", "王", "爷", "哥", "姐",
            "侠", "魂", "灵", "歌", "剑", "刀", "枪", "弓", "扇", "雨",
            "风", "雪", "月", "花", "云", "星", "光", "尘", "霜", "焰",
            "梦", "泪", "笑", "痴", "缘", "忆",
        };

        private static HashSet<string> _realCharacterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string RandomBotName(HashSet<string> used)
        {
            string name = Rng.Next(100) < 30
                ? First[Rng.Next(First.Length)] + Second[Rng.Next(Second.Length)]
                : First[Rng.Next(First.Length)] + Second[Rng.Next(Second.Length)] + Third[Rng.Next(Third.Length)];

            if (used.Contains(name)) return null;
            if (_realCharacterNames.Contains(name)) return null;

            return name;
        }

        private static string MakeName(HashSet<string> used)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                string name = RandomBotName(used);

                if (name == null) continue;

                used.Add(name);
                return name;
            }

            string fallback;
            int n = 1;
            do { fallback = "行者" + n++; } while (used.Contains(fallback));
            used.Add(fallback);
            return fallback;
        }

        private static int _failCount = 0, _passCount = 0;

        private static void Check(bool ok, string desc)
        {
            if (ok) { _passCount++; Console.WriteLine("  [通过] " + desc); }
            else { _failCount++; Console.WriteLine("  [失败] " + desc); }
        }

        private static void Main()
        {
            Console.WriteLine("==== 假人取名逻辑自测 ====\n");

            // ---------- 测试 1：2 万全随机，唯一性 + 耗时 ----------
            {
                var used = new HashSet<string>(stringComparerOrd);
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < 20000; i++) MakeName(used);

                sw.Stop();

                int fallbackCount = 0;
                foreach (var n in used) if (n.StartsWith("行者")) fallbackCount++;

                Check(used.Count == 20000, $"20000 个假人全部取到名（实际 {used.Count}）");
                Check(fallbackCount == 0, $"无\"行者N\"兜底名（兜底 {fallbackCount} 个）");
                Check(sw.ElapsedMilliseconds < 3000, $"取名耗时 {sw.ElapsedMilliseconds}ms（阈值 3000ms）");
            }

            // ---------- 测试 2：与真实角色不撞名（含大小写变体）----------
            {
                _realCharacterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "风云无双", "XIAOYAO", "战神天下" };

                var used = new HashSet<string>(stringComparerOrd);

                for (int i = 0; i < 20000; i++) MakeName(used);

                bool clash = false;
                foreach (var n in used)
                    if (_realCharacterNames.Contains(n)) { clash = true; break; }

                Check(!clash, "20000 个名字与真实角色（含大小写变体）零撞名");
            }

            // ---------- 测试 3：模拟"文件不存在→按 Count 生成模板"再读回 ----------
            {
                string tmp = Path.Combine(Path.GetTempPath(), "FakePlayerNames_test.txt");
                int count = 20000;

                // 模拟 LoadBotNames 的模板生成段
                var sb = new StringBuilder();
                HashSet<string> generated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < count; i++)
                {
                    string name = RandomBotName(generated);
                    if (name == null)
                    {
                        int n = 1;
                        do { name = "行者" + n++; } while (!generated.Add(name));
                    }
                    else generated.Add(name);

                    sb.AppendLine(name);
                }
                File.WriteAllText(tmp, sb.ToString());

                // 模拟读回解析（跳过注释/空行/去重）
                var parsed = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in File.ReadAllLines(tmp))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;
                    if (!seen.Add(line)) continue;
                    parsed.Add(line);
                }

                Check(parsed.Count == count, $"模板生成并读回 {count} 个名字（实际 {parsed.Count}）");
                Check(new HashSet<string>(parsed, StringComparer.OrdinalIgnoreCase).Count == count, "模板内部无重复名");

                // ---------- 测试 4：配置 5000 个 + Count=20000 → 配置优先、剩余随机、无重名 ----------
                {
                    var configNames = parsed.GetRange(0, 5000);
                    var used = new HashSet<string>(stringComparerOrd);
                    int cfgIdx = 0;
                    var finalNames = new List<string>();

                    for (int i = 0; i < count; i++)
                    {
                        string name;
                        if (cfgIdx < configNames.Count) { name = configNames[cfgIdx++]; used.Add(name); }
                        else name = MakeName(used);
                        finalNames.Add(name);
                    }

                    Check(cfgIdx == 5000, $"配置名全部用上（用了 {cfgIdx} 个）");
                    Check(new HashSet<string>(finalNames, StringComparer.OrdinalIgnoreCase).Count == count,
                          "配置+随机混合后 20000 个名字零重复");
                    bool first5kMatch = true;
                    for (int i = 0; i < 5000; i++)
                        if (!string.Equals(finalNames[i], configNames[i], StringComparison.OrdinalIgnoreCase)) { first5kMatch = false; break; }
                    Check(first5kMatch, "前 5000 个（序号靠前的假人）恰好按文件顺序拿到配置名");
                }

                // ---------- 测试 5：配置超过 Count → 只取前 Count 个 ----------
                {
                    int take = Math.Min(300, parsed.Count);
                    Check(take == 300, $"配置 20000 个、Count=300 时只取前 300（取 {take}）");
                }

                // ---------- 测试 6：空文件/全注释 → 0 个配置名 ----------
                {
                    var seen6 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int cfg = 0;
                    foreach (var raw in new[] { "# 注释", "; 注释2", "// 注释3", "", "   " })
                    {
                        var line = raw.Trim();
                        if (line.Length == 0) continue;
                        if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//")) continue;
                        if (!seen6.Add(line)) continue;
                        cfg++;
                    }
                    Check(cfg == 0, "全注释/空行文件解析出 0 个配置名（全随机）");
                }

                File.Delete(tmp);
            }

            Console.WriteLine($"\n==== 结果：{_passCount} 通过 / {_failCount} 失败 ====");
            Environment.Exit(_failCount == 0 ? 0 : 1);
        }

        private static readonly StringComparer stringComparerOrd = StringComparer.OrdinalIgnoreCase;
    }
}
