using System.Text;
using Server.MirEnvir;

namespace Server.MirDatabase
{
    /// <summary>
    /// 自定义 BUFF 的单条属性加成。
    ///
    ///   最终加成 = Base + PerLevel × 等级
    ///
    /// 等级取「施法者的等级」；没有施法者（如 GM 给自己加）时取目标自身等级。
    /// 所以 GM 给自己加 = 按自己等级算，给玩家加 = 同样按该玩家等级算 —— 与直觉一致。
    /// </summary>
    public class BuffStatDef
    {
        public Stat Stat;
        public int Base;
        public int PerLevel;

        public int At(int level)
        {
            if (level < 0) level = 0;

            return Base + PerLevel * level;
        }

        /// <summary>回写成 ini 里的 "基础+每级" 文本。</summary>
        public string ToText()
        {
            return PerLevel == 0 ? Base.ToString() : string.Format("{0}+{1}", Base, PerLevel);
        }

        /// <summary>
        /// 解析 "10" 或 "10+2"。
        /// ★只认半角 '+'，不认全角 ＋（脚本系统踩过全角空格的坑，这里提前收紧）。
        /// </summary>
        public static bool TryParse(string text, out int baseValue, out int perLevel)
        {
            baseValue = 0;
            perLevel = 0;

            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim();

            int index = text.IndexOf('+');

            if (index < 0) return int.TryParse(text, out baseValue);

            if (!int.TryParse(text.Substring(0, index).Trim(), out baseValue)) return false;
            if (!int.TryParse(text.Substring(index + 1).Trim(), out perLevel)) return false;

            return true;
        }
    }

    /// <summary>
    /// 一个自定义 BUFF（或对已有 BUFF 的属性覆盖）的完整定义。
    /// 对应 Configs\BuffSystem.ini 里的一个 [节]。
    /// </summary>
    public class BuffDef
    {
        /// <summary>映射到的 BuffType。自定义槽位 = 230 + 槽位号 - 1。</summary>
        public BuffType Type;

        /// <summary>1..24 = 自定义槽位；0 = 挂在已有 BUFF 上（Type 由节名解析而来）。</summary>
        public int Slot;

        /// <summary>显示名（下发给客户端）。为空时客户端回退到枚举名。</summary>
        public string Name = string.Empty;

        /// <summary>图标索引。0 = 用客户端原有映射；10000+/20000+/40000+ 见 ClientBuff.Icon 注释。</summary>
        public int Icon;

        public bool Visible = true;

        public BuffStackType Stack = BuffStackType.ResetDuration;

        public BuffProperty Property = BuffProperty.None;

        /// <summary>说明文本（GM 工具里编辑，暂不下发）。</summary>
        public string Desc = string.Empty;

        //★ 哪些字段是「配置里真写了」的。
        //  已有 BUFF（如 [隐身术] 只加一条属性）不能因为没写 Visible/Stack 就被默认值覆盖回
        //  True / ResetDuration —— 那会把 buff 原本的行为改坏。自定义槽位是新东西，默认值随便用。
        public bool HasName;
        public bool HasIcon;
        public bool HasVisible;
        public bool HasStack;
        public bool HasProperty;

        public readonly List<BuffStatDef> Stats = new List<BuffStatDef>();

        public BuffDef(BuffType type, int slot)
        {
            Type = type;
            Slot = slot;
        }

        public bool IsCustom { get { return Slot > 0; } }

        /// <summary>ini 里的节名：自定义槽位写数字，已有 BUFF 写它的枚举名。</summary>
        public string Section
        {
            get { return IsCustom ? Slot.ToString() : Type.ToString(); }
        }

        /// <summary>按等级算出一份属性表。</summary>
        public Stats BuildStats(int level)
        {
            Stats stats = new Stats();

            for (int i = 0; i < Stats.Count; i++)
            {
                BuffStatDef def = Stats[i];
                int value = def.At(level);

                if (value == 0) continue;

                stats[def.Stat] += value;
            }

            return stats;
        }
    }

    /// <summary>
    /// 自定义 BUFF 配置表（Configs\BuffSystem.ini）。
    ///
    /// 为什么不用数据库：Server.MirDB 是「启动全量读、关闭全量重写」的二进制存档，
    /// 还带几十处 LoadVersion 版本分支；往里加新表要动序列化与兼容，风险远大于收益。
    /// 走独立 ini 可以随时手改、热重载，也不怕把库写坏。
    ///
    /// 文件格式（UTF-8，' ; ' 开头为注释）：
    ///
    ///     [1]                      ← 自定义槽位 1（对应 BuffType.Custom1）
    ///     Name=羽化登仙
    ///     Icon=30034
    ///     Visible=True
    ///     Stack=ResetDuration
    ///     Property=None
    ///     Desc=每级提升防御
    ///     Stat.MaxAC=5+2           ← 基础 5，每级再 +2
    ///     Stat.HP=100
    ///
    ///     [隐身术]                  ← 也可以直接给已有 BUFF 加属性/换图标
    ///     Stat.攻击速度=3
    /// </summary>
    public static class BuffConfig
    {
        /// <summary>BuffType.Custom1 的枚举值。</summary>
        public const int FirstCustomType = 230;

        /// <summary>自定义槽位数量（Custom1..Custom24 = 230..253，byte 上限 255）。</summary>
        public const int CustomSlotCount = 24;

        public static string FilePath { get { return Path.Combine(Settings.ConfigPath, "BuffSystem.ini"); } }

        public static List<BuffDef> Defs { get; private set; } = new List<BuffDef>();

        private static readonly Dictionary<BuffType, BuffDef> Lookup = new Dictionary<BuffType, BuffDef>();

        /// <summary>加载失败时的最后一次错误（GM 命令与编辑器用来提示）。</summary>
        public static string LastError { get; private set; } = string.Empty;

        public static BuffType SlotToType(int slot)
        {
            return (BuffType)(FirstCustomType + slot - 1);
        }

        public static int TypeToSlot(BuffType type)
        {
            int value = (int)type;

            if (value < FirstCustomType || value >= FirstCustomType + CustomSlotCount) return 0;

            return value - FirstCustomType + 1;
        }

        public static BuffDef Get(BuffType type)
        {
            BuffDef def;
            return Lookup.TryGetValue(type, out def) ? def : null;
        }

        /// <summary>某个 BUFF 的显示名（没有配置或没写名字时返回空串）。</summary>
        public static string GetName(BuffType type)
        {
            BuffDef def = Get(type);
            return def == null ? string.Empty : def.Name;
        }

        /// <summary>
        /// 把「人写的 BUFF 名」解析成枚举名 —— 给 NPC 脚本的 GIVEBUFF / CHECKBUFF 用。
        ///
        /// 依次尝试：① 枚举名（Custom1 / 隐身术）② 槽位号（"1"）③ 配置里的显示名（羽化登仙）。
        /// 原来的脚本只写枚举名，走的是第 ① 条，行为完全不变；
        /// 这样自定义 BUFF 在脚本里既能写 Custom1、也能直接写中文名。
        /// 找不到返回 null。
        /// </summary>
        public static string ResolveScriptName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            BuffType type;

            if (Enum.TryParse(name, true, out type) && Enum.IsDefined(typeof(BuffType), type) && type != BuffType.None)
                return type.ToString();

            int slot;

            if (int.TryParse(name, out slot) && slot >= 1 && slot <= CustomSlotCount)
                return SlotToType(slot).ToString();

            for (int i = 0; i < Defs.Count; i++)
            {
                BuffDef def = Defs[i];

                if (string.IsNullOrEmpty(def.Name)) continue;
                if (!string.Equals(def.Name, name, StringComparison.Ordinal)) continue;

                return def.Type.ToString();
            }

            return null;
        }

        public static int GetIcon(BuffType type)
        {
            BuffDef def = Get(type);
            return def == null ? 0 : def.Icon;
        }

        /// <summary>
        /// 重新加载配置文件。文件不存在时按「空配置」处理（原有 BUFF 行为完全不变），
        /// 不抛异常 —— 这个函数会在 GM 在线命令里被调用，抛出去就是崩服。
        /// </summary>
        public static void Load()
        {
            Defs = new List<BuffDef>();
            Lookup.Clear();
            LastError = string.Empty;

            try
            {
                LoadInternal();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Defs = new List<BuffDef>();
                Lookup.Clear();

                MessageQueue.Instance.Enqueue("BUFF 配置加载失败（已按空配置运行）: " + ex.Message);
            }
        }

        private static void LoadInternal()
        {
            string path = FilePath;

            if (!File.Exists(path))
            {
                // 首次运行：落一份带示例的模板，方便直接改
                WriteTemplate(path);
                return;
            }

            BuffDef current = null;
            int lineNo = 0;

            foreach (string raw in File.ReadAllLines(path))
            {
                lineNo++;

                string line = raw.Trim();

                // 注释：整行以 ; 开头（行尾注释不支持——Stat 值里可能带 + 号，没必要冒险）
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("//")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string section = line.Substring(1, line.Length - 2).Trim();

                    current = CreateDef(section);

                    if (current == null)
                    {
                        MessageQueue.Instance.Enqueue(string.Format(
                            "BUFF 配置第 {0} 行：无法识别的节 [{1}]（应为数字槽位 1~{2}，或已有 BUFF 名）",
                            lineNo, section, CustomSlotCount));
                    }
                    else
                    {
                        Defs.Add(current);
                    }

                    continue;
                }

                if (current == null) continue;

                int eq = line.IndexOf('=');

                if (eq <= 0)
                {
                    MessageQueue.Instance.Enqueue(string.Format("BUFF 配置第 {0} 行：缺少 '='，已忽略：{1}", lineNo, line));
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                ApplyKey(current, key, value, lineNo);
            }

            // 建索引（后面定义覆盖前面的，手改文件时方便）
            for (int i = 0; i < Defs.Count; i++)
            {
                Lookup[Defs[i].Type] = Defs[i];
            }
        }

        private static BuffDef CreateDef(string section)
        {
            int slot;

            if (int.TryParse(section, out slot))
            {
                if (slot < 1 || slot > CustomSlotCount) return null;

                BuffDef custom = new BuffDef(SlotToType(slot), slot);
                custom.Name = "自定义BUFF" + slot;

                return custom;
            }

            // 已有 BUFF：按枚举名匹配
            BuffType type;

            if (!Enum.TryParse(section, false, out type)) return null;
            if (type == BuffType.None) return null;

            return new BuffDef(type, 0);
        }

        private static void ApplyKey(BuffDef def, string key, string value, int lineNo)
        {
            if (key.Equals("Name", StringComparison.OrdinalIgnoreCase))
            {
                def.Name = value;
                def.HasName = true;
                return;
            }

            if (key.Equals("Icon", StringComparison.OrdinalIgnoreCase))
            {
                int icon;
                if (int.TryParse(value, out icon))
                {
                    def.Icon = icon;
                    def.HasIcon = true;
                }
                return;
            }

            if (key.Equals("Visible", StringComparison.OrdinalIgnoreCase))
            {
                bool visible;
                if (bool.TryParse(value, out visible))
                {
                    def.Visible = visible;
                    def.HasVisible = true;
                }
                return;
            }

            if (key.Equals("Stack", StringComparison.OrdinalIgnoreCase))
            {
                BuffStackType stack;
                if (Enum.TryParse(value, true, out stack))
                {
                    def.Stack = stack;
                    def.HasStack = true;
                }
                else MessageQueue.Instance.Enqueue(string.Format("BUFF 配置第 {0} 行：未知堆叠方式 {1}", lineNo, value));
                return;
            }

            if (key.Equals("Property", StringComparison.OrdinalIgnoreCase))
            {
                def.Property = ParseProperty(value, lineNo);
                def.HasProperty = true;
                return;
            }

            if (key.Equals("Desc", StringComparison.OrdinalIgnoreCase))
            {
                def.Desc = value;
                return;
            }

            // Stat.MaxAC=5+2 / Stat.暴击伤害=10
            if (key.StartsWith("Stat.", StringComparison.OrdinalIgnoreCase))
            {
                string statName = key.Substring(5).Trim();

                Stat stat;

                if (!Enum.TryParse(statName, false, out stat))
                {
                    MessageQueue.Instance.Enqueue(string.Format("BUFF 配置第 {0} 行：未知属性 {1}", lineNo, statName));
                    return;
                }

                int baseValue, perLevel;

                if (!BuffStatDef.TryParse(value, out baseValue, out perLevel))
                {
                    MessageQueue.Instance.Enqueue(string.Format("BUFF 配置第 {0} 行：数值格式应为 基础 或 基础+每级，实际为 {1}", lineNo, value));
                    return;
                }

                def.Stats.Add(new BuffStatDef { Stat = stat, Base = baseValue, PerLevel = perLevel });
                return;
            }

            MessageQueue.Instance.Enqueue(string.Format("BUFF 配置第 {0} 行：未知字段 {1}", lineNo, key));
        }

        /// <summary>Property 支持多个标志：RemoveOnDeath|Debuff，也接受逗号分隔。</summary>
        private static BuffProperty ParseProperty(string value, int lineNo)
        {
            if (string.IsNullOrWhiteSpace(value)) return BuffProperty.None;

            BuffProperty result = BuffProperty.None;
            bool any = false;

            foreach (string part in value.Split(new[] { '|', ',', '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();

                if (name.Equals("None", StringComparison.OrdinalIgnoreCase)) continue;

                BuffProperty flag;

                if (!Enum.TryParse(name, true, out flag))
                {
                    MessageQueue.Instance.Enqueue(string.Format("BUFF 配置第 {0} 行：未知标记 {1}", lineNo, name));
                    continue;
                }

                result |= flag;
                any = true;
            }

            return any ? result : BuffProperty.None;
        }

        /// <summary>把当前内存里的配置写回文件（GM 编辑器保存时用）。</summary>
        public static void Save()
        {
            string path = FilePath;
            string dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("; ============================================================");
            sb.AppendLine("; 自定义 BUFF 属性表 —— 由 GM 工具「数据库 → 自定义BUFF属性」维护");
            sb.AppendLine("; 编码：UTF-8（无 BOM）；; 开头为注释");
            sb.AppendLine(";");
            sb.AppendLine("; [节名] 两种写法：");
            sb.AppendLine(";   数字 1~24  = 自定义 BUFF 槽位（对应 BuffType.Custom1~Custom24）");
            sb.AppendLine(";   枚举名     = 给已有 BUFF 追加属性（如 [隐身术]）");
            sb.AppendLine(";");
            sb.AppendLine("; 字段：");
            sb.AppendLine(";   Name     显示名（客户端 BUFF 提示框标题；自定义槽位必填）");
            sb.AppendLine(";   Icon     图标索引：0=客户端原有映射；10000+=Prguse2；20000+=MagIcon；40000+=MagIcon2");
            sb.AppendLine(";   Visible  是否对其他玩家可见（默认 True）");
            sb.AppendLine(";   Stack    堆叠方式：None/ResetDuration/StackDuration/StackStat/StackStatAndDuration/");
            sb.AppendLine(";            Infinite/ResetStat/ResetStatAndDuration/AttrStackStat/AttrStackStatAndDuration");
            sb.AppendLine(";   Property 标记（可用 | 叠加）：None/RemoveOnDeath/RemoveOnExit/Debuff/PauseInSafeZone");
            sb.AppendLine(";   Desc     说明文本");
            sb.AppendLine(";   Stat.属性=数值     数值写「基础」或「基础+每级」");
            sb.AppendLine(";            例：Stat.MaxAC=5+2  表示基础+5，施法者每级再+2");
            sb.AppendLine("; ============================================================");
            sb.AppendLine();

            for (int i = 0; i < Defs.Count; i++)
            {
                BuffDef def = Defs[i];

                sb.AppendLine(string.Format("[{0}]", def.Section));

                if (def.IsCustom || !string.IsNullOrEmpty(def.Name))
                    sb.AppendLine("Name=" + def.Name);

                if (def.Icon != 0) sb.AppendLine("Icon=" + def.Icon);
                if (!def.Visible) sb.AppendLine("Visible=False");
                if (def.Stack != BuffStackType.ResetDuration) sb.AppendLine("Stack=" + def.Stack);
                if (def.Property != BuffProperty.None) sb.AppendLine("Property=" + def.Property);
                if (!string.IsNullOrEmpty(def.Desc)) sb.AppendLine("Desc=" + def.Desc);

                for (int j = 0; j < def.Stats.Count; j++)
                {
                    BuffStatDef stat = def.Stats[j];
                    sb.AppendLine(string.Format("Stat.{0}={1}", stat.Stat, stat.ToText()));
                }

                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>配置文件不存在时生成一份带示例的模板（只在首次运行时发生）。</summary>
        private static void WriteTemplate(string path)
        {
            string dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("; 自定义 BUFF 属性表（示例模板）");
            sb.AppendLine("; [数字] = 自定义 BUFF 槽位 1~24；[枚举名] = 给已有 BUFF 追加属性");
            sb.AppendLine("; 数值写法：基础 或 基础+每级");
            sb.AppendLine();
            sb.AppendLine("[1]");
            sb.AppendLine("Name=示例BUFF");
            sb.AppendLine("Icon=30034");
            sb.AppendLine("Visible=True");
            sb.AppendLine("Stack=ResetDuration");
            sb.AppendLine("Desc=示例：基础+5 防御，每级再+2");
            sb.AppendLine("Stat.MaxAC=5+2");
            sb.AppendLine("Stat.MaxMAC=5+2");
            sb.AppendLine();
            sb.AppendLine("; 想给已有 BUFF 加属性就照下面这样写（去掉行首分号）");
            sb.AppendLine(";[隐身术]");
            sb.AppendLine(";Stat.攻击速度=3");

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            MessageQueue.Instance.Enqueue("已生成 BUFF 配置模板：" + path);
        }
    }
}
