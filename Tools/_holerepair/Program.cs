using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Client.MirGraphics;

// 用【客户端真实代码】Client/MirGraphics/BlackHoleRepair.cs 处理图库帧，
// 输出 before / after 两张 PNG（铺白底），用来确认补洞效果和耗时。
//
// 用法： HoleRepairTest.exe <lib路径> <帧号> [帧号...] <输出目录>
internal static class Program
{
    private static byte[] _all;

    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("用法：HoleRepairTest.exe <lib路径> <帧号...> <输出目录>");
            return 2;
        }

        string lib = args[0];
        string outDir = args[args.Length - 1];
        Directory.CreateDirectory(outDir);

        _all = File.ReadAllBytes(lib);
        using var ms = new MemoryStream(_all);
        using var reader = new BinaryReader(ms);
        reader.ReadInt32();
        int count = reader.ReadInt32();
        reader.ReadInt32();
        var index = new int[count];
        for (int i = 0; i < count; i++) index[i] = reader.ReadInt32();

        for (int a = 1; a < args.Length - 1; a++)
        {
            int frame = int.Parse(args[a]);
            if (frame < 0 || frame >= count) continue;

            int off = index[frame];
            if (off <= 0) continue;

            ms.Position = off;
            short w = reader.ReadInt16();
            short h = reader.ReadInt16();
            reader.ReadInt16(); reader.ReadInt16(); reader.ReadInt16(); reader.ReadInt16();
            reader.ReadByte();
            int length = reader.ReadInt32();

            long dataPos = (long)off + 17;
            if (w <= 0 || h <= 0 || length <= 0 || dataPos + length > _all.Length) continue;

            var packed = new byte[length];
            Array.Copy(_all, dataPos, packed, 0, length);

            byte[] raw;
            using (var gz = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress))
            using (var o = new MemoryStream())
            {
                gz.CopyTo(o);
                raw = o.ToArray();
            }

            byte[] before = (byte[])raw.Clone();

            Save(raw, w, h, Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(lib)}_{frame}_before.png"));

            var sw = Stopwatch.StartNew();
            BlackHoleRepair.Repair(raw, w, h);
            sw.Stop();

            Save(raw, w, h, Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(lib)}_{frame}_after.png"));
            SavePair(before, raw, w, h, Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(lib)}_{frame}_对比.png"));

            Console.WriteLine($"帧 {frame}  {w}x{h}  补洞耗时 {sw.Elapsed.TotalMilliseconds:F2} ms");
        }

        return 0;
    }

    /// <summary>左=修补前，右=修补后，铺在深色地面上，便于对比</summary>
    private static void SavePair(byte[] before, byte[] after, int w, int h, string path)
    {
        using var dst = new Bitmap(w * 2, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst))
        {
            g.Clear(Color.FromArgb(255, 34, 40, 30));
            using var a = ToBitmap(before, w, h);
            using var b = ToBitmap(after, w, h);
            g.DrawImage(a, 0, 0, w, h);
            g.DrawImage(b, w, 0, w, h);
            using var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1);
            g.DrawLine(pen, w, 0, w, h);
        }
        dst.Save(path, ImageFormat.Png);
    }

    private static Bitmap ToBitmap(byte[] raw, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(raw, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4);
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return bmp;
    }

    private static void Save(byte[] raw, int w, int h, string path)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(raw, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4);
        }
        finally
        {
            bmp.UnlockBits(bd);
        }

        // 铺白底，方便直接看哪些地方透出来了
        using var flat = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(flat))
        {
            g.Clear(Color.White);
            g.DrawImage(bmp, 0, 0, w, h);
        }
        flat.Save(path, ImageFormat.Png);
    }
}
