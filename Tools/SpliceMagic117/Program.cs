using System;
using System.Globalization;
using System.IO;
using Server;
using Server.MirEnvir;

namespace SpliceMagic117
{
    /// <summary>
    /// 用"1"（v117 引擎）自带的程序集加载它的 Server.MirDB，
    /// 把 MagicInfoList 导出为制表符分隔的文本，供 v110 侧工具重新序列化。
    /// 每行: Name \t Spell \t BaseCost \t LevelCost \t Icon \t L1 \t L2 \t L3 \t
    ///        Need1 \t Need2 \t Need3 \t DelayBase \t DelayReduction \t
    ///        PowerBase \t PowerBonus \t MPowerBase \t MPowerBonus \t Range \t MultiplierBase \t MultiplierBonus
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("用法: SpliceMagic117 <Server.MirDB(v117)> <输出txt>");
                    return 1;
                }

                string dbPath = Path.GetFullPath(args[0]);
                string outPath = Path.GetFullPath(args[1]);

                string tempDir = Path.Combine(Path.GetTempPath(), "SpliceMagic117_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                File.Copy(dbPath, Path.Combine(tempDir, "Server.MirDB"));

                string oldCwd = Environment.CurrentDirectory;
                Environment.CurrentDirectory = tempDir;
                try
                {
                    var envir = Envir.Main;
                    if (!envir.LoadDB())
                    {
                        while (MessageQueue.Instance.MessageLog.TryDequeue(out var msg))
                            Console.Write("MQ: " + msg);
                        Console.WriteLine("LoadDB 失败");
                        return 2;
                    }

                    using var writer = new StreamWriter(outPath);
                    int count = 0;
                    foreach (var m in envir.MagicInfoList)
                    {
                        if (string.IsNullOrEmpty(m.Name)) continue;   // FillMagicInfoList 生成的默认条目
                        writer.WriteLine(string.Join("\t",
                            m.Name, (byte)m.Spell, m.BaseCost, m.LevelCost, m.Icon,
                            m.Level1, m.Level2, m.Level3,
                            m.Need1, m.Need2, m.Need3,
                            m.DelayBase, m.DelayReduction,
                            m.PowerBase, m.PowerBonus, m.MPowerBase, m.MPowerBonus,
                            m.Range,
                            m.MultiplierBase.ToString("R", CultureInfo.InvariantCulture),
                            m.MultiplierBonus.ToString("R", CultureInfo.InvariantCulture)));
                        count++;
                    }
                    Console.WriteLine($"导出技能 {count} 条 -> {outPath}");
                    return 0;
                }
                finally
                {
                    Environment.CurrentDirectory = oldCwd;
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("异常: " + ex);
                return 3;
            }
        }
    }
}
