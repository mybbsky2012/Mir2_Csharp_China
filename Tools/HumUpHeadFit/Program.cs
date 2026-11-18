using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

// 羽化内观「头盔/发型随头」标定工具（只读）。
// 1) 量每个素体(320~337)的头顶行与「头部横带重心 x」；
// 2) 与原版裸体(340 男 / 341 女)同口径相减，得到头应该偏移多少；
// 3) 把「素体 + 发型」按指定偏移合成 PNG，人眼可以直接验收。
//
// 用法： HumUpHeadFit.exe <Prguse.Lib> [输出目录]
internal static class Program
{
    private static byte[] _all;
    private const int CanvasW = 560;
    private const int CanvasH = 560;
    private const int Pad = 40;

    // 与 Client/MirScenes/Dialogs/CharacterDialog.cs 的 GetHumUpBody 保持一致
    private static void BodySprite(MirClassKind cls, bool male, out int index, out int offX, out int offY)
    {
        switch (cls)
        {
            case MirClassKind.战士: index = male ? 330 : 331; break;
            case MirClassKind.法师: index = male ? 332 : 333; break;
            case MirClassKind.道士: index = male ? 334 : 335; break;
            case MirClassKind.刺客: index = male ? 336 : 337; break;
            default: index = male ? 320 : 321; break;
        }
        switch (index)
        {
            case 320: offX = 2; offY = 7; break;
            case 321: offX = 1; offY = 8; break;
            case 330: offX = 0; offY = 7; break;
            case 331: offX = 6; offY = 9; break;
            case 332: offX = 9; offY = 7; break;
            case 333: offX = 4; offY = 10; break;
            case 334: offX = 10; offY = 7; break;
            case 335: offX = 4; offY = 6; break;
            case 336: offX = 1; offY = 10; break;
            default: offX = 4; offY = 6; break;
        }
    }

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length < 1) { Console.WriteLine("用法：HumUpHeadFit.exe <Prguse.Lib> [输出目录]"); return 2; }
        string lib = args[0];
        if (!File.Exists(lib)) { Console.WriteLine("找不到：" + Path.GetFullPath(lib)); return 2; }
        string outDir = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "humupfit");
        Directory.CreateDirectory(outDir);

        _all = File.ReadAllBytes(lib);
        int count, frameSeek;
        int[] indexList = ReadIndexList(out count, out frameSeek);
        Console.WriteLine($"图库 {Path.GetFileName(lib)}：{count} 帧");

        // 诊断：把单帧原样导成 PNG（含 alpha），用来核对解码是否跟客户端一致
        if (args.Length >= 3 && args[1] == "-dump")
        {
            string ddir = args.Length > 3 ? args[3] : Path.Combine(Path.GetTempPath(), "humupfit");
            Directory.CreateDirectory(ddir);
            foreach (string s in args[2].Split(','))
            {
                int i = int.Parse(s);
                using Bitmap bmp = Decode(indexList[i], out short dfx, out short dfy);
                if (bmp == null) { Console.WriteLine($"{i} 解码失败"); continue; }
                string f = Path.Combine(ddir, $"frame_{i}.png");
                using (var c = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(c)) { g.Clear(Color.Transparent); g.DrawImage(bmp, 0, 0); }
                    c.Save(f, ImageFormat.Png);
                }
                Console.WriteLine($"{i} -> {f}  ({bmp.Width}x{bmp.Height} fx={dfx} fy={dfy})");
            }
            return 0;
        }

        // 盔甲叠加预览：把 DB 里真实的盔甲内观图（StateItem.Lib 的帧号）画到羽化素体上，
        // 盔甲坐标完全照客户端 CharacterDialog.AfterDraw 的写法：DisplayLocation(0,0) + 帧XY。
        // 用法： HumUpHeadFit.exe <Prguse.Lib> D:/out -armor <StateItem.Lib> <男图A> <男图B> <女图A> <女图B>
        if (args.Length >= 8 && args[2] == "-armor")
        {
            string armorLib = args[3];
            string adir = args[1];
            Directory.CreateDirectory(adir);
            int[] mImgs = { int.Parse(args[4]), int.Parse(args[5]) };
            int[] fImgs = { int.Parse(args[6]), int.Parse(args[7]) };

            byte[] armorBytes = File.ReadAllBytes(armorLib);
            int armorCount;
            int[] armorIndex;
            {
                var save = _all;
                _all = armorBytes;
                armorIndex = ReadIndexList(out armorCount, out _);
                _all = save;
            }

            var clsAll = new[] { MirClassKind.弓箭, MirClassKind.战士, MirClassKind.法师, MirClassKind.道士, MirClassKind.刺客 };
            foreach (bool male in new[] { true, false })
            {
                int[] imgs = male ? mImgs : fImgs;
                var rows = new List<List<Bitmap>>();
                foreach (var cls in clsAll)
                {
                    int idx, offX, offY;
                    BodySprite(cls, male, out idx, out offX, out offY);

                    var row = new List<Bitmap>
                    {
                        FullPanel(indexList[idx], offX, offY, null, null, 0),              // 素体
                        FullPanel(indexList[idx], offX, offY, armorLib, armorIndex, imgs[0]), // 素体 + 盔甲A
                        FullPanel(indexList[idx], offX, offY, armorLib, armorIndex, imgs[1]), // 素体 + 盔甲B
                        FullPanel(0, 0, 0, armorLib, armorIndex, imgs[0]),               // 盔甲A 单图（原版对齐）
                    };
                    rows.Add(row);

                    // 量一下落地后的实际包围盒，给出「素体 vs 盔甲」的差
                    using (Bitmap bb = Decode(indexList[idx], out short bfx, out short bfy))
                    using (Bitmap ab = DecodeArmor(armorLib, imgs[0], out short afx, out short afy))
                    {
                        if (bb != null && ab != null)
                        {
                            ContentBBox(bb, out int bx0, out int by0, out int bx1, out int by1);
                            ContentBBox(ab, out int ax0, out int ay0, out int ax1, out int ay1);
                            int BX0 = bfx + bx0 + offX, BY0 = bfy + by0 + offY, BX1 = bfx + bx1 + offX, BY1 = bfy + by1 + offY;
                            int AX0 = afx + ax0, AY0 = afy + ay0, AX1 = afx + ax1, AY1 = afy + ay1;
                            double sh = (double)(BY1 - BY0) / Math.Max(1, AY1 - AY0);
                            Console.WriteLine($"  {(male ? "男" : "女")} {cls,-4} 素体{idx}  盔甲{imgs[0]}/{imgs[1]}"
                                + $"   素体[{BX0},{BY0}]-[{BX1},{BY1}] {BX1 - BX0}x{BY1 - BY0}"
                                + $"   盔甲[{AX0},{AY0}]-[{AX1},{AY1}] {AX1 - AX0}x{AY1 - AY0}"
                                + $"   高度比={sh:F3}  底部差={BY1 - AY1}  中心X差={(BX0 + BX1) / 2.0 - (AX0 + AX1) / 2.0:F1}");
                        }
                    }
                }
                using var sheet = ComposeFull(rows);
                string name = $"armor_{(male ? "male" : "female")}.png";
                sheet.Save(Path.Combine(adir, name), ImageFormat.Png);
                foreach (var r in rows) foreach (var b in r) b?.Dispose();
                Console.WriteLine("  -> " + Path.Combine(adir, name));
            }
            return 0;
        }

        var classes = new[] { MirClassKind.弓箭, MirClassKind.战士, MirClassKind.法师, MirClassKind.道士, MirClassKind.刺客 };

        Console.WriteLine();
        Console.WriteLine("== 素体头部量测（画布坐标 = 帧XY + 偏移后落点） ==");
        Console.WriteLine("性别 职业   素体  画布      帧XY      头顶行  头部重心X  落点后头顶Y  落点后重心X");

        var bodyMet = new Dictionary<string, (int top, double cx, int fx, int fy)>();
        foreach (bool male in new[] { true, false })
            foreach (var cls in classes)
            {
                int idx, offX, offY;
                BodySprite(cls, male, out idx, out offX, out offY);
                using Bitmap bmp = Decode(indexList[idx], out short fx, out short fy);
                if (bmp == null) { Console.WriteLine($"{(male ? "男" : "女")} {cls,-4} {idx} 解码失败"); continue; }
                HeadMetrics(bmp, out int top, out double cx);
                bodyMet[$"{male}_{cls}"] = (top, cx, fx, fy);
                Console.WriteLine($"{(male ? "男" : "女"),-4} {cls,-5} {idx,4}  {bmp.Width}x{bmp.Height,-3} ({fx},{fy})  bmpTop={top,3}  cx={cx,7:F1}   {offY + fy + top,6}      {offX + fx + cx,7:F1}");
            }

        Console.WriteLine();
        Console.WriteLine("== 原版裸体基线（340/341 与 378/379 逐像素差得到裸体掩码） ==");
        var baseMet = new Dictionary<bool, (int top, double cx, int fx, int fy)>();
        foreach (bool male in new[] { true, false })
        {
            int idx = male ? 340 : 341;
            int idxNoBody = male ? 378 : 379;
            using Bitmap withBody = Decode(indexList[idx], out short fx, out short fy);
            using Bitmap noBody = Decode(indexList[idxNoBody], out _, out _);
            if (withBody == null || noBody == null) { Console.WriteLine($"{idx}/{idxNoBody} 解码失败"); continue; }

            DiffMetrics(withBody, noBody, out int top, out double cx, out int bx0, out int by0, out int bx1, out int by1);
            baseMet[male] = (top, cx, fx, fy);
            Console.WriteLine($"{(male ? "男" : "女")} {idx}  {withBody.Width}x{withBody.Height} ({fx},{fy})  裸体掩码bbox=({bx0},{by0})-({bx1},{by1})  头顶行={top}  头部重心X={cx:F1}  → 落点后 头顶Y={fy + top}  重心X={fx + cx:F1}");
        }

        Console.WriteLine();
        Console.WriteLine("== 应加偏移（素体 - 原版裸体） 对照现表 ==");
        Console.WriteLine("性别 职业   应该(dx,dy)   现表(dx,dy)   差");
        foreach (bool male in new[] { true, false })
            foreach (var cls in classes)
            {
                if (!bodyMet.ContainsKey($"{male}_{cls}") || !baseMet.ContainsKey(male)) continue;
                int idx, offX, offY;
                BodySprite(cls, male, out idx, out offX, out offY);
                var b = bodyMet[$"{male}_{cls}"];
                var o = baseMet[male];
                double dx = (offX + b.fx + b.cx) - (o.fx + o.cx);
                double dy = (offY + b.fy + b.top) - (o.fy + o.top);
                CurOffset(cls, male, out int cdx, out int cdy);
                Console.WriteLine($"{(male ? "男" : "女"),-4} {cls,-5} ({Math.Round(dx),3},{Math.Round(dy),3})".PadRight(38)
                                  + $"({cdx},{cdy})".PadRight(13) + $"({Math.Round(dx) - cdx},{Math.Round(dy) - cdy})");
            }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("== 头部对比图（原版基准 / 旧表 / 新表 / 光头） ==");
        foreach (bool male in new[] { true, false })
        {
            var rows = new List<List<Bitmap>>();
            foreach (var cls in classes)
            {
                if (!bodyMet.ContainsKey($"{male}_{cls}")) continue;
                int idx, offX, offY;
                BodySprite(cls, male, out idx, out offX, out offY);
                OldOffset(cls, male, out int odx, out int ody);
                CurOffset(cls, male, out int ndx, out int ndy);

                int hair = 441 + HAIR_STYLE + (cls == MirClassKind.刺客 ? 20 : 0) + (male ? 0 : 40);
                int ex = cls == MirClassKind.刺客 ? (male ? 6 : 4) : 0;
                int ey = cls == MirClassKind.刺客 ? (male ? 25 : 18) : 0;

                rows.Add(new List<Bitmap>
                {
                    RenderPanel(indexList[340 + (male ? 0 : 1)], 0, 0, indexList[hair], 0, 0),
                    RenderPanel(indexList[idx], offX, offY, indexList[hair], odx + ex, ody + ey),
                    RenderPanel(indexList[idx], offX, offY, indexList[hair], ndx + ex, ndy + ey),
                    RenderPanel(indexList[idx], offX, offY, 0, 0, 0),
                });
                Console.WriteLine($"  {(male ? "男" : "女")} {cls,-4} 旧({odx},{ody})  新({ndx},{ndy})");
            }

            using var sheet = ComposeGrid(rows);
            string name = $"{(male ? "male" : "female")}_all.png";
            sheet.Save(Path.Combine(outDir, name), ImageFormat.Png);
            foreach (var r in rows) foreach (var pb in r) pb.Dispose();
            Console.WriteLine("  -> " + Path.Combine(outDir, name));
        }

        return 0;
    }


    private const int HAIR_STYLE = 1;          // 发型编号（0~7）
    private const int Zoom = 5;
    private static readonly Rectangle HeadWindow = new Rectangle(104, 148, 80, 80);

    // 把「身体 + 发型」按内观坐标(0,0 起)画进一张 248x281 的画布（跟 Prguse 340 同尺寸，方便同窗口裁剪）
    private static Bitmap RenderPanel(int bodyOff, int bx, int by, int hairOff, int hx, int hy)
    {
        var bmp = new Bitmap(248, 281, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 30, 30, 34));
            if (bodyOff > 0) DrawSprite(g, bodyOff, bx, by);
            if (hairOff > 0) DrawSprite(g, hairOff, hx, hy);
        }
        return bmp;
    }

    private static Bitmap Compose(List<Bitmap> panels)
    {
        int w = HeadWindow.Width * Zoom, h = HeadWindow.Height * Zoom;
        var sheet = new Bitmap(w * panels.Count, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(sheet);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.Clear(Color.FromArgb(255, 12, 12, 14));
        for (int i = 0; i < panels.Count; i++)
            g.DrawImage(panels[i], new Rectangle(i * w, 0, w, h), HeadWindow, GraphicsUnit.Pixel);
        return sheet;
    }

    private enum MirClassKind { 战士, 法师, 道士, 刺客, 弓箭 }




    private static Bitmap DecodeArmor(string lib, int frame, out short fx, out short fy)
    {
        fx = fy = 0;
        if (_armorBytes == null || _armorPath != lib)
        {
            _armorPath = lib;
            _armorBytes = File.ReadAllBytes(lib);
            _armorIndex = ReadIndexListOf(_armorBytes);
        }
        var save = _all;
        _all = _armorBytes;
        try { return Decode(_armorIndex[frame], out fx, out fy); }
        finally { _all = save; }
    }

    // 不透明内容包围盒（A 通道 > 8）。
    // ★Prguse 的素体帧里有一行/一列「边缘杂点」（画布最后一行 x=0..w 上零星几个像素），
    //   直接取 min/max 会把整个画布算进去 → 先按列计数>=5 定左右，再在这些列里按行计数>=2 定上下。
    private static void ContentBBox(Bitmap bmp, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = y0 = x1 = y1 = 0;
        byte[] px = GetPixels(bmp);
        int w = bmp.Width, h = bmp.Height;
        var col = new int[w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] > 8) col[x]++;

        int cx0 = -1, cx1 = -1;
        for (int x = 0; x < w; x++)
        {
            if (col[x] < 5) continue;
            if (cx0 < 0) cx0 = x;
            cx1 = x;
        }
        if (cx0 < 0) return;

        int cy0 = -1, cy1 = -1;
        for (int y = 0; y < h; y++)
        {
            int n = 0;
            for (int x = cx0; x <= cx1; x++) if (px[(y * w + x) * 4 + 3] > 8) n++;
            if (n < 2) continue;
            if (cy0 < 0) cy0 = y;
            cy1 = y;
        }
        if (cy0 < 0) return;

        x0 = cx0; x1 = cx1; y0 = cy0; y1 = cy1;
    }

    private static readonly Rectangle FullWindow = new Rectangle(62, 150, 140, 188);
    private const int FullZoom = 2;

    // 全身内观合成：素体(可空) + 盔甲(可空)，坐标与客户端一致（dialog 0,0 + 帧XY）
    private static Bitmap FullPanel(int bodyOff, int bx, int by, string armorLib, int[] armorIndex, int armorFrame)
    {
        var bmp = new Bitmap(262, 380, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(255, 26, 26, 30));
            if (bodyOff > 0) DrawSprite(g, bodyOff, bx, by);
            if (armorLib != null && armorIndex != null && armorFrame >= 0 && armorFrame < armorIndex.Length)
            {
                if (_armorBytes == null || _armorPath != armorLib)
                {
                    _armorPath = armorLib;
                    _armorBytes = File.ReadAllBytes(armorLib);
                    _armorIndex = ReadIndexListOf(_armorBytes);
                }
                var save = _all;
                _all = _armorBytes;
                DrawSprite(g, _armorIndex[armorFrame], 0, 0);
                _all = save;
            }
        }
        return bmp;
    }

    private static int[] ReadIndexListOf(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        int version = reader.ReadInt32();
        int count = reader.ReadInt32();
        if (version >= 3) reader.ReadInt32();
        var list = new int[count];
        for (int i = 0; i < count; i++) list[i] = reader.ReadInt32();
        return list;
    }

    private static Bitmap ComposeFull(List<List<Bitmap>> rows)
    {
        int cw = FullWindow.Width * FullZoom, ch = FullWindow.Height * FullZoom;
        int cols = rows[0].Count;
        var sheet = new Bitmap(cw * cols, ch * rows.Count, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(sheet);
        g.Clear(Color.FromArgb(255, 12, 12, 14));
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < cols; c++)
                if (rows[r][c] != null)
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.DrawImage(rows[r][c], new Rectangle(c * cw, r * ch, cw, ch), FullWindow, GraphicsUnit.Pixel);
                }
        return sheet;
    }

    // 每行 4 格（原版/旧表/新表/光头），纵向堆叠成一张图
    private static Bitmap ComposeGrid(List<List<Bitmap>> rows)
    {
        int cw = HeadWindow.Width * Zoom, ch = HeadWindow.Height * Zoom;
        int cols = rows[0].Count;
        var sheet = new Bitmap(cw * cols, ch * rows.Count, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(sheet);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.Clear(Color.FromArgb(255, 12, 12, 14));
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < cols; c++)
                g.DrawImage(rows[r][c], new Rectangle(c * cw, r * ch, cw, ch), HeadWindow, GraphicsUnit.Pixel);
        return sheet;
    }

    // 旧表（改前用的那一版，仅用于对比图第 2 列）
    private static void OldOffset(MirClassKind cls, bool male, out int dx, out int dy)
    {
        if (male)
        {
            switch (cls)
            {
                case MirClassKind.战士: dx = -3; dy = -17; return;
                case MirClassKind.法师: dx = 7; dy = -17; return;
                case MirClassKind.道士: dx = 8; dy = -17; return;
                case MirClassKind.刺客: dx = -2; dy = -14; return;
                default: dx = 0; dy = -17; return;
            }
        }
        switch (cls)
        {
            case MirClassKind.战士: dx = 6; dy = -8; return;
            case MirClassKind.法师: dx = 3; dy = -7; return;
            case MirClassKind.道士: dx = 4; dy = -11; return;
            case MirClassKind.刺客: dx = 6; dy = -13; return;
            default: dx = 1; dy = -9; return;
        }
    }

    // 新表（与 Client 的 CharacterDialog.GetHumUpHeadOffset 保持一致）
    private static void CurOffset(MirClassKind cls, bool male, out int dx, out int dy)
    {
        if (male)
        {
            switch (cls)
            {
                case MirClassKind.战士: dx = -6; dy = -17; return;
                case MirClassKind.法师: dx = 3; dy = -17; return;
                case MirClassKind.道士: dx = 4; dy = -17; return;
                case MirClassKind.刺客: dx = -5; dy = -14; return;
                default: dx = -4; dy = -17; return;
            }
        }
        switch (cls)
        {
            case MirClassKind.战士: dx = 4; dy = -8; return;
            case MirClassKind.法师: dx = 1; dy = -7; return;
            case MirClassKind.道士: dx = 2; dy = -11; return;
            case MirClassKind.刺客: dx = 2; dy = -11; return;
            default: dx = -1; dy = -9; return;
        }
    }

    // 两张同尺寸帧逐像素做差，得到「有裸体 vs 无裸体」的掩码；
    // 再在掩码上量头顶行（第一行 >=3 个差异像素）与头部重心 X（头顶以下 16 行）。
    private static void DiffMetrics(Bitmap a, Bitmap b, out int topY, out double centroidX,
                                    out int x0, out int y0, out int x1, out int y1)
    {
        topY = -1; centroidX = 0; x0 = y0 = int.MaxValue; x1 = y1 = -1;
        if (a.Width != b.Width || a.Height != b.Height) return;

        byte[] pa = GetPixels(a), pb = GetPixels(b);
        int w = a.Width, h = a.Height;
        var mask = new bool[w * h];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                int d = Math.Abs(pa[i] - pb[i]) + Math.Abs(pa[i + 1] - pb[i + 1]) + Math.Abs(pa[i + 2] - pb[i + 2]) + Math.Abs(pa[i + 3] - pb[i + 3]);
                if (d <= 12) continue;
                mask[y * w + x] = true;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }

        // 只在中央「身体列窗口」里找头，避开面板两侧装备槽/装饰的差异干扰
        int wx0 = Math.Max(0, (int)(w * 80 / 248.0)), wx1 = Math.Min(w - 1, (int)(w * 165 / 248.0));
        for (int y = 0; y < h; y++)
        {
            int n = 0;
            for (int x = wx0; x <= wx1; x++) if (mask[y * w + x]) n++;
            if (n >= 3) { topY = y; break; }
        }
        if (topY < 0) return;

        if (DebugRuns)
        {
            Console.WriteLine($"   掩码前 22 行分布：");
            for (int y = topY; y < Math.Min(h, topY + 22); y++)
            {
                var sb = new StringBuilder();
                int x = 0;
                while (x < w)
                {
                    if (!mask[y * w + x]) { x++; continue; }
                    int s = x;
                    while (x < w && mask[y * w + x]) x++;
                    if (sb.Length < 400) sb.Append($"[{s}-{x - 1}]");
                }
                Console.WriteLine($"     row{y,3}: {sb}");
            }
        }

        long sum = 0, cnt = 0;
        int end = Math.Min(h - 1, topY + 16);
        for (int y = topY; y <= end; y++)
            for (int x = wx0; x <= wx1; x++)
                if (mask[y * w + x]) { sum += x; cnt++; }
        centroidX = cnt > 0 ? (double)sum / cnt : 0;
    }

    // 诊断：打印前 n 行里「alpha>128 的像素数」以及首尾非透明 x，用来确认解码是否正常
    private static void DumpRows(Bitmap bmp, int rows)
    {
        byte[] px = GetPixels(bmp);
        int w = bmp.Width;
        for (int y = 0; y < Math.Min(rows, bmp.Height); y++)
        {
            int n = 0, x0 = -1, x1 = -1;
            for (int x = 0; x < w; x++)
            {
                if (px[(y * w + x) * 4] == 0) continue;
                n++;
                if (x0 < 0) x0 = x;
                x1 = x;
            }
            Console.WriteLine($"     row{y,3}: n={n,4} x=[{x0},{x1}]");
        }
    }

    // 头顶行 = 第一行「不透明像素 >= 3 个」的行；头部重心 = 头顶以下 16 行内不透明像素的 x 均值
    private static void HeadMetrics(Bitmap bmp, out int topY, out double centroidX)
    {
        topY = -1; centroidX = 0;
        byte[] px = GetPixels(bmp);
        int w = bmp.Width, h = bmp.Height;

        for (int y = 0; y < h; y++)
        {
            int n = 0;
            for (int x = 0; x < w; x++) if (px[(y * w + x) * 4] != 0) n++;
            if (n >= 3) { topY = y; break; }
        }
        if (topY < 0) return;

        long sum = 0, cnt = 0;
        int end = Math.Min(h - 1, topY + 16);
        for (int y = topY; y <= end; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4] != 0) { sum += x; cnt++; }
        centroidX = cnt > 0 ? (double)sum / cnt : 0;
    }

    private static void DrawSprite(Graphics g, int off, int px, int py)
    {
        using Bitmap bmp = Decode(off, out short fx, out short fy);
        if (bmp == null) return;
        g.DrawImage(bmp, px + fx, py + fy, bmp.Width, bmp.Height);
    }

    private static byte[] _pxBuf;
    private static byte[] _armorBytes;
    private static int[] _armorIndex;
    private static string _armorPath;
    private static bool DebugRuns = Environment.GetEnvironmentVariable("HUMUP_DEBUG") == "1";

    private static byte[] GetPixels(Bitmap bmp)
    {
        BitmapData bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] px = new byte[bmp.Width * bmp.Height * 4];
            for (int y = 0; y < bmp.Height; y++)
                Marshal.Copy(IntPtr.Add(bd.Scan0, y * bd.Stride), px, y * bmp.Width * 4, bmp.Width * 4);
            return px;
        }
        finally { bmp.UnlockBits(bd); }
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
