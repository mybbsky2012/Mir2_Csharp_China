using System.Text;
using System.Text.RegularExpressions;
using Client.MirObjects;

namespace Client
{
    /// <summary>
    /// 地面物品名字过滤（内挂 -> 物品过滤）。
    /// 规则来自 Data\ItemFilter.txt：只有列表里匹配到的地面物品才显示名字，其余不绘制（物品本身和拾取都不受影响）。
    /// 文件不存在 / 列表为空 / 开关关闭时一律不过滤，避免误开后满屏空白。
    /// </summary>
    internal static class ItemFilter
    {
        private static readonly string FilePath = Path.Combine(Settings.DataPath, "ItemFilter.txt");

        private static readonly List<string> Names = new List<string>();
        private static ItemGrade minGrade = ItemGrade.None;
        private static DateTime stamp = DateTime.MinValue;
        private static bool loaded;
        private static long nextCheck;

        public static int NameCount => Names.Count;
        public static ItemGrade MinGrade => minGrade;
        public static string CurrentFile => FilePath;

        /// <summary>强制重读列表文件（勾选开关 / 手动重载时调用）</summary>
        public static void Reload()
        {
            Names.Clear();
            minGrade = ItemGrade.None;
            loaded = true;
            stamp = DateTime.MinValue;
            nextCheck = 0;

            try
            {
                if (!File.Exists(FilePath)) return;

                stamp = File.GetLastWriteTimeUtc(FilePath);

                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == ';' || line[0] == '#' || line.StartsWith("//")) continue;

                    // 品质行：品质=宝物 / 品质：宝物 / 品质 >= 宝物
                    if (line.StartsWith("品质"))
                    {
                        int eq = line.IndexOfAny(new[] { '=', '：', ':' });
                        if (eq < 0) continue;

                        string value = line.Substring(eq + 1).Trim().TrimStart('>', '=', ' ', '≥');
                        ItemGrade grade = ParseGrade(value);
                        if (grade != ItemGrade.None && grade > minGrade) minGrade = grade; // 多条取最高
                        continue;
                    }

                    Names.Add(line);
                }
            }
            catch
            {
                // 文件被占用 / 编码异常：保持空列表，等于不过滤
            }
        }

        /// <summary>判断这个地面物品该不该显示名字</summary>
        public static bool ShouldShow(ItemObject ob)
        {
            if (ob == null) return true;
            if (!Settings.ItemFilterOn) return true;

            EnsureLoaded();

            // 列表为空 → 不过滤，防止误开开关后什么都看不到
            if (Names.Count == 0 && minGrade == ItemGrade.None) return true;

            if (minGrade != ItemGrade.None && ob.Grade >= minGrade) return true;

            if (Names.Count == 0) return false;

            string name = CleanName(ob.Name);
            if (name.Length == 0) return false;

            for (int i = 0; i < Names.Count; i++)
                if (name.IndexOf(Names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }

        /// <summary>
        /// 判断这个地面物品该不该被自动捡取（内挂「自动捡物」在总开关开启时用）。
        /// 与 ShouldShow 的区别：不看显示开关——列表里有内容就按列表过滤，列表为空则全部可捡（保持原行为）。
        /// </summary>
        public static bool IsWanted(ItemObject ob)
        {
            if (ob == null) return true;

            EnsureLoaded();

            // 列表为空 → 不过滤，全部可捡
            if (Names.Count == 0 && minGrade == ItemGrade.None) return true;

            if (minGrade != ItemGrade.None && ob.Grade >= minGrade) return true;

            if (Names.Count == 0) return false;

            string name = CleanName(ob.Name);
            if (name.Length == 0) return false;

            for (int i = 0; i < Names.Count; i++)
                if (name.IndexOf(Names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }

        /// <summary>文件被改过就自动重读，最多每 2 秒检查一次，避免每帧 IO</summary>
        private static void EnsureLoaded()
        {
            if (!loaded)
            {
                Reload();
                return;
            }

            long now = Environment.TickCount64;
            if (now < nextCheck) return;
            nextCheck = now + 2000;

            try
            {
                if (!File.Exists(FilePath))
                {
                    if (Names.Count > 0 || minGrade != ItemGrade.None) Reload();
                    return;
                }

                if (File.GetLastWriteTimeUtc(FilePath) != stamp) Reload();
            }
            catch
            {
            }
        }

        private static ItemGrade ParseGrade(string value)
        {
            switch (value)
            {
                case "普通": case "1": return ItemGrade.普通;
                case "宝物": case "2": return ItemGrade.宝物;
                case "圣物": case "3": return ItemGrade.圣物;
                case "神物": case "4": return ItemGrade.神物;
                case "英雄": case "5": return ItemGrade.英雄;
            }

            ItemGrade grade;
            return Enum.TryParse(value, out grade) ? grade : ItemGrade.None;
        }

        /// <summary>去掉服务端附加的数量后缀「 (3)」和尾部数字，便于按名字匹配</summary>
        private static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            string result = Regex.Replace(name, @"\s*\(\d+\)\s*$", string.Empty);
            result = Regex.Replace(result, @"\d+$", string.Empty);
            return result.Trim();
        }
    }
}
