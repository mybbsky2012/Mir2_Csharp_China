using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;

namespace DropCheck;

/// <summary>
/// 掉落行解析自检：用服务端真实的 DropInfo.FromLine 跑一遍 Envir\Drops\*.txt，
/// 统计解析失败的行、以及带 Q（任务物品）标记的行数。
///
/// 用法：放到服务端目录（与 Server.MirDB、Envir 同级）运行
///   DropCheck.exe             —— 全量检查
///   DropCheck.exe 怪物名       —— 只看某个怪的掉落文件（省略 .txt）
///
/// 判定标准：失败行数应为 0；QuestRequired 行数应与数据里的 " Q" 行数一致。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        string only = args.Length > 0 ? args[0] : null;
        if (only != null && only.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            only = only.Substring(0, only.Length - 4);

        string dir = Path.Combine("Envir", "Drops");
        if (!Directory.Exists(dir))
        {
            Console.WriteLine("找不到 Envir\\Drops 目录");
            return 2;
        }

        var files = Directory.GetFiles(dir, "*.txt");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        int totalLines = 0, ok = 0, fail = 0, questOk = 0, questData = 0, skipped = 0;
        var failSamples = new List<string>();
        var questMismatch = new List<string>();

        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (only != null && !name.Equals(only, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (string raw in File.ReadAllLines(file))
            {
                string line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") ||
                    line == "{" || line == "}")
                {
                    skipped++;
                    continue;
                }

                totalLines++;

                DropInfo drop = null;
                try { drop = DropInfo.FromLine(line); }
                catch (Exception ex) { drop = null; if (failSamples.Count < 200) failSamples.Add($"{name}|异常 {ex.GetType().Name}: {line}"); }

                bool dataQuest = line.EndsWith(" Q", StringComparison.OrdinalIgnoreCase) ||
                                 line.Contains(" Q ");

                if (dataQuest) questData++;

                if (drop == null)
                {
                    fail++;
                    if (failSamples.Count < 200) failSamples.Add($"{name}|{line}");
                    continue;
                }

                ok++;
                if (drop.QuestRequired) questOk++;

                //数据里有 Q、解析后却没有 → Q 标记丢了（会变成「没接任务也掉」）
                if (dataQuest && !drop.QuestRequired && questMismatch.Count < 50)
                    questMismatch.Add($"{name}|{line}");
            }
        }

        Console.WriteLine($"掉落文件数 : {files.Length}");
        Console.WriteLine($"有效行数   : {totalLines}   （跳过空行/注释 {skipped}）");
        Console.WriteLine($"解析成功   : {ok}");
        Console.WriteLine($"解析失败   : {fail}");
        Console.WriteLine($"Q 行(数据) : {questData}");
        Console.WriteLine($"Q 行(解析) : {questOk}");
        Console.WriteLine();

        if (fail > 0)
        {
            Console.WriteLine("== 解析失败样例 ==");
            foreach (string s in failSamples) Console.WriteLine("  " + s);
            Console.WriteLine();
        }

        if (questMismatch.Count > 0)
        {
            Console.WriteLine("!! Q 标记丢失（数据有 Q，解析后未置位） ==");
            foreach (string s in questMismatch) Console.WriteLine("  " + s);
            Console.WriteLine();
        }

        bool pass = fail == 0 && questMismatch.Count == 0;
        Console.WriteLine(pass ? "RESULT: PASS" : "RESULT: FAIL");
        return pass ? 0 : 1;
    }
}
