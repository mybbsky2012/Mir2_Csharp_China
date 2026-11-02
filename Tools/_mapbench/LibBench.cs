using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace LibBench
{
    static class Program
    {
        static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                return output.ToArray();
            }
        }

        static void Main(string[] args)
        {
            string file = args.Length > 0 ? args[0] : @"D:\龙之传奇\Data\Map\WemadeMir2\Tiles.Lib";
            int sample = args.Length > 1 ? int.Parse(args[1]) : 400;

            var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs);

            int ver = br.ReadInt32();
            int count = br.ReadInt32();
            int frameSeek = 0;
            if (ver >= 3) frameSeek = br.ReadInt32();

            int[] index = new int[count];
            for (int i = 0; i < count; i++) index[i] = br.ReadInt32();

            Console.WriteLine(string.Format("{0}  ver={1}  图像数={2}  索引表={3:0.0}MB",
                Path.GetFileName(file), ver, count, count * 4 / 1048576.0));

            var rnd = new Random(12345);
            int n = Math.Min(sample, count);
            long totalRaw = 0, totalComp = 0;
            var sw = Stopwatch.StartNew();

            for (int k = 0; k < n; k++)
            {
                int i = rnd.Next(count);
                fs.Position = index[i];
                short w = br.ReadInt16();
                short h = br.ReadInt16();
                br.ReadInt16(); br.ReadInt16(); br.ReadInt16(); br.ReadInt16();
                byte shadow = br.ReadByte();
                int len = br.ReadInt32();
                byte[] comp = br.ReadBytes(len);
                byte[] raw = Decompress(comp);
                totalRaw += raw.Length;
                totalComp += len;
            }

            sw.Stop();
            Console.WriteLine(string.Format("随机解码 {0} 张：总 {1:0}ms，单张 {2:0.00}ms，平均压缩 {3:0}B → 解压 {4:0}B",
                n, sw.ElapsedMilliseconds, (double)sw.ElapsedMilliseconds / n, (double)totalComp / n, (double)totalRaw / n));

            // 顺序读 400 张（模拟首次进图，冷/热都算一遍）
            sw.Restart();
            for (int i = 0; i < Math.Min(400, count); i++)
            {
                fs.Position = index[i];
                br.ReadInt16(); br.ReadInt16();
                br.ReadInt16(); br.ReadInt16(); br.ReadInt16(); br.ReadInt16();
                br.ReadByte();
                int len2 = br.ReadInt32();
                byte[] comp = br.ReadBytes(len2);
                Decompress(comp);
            }
            sw.Stop();
            Console.WriteLine("顺序解码 400 张：" + sw.ElapsedMilliseconds + "ms");
        }
    }
}
