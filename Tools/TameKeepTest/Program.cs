using System.Text;
using System.Text.RegularExpressions;
using Server;
using Server.MirEnvir;

namespace TameKeepTest
{
    /// <summary>
    /// 「诱惑宠脱主时间」自测（20261106）。
    ///
    /// 分两块：
    ///  1) 运行时语义 —— 直接引用 Server.Library.dll，调真实的 Settings.TameExpireTime，
    ///     验证 0 = 永不脱主、60 = 1 小时、上限 7 天不溢出。这部分不是文本扫描，改坏了会真报错。
    ///  2) 源码约束 —— 配置读写/界面/两处召唤点/到期判定/主人死亡链路/存档格式未变。
    ///     最后一块专门盯着「别为了脱主时间去动存档格式」：一旦 PetInfo.Save 里出现 TameTime，
    ///     说明存档版本被抬了，旧版服务端会读不了新存档。
    /// </summary>
    internal static class Program
    {
        static int pass, fail;
        static string root;

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            root = args.Length > 0 ? args[0] : FindRoot();

            Console.WriteLine("工程根目录：" + root);
            Console.WriteLine(new string('-', 72));

            ConfigChecks();
            RuntimeChecks();
            LifetimeChecks();
            UiChecks();
            ArchiveChecks();
            ProductChecks();

            Console.WriteLine(new string('-', 72));
            Console.WriteLine("结果：通过 " + pass + "，失败 " + fail);
            return fail == 0 ? 0 : 1;
        }

        // ---------------- 配置层 ----------------

        static void ConfigChecks()
        {
            Section("配置层");

            var settings = Read("Server/Settings.cs");
            var human = Read("Server/MirObjects/HumanObject.cs");
            var wolf = Read("Server/MirObjects/Monsters/SnowWolfKing.cs");

            Check("Settings 声明 TameKeepMinutes，默认 60（原版值）",
                Regex.IsMatch(settings, @"public\s+static\s+int\s+TameKeepMinutes\s*=\s*60\s*;"));

            Check("Load 读 [Optional] TameKeepMinutes",
                settings.Contains("Reader.ReadInt32(\"Optional\", \"TameKeepMinutes\", TameKeepMinutes)"));

            Check("Save 写 [Optional] TameKeepMinutes",
                settings.Contains("Reader.Write(\"Optional\", \"TameKeepMinutes\", TameKeepMinutes)"));

            Check("Load 后钳制下限 0（0 = 永不脱主）",
                Regex.IsMatch(settings, @"if\s*\(\s*TameKeepMinutes\s*<\s*0\s*\)\s*TameKeepMinutes\s*=\s*0\s*;"));

            Check("Load 后钳制上限 10080（7 天）",
                Regex.IsMatch(settings, @"if\s*\(\s*TameKeepMinutes\s*>\s*10080\s*\)\s*TameKeepMinutes\s*=\s*10080\s*;"));

            Check("Settings 提供 TameExpireTime(now) 统一出口",
                Regex.IsMatch(settings, @"public\s+static\s+long\s+TameExpireTime\s*\(\s*long\s+now\s*\)"));

            Check("TameExpireTime 在 0 时返回 0（永不脱主）",
                Regex.IsMatch(settings, @"if\s*\(\s*TameKeepMinutes\s*<=\s*0\s*\)\s*return\s+0\s*;"));

            Check("HumanObject 召唤走 Settings.TameExpireTime（不再写死 60）",
                human.Contains("target.TameTime = Settings.TameExpireTime(Envir.Time);"));

            Check("SnowWolfKing 召唤走 Settings.TameExpireTime（不再写死 60）",
                wolf.Contains("mob.TameTime = Settings.TameExpireTime(Envir.Time);"));

            int hardcoded = CountMatches(Read("Server/MirObjects/HumanObject.cs"), @"Settings\.Minute\s*\*\s*60")
                          + CountMatches(Read("Server/MirObjects/Monsters/SnowWolfKing.cs"), @"Settings\.Minute\s*\*\s*60");

            Check("两处召唤点无残留 `Settings.Minute * 60` 魔数（实得 " + hardcoded + " 处）", hardcoded == 0);

            Check("脱主时间只在 PetSave=False 时生效（PetSave=True 时宠物本就永久）",
                Regex.IsMatch(human, @"if\s*\(\s*!\s*Settings\.PetSave\s*\)\s*\{\s*(//[^\r\n]*\r?\n\s*)*target\.TameTime\s*=\s*Settings\.TameExpireTime"));
        }

        // ---------------- 运行时语义 ----------------

        static void RuntimeChecks()
        {
            Section("运行时语义（真实调用 Settings.TameExpireTime）");

            const long now = 1_000_000L;

            Settings.TameKeepMinutes = 0;
            Check("0 分钟 → 0（永不脱主）", Settings.TameExpireTime(now) == 0L);

            Settings.TameKeepMinutes = 60;
            Check("60 分钟 → now + 1 小时（与原版一致）", Settings.TameExpireTime(now) == now + 3_600_000L);

            Settings.TameKeepMinutes = 1;
            Check("1 分钟 → now + 60 秒", Settings.TameExpireTime(now) == now + 60_000L);

            Settings.TameKeepMinutes = 1440;
            Check("1440 分钟（24 小时）→ now + 86,400,000", Settings.TameExpireTime(now) == now + 86_400_000L);

            Settings.TameKeepMinutes = 10080;
            var max = Settings.TameExpireTime(now);
            Check("10080 分钟（7 天上限）不溢出，差值正确", max == now + 604_800_000L);

            Settings.TameKeepMinutes = -5;
            Check("负数兜底按永不脱主处理（防手改 ini 写负值）", Settings.TameExpireTime(now) == 0L);

            Settings.TameKeepMinutes = 60;
        }

        // ---------------- 寿命与脱主链路 ----------------

        static void LifetimeChecks()
        {
            Section("脱主与主人死亡链路");

            var monster = Read("Server/MirObjects/MonsterObject.cs");
            var player = Read("Server/MirObjects/PlayerObject.cs");

            Check("到期判定保留 `TameTime > 0`（0 永不到期）",
                Regex.IsMatch(monster, @"if\s*\(\s*Master\s*!=\s*null\s*&&\s*TameTime\s*>\s*0\s*&&\s*Envir\.Time\s*>\s*=\s*TameTime\s*\)"));

            var block = ExtractBlock(monster, "if (Master != null && TameTime > 0 && Envir.Time >= TameTime)");
            Check("到期后从主人宠物表移除并脱主",
                block.Contains("Master.Pets.Remove(this)") && block.Contains("Master = null"));

            Check("主人死亡：Die() 里无条件让所有宠物死亡（不受脱主时间影响）",
                Regex.IsMatch(player, @"for\s*\(\s*int\s+i\s*=\s*Pets\.Count\s*-\s*1;\s*i\s*>=\s*0;\s*i--\s*\)\s*\{\s*if\s*\(\s*Pets\[i\]\.Dead\s*\)\s*continue;\s*Pets\[i\]\.Die\(\);"));

            Check("主人下线：PetDieWithMaster 分支存在（下线即消失）",
                player.Contains("if (Settings.PetDieWithMaster && !IsClonePet(pet))"));

            Check("下线存档时 0（永不脱主）原样存 0，不写负数",
                player.Contains("TameTime = pet.TameTime > 0 ? pet.TameTime - Envir.Time : 0"));

            Check("重登恢复时 0 仍为 0，不会 Envir.Time + 0 立刻过期",
                player.Contains("monster.TameTime = info.TameTime > 0 ? Envir.Time + info.TameTime : 0"));
        }

        // ---------------- 界面 ----------------

        static void UiChecks()
        {
            Section("配置界面（其他选项 → 特殊设置 → 诱惑之光）");

            var form = Read("Server.MirForms/ConfigForm.cs");

            Check("声明 TameKeepMinutesUpDown 输入框",
                Regex.IsMatch(form, @"private\s+NumericUpDown\s+TameKeepMinutesUpDown\s*;"));

            Check("输入框范围 0 ~ 10080（0 = 永不脱主，上限 7 天）",
                Regex.IsMatch(form, @"TameKeepMinutesUpDown\s*=\s*new\s+NumericUpDown\s*\{[^}]*Minimum\s*=\s*0[^}]*Maximum\s*=\s*10080", RegexOptions.Singleline));

            Check("打开配置页时回填当前值（越界夹取）",
                form.Contains("TameKeepMinutesUpDown.Value = Clamp(Settings.TameKeepMinutes, 0, 10080);"));

            Check("保存时写回 Settings.TameKeepMinutes（越界夹取）",
                form.Contains("Settings.TameKeepMinutes = Clamp((int)TameKeepMinutesUpDown.Value, 0, 10080);"));

            Check("数值变化联动提示文案",
                form.Contains("TameKeepMinutesUpDown.ValueChanged += (o, e) => SyncTameKeepUi();"));

            Check("提示文案覆盖三种语义（0 / 60 / 其它）",
                form.Contains("0 = 永不脱主") && form.Contains("60 分钟 = 原版默认") && form.Contains("分钟后自动脱主"));

            Check("控件挂到诱惑之光页上",
                form.Contains("page.Controls.Add(keepGroup);"));

            Check("与「下线存档」分组不重叠（存档组在脱主组下方）",
                Regex.IsMatch(form, @"Text\s*=\s*""下线存档""[^}]*Location\s*=\s*new\s+Point\(12,\s*412\)", RegexOptions.Singleline));

            Check("提供「永久（设为 0）」快捷按钮",
                form.Contains("永久（设为 0）"));
        }

        // ---------------- 存档格式 ----------------

        static void ArchiveChecks()
        {
            Section("存档格式（必须保持原样，否则旧版服务端读不了新存档）");

            Check("Envir.Version 仍为 111（没为了脱主时间抬版本）", Envir.Version == 111);

            var info = Read("Server/MirDatabase/CharacterInfo.cs");

            // 文件里有多个 Save(BinaryWriter)，必须先切到 PetInfo 类里再取块，否则测的是 CharacterInfo.Save
            int petClassStart = info.IndexOf("public class PetInfo", StringComparison.Ordinal);
            Check("能定位到 PetInfo 类", petClassStart >= 0);

            var petClass = petClassStart >= 0 ? info.Substring(petClassStart) : info;
            var petSave = ExtractBlock(petClass, "public void Save(BinaryWriter writer)");
            var petLoad = ExtractBlock(petClass, "public PetInfo(BinaryReader reader");

            Check("PetInfo.Save 不写 TameTime（存档格式未变）", !petSave.Contains("TameTime"));
            Check("PetInfo 构造函数不读 TameTime（读取顺序未变）", !petLoad.Contains("TameTime"));
        }

        // ---------------- 产物 ----------------

        static void ProductChecks()
        {
            Section("构建产物");

            var src = Path.Combine(root, "Server", "Settings.cs");
            var binOut = Path.Combine(root, "Server", "bin", "Release", "Server.Library.dll");
            var distOut = Path.Combine(root, "Build", "Server", "Release", "Server.Library.dll");

            Check("Server/bin/Release/Server.Library.dll 存在", File.Exists(binOut));
            Check("Build/Server/Release/Server.Library.dll 存在", File.Exists(distOut));

            if (File.Exists(binOut) && File.Exists(src))
                Check("bin 产物新于 Settings.cs（已重新编译）",
                    File.GetLastWriteTimeUtc(binOut) >= File.GetLastWriteTimeUtc(src));

            if (File.Exists(binOut) && File.Exists(distOut))
                Check("Build 产物与 bin 产物一致（MD5 相同，避免部署旧版本）",
                    Md5(binOut) == Md5(distOut));
        }

        // ---------------- 工具 ----------------

        static void Section(string name)
        {
            Console.WriteLine();
            Console.WriteLine("【" + name + "】");
        }

        static void Check(string name, bool ok)
        {
            if (ok) { pass++; Console.WriteLine("  PASS  " + name); }
            else { fail++; Console.WriteLine("  FAIL  " + name); }
        }

        static string Read(string relative)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("找不到文件：" + path);
            return File.ReadAllText(path);
        }

        static int CountMatches(string text, string pattern)
        {
            return Regex.Matches(text, pattern).Count;
        }

        /// <summary>从某一行开始，截取大括号配平的整块（用来只看某个方法体）。</summary>
        static string ExtractBlock(string text, string header)
        {
            int i = text.IndexOf(header, StringComparison.Ordinal);
            if (i < 0) return string.Empty;

            int start = text.IndexOf('{', i);
            if (start < 0) return string.Empty;

            int depth = 0;
            for (int p = start; p < text.Length; p++)
            {
                if (text[p] == '{') depth++;
                else if (text[p] == '}')
                {
                    depth--;
                    if (depth == 0) return text.Substring(start, p - start + 1);
                }
            }

            return text.Substring(start);
        }

        static string Md5(string path)
        {
            using var stream = File.OpenRead(path);
            using var md5 = System.Security.Cryptography.MD5.Create();
            return Convert.ToHexString(md5.ComputeHash(stream));
        }

        /// <summary>从可执行文件所在目录向上找含 Server/Settings.cs 的目录，避免写死盘符。</summary>
        static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Server", "Settings.cs")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return @"D:\6b";
        }
    }
}
