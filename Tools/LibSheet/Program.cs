using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

// 只读工具：把 .Lib 里指定区间的图像拼成一张带编号的缩略图总览，用来肉眼确认
// 「第几帧到第几帧是攻击/特效/尸体」——客户端特效分支里的魔法数字就是这么定出来的。
//
// 用法： LibSheet.exe <lib路径> <起始索引> <结束索引> <输出png> [每行个数=10] [单元宽=96] [单元高=120]
// 输出： png 缩略图；同时打印区间内为空的索引段（这些索引没有图像）
//
// .Lib 结构（与 Client/MirGraphics/MLibrary.cs 一致）：
//   int32 version / int32 count / [v>=3] int32 frameSeek / int32[count] 索引偏移表
//   每个条目：Width,Height,X,Y,ShadowX,ShadowY (int16×6) + Shadow(byte) + Length(int32)
//   紧随其后 Length 字节 = gzip 压缩的 Width*Height*4 裸像素（BGRA）
internal static class Program
{
    private static byte[] _all;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length < 4)
        {
            Console.WriteLine("用法：LibSheet.exe <lib路径> <起始索引> <结束索引> <输出png> [每行=10] [单元宽=96] [单元高=120]");
            Console.WriteLine("      LibSheet.exe <lib路径> --runs <起始索引> <结束索引>   （只列连续非空帧段和尺寸，不导出图）");
            return 2;
        }

        string libPath = args[0];

        if (args.Length >= 4 && args[1].Equals("--runs", StringComparison.OrdinalIgnoreCase))
        {
            return ListRuns(libPath, int.Parse(args[2]), int.Parse(args[3]));
        }

        if (args.Length >= 4 && args[1].Equals("--export", StringComparison.OrdinalIgnoreCase))
        {
            return ExportFrames(libPath, args[2], args.Skip(3).ToArray());
        }
        int start = int.Parse(args[1]);
        int end = int.Parse(args[2]);
        string outPng = args[3];
        int cols = args.Length > 4 ? int.Parse(args[4]) : 10;
        int cellW = args.Length > 5 ? int.Parse(args[5]) : 96;
        int cellH = args.Length > 6 ? int.Parse(args[6]) : 120;

        if (!File.Exists(libPath))
        {
            Console.WriteLine("找不到文件：" + Path.GetFullPath(libPath));
            return 2;
        }

        _all = File.ReadAllBytes(libPath);

        int count, frameSeek;
        int[] indexList = ReadIndexList(out count, out frameSeek);

        Console.WriteLine($"图库 {Path.GetFileName(libPath)}：图像总数 {count}");

        start = Math.Max(0, start);
        end = Math.Min(count - 1, end);
        if (end < start)
        {
            Console.WriteLine("区间无效");
            return 1;
        }

        int n = end - start + 1;
        int rows = (n + cols - 1) / cols;
        int labelH = 14;

        using var sheet = new Bitmap(cols * cellW, rows * (cellH + labelH), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(40, 40, 48));

            using var font = new Font("Consolas", 8f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var brushIndex = new SolidBrush(Color.FromArgb(200, 230, 230, 130));
            using var brushEmpty = new SolidBrush(Color.FromArgb(90, 90, 100));
            using var penGrid = new Pen(Color.FromArgb(70, 70, 80));

            int emptyCount = 0;
            var emptyList = new List<int>();

            for (int i = 0; i < n; i++)
            {
                int idx = start + i;
                int cx = (i % cols) * cellW;
                int cy = (i / cols) * (cellH + labelH);

                g.DrawRectangle(penGrid, cx, cy, cellW - 1, cellH + labelH - 1);
                g.DrawString(idx.ToString(), font, brushIndex, cx + 2, cy + cellH + 1);

                using Bitmap bmp = Decode(indexList[idx]);
                if (bmp == null)
                {
                    emptyCount++;
                    emptyList.Add(idx);
                    g.FillRectangle(brushEmpty, cx + cellW / 2 - 3, cy + cellH / 2 - 1, 6, 2);
                    continue;
                }

                float scale = Math.Min((cellW - 4f) / bmp.Width, (cellH - 4f) / bmp.Height);
                int dw = Math.Max(1, (int)(bmp.Width * scale));
                int dh = Math.Max(1, (int)(bmp.Height * scale));

                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(bmp, cx + (cellW - dw) / 2, cy + (cellH - dh) / 2, dw, dh);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPng)));
            sheet.Save(outPng, ImageFormat.Png);

            Console.WriteLine($"已输出 {outPng}  （{cols} 列 × {rows} 行，共 {n} 帧）");
            Console.WriteLine($"空索引 {emptyCount} 个{(emptyCount > 0 ? "：" + JoinRanges(emptyList) : "")}");
        }

        return 0;
    }

    /// <summary>按帧段导出 PNG，并写一份 frames.js 记录每帧的绘制偏移（X/Y）与尺寸，供预览页合成用</summary>
    private static int ExportFrames(string libPath, string outDir, string[] specs)
    {
        if (!File.Exists(libPath))
        {
            Console.WriteLine("找不到文件：" + Path.GetFullPath(libPath));
            return 2;
        }

        _all = File.ReadAllBytes(libPath);

        int count, frameSeek;
        int[] indexList = ReadIndexList(out count, out frameSeek);

        Directory.CreateDirectory(outDir);

        var wanted = new List<int>();
        foreach (string spec in specs)
        {
            foreach (string part in spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = part.Trim();
                int dash = s.IndexOf('-');
                if (dash > 0)
                {
                    int a = int.Parse(s.Substring(0, dash));
                    int b = int.Parse(s.Substring(dash + 1));
                    for (int i = a; i <= b; i++) if (!wanted.Contains(i)) wanted.Add(i);
                }
                else
                {
                    int a = int.Parse(s);
                    if (!wanted.Contains(a)) wanted.Add(a);
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append("window.LIBFRAMES = {\n");

        int ok = 0, skipped = 0;
        bool first = true;

        foreach (int i in wanted)
        {
            if (i < 0 || i >= count)
            {
                skipped++;
                continue;
            }

            using Bitmap bmp = Decode(indexList[i]);
            if (bmp == null || bmp.Width <= 1 || bmp.Height <= 1)
            {
                skipped++;
                continue;
            }

            GetOffsets(indexList[i], out int ox, out int oy);

            bmp.Save(Path.Combine(outDir, i + ".png"), ImageFormat.Png);
            ok++;

            if (!first) sb.Append(",\n");
            first = false;
            sb.AppendFormat("  \"{0}\": {{\"x\":{1},\"y\":{2},\"w\":{3},\"h\":{4}}}", i, ox, oy, bmp.Width, bmp.Height);
        }

        sb.Append("\n};\n");
        File.WriteAllText(Path.Combine(outDir, "frames.js"), sb.ToString(), new UTF8Encoding(false));

        Console.WriteLine($"已导出 {ok} 帧到 {outDir}（跳过 {skipped}）");
        Console.WriteLine("已写出 frames.js");
        return 0;
    }

    private static void GetOffsets(int off, out int ox, out int oy)
    {
        ox = BitConverter.ToInt16(_all, off + 4);
        oy = BitConverter.ToInt16(_all, off + 6);
    }

    /// <summary>只读某索引的尺寸头，不解析像素数据</summary>
    private static bool GetSize(int off, out int w, out int h)
    {
        w = h = 0;
        if (off <= 0 || off + 17 > _all.Length) return false;

        w = BitConverter.ToInt16(_all, off);
        h = BitConverter.ToInt16(_all, off + 2);
        return true;
    }

    /// <summary>只读索引表</summary>
    private static int[] ReadIndexList(out int count, out int frameSeek)
    {
        using var ms = new MemoryStream(_all);
        using var reader = new BinaryReader(ms);

        int version = reader.ReadInt32();
        count = reader.ReadInt32();
        frameSeek = version >= 3 ? reader.ReadInt32() : 0;

        int[] indexList = new int[count];
        for (int i = 0; i < count; i++) indexList[i] = reader.ReadInt32();
        return indexList;
    }

    /// <summary>列出区间内「连续非空帧」的段，每段给出起止和第一帧尺寸 —— 用来判断特效图各段的边界</summary>
    private static int ListRuns(string libPath, int start, int end)
    {
        if (!File.Exists(libPath))
        {
            Console.WriteLine("找不到文件：" + Path.GetFullPath(libPath));
            return 2;
        }

        _all = File.ReadAllBytes(libPath);

        int count, frameSeek;
        int[] indexList = ReadIndexList(out count, out frameSeek);

        Console.WriteLine($"图库 {Path.GetFileName(libPath)}：图像总数 {count}");
        Console.WriteLine();

        start = Math.Max(0, start);
        end = Math.Min(count - 1, end);

        int runStart = -1;
        int total = 0;

        for (int i = start; i <= end + 1; i++)
        {
            bool has = i <= end && GetSize(indexList[i], out int fw, out int fh) && fw > 0 && fh > 0;

            if (has && runStart < 0)
            {
                runStart = i;
                continue;
            }

            if (has || runStart < 0) continue;

            // 段结束于 i-1
            int a = runStart, b = i - 1;
            int len = b - a + 1;
            total += len;

            string size;
            using (Bitmap bmp = Decode(indexList[a]))
                size = bmp == null ? "解码失败" : (bmp.Width + "x" + bmp.Height);

            Console.WriteLine(string.Format("  {0,5} - {1,5}   共 {2,4} 帧   首帧尺寸 {3}", a, b, len, size));

            runStart = -1;
        }

        Console.WriteLine();
        Console.WriteLine($"区间内非空帧合计 {total} 个");
        return 0;
    }

    /// <summary>把空索引压成「起-止」区间，方便看哪些段是空的</summary>
    private static string JoinRanges(List<int> list)
    {
        if (list.Count == 0) return "";
        var sb = new StringBuilder();
        int s = list[0], p = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i] == p + 1) { p = list[i]; continue; }
            sb.Append(s == p ? s.ToString() : (s + "-" + p)).Append(' ');
            s = p = list[i];
        }
        sb.Append(s == p ? s.ToString() : (s + "-" + p));
        return sb.ToString();
    }

    private static Bitmap Decode(int off)
    {
        if (off <= 0 || off + 17 > _all.Length) return null;

        using var ms = new MemoryStream(_all) { Position = off };
        using var reader = new BinaryReader(ms);

        short w = reader.ReadInt16();
        short h = reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadByte();
        int length = reader.ReadInt32();

        if (w <= 0 || h <= 0 || length <= 0) return null;

        long dataPos = (long)off + 17;
        if (dataPos + length > _all.Length) return null;

        try
        {
            byte[] raw = new byte[length];
            Array.Copy(_all, dataPos, raw, 0, length);

            byte[] pixels = Gunzip(raw, w * h * 4);
            if (pixels == null) return null;

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(pixels, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4);
            }
            finally
            {
                bmp.UnlockBits(bd);
            }

            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Gunzip(byte[] compressed, int expectedSize)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        byte[] buffer = new byte[expectedSize];
        int read = 0;
        while (read < expectedSize)
        {
            int n = gz.Read(buffer, read, expectedSize - read);
            if (n <= 0) break;
            read += n;
        }
        return read == expectedSize ? buffer : null;
    }
}
