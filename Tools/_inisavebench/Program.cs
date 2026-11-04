using System.Diagnostics;

// 复现 Settings.Save() 的真实开销，对比两种落盘策略：
//   A 旧行为：InIReader.Write 每写一个键就 File.WriteAllLines 整个文件（不进批量模式）
//   B 新行为：BeginBatch/EndBatch 把整段合并成一次写盘（Client/Settings.Save 现在用的方式）
// 参数：<iniPath> [writeCount=130] [rounds=2]
static class Program
{
    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "bench.ini";
        int writeCount = args.Length > 1 ? int.Parse(args[1]) : 130;
        int rounds = args.Length > 2 ? int.Parse(args[2]) : 2;

        if (!File.Exists(path))
        {
            Console.WriteLine("找不到文件：" + Path.GetFullPath(path));
            return 1;
        }

        string backup = path + ".bakbench";
        File.Copy(path, backup, true);

        try
        {
            List<(string Section, string Key)> keys = new List<(string, string)>();
            string cur = null;
            foreach (string line in File.ReadAllLines(path))
            {
                string s = line.Trim();
                if (s.StartsWith("[") && s.EndsWith("]")) { cur = s.Substring(1, s.Length - 2); continue; }
                if (cur == null) continue;
                int eq = s.IndexOf('=');
                if (eq <= 0) continue;
                keys.Add((cur, s.Substring(0, eq)));
            }

            Console.WriteLine(string.Format("文件 {0}  ({1} 行, {2} 个键)",
                Path.GetFullPath(path), File.ReadAllLines(path).Length, keys.Count));
            Console.WriteLine(string.Format("每次 Save() 写 {0} 个键，各跑 {1} 轮", writeCount, rounds));
            Console.WriteLine();

            InIReader reader = new InIReader(path);

            long legacyTotal = 0;
            Console.WriteLine("[A] 旧行为：每个键全量写盘");
            for (int round = 1; round <= rounds; round++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < writeCount; i++)
                {
                    var k = keys[i % keys.Count];
                    reader.Write(k.Section, k.Key, i);
                }
                sw.Stop();
                legacyTotal += sw.ElapsedMilliseconds;
                Console.WriteLine(string.Format("    第 {0} 轮：{1,6} ms", round, sw.ElapsedMilliseconds));
            }

            Console.WriteLine();
            long batchTotal = 0;
            Console.WriteLine("[B] 新行为：BeginBatch → 全部 Write → EndBatch 合并一次落盘");
            for (int round = 1; round <= rounds; round++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                reader.BeginBatch();
                try
                {
                    for (int i = 0; i < writeCount; i++)
                    {
                        var k = keys[i % keys.Count];
                        reader.Write(k.Section, k.Key, i);
                    }
                }
                finally
                {
                    reader.EndBatch();
                }
                sw.Stop();
                batchTotal += sw.ElapsedMilliseconds;
                Console.WriteLine(string.Format("    第 {0} 轮：{1,6} ms", round, sw.ElapsedMilliseconds));
            }

            Console.WriteLine();
            Console.WriteLine(string.Format("合计：旧 {0} ms  →  新 {1} ms（{2} 轮平均 {3} ms vs {4} ms）",
                legacyTotal, batchTotal, rounds,
                (legacyTotal / (double)rounds).ToString("0.00"),
                (batchTotal / (double)rounds).ToString("0.00")));

            // 再确认合并落盘后文件内容确实更新了（不是只改了内存）
            var lastKey = keys[(writeCount - 1) % keys.Count];
            string expect = lastKey.Key + "=" + (writeCount - 1);
            bool inSection = false;
            bool found = false;
            foreach (string line in File.ReadAllLines(path))
            {
                string s = line.Trim();
                if (s.StartsWith("[") && s.EndsWith("]"))
                {
                    inSection = s == "[" + lastKey.Section + "]";
                    continue;
                }
                if (inSection && s == expect) { found = true; break; }
            }

            Console.WriteLine("落盘校验：" + (found
                ? "OK，文件里已是新值 [" + lastKey.Section + "] " + expect
                : "失败！文件里没有 " + expect));
        }
        finally
        {
            File.Copy(backup, path, true);
            File.Delete(backup);
        }

        return 0;
    }
}
