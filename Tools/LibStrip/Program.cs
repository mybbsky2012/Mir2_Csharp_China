using System.IO.Compression;
using System.Text;

// 改写工具：把 .Lib 里「半透明黑」的脏边清成完全透明。
//
// 背景：623 这类韩服素材是从 3D 渲染导出的，去黑底时只把「纯黑」抠成了 alpha=0，
//       抗锯齿边缘留下一大片 RGB≈(10,5,5)、alpha≈100 的像素。
//       客户端怪物本体走的是普通 alpha 混合（非加法），这些像素会把背景压暗，
//       在亮一点的地面上就是一圈发灰发脏的「花边」。
//
// 做法：把 alpha < aMax 且 max(R,G,B) < rgbMax 的像素整颗清零（RGB 也清 0），
//       其余像素一个字节都不动 —— 火焰本体的暗红、骷髅的阴影都不会受影响。
//
// 用法： LibStrip.exe <源Lib> <目标Lib> [hard aMax=200 rgbMax=60 | soft rgbMax=60]
//  hard：旧方案，整颗清零 —— 会毁掉抗锯齿过渡带（阶梯硬边），已弃用，仅留作对比。
//  soft：按亮度把 alpha 成比例压低（L=0 → 全透明，L>=rgbMax → 不变），
//        过渡是连续渐变，不会出现硬边；黑色越黑越透明，即「提高黑色的透明度」。
// 产出： 目标 Lib（索引表/帧表全部重建，可直接替换原文件）

internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length < 2)
        {
            Console.WriteLine("用法：LibStrip.exe <源Lib> <目标Lib> [aMax=200] [rgbMax=60]");
            return 2;
        }

        string inPath = args[0], outPath = args[1];

        // 兼容两种写法：旧版 <src> <dst> [aMax] [rgbMax] = hard；新版 <src> <dst> soft|hard [rgbMax]
        string mode = "soft";
        int aMax = 200, rgbMax = 60;
        if (args.Length > 2)
        {
            if (args[2] == "soft" || args[2] == "hard")
            {
                mode = args[2];
                if (args.Length > 3) rgbMax = int.Parse(args[3]);
            }
            else
            {
                mode = "hard";
                aMax = int.Parse(args[2]);
                if (args.Length > 3) rgbMax = int.Parse(args[3]);
            }
        }

        if (!File.Exists(inPath)) { Console.WriteLine("找不到：" + inPath); return 2; }
        if (string.Equals(Path.GetFullPath(inPath), Path.GetFullPath(outPath), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("源和目标是同一个文件，请指定不同的输出路径。");
            return 2;
        }

        byte[] all = File.ReadAllBytes(inPath);
        using var ms = new MemoryStream(all);
        using var br = new BinaryReader(ms);

        int version = br.ReadInt32();
        int count = br.ReadInt32();
        int frameSeek = version >= 3 ? br.ReadInt32() : 0;

        int[] index = new int[count];
        for (int i = 0; i < count; i++) index[i] = br.ReadInt32();

        int headerLen = 12 + count * 4;
        Console.WriteLine($"源：{Path.GetFileName(inPath)}  version={version} 帧数={count} frameSeek={frameSeek} 大小={all.Length:N0}");

        var outMs = new MemoryStream(all.Length);
        int[] newIndex = new int[count];
        long clearedTotal = 0;
        int framesTouched = 0;
        long total = 0;

        using (var bw = new BinaryWriter(outMs, Encoding.UTF8, true))
        {
            bw.Write(version);
            bw.Write(count);
            bw.Write(0);                                   // frameSeek 占位
            for (int i = 0; i < count; i++) bw.Write(0);    // 索引表占位

            for (int i = 0; i < count; i++)
            {
                ms.Position = index[i];

                short w = br.ReadInt16();
                short h = br.ReadInt16();
                short ox = br.ReadInt16();
                short oy = br.ReadInt16();
                short sx = br.ReadInt16();
                short sy = br.ReadInt16();
                byte shadow = br.ReadByte();
                int len = br.ReadInt32();

                byte[] packed = br.ReadBytes(len);
                int expected = w * h * 4;
                byte[] raw = Decompress(packed, expected);

                int changed = mode == "soft" ? Soften(raw, aMax, rgbMax) : Strip(raw, aMax, rgbMax);
                clearedTotal += changed;
                if (changed > 0) framesTouched++;

                byte[] newPacked = Compress(raw);

                // mask 层（染色遮罩）原样搬运
                byte[] maskTail = Array.Empty<byte>();
                if ((shadow >> 7) == 1)
                {
                    byte[] mh = br.ReadBytes(12);
                    if (mh.Length == 12)
                    {
                        int maskLen = BitConverter.ToInt32(mh, 8);
                        byte[] md = br.ReadBytes(maskLen);
                        maskTail = new byte[12 + md.Length];
                        Buffer.BlockCopy(mh, 0, maskTail, 0, 12);
                        Buffer.BlockCopy(md, 0, maskTail, 12, md.Length);
                    }
                }

                long newOffset = headerLen + total;
                if (newOffset > int.MaxValue) { Console.WriteLine("!! 索引溢出（文件超过 2GB）"); return 3; }
                newIndex[i] = (int)newOffset;

                bw.Write(w); bw.Write(h); bw.Write(ox); bw.Write(oy);
                bw.Write(sx); bw.Write(sy); bw.Write(shadow); bw.Write(newPacked.Length);
                bw.Write(newPacked);
                if (maskTail.Length > 0) bw.Write(maskTail);

                total += 17 + newPacked.Length + maskTail.Length;

                if (i == 0 || i == count - 1 || (i + 1) % 50 == 0)
                    Console.WriteLine($"  帧 {i,4}  {w}x{h}  {len,9:N0} -> {newPacked.Length,9:N0}   处理 {changed,7:N0}");
            }

            // 帧表原样追加
            long newFrameSeek = headerLen + total;
            byte[] tail = new byte[all.Length - frameSeek];
            Buffer.BlockCopy(all, frameSeek, tail, 0, tail.Length);
            bw.Write(tail);

            // 回填头
            outMs.Position = 8;
            bw.Write((int)newFrameSeek);
            for (int i = 0; i < count; i++) bw.Write(newIndex[i]);
            bw.Flush();
        }

        File.WriteAllBytes(outPath, outMs.ToArray());

        Console.WriteLine();
        Console.WriteLine($"完成：{Path.GetFileName(outPath)}");
        Console.WriteLine($"  处理帧数 {framesTouched}/{count}，清除像素 {clearedTotal:N0}");
        Console.WriteLine($"  大小 {all.Length:N0} -> {outMs.Length:N0}   frameSeek {frameSeek} -> {(int)(headerLen + total)}");
        return 0;
    }

    /// <summary>soft：按亮度把 alpha 成比例压低（L=0 → 全透明，L≥rgbMax → 不动），返回处理像素数。
    /// 过渡连续渐变，不会产生 hard 模式的阶梯硬边。</summary>
    private static int Soften(byte[] raw, int aMax, int rgbMax)
    {
        int n = 0;
        for (int p = 0; p + 3 < raw.Length; p += 4)
        {
            int a = raw[p + 3];
            if (a == 0 || a >= aMax) continue;

            int b = raw[p], g = raw[p + 1], r = raw[p + 2];
            int luma = Math.Max(r, Math.Max(g, b));
            if (luma >= rgbMax) continue;

            int na = a * luma / rgbMax;
            if (na == a) continue;
            //★最小留 1 而不是 0：客户端 BlackHoleRepair 会把「alpha==0 且封闭」的洞补成不透明，
            //  留 1/255 视觉上等于全透明，又不会被洞修复补出色块。
            if (na < 1) na = 1;
            raw[p + 3] = (byte)na;
            n++;
        }
        return n;
    }

    /// <summary>hard（旧方案，仅留作对比）：把「低 alpha + 近黑」的像素整颗清成全透明，返回处理掉的像素数。</summary>
    private static int Strip(byte[] raw, int aMax, int rgbMax)
    {
        int n = 0;
        for (int p = 0; p + 3 < raw.Length; p += 4)
        {
            int a = raw[p + 3];
            if (a == 0 || a >= aMax) continue;

            int b = raw[p], g = raw[p + 1], r = raw[p + 2];
            if (r < rgbMax && g < rgbMax && b < rgbMax)
            {
                raw[p] = 0; raw[p + 1] = 0; raw[p + 2] = 0; raw[p + 3] = 0;
                n++;
            }
        }
        return n;
    }

    private static byte[] Decompress(byte[] data, int expected)
    {
        using var input = new MemoryStream(data);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        byte[] buf = new byte[expected];
        int read = 0;
        while (read < expected)
        {
            int n = gz.Read(buf, read, expected - read);
            if (n <= 0) break;
            read += n;
        }
        if (read != expected)
            throw new InvalidDataException($"解压长度不符：得到 {read}，应为 {expected}");
        return buf;
    }

    private static byte[] Compress(byte[] raw)
    {
        using var outMs = new MemoryStream();
        using (var gz = new GZipStream(outMs, CompressionLevel.Optimal, true))
            gz.Write(raw, 0, raw.Length);
        return outMs.ToArray();
    }
}
