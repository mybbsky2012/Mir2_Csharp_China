using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

// 羽化内观「实际合成」预览工具（只读）。
// 严格按客户端 CharacterDialog 的绘制顺序与偏移规则拼出来：
//   底板 Prguse(340 男 / 341 女) 在 (0,0)
//   素体 Prguse(320/321 + GetHumUpBody 的 offX/offY)
//   盔甲立绘 StateItem_<职业>_<性别> 固定帧（offSet:true → 自己加 mi.X/mi.Y）
//   武器立绘 同上
// 用法： HumUpLook.exe <Prguse.Lib> <输出目录> <StateItem男.Lib> <StateItem女.Lib> [盔甲男帧] [盔甲女帧] [武器帧]
// 默认帧：男甲 2454、女甲 2464、武器 3395（弓手）。
internal static class Program
{
    private static byte[] _lib;
    private static string _libPath;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length < 4)
        {
            Console.WriteLine("用法：HumUpLook.exe <Prguse.Lib> <输出目录> <StateItem男.Lib> <StateItem女.Lib> [甲男] [甲女] [武器]");
            return 2;
        }

        string prguse = args[0];
        string outDir = args[1];
        string manLib = args[2];
        string womanLib = args[3];
        int armourMan = args.Length > 4 ? int.Parse(args[4]) : 2454;
        int armourWoman = args.Length > 5 ? int.Parse(args[5]) : 2464;
        int weaponFrame = args.Length > 6 ? int.Parse(args[6]) : 3395;
        // 羽化内观的底板是 378(男)/379(女)（340/341 里烘焙着原版裸体，羽化时会叠人）
        int panelMan = args.Length > 7 ? int.Parse(args[7]) : 378;
        int panelWoman = args.Length > 8 ? int.Parse(args[8]) : 379;

        Directory.CreateDirectory(outDir);

        foreach (bool male in new[] { true, false })
        {
            int panel = male ? panelMan : panelWoman;
            int body = male ? 320 : 321;
            int bodyOffX = male ? 2 : 1;
            int bodyOffY = male ? 7 : 8;

            string state = male ? manLib : womanLib;
            int armour = male ? armourMan : armourWoman;

            using Bitmap panelBmp = LoadFrame(prguse, panel, out short pfx, out short pfy, out _, out _);
            if (panelBmp == null) { Console.WriteLine($"底板 {panel} 解码失败"); return 1; }

            //★底板自己也有 (X,Y) 偏移：340/341/378/379 的偏移是 (8,90)，即图里的面板
            //  相对它所在画布的左上角往右下挪了 8,90。叠加层用的是同一个 DisplayLocation
            //  原点（客户端 DisplayLocation 不含图自身的 X/Y），所以合成时必须把底板画在
            //  (pfx,pfy)，否则所有叠加层会整体偏右 8 / 偏下 90 —— 看起来就是"武器偏低"。
            int W = Math.Max(panelBmp.Width + pfx, 248), H = Math.Max(panelBmp.Height + pfy, 281);
            Console.WriteLine($"==== {(male ? "男" : "女")}  底板 {panel} {panelBmp.Width}x{panelBmp.Height}@({pfx},{pfy})  素体 {body}(+{bodyOffX},{bodyOffY})  盔甲 {armour}  武器 {weaponFrame} ====");

            // 各层信息 + 单独导出
            using Bitmap bodyBmp = LoadFrame(prguse, body, out short bfx, out short bfy, out short bw, out short bh);
            using Bitmap armourBmp = LoadFrame(state, armour, out short afx, out short afy, out short aw, out short ah);
            using Bitmap weaponBmp = LoadFrame(state, weaponFrame, out short wfx, out short wfy, out short ww, out short wh);

            Console.WriteLine($"  素体  {bw}x{bh} fx={bfx} fy={bfy} → 落点({bfx + bodyOffX},{bfy + bodyOffY})");
            Console.WriteLine($"  盔甲  {aw}x{ah} fx={afx} fy={afy} → 落点({afx},{afy})");
            Console.WriteLine($"  武器  {ww}x{wh} fx={wfx} fy={wfy} → 落点({wfx},{wfy})");

            var cols = new List<Bitmap>();
            cols.Add(Compose(W, H, (panelBmp, pfx, pfy)));
            cols.Add(Compose(W, H, (panelBmp, pfx, pfy), (bodyBmp, bfx + bodyOffX, bfy + bodyOffY)));
            cols.Add(Compose(W, H, (panelBmp, pfx, pfy), (bodyBmp, bfx + bodyOffX, bfy + bodyOffY), (armourBmp, (int)afx, (int)afy)));
            cols.Add(Compose(W, H, (panelBmp, pfx, pfy), (bodyBmp, bfx + bodyOffX, bfy + bodyOffY), (armourBmp, (int)afx, (int)afy), (weaponBmp, (int)wfx, (int)wfy)));

            string file = Path.Combine(outDir, $"look_{(male ? "male" : "female")}.png");
            using (var sheet = new Bitmap(W * cols.Count, H, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(255, 26, 26, 30));
                    for (int i = 0; i < cols.Count; i++)
                        g.DrawImage(cols[i], i * W, 0);
                    using var pen = new Pen(Color.FromArgb(90, 255, 255, 255));
                    for (int i = 1; i < cols.Count; i++)
                        g.DrawLine(pen, i * W, 0, i * W, H);
                }
                sheet.Save(file, ImageFormat.Png);
            }

            foreach (var c in cols) c.Dispose();
            Console.WriteLine("  -> " + file);
        }

        return 0;
    }

    private static Bitmap Compose(int w, int h, (Bitmap img, int x, int y) panel, params (Bitmap img, int x, int y)[] layers)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(255, 26, 26, 30));
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        if (panel.img != null) g.DrawImage(panel.img, panel.x, panel.y, panel.img.Width, panel.img.Height);
        foreach (var (img, x, y) in layers)
            if (img != null) g.DrawImage(img, x, y, img.Width, img.Height);
        return bmp;
    }

    private static Bitmap LoadFrame(string path, int index, out short fx, out short fy, out short w, out short h)
    {
        fx = fy = w = h = 0;
        if (!File.Exists(path)) { Console.WriteLine("找不到：" + path); return null; }

        if (_libPath != path)
        {
            _libPath = path;
            _lib = File.ReadAllBytes(path);
        }

        int[] indexList = ReadIndexList(out int count, out _);
        if (index < 0 || index >= count) return null;

        int off = indexList[index];
        if (off <= 0 || off + 17 > _lib.Length) return null;

        using var ms = new MemoryStream(_lib) { Position = off };
        using var reader = new BinaryReader(ms);

        w = reader.ReadInt16();
        h = reader.ReadInt16();
        fx = reader.ReadInt16();
        fy = reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadInt16();
        reader.ReadByte();
        int length = reader.ReadInt32();

        if (w <= 0 || h <= 0 || length <= 0) return null;
        long dataPos = (long)off + 17;
        if (dataPos + length > _lib.Length) return null;

        try
        {
            byte[] raw = new byte[length];
            Array.Copy(_lib, dataPos, raw, 0, length);
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

    private static int[] ReadIndexList(out int count, out int frameSeek)
    {
        using var ms = new MemoryStream(_lib);
        using var reader = new BinaryReader(ms);
        int version = reader.ReadInt32();
        count = reader.ReadInt32();
        frameSeek = version >= 3 ? reader.ReadInt32() : 0;
        var list = new int[count];
        for (int i = 0; i < count; i++) list[i] = reader.ReadInt32();
        return list;
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
