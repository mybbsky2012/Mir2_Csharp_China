using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

// 只读工具：打印 .Lib 里若干帧的「画布尺寸 / 绘制偏移 / 不透明内容包围盒」。
// 预览页合成特效时需要的是「内容」而不是画布，否则大画布 + 小图形会被算成超大范围。
//
// 用法： LibBBox.exe <lib路径> <起始索引> <结束索引>
internal static class Program
{
    private static byte[] _all;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length < 3)
        {
            Console.WriteLine("用法：LibBBox.exe <lib路径> <起始索引> <结束索引>");
            return 2;
        }

        string libPath = args[0];
        if (!File.Exists(libPath)) { Console.WriteLine("找不到文件：" + Path.GetFullPath(libPath)); return 2; }

        _all = File.ReadAllBytes(libPath);
        int count, frameSeek;
        int[] indexList = ReadIndexList(out count, out frameSeek);

        int start = Math.Max(0, int.Parse(args[1]));
        int end = Math.Min(count - 1, int.Parse(args[2]));

        Console.WriteLine($"图库 {Path.GetFileName(libPath)}：图像总数 {count}");
        Console.WriteLine("帧号  画布      绘制X,Y    内容 w×h    内容世界范围 (x0,y0)-(x1,y1)");

        for (int i = start; i <= end; i++)
        {
            if (indexList[i] <= 0) continue;
            using Bitmap bmp = Decode(indexList[i], out short fx, out short fy);
            if (bmp == null) continue;

            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] px = new byte[bmp.Width * bmp.Height * 4];
                for (int y = 0; y < bmp.Height; y++)
                    Marshal.Copy(IntPtr.Add(bd.Scan0, y * bd.Stride), px, y * bmp.Width * 4, bmp.Width * 4);

                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (px[(y * bmp.Width + x) * 4 + 3] <= 8) continue;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            }
            finally { bmp.UnlockBits(bd); }

            if (x1 < 0) { Console.WriteLine($"{i,5}  {bmp.Width}x{bmp.Height}  空帧"); continue; }

            Console.WriteLine(string.Format("{0,5}  {1}x{2}   ({3},{4})   {5}x{6}   ({7},{8})-({9},{10})",
                i, bmp.Width, bmp.Height, fx, fy, x1 - x0 + 1, y1 - y0 + 1,
                fx + x0, fy + y0, fx + x1, fy + y1));
        }

        return 0;
    }

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

    private static Bitmap Decode(int off, out short fx, out short fy)
    {
        fx = fy = 0;
        if (off <= 0 || off + 17 > _all.Length) return null;

        using var ms = new MemoryStream(_all) { Position = off };
        using var reader = new BinaryReader(ms);

        short w = reader.ReadInt16();
        short h = reader.ReadInt16();
        fx = reader.ReadInt16();
        fy = reader.ReadInt16();
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
            finally { bmp.UnlockBits(bd); }
            return bmp;
        }
        catch { return null; }
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
