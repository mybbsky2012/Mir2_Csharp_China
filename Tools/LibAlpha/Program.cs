using System.IO.Compression;
using System.Text;

// 只读诊断工具：统计 .Lib 内每帧的 alpha 分布，用来判断
// 「黑色区域变透明」到底是素材本身 alpha 被抠掉了，还是客户端混合方式的问题。
//
// 用法： LibAlpha.exe <lib路径> [帧索引...]        不给帧索引就取前若干帧 + 均匀采样
//
// 输出：每帧 尺寸 / A=0 数 / A=0 但 RGB 不为 0 的数（=被抠掉的实色像素） /
//       A 半透明数 / 抽样像素（RGB, A）对照
internal static class Program
{
    private static byte[] _all;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length < 1)
        {
            Console.WriteLine("用法：LibAlpha.exe <lib路径> [帧索引...]");
            return 2;
        }

        string path = args[0];
        if (!File.Exists(path))
        {
            Console.WriteLine("找不到：" + path);
            return 2;
        }

        _all = File.ReadAllBytes(path);
        using var ms = new MemoryStream(_all);
        using var reader = new BinaryReader(ms);
        int version = reader.ReadInt32();
        int count = reader.ReadInt32();
        int frameSeek = version >= 3 ? reader.ReadInt32() : 0;
        int[] index = new int[count];
        for (int i = 0; i < count; i++) index[i] = reader.ReadInt32();

        Console.WriteLine($"文件 {Path.GetFileName(path)}  version={version}  count={count}  frameSeek={frameSeek}");

        List<int> frames = new List<int>();
        if (args.Length > 1)
        {
            for (int i = 1; i < args.Length; i++) frames.Add(int.Parse(args[i]));
        }
        else
        {
            int step = Math.Max(1, count / 12);
            for (int i = 0; i < count; i += step) frames.Add(i);
        }

        Console.WriteLine();
        Console.WriteLine("帧    尺寸        像素       A=0      A=0但RGB≠0   半透明数    行内空洞    行内半透明   暗实色      平均色");
        Console.WriteLine("----------------------------------------------------------------------------------------------------");

        foreach (int f in frames)
        {
            if (f < 0 || f >= count) continue;
            Stat(f, index[f]);
        }

        return 0;
    }

    private static void Stat(int frame, int off)
    {
        if (off <= 0 || off + 17 > _all.Length)
        {
            Console.WriteLine($"{frame,-5} <空索引>");
            return;
        }

        using var ms = new MemoryStream(_all) { Position = off };
        using var reader = new BinaryReader(ms);
        short w = reader.ReadInt16();
        short h = reader.ReadInt16();
        reader.ReadInt16(); reader.ReadInt16(); reader.ReadInt16(); reader.ReadInt16();
        reader.ReadByte();
        int length = reader.ReadInt32();

        // 顺带看第二层（mask）
        try
        {
            using var msH = new MemoryStream(_all) { Position = off };
            using var rH = new BinaryReader(msH);
            rH.ReadInt16(); rH.ReadInt16(); rH.ReadInt16(); rH.ReadInt16(); rH.ReadInt16(); rH.ReadInt16();
            byte shadow = rH.ReadByte();
            int l1 = rH.ReadInt32();
            bool hasMask = (shadow >> 7) == 1;
            Console.Write($"[mask={(hasMask ? "有" : "无")} shadow={shadow} len={l1}] ");
            if (hasMask)
            {
                msH.Position = off + 17 + l1;
                short mw = rH.ReadInt16(), mh = rH.ReadInt16();
                rH.ReadInt16(); rH.ReadInt16();
                int ml = rH.ReadInt32();
                Console.Write($"[mask层 {mw}x{mh} len={ml}] ");
                if (ml > 0 && msH.Position + ml <= _all.Length)
                {
                    byte[] mp = new byte[ml];
                    Array.Copy(_all, (int)msH.Position, mp, 0, ml);
                    using var gz2 = new GZipStream(new MemoryStream(mp), CompressionMode.Decompress);
                    using var o2 = new MemoryStream();
                    gz2.CopyTo(o2);
                    byte[] mpx = o2.ToArray();
                    int n2 = Math.Min(mw * mh, mpx.Length / 4);
                    int z2 = 0, semi2 = 0, col2 = 0;
                    for (int i = 0; i < n2; i++)
                    {
                        int q = i * 4;
                        if (mpx[q + 3] == 0) { z2++; if (mpx[q] != 0 || mpx[q + 1] != 0 || mpx[q + 2] != 0) col2++; }
                        else if (mpx[q + 3] < 255) semi2++;
                    }
                    Console.Write($"mask层 A=0:{z2} 半透:{semi2} 有色无声:{col2} ");
                }
            }
            Console.WriteLine();
        }
        catch (Exception ex) { Console.WriteLine("[mask 读取失败] " + ex.Message); }

        long dataPos = (long)off + 17;
        if (w <= 0 || h <= 0 || length <= 0 || dataPos + length > _all.Length)
        {
            Console.WriteLine($"{frame,-5} <无效 {w}x{h} len={length}>");
            return;
        }

        byte[] packed = new byte[length];
        Array.Copy(_all, dataPos, packed, 0, length);

        byte[] px;
        try
        {
            using var gz = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            px = outMs.ToArray();
        }
        catch
        {
            Console.WriteLine($"{frame,-5} <解压失败>");
            return;
        }

        if (Environment.GetEnvironmentVariable("LIBALPHA_MAP") == "1")
        {
            int cols = 64, rows = 32;
            var sb = new StringBuilder();
            for (int ry = 0; ry < rows; ry++)
            {
                for (int rx = 0; rx < cols; rx++)
                {
                    int x = rx * w / cols, y = ry * h / rows;
                    int p0 = (y * w + x) * 4;
                    if (p0 + 3 >= px.Length) { sb.Append('?'); continue; }
                    byte a = px[p0 + 3];
                    sb.Append(a == 0 ? '.' : a == 255 ? '#' : '+');
                }
                sb.AppendLine();
            }
            Console.WriteLine(sb.ToString());
        }

        Console.Write($"[解压后 {px.Length} 字节，应为 {w * h * 4}] ");
        int total = w * h;
        int aZero = 0, aZeroColor = 0, aPartial = 0, aFull = 0;
        long rSum = 0, gSum = 0, bSum = 0;
        int lumMatch = 0, lumSampled = 0;

        // 内部空洞：每一行在「第一个不透明像素」到「最后一个不透明像素」之间，
        // alpha=0 的像素就是被抠掉的洞（背景透明只会出现在两端，不会在中间）
        int holes = 0, bodySpan = 0, semiInBody = 0, darkOpaque = 0;
        var alphaHist = new int[8];

        int maxPixels = Math.Min(total, px.Length / 4);

        for (int y = 0; y < h; y++)
        {
            int rowStart = -1, rowEnd = -1;
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 4;
                if (p + 3 >= px.Length) break;
                if (px[p + 3] != 0) { if (rowStart < 0) rowStart = x; rowEnd = x; }
            }
            if (rowStart < 0) continue;

            bodySpan += rowEnd - rowStart + 1;
            for (int x = rowStart; x <= rowEnd; x++)
            {
                int p = (y * w + x) * 4;
                byte a = px[p + 3];
                if (a == 0) { holes++; continue; }
                if (a < 255) semiInBody++;
            }
        }

        for (int i = 0; i < maxPixels; i++)
        {
            int p = i * 4;
            byte b = px[p], g = px[p + 1], r = px[p + 2], a = px[p + 3];

            alphaHist[a >> 5]++;
            if (a > 0 && Math.Max(r, Math.Max(g, b)) <= 12) darkOpaque++;

            rSum += r; gSum += g; bSum += b;

            if (a == 0)
            {
                aZero++;
                if (r != 0 || g != 0 || b != 0) aZeroColor++;
            }
            else if (a == 255) aFull++;
            else aPartial++;

            // 抽样判断：alpha 是否约等于亮度/最大值（说明是「按亮度生成 alpha」的素材）
            if ((i & 31) == 0)
            {
                lumSampled++;
                int maxC = Math.Max(r, Math.Max(g, b));
                int lum = (r * 30 + g * 59 + b * 11) / 100;
                if (Math.Abs(a - maxC) <= 8 || Math.Abs(a - lum) <= 8) lumMatch++;
            }
        }

        double avgR = maxPixels > 0 ? (double)rSum / maxPixels : 0;
        double avgG = maxPixels > 0 ? (double)gSum / maxPixels : 0;
        double avgB = maxPixels > 0 ? (double)bSum / maxPixels : 0;
        double lumPct = lumSampled > 0 ? 100.0 * lumMatch / lumSampled : 0;

        // 主色统计：看看「看起来发白的小点」到底是纯白像素还是透明
        var colorCount = new Dictionary<string, int>();
        for (int i = 0; i < maxPixels; i++)
        {
            int p = i * 4;
            if (px[p + 3] == 0) continue;
            string key = $"{px[p + 2]},{px[p + 1]},{px[p]}";
            colorCount.TryGetValue(key, out int c);
            colorCount[key] = c + 1;
        }
        var top = colorCount.OrderByDescending(kv => kv.Value).Take(6)
                            .Select(kv => kv.Key + " x" + kv.Value);
        Console.WriteLine("      主色(RGB)： " + string.Join("  ", top));

        // 4 连通洪水填充：从图像四边出发能走到的透明像素算「背景」，
        // 走不到的透明像素 = 被身体包住的空洞（黑底被抠掉的痕迹）
        int enclosed = 0;
        {
            var visited = new bool[w * h];
            var stack = new Stack<int>();
            for (int x = 0; x < w; x++)
            {
                int t = x, b = (h - 1) * w + x;
                if (px[t * 4 + 3] == 0 && !visited[t]) { visited[t] = true; stack.Push(t); }
                if (px[b * 4 + 3] == 0 && !visited[b]) { visited[b] = true; stack.Push(b); }
            }
            for (int y = 0; y < h; y++)
            {
                int l = y * w, r = y * w + w - 1;
                if (px[l * 4 + 3] == 0 && !visited[l]) { visited[l] = true; stack.Push(l); }
                if (px[r * 4 + 3] == 0 && !visited[r]) { visited[r] = true; stack.Push(r); }
            }
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                int cx = cur % w, cy = cur / w;
                if (cx > 0) Try(cur - 1);
                if (cx < w - 1) Try(cur + 1);
                if (cy > 0) Try(cur - w);
                if (cy < h - 1) Try(cur + w);
            }
            void Try(int n)
            {
                if (n < 0 || n >= w * h || visited[n]) return;
                if (px[n * 4 + 3] != 0) return;
                visited[n] = true;
                stack.Push(n);
            }
            for (int i = 0; i < w * h; i++)
                if (px[i * 4 + 3] == 0 && !visited[i]) enclosed++;
        }

        string hist = string.Join(" ", alphaHist.Select((v, i) => $"{(i * 32)}-{i * 32 + 31}:{v}"));
        Console.WriteLine("      alpha 分布： " + hist);

        double holePct = bodySpan > 0 ? 100.0 * holes / bodySpan : 0;
        double semiPct = bodySpan > 0 ? 100.0 * semiInBody / bodySpan : 0;

        Console.WriteLine($"{frame,-5} {w}x{h,-6} {maxPixels,-9} {aZero,-8} {aZeroColor,-12} {aPartial,-10} " +
                          $"洞{holePct,5:F1}%  半透{semiPct,5:F1}%  封闭洞{enclosed,-6} 暗实色{darkOpaque,-7} ({avgR:F0},{avgG:F0},{avgB:F0})");
    }
}
