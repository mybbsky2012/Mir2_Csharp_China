using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

// 羽化内观「头盔/发型 → 各职业 StateItem 立绘」头部锚点标定工具（只读）。
// 背景：羽化穿甲后盔甲层画的是分职业 StateItem 固定立绘（2450~2454 / 2460~2464），
//       每个职业立绘里人物的头位置都不一样，头盔/发型不能再共用一个常数。
// 用法：
//   HumUpHeadFit2 dump    <lib> <帧号,逗号> <输出目录> [放大倍数]
//   HumUpHeadFit2 measure <输出目录>
//   HumUpHeadFit2 sheet   <输出目录> <头盔帧> <发型编号> [额外dx] [额外dy]
internal static class Program
{
    private const string DataDir = @"E:\micromir2\Data\";
    private const string HumUpDir = DataDir + @"HumUp\";

    private static readonly string[] ClassNames = { "战士", "法师", "道士", "刺客", "弓箭" };
    private const int ClsWarrior = 0, ClsWizard = 1, ClsTaoist = 2, ClsAssassin = 3, ClsArcher = 4;

    // 素体帧（Prguse）+ 客户端 GetHumUpBody 的落点偏移
    private static void BodySprite(int cls, bool male, out int frame, out int offX, out int offY)
    {
        switch (cls)
        {
            case ClsWarrior: frame = male ? 330 : 331; break;
            case ClsWizard: frame = male ? 332 : 333; break;
            case ClsTaoist: frame = male ? 334 : 335; break;
            case ClsAssassin: frame = male ? 336 : 337; break;
            default: frame = male ? 320 : 321; break;
        }
        switch (frame)
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

    // 分职业 StateItem 立绘：衣服帧 / 武器帧（与 Client GetHumUpStateFrames 一致）
    private static void StateFrames(int cls, bool male, out int armour, out int weapon)
    {
        switch (cls)
        {
            case ClsWizard: armour = male ? 2451 : 2461; weapon = 2432; break;
            case ClsTaoist: armour = male ? 2452 : 2462; weapon = 2434; break;
            case ClsAssassin: armour = male ? 2453 : 2463; weapon = 2436; break;
            case ClsArcher: armour = male ? 2454 : 2464; weapon = 3395; break;
            default: armour = male ? 2450 : 2460; weapon = 2430; break;
        }
    }

    private static string StateLibPath(int cls, bool male)
    {
        string who = male ? "Man" : "Woman";
        switch (cls)
        {
            case ClsWizard: return HumUpDir + "StateItem_Wizard_" + who + ".Lib";
            case ClsTaoist: return HumUpDir + "StateItem_Tao_" + who + ".Lib";
            case ClsAssassin: return HumUpDir + "StateItem_Assassin_" + who + ".Lib";
            case ClsArcher: return HumUpDir + "StateItem_Archer_" + who + ".Lib";
            default: return HumUpDir + "StateItem_Warrior_" + who + ".Lib";
        }
    }

    // 发型帧（Prguse 441+）+ 刺客额外位移（客户端原逻辑）
    private static void HairSprite(int cls, bool male, int style, out int frame, out int offX, out int offY)
    {
        frame = 441 + style + (cls == ClsAssassin ? 20 : 0) + (male ? 0 : 40);
        offX = cls == ClsAssassin ? (male ? 6 : 4) : 0;
        offY = cls == ClsAssassin ? (male ? 25 : 18) : 0;
    }

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        if (args.Length < 2) { Console.WriteLine("用法见源码顶部注释"); return 2; }
        string mode = args[0];
        string outDir = args[1];
        if (mode != "freeze") Directory.CreateDirectory(outDir);   // freeze 的 args[1] 是输出 lib 文件，不能建目录

        if (mode == "dump" || mode == "dumpa")   // dumpa：透明底导出（预览合成用）
        {
            string lib = args[2];
            var L = Lib.Load(lib);
            Console.WriteLine($"{Path.GetFileName(lib)}: {L.Count} 帧");
            int zoom = args.Length > 4 ? int.Parse(args[4]) : 1;
            bool transparent = mode == "dumpa";
            foreach (string s in args[3].Split(','))
            {
                int f = int.Parse(s.Trim());
                Bitmap b = L.Decode(f);
                if (b == null) { Console.WriteLine($"  {f} 解码失败"); continue; }
                string p = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(lib)}_{f}.png");
                if (transparent) b.Save(p, ImageFormat.Png);
                else Save(b, p, zoom, true);
                Console.WriteLine($"  {f} -> {p}  {b.Width}x{b.Height} 帧XY=({L.FrameX(f)},{L.FrameY(f)})");
            }
            return 0;
        }

        if (mode == "measure")
        {
            var prg = Lib.Load(DataDir + "Prguse.Lib");
            Console.WriteLine("== 素体 vs 职业立绘 头部量测 ==");
            Console.WriteLine("性别 职业  素体帧 素体(头顶行,脸心X,落点X,落点Y)   立绘帧 立绘(头顶行,脸心X,落点X,落点Y)   差值(dx,dy)");
            foreach (bool male in new[] { true, false })
                for (int cls = 0; cls < 5; cls++)
                {
                    BodySprite(cls, male, out int bf, out int bOx, out int bOy);
                    Bitmap bb = prg.Decode(bf);
                    if (bb == null) { Console.WriteLine($"素体 {bf} 解码失败"); continue; }
                    FaceMetrics(bb, out int bTop, out double bCx, out _, out _);
                    double bx = bOx + prg.FrameX(bf) + bCx;
                    double by = bOy + prg.FrameY(bf) + bTop;

                    StateFrames(cls, male, out int sf, out _);
                    var sl = Lib.Load(StateLibPath(cls, male));
                    Bitmap sb = sl.Decode(sf);
                    if (sb == null) { Console.WriteLine($"立绘 {sf} 解码失败"); continue; }
                    FaceMetrics(sb, out int sTop, out double sCx, out _, out _);
                    double sx = sl.FrameX(sf) + sCx;
                    double sy = sl.FrameY(sf) + sTop;

                    Console.WriteLine($"{(male ? "男" : "女"),-2} {ClassNames[cls],-3} {bf,5} ({bTop,3},{bCx,6:F1},{bx,7:F1},{by,6:F1})   "
                                    + $"{sf,5} ({sTop,3},{sCx,6:F1},{sx,7:F1},{sy,6:F1})   ({sx - bx,6:F1},{sy - by,6:F1})");
                }
            return 0;
        }

        if (mode == "sheet")
        {
            int helmFrame = int.Parse(args[2]);
            int hairStyle = int.Parse(args[3]);
            bool commonSheet = args.Length > 4 && args[4] == "common";

            var prg = Lib.Load(DataDir + "Prguse.Lib");
            var helmLib = Lib.Load(DataDir + "StateItem.Lib");

            foreach (bool male in new[] { true, false })
            {
                var panels = new List<(string tag, Bitmap bmp)>();
                for (int cls = 0; cls < 5; cls++)
                {
                    var sl = Lib.Load(commonSheet
                        ? HumUpDir + $"StateItem_Common_{(male ? "Man" : "Woman")}.Lib"
                        : StateLibPath(cls, male));
                    StateFrames(cls, male, out int sf, out _);
                    BodySprite(cls, male, out int bf, out int boffx, out int boffy);
                    HairSprite(cls, male, hairStyle, out int hf, out int hox, out int hoy);
                    HeadOffsetTable(cls, male, out int hdx, out int hdy);

                    // extra = 职业立绘头部落点 - 素体头部落点（肤色口径现场量测）
                    (int ex, int ey) = MeasureExtra(prg, bf, boffx, boffy, sl, sf);

                    // ★立绘上的物件偏移 = 素体表 headOffset + 头部位移 extra（两段缺一不可）
                    //   common 立绘（StateItem_Common_*）头落点单独量测
                    int cex = ex, cey = ey;
                    if (commonSheet)
                    {
                        Bitmap cbm = sl.Decode(sf);
                        FaceMetrics(cbm, out int cTop, out double cCx, out _, out _);
                        FaceMetrics(prg.Decode(bf), out int pTop, out double pCx, out _, out _);
                        cex = (int)Math.Round((double)(sl.FrameX(sf) + cCx - (boffx + prg.FrameX(bf) + pCx)));
                        cey = (int)Math.Round((double)(sl.FrameY(sf) + cTop - (boffy + prg.FrameY(bf) + pTop)));
                    }

                    panels.Add(($"{(male ? "男" : "女")}{ClassNames[cls]} 素体+发", Compose2(prg, bf, boffx, boffy, prg, hf, hdx + hox, hdy + hoy)));
                    panels.Add(($"{(male ? "男" : "女")}{ClassNames[cls]} 立绘+发", Compose2(sl, sf, 0, 0, prg, hf, hdx + hox + cex, hdy + hoy + cey)));
                    panels.Add(($"{(male ? "男" : "女")}{ClassNames[cls]} 素体+盔", Compose2(prg, bf, boffx, boffy, helmLib, helmFrame, hdx, hdy)));
                    panels.Add(($"{(male ? "男" : "女")}{ClassNames[cls]} 立绘+盔", Compose2(sl, sf, 0, 0, helmLib, helmFrame, hdx + cex, hdy + cey)));
                }
                string p = Path.Combine(outDir, $"sheet_{helmFrame}_{hairStyle}_{(male ? "m" : "w")}.png");
                SaveSheet(panels, p);
                foreach (var it in panels) it.bmp.Dispose();
                Console.WriteLine("  -> " + p);
            }
            return 0;
        }

        if (mode == "verify")
        {
            int helmFrame = int.Parse(args[2]);
            int hairStyle = int.Parse(args[3]);
            var prg = Lib.Load(DataDir + "Prguse.Lib");
            var helmLib = Lib.Load(DataDir + "StateItem.Lib");

            Console.WriteLine("性别 职业  层      基准(素体) 头-物差   立绘 头-物差   应一致");
            foreach (bool male in new[] { true, false })
                for (int cls = 0; cls < 5; cls++)
                {
                    var sl = Lib.Load(StateLibPath(cls, male));
                    StateFrames(cls, male, out int sf, out _);
                    BodySprite(cls, male, out int bf, out int boffx, out int boffy);
                    HairSprite(cls, male, hairStyle, out int hf, out int hox, out int hoy);
                    HeadOffsetTable(cls, male, out int hdx, out int hdy);
                    (int ex, int ey) = MeasureExtra(prg, bf, boffx, boffy, sl, sf);

                    // 基准：素体 + 物件（发型/头盔），量「物件掩码顶 - 头顶行」
                    int dB = LayerDelta(prg, bf, boffx, boffy, prg, hf, hdx + hox, hdy + hoy, out double cxB);
                    // 立绘：物件 = headOffset + extra（正确公式），应与基准一致
                    int dP = LayerDelta(sl, sf, 0, 0, prg, hf, hdx + hox + ex, hdy + hoy + ey, out double cxP);

                    Console.WriteLine($"{(male ? "男" : "女"),-2} {ClassNames[cls],-3} 发型    {dB,6}      {dP,6}      {(dB == dP ? "OK" : "差" + (dP - dB))}   (dx相对差{RelDiff(cxB, prg, bf, boffx, boffy, cxP, sl, sf):F1})");

                    int dBH = LayerDelta(prg, bf, boffx, boffy, helmLib, helmFrame, hdx, hdy, out double cxBH);
                    int dPH = LayerDelta(sl, sf, 0, 0, helmLib, helmFrame, hdx + ex, hdy + ey, out double cxPH);
                    Console.WriteLine($"{(male ? "男" : "女"),-2} {ClassNames[cls],-3} 头盔    {dBH,6}      {dPH,6}      {(dBH == dPH ? "OK" : "差" + (dPH - dBH))}   (dx相对差{RelDiff(cxBH, prg, bf, boffx, boffy, cxPH, sl, sf):F1})");
                }
            return 0;
        }

        if (mode == "common")
        {
            // 量 StateItem_Common_* 2450/2460 头部落点，与各职业立绘对比
            var prg = Lib.Load(DataDir + "Prguse.Lib");
            foreach (bool male in new[] { true, false })
            {
                int frame = male ? 2450 : 2460;
                var cm = Lib.Load(HumUpDir + $"StateItem_Common_{(male ? "Man" : "Woman")}.Lib");
                Bitmap cb = cm.Decode(frame);
                FaceMetrics(cb, out int cTop, out double cCx, out _, out _);
                double cx = cm.FrameX(frame) + cCx, cy = cm.FrameY(frame) + cTop;
                Console.WriteLine($"{(male ? "男" : "女")} Common{frame}: {cb.Width}x{cb.Height} 帧XY=({cm.FrameX(frame)},{cm.FrameY(frame)}) 头落点=({cx:F1},{cy:F1})");

                for (int cls = 0; cls < 5; cls++)
                {
                    var sl = Lib.Load(StateLibPath(cls, male));
                    StateFrames(cls, male, out int sf, out _);
                    Bitmap sb = sl.Decode(sf);
                    FaceMetrics(sb, out int sTop, out double sCx, out _, out _);
                    double sx = sl.FrameX(sf) + sCx, sy = sl.FrameY(sf) + sTop;
                    Console.WriteLine($"   {ClassNames[cls]}{sf}: {sb.Width}x{sb.Height} 帧XY=({sl.FrameX(sf)},{sl.FrameY(sf)}) 头落点=({sx:F1},{sy:F1}) 差=({cx - sx:F1},{cy - sy:F1})");
                }
            }
            return 0;
        }

        if (mode == "pair")
        {
            // 单职业新旧偏移对比：HumUpHeadFit2 pair <outDir> <职业号0-4> <男1/女0> <头盔帧> <发型号> <旧dx> <旧dy> <新dx> <新dy>
            int cls = int.Parse(args[2]);
            bool male = args[3] == "1";
            int helmFrame = int.Parse(args[4]);
            int hairStyle = int.Parse(args[5]);
            int odx = int.Parse(args[6]), ody = int.Parse(args[7]);
            int ndx = int.Parse(args[8]), ndy = int.Parse(args[9]);

            var prg = Lib.Load(DataDir + "Prguse.Lib");
            var helmLib = Lib.Load(DataDir + "StateItem.Lib");
            var sl = Lib.Load(StateLibPath(cls, male));
            StateFrames(cls, male, out int sf, out _);
            BodySprite(cls, male, out int bf, out int boffx, out int boffy);
            HairSprite(cls, male, hairStyle, out int hf, out int hox, out int hoy);
            HeadOffsetTable(cls, male, out int hdx, out int hdy);   // 素体基准用

            var panels = new List<(string tag, Bitmap bmp)>
            {
                ("素体+发型(基准)",  Compose2(prg, bf, boffx, boffy, prg, hf, hdx + hox, hdy + hoy)),
                ("立绘+发型(旧)",    Compose2(sl, sf, 0, 0, prg, hf, odx + hox, ody + hoy)),
                ("立绘+发型(新)",    Compose2(sl, sf, 0, 0, prg, hf, ndx + hox, ndy + hoy)),
                ("素体+头盔(基准)",  Compose2(prg, bf, boffx, boffy, helmLib, helmFrame, hdx, hdy)),
                ("立绘+头盔(旧)",    Compose2(sl, sf, 0, 0, helmLib, helmFrame, odx, ody)),
                ("立绘+头盔(新)",    Compose2(sl, sf, 0, 0, helmLib, helmFrame, ndx, ndy)),
            };
            string p = Path.Combine(outDir, $"pair_{ClassNames[cls]}_{(male ? "m" : "w")}_{odx}_{ody}_vs_{ndx}_{ndy}.png");
            SaveSheet(panels, p);
            foreach (var it in panels) it.bmp.Dispose();
            Console.WriteLine("  -> " + p);
            return 0;
        }

        if (mode == "diff")
        {
            // 逐行差异分析：diff <outDir> <lib> <start> <count>
            // 按客户端 Draw 语义（帧XY落点）把各帧画到同一画布，输出相邻帧每行差异像素数，
            // 判断动的是上身还是下身（腿）。
            var lib = Lib.Load(args[2]);
            int start = int.Parse(args[3]);
            int count = int.Parse(args[4]);

            const int W = 280, H = 320;
            var canvases = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    lib.Draw(g, start + i, 40, 60);
                canvases[i] = GetPixels(bmp);
            }

            Console.WriteLine("行y  " + string.Join(" ", Enumerable.Range(1, count - 1).Select(i => $"f{i - 1}->f{i}")));
            for (int y = 0; y < H; y++)
            {
                var row = new int[count - 1];
                int total = 0;
                for (int i = 1; i < count; i++)
                {
                    int n = 0;
                    for (int x = 0; x < W; x++)
                    {
                        int p = (y * W + x) * 4;
                        int d = Math.Abs(canvases[i - 1][p] - canvases[i][p]) + Math.Abs(canvases[i - 1][p + 1] - canvases[i][p + 1])
                              + Math.Abs(canvases[i - 1][p + 2] - canvases[i][p + 2]) + Math.Abs(canvases[i - 1][p + 3] - canvases[i][p + 3]);
                        if (d > 24) n++;
                    }
                    row[i - 1] = n; total += n;
                }
                if (total >= 3)
                    Console.WriteLine($"{y,3}  " + string.Join(" ", row.Select(n => n.ToString().PadLeft(4))));
            }
            Console.WriteLine("--- 半区合计（上身 y<160 / 下身 y>=160，画布上人物落点 y≈60+fy）---");
            for (int half = 0; half < 2; half++)
            {
                int y0 = half == 0 ? 0 : 160, y1 = half == 0 ? 159 : H - 1;
                var sum = new int[count - 1];
                for (int y = y0; y <= y1; y++)
                    for (int i = 1; i < count; i++)
                    {
                        int n = 0;
                        for (int x = 0; x < W; x++)
                        {
                            int p = (y * W + x) * 4;
                            int d = Math.Abs(canvases[i - 1][p] - canvases[i][p]) + Math.Abs(canvases[i - 1][p + 1] - canvases[i][p + 1])
                                  + Math.Abs(canvases[i - 1][p + 2] - canvases[i][p + 2]) + Math.Abs(canvases[i - 1][p + 3] - canvases[i][p + 3]);
                            if (d > 24) n++;
                        }
                        sum[i - 1] += n;
                    }
                Console.WriteLine($"{(half == 0 ? "上身" : "下身")} y{y0}-{y1}: " + string.Join(" ", sum));
            }

            // 两两配对：帧0 vs 各帧，分「人物上半 (画布y<55) / 下半 (y>=55)」合计差异
            Console.WriteLine("--- 帧两两差异（基准=f0；上半=画布y<55≈头部+躯干，下半=y>=55≈腰以下腿部）---");
            int baseIdx = 0;
            for (int i = 1; i < count; i++)
            {
                int up = 0, down = 0;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int p = (y * W + x) * 4;
                        int d = Math.Abs(canvases[baseIdx][p] - canvases[i][p]) + Math.Abs(canvases[baseIdx][p + 1] - canvases[i][p + 1])
                              + Math.Abs(canvases[baseIdx][p + 2] - canvases[i][p + 2]) + Math.Abs(canvases[baseIdx][p + 3] - canvases[i][p + 3]);
                        if (d > 24) { if (y < 55) up++; else down++; }
                    }
                Console.WriteLine($"f{baseIdx} vs f{i}: 上半 {up,5}   下半 {down,5}");
            }
            return 0;
        }

        if (mode == "freeze")
        {
            // 站立段下身冻结重烘焙：freeze <outLib> <srcLib> <腰部行seamRow> <块起始1> [块起始2...]
            // （outLib 占 args[1] 槽位，与 dump 的 outDir 一致）
            // 每块 8 方向×8 帧，处理每方向 k=1..5（真帧）：对齐 fx/fy 后，
            //   base顶行+seamRow 以下 ← 方向首帧像素（下身/袍摆/阴影完全冻结）；
            //   以上 ← 各帧原像素（上身照常晃动）。
            // 其余帧原样拷贝；输出同版本格式 lib。
            string outLib = args[1];
            string srcLib = args[2];
            int seamRow = int.Parse(args[3]);
            var blocks = args.Skip(4).Select(int.Parse).ToList();

            byte[] src = File.ReadAllBytes(srcLib);
            using var ms = new MemoryStream(src) { Position = 0 };
            using var br = new BinaryReader(ms);
            int version = br.ReadInt32();
            int count = br.ReadInt32();
            int extra = version >= 3 ? br.ReadInt32() : 0;
            var offs = new int[count];
            for (int i = 0; i < count; i++) offs[i] = br.ReadInt32();

            // 解析每帧原始 blob（17 字节头 + gzip 数据）
            var blobs = new byte[count][];
            var frozen = new HashSet<int>();
            foreach (int bstart in blocks)
                for (int d = 0; d < 8; d++)
                    for (int k = 1; k < 6; k++)
                        frozen.Add(bstart + d * 8 + k);

            const int Pad = 80;
            var srcLibObj = Lib.Load(srcLib);
            for (int i = 0; i < count; i++)
            {
                int off = offs[i];
                if (off <= 0 || off + 17 > src.Length) { blobs[i] = new byte[17]; continue; }
                using var fs = new MemoryStream(src) { Position = off };
                using var fr = new BinaryReader(fs);
                short w = fr.ReadInt16(), h = fr.ReadInt16();
                short fx = fr.ReadInt16(), fy = fr.ReadInt16();
                byte[] head = new byte[17];
                Array.Copy(src, off, head, 0, 17);
                int len = BitConverter.ToInt32(head, 13);
                if (w <= 0 || h <= 0 || len <= 0 || off + 17 + len > src.Length) { blobs[i] = head; continue; }

                if (!frozen.Contains(i)) { blobs[i] = src.Skip(off).Take(17 + len).ToArray(); continue; }

                // 该帧所在方向的首帧（base）：块起始 = blocks 中包含 i 的那个；块内偏移 /8*8 = 方向首帧
                int blockStart = blocks.First(b => i >= b && i < b + 64);
                int inBlock = i - blockStart;
                int dirBase = blockStart + (inBlock / 8) * 8;
                // 直接从 src 读 base 头
                int boff = offs[dirBase];
                short bfx = BitConverter.ToInt16(src, boff + 4), bfy = BitConverter.ToInt16(src, boff + 6);

                Bitmap baseBmp = srcLibObj.Decode(dirBase), fBmp = srcLibObj.Decode(i);
                if (baseBmp == null || fBmp == null) { blobs[i] = src.Skip(off).Take(17 + len).ToArray(); continue; }

                int W = w + Pad * 2, H = h + Pad * 2 + 8;
                byte[] cvB, cvF;
                using (var cB = new Bitmap(W, H, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(cB))
                {
                    g.Clear(Color.FromArgb(0, 0, 0, 0));
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    DrawRaw(g, baseBmp, Pad + bfx, Pad + bfy);   // 只画 base
                    cvB = GetPixels(cB);
                }
                using (var cF = new Bitmap(W, H, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(cF))
                {
                    g.Clear(Color.FromArgb(0, 0, 0, 0));
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    DrawRaw(g, fBmp, Pad + fx, Pad + fy);        // 只画本帧
                    cvF = GetPixels(cF);
                }

                // 游戏内绝对坐标：帧f像素(x,y) 落在 (fx+x, fy+y)；base像素(x',y') 落在 (bfx+x', bfy+y')。
                // 同一绝对位置 ⇒ base采样 = (bfx + (fx - bfx) + x, bfy + (fy - bfy) + y) = (fx+x, fy+y)（画布坐标相同）。
                // 冻结线：base 顶行(fy=bfy) 往下 seamRow → 画布 y = Pad + bfy + seamRow
                int seamY = Pad + bfy + seamRow;
                byte[] outPx = new byte[w * h * 4];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int cx = Pad + fx + x, cy = Pad + fy + y;
                        int o = (y * w + x) * 4, c = (cy * W + cx) * 4;
                        if (cy < seamY)
                        {
                            // 上身：本帧原像素（上身照常晃动）
                            outPx[o] = cvF[c]; outPx[o + 1] = cvF[c + 1]; outPx[o + 2] = cvF[c + 2]; outPx[o + 3] = cvF[c + 3];
                        }
                        else
                        {
                            // 下身冻结：取同一绝对位置的 base 像素（对齐 fx/fy），base 无像素则透明
                            if (cvB[c + 3] > 16)
                            { outPx[o] = cvB[c]; outPx[o + 1] = cvB[c + 1]; outPx[o + 2] = cvB[c + 2]; outPx[o + 3] = cvB[c + 3]; }
                        }
                    }

                byte[] gz = GZipCompress(outPx);
                byte[] nb = new byte[17 + gz.Length];
                Array.Copy(head, nb, 17);
                BitConverter.GetBytes(gz.Length).CopyTo(nb, 13);
                Array.Copy(gz, 0, nb, 17, gz.Length);
                blobs[i] = nb;
                baseBmp.Dispose(); fBmp.Dispose();
            }

            // 写新 lib
            int headerSize = (version >= 3 ? 12 : 8) + 4 * count;
            using var fo = new FileStream(outLib, FileMode.Create);
            using var writer = new BinaryWriter(fo);
            writer.Write(version); writer.Write(count);
            if (version >= 3) writer.Write(extra);
            int acc = headerSize;
            var newOffs = new int[count];
            for (int i = 0; i < count; i++) { newOffs[i] = acc; acc += blobs[i].Length; }
            for (int i = 0; i < count; i++) writer.Write(newOffs[i]);
            for (int i = 0; i < count; i++) writer.Write(blobs[i]);
            Console.WriteLine($"  -> {outLib}  version={version} count={count} seam={seamRow} 冻结帧数={frozen.Count}");
            return 0;
        }

        if (mode == "statetest")
        {
            // 内观组合渲染：statetest <outDir> <lib> <衣服帧> <武器帧>
            // 与客户端 Draw(frame, DisplayLocation) 语义一致：两帧都按各自帧XY从同一原点画
            var sl = Lib.Load(args[2]);
            int af = int.Parse(args[3]), wf = int.Parse(args[4]);
            foreach (bool male in new[] { true, false }) { }
            Bitmap b = Compose2(sl, af, 0, 0, sl, wf, 0, 0);
            string p = Path.Combine(outDir, $"statetest_{Path.GetFileNameWithoutExtension(args[2])}_{af}_{wf}.png");
            Save(b, p, 2, true);
            Console.WriteLine("  -> " + p);
            return 0;
        }

        Console.WriteLine("未知模式");
        return 2;
    }

    // 不加帧XY、按指定落点直接画位图（freeze 用，帧XY已在参数里算过）
    private static void DrawRaw(Graphics g, Bitmap bmp, int x, int y)
    {
        g.DrawImage(bmp, x, y, bmp.Width, bmp.Height);
    }

    private static byte[] GZipCompress(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress))
            gz.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    // 物件重心相对「所在画面头部重心」的偏移，立绘与基准的差（>0 = 立绘上偏右）
    private static double RelDiff(double cxB, Lib prg, int bf, int boffx, int boffy, double cxP, Lib sl, int sf)
    {
        FaceMetrics(Render(prg, bf, boffx, boffy), out _, out double hcxB, out _, out _);
        FaceMetrics(Render(sl, sf, 0, 0), out _, out double hcxP, out _, out _);
        return (cxP - hcxP) - (cxB - hcxB);
    }

    // 在 base 上叠 layer，diff 出 layer 掩码，量「掩码顶行 - base 头顶行(肤色)」以及掩码重心X
    private static int LayerDelta(Lib baseLib, int baseFrame, int bdx, int bdy, Lib overLib, int overFrame, int odx, int ody, out double cx)
    {
        cx = 0;
        Bitmap bOnly = Render(baseLib, baseFrame, bdx, bdy);
        Bitmap bWith = Render(baseLib, baseFrame, bdx, bdy, overLib, overFrame, odx, ody);
        if (bOnly == null || bWith == null) return int.MinValue;

        byte[] p0 = GetPixels(bOnly), p1 = GetPixels(bWith);
        int w = bOnly.Width, h = bOnly.Height;

        // diff 掩码（物件像素）
        var mask = new bool[w * h];
        int mx0 = int.MaxValue, mx1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                int d = Math.Abs(p0[i] - p1[i]) + Math.Abs(p0[i + 1] - p1[i + 1]) + Math.Abs(p0[i + 2] - p1[i + 2]) + Math.Abs(p0[i + 3] - p1[i + 3]);
                if (d > 24) { mask[y * w + x] = true; if (x < mx0) mx0 = x; if (x > mx1) mx1 = x; }
            }
        if (mx1 < 0) return int.MinValue;

        // base 头顶行（肤色）
        FaceMetrics(bOnly, out int headTop, out double _, out _, out _);
        if (headTop < 0) return int.MinValue;

        // 物件掩码顶行
        int top = -1;
        for (int y = 0; y < h; y++)
        {
            int n = 0;
            for (int x = mx0; x <= mx1; x++) if (mask[y * w + x]) n++;
            if (n >= 2) { top = y; break; }
        }
        long sum = 0, cnt = 0;
        for (int y = top; y < Math.Min(h, top + 16); y++)
            for (int x = mx0; x <= mx1; x++)
                if (mask[y * w + x]) { sum += x; cnt++; }
        cx = cnt > 0 ? (double)sum / cnt : 0;
        return top - headTop;
    }

    private static Bitmap Render(Lib baseLib, int baseFrame, int bdx, int bdy, Lib overLib = null, int overFrame = 0, int odx = 0, int ody = 0)
    {
        var bmp = new Bitmap(280, 320, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(0, 0, 0, 0));
        baseLib.Draw(g, baseFrame, bdx, bdy);
        if (overLib != null) overLib.Draw(g, overFrame, odx, ody);
        return bmp;
    }

    private static Bitmap Compose2(Lib baseLib, int baseFrame, int bdx, int bdy, Lib overLib, int overFrame, int odx, int ody)
    {
        var bmp = new Bitmap(280, 320, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(255, 26, 26, 30));
        baseLib.Draw(g, baseFrame, bdx, bdy);
        if (overLib != null) overLib.Draw(g, overFrame, odx, ody);
        return bmp;
    }

    // extra = 立绘头部落点 - 素体头部落点（肤色口径）
    private static (int, int) MeasureExtra(Lib prg, int bodyFrame, int boffx, int boffy, Lib sl, int stateFrame)
    {
        Bitmap bb = prg.Decode(bodyFrame);
        Bitmap sb = sl.Decode(stateFrame);
        if (bb == null || sb == null) return (0, 0);
        FaceMetrics(bb, out int bTop, out double bCx, out _, out _);
        FaceMetrics(sb, out int sTop, out double sCx, out _, out _);
        double bx = boffx + prg.FrameX(bodyFrame) + bCx;
        double by = boffy + prg.FrameY(bodyFrame) + bTop;
        double sx = sl.FrameX(stateFrame) + sCx;
        double sy = sl.FrameY(stateFrame) + sTop;
        return ((int)Math.Round(sx - bx), (int)Math.Round(sy - by));
    }

    // 客户端 CharacterDialog.GetHumUpHeadOffset（素体偏移，11-09 定稿用户已验证）
    private static void HeadOffsetTable(int cls, bool male, out int dx, out int dy)
    {
        if (male)
        {
            switch (cls)
            {
                case ClsWarrior: dx = -6; dy = -17; return;
                case ClsWizard: dx = 3; dy = -17; return;
                case ClsTaoist: dx = 4; dy = -17; return;
                case ClsAssassin: dx = -5; dy = -14; return;
                default: dx = -4; dy = -17; return;
            }
        }
        switch (cls)
        {
            case ClsWarrior: dx = 4; dy = -8; return;
            case ClsWizard: dx = 1; dy = -7; return;
            case ClsTaoist: dx = 2; dy = -11; return;
            case ClsAssassin: dx = 2; dy = -11; return;
            default: dx = -1; dy = -9; return;
        }
    }

    // 量测：先用 alpha 求人物包围盒，再在"上部中央"窗口里找肤色像素
    // 头顶行 = 窗口内肤色最高行（>=3 像素）；脸心X = 该行往下 15 行肤色重心
    private static void FaceMetrics(Bitmap bmp, out int topY, out double cx, out int x0, out int x1)
    {
        topY = -1; cx = 0; x0 = x1 = 0;
        byte[] px = GetPixels(bmp);
        int w = bmp.Width, h = bmp.Height;

        // 人物包围盒（alpha > 16；列像素 >= 5 过滤边缘杂点，行像素 >= 2）
        var cols = new int[w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] > 16) cols[x]++;
        int bx0 = -1, bx1 = -1;
        for (int x = 0; x < w; x++) { if (cols[x] < 5) continue; if (bx0 < 0) bx0 = x; bx1 = x; }
        if (bx0 < 0) return;

        int y0 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
        {
            int n = 0;
            for (int x = bx0; x <= bx1; x++) if (px[(y * w + x) * 4 + 3] > 16) n++;
            if (n < 2) continue;
            if (y0 < 0) y0 = y;
            y1 = y;
        }
        if (y0 < 0) return;

        int ww = bx1 - bx0 + 1, hh = y1 - y0 + 1;
        int wx0 = bx0 + (int)(ww * 0.30), wx1 = bx1 - (int)(ww * 0.30);
        int wy1 = y0 + (int)(hh * 0.45);

        int best = -1;
        for (int y = y0; y <= wy1; y++)
        {
            int n = 0;
            for (int x = wx0; x <= wx1; x++)
            {
                int i = (y * w + x) * 4;
                if (!IsSkin(px[i + 2], px[i + 1], px[i], px[i + 3])) continue;
                n++;
            }
            if (n >= 3) { best = y; break; }
        }
        if (best < 0) return;

        topY = best;
        long sum = 0, cnt = 0;
        int end = Math.Min(h - 1, best + 15);
        int rx0 = int.MaxValue, rx1 = -1;
        for (int y = best; y <= end; y++)
            for (int x = wx0; x <= wx1; x++)
            {
                int i = (y * w + x) * 4;
                if (!IsSkin(px[i + 2], px[i + 1], px[i], px[i + 3])) continue;
                sum += x; cnt++;
                if (x < rx0) rx0 = x;
                if (x > rx1) rx1 = x;
            }
        cx = cnt > 0 ? (double)sum / cnt : 0;
        x0 = rx0 == int.MaxValue ? 0 : rx0;
        x1 = rx1 < 0 ? 0 : rx1;
    }

    // 肤色判定（传奇人物皮肤：暖色、R>G>B，亮度中等偏高）
    private static bool IsSkin(byte r, byte g, byte b, byte a)
    {
        if (a < 200) return false;
        if (r < 120 || r > 250) return false;
        if (r <= g || g <= b) return false;
        if (r - b < 30) return false;
        if (g - b < 8) return false;
        if (r - g > 90) return false;
        return true;
    }

    private static void Save(Bitmap bmp, string path, int zoom, bool checker)
    {
        int w = bmp.Width * zoom, h = bmp.Height * zoom;
        using var sheet = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 26, 26, 30));
            if (checker)
                for (int y = 0; y < h; y += 8)
                    for (int x = 0; x < w; x += 8)
                        if (((x / 8) + (y / 8)) % 2 == 0)
                            using (var br = new SolidBrush(Color.FromArgb(255, 40, 40, 46)))
                                g.FillRectangle(br, x, y, 8, 8);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(bmp, new Rectangle(0, 0, w, h));
        }
        sheet.Save(path, ImageFormat.Png);
    }

    // 面板网格：4 列 × 5 行，裁头部窗口放大
    private static void SaveSheet(List<(string tag, Bitmap bmp)> panels, string path)
    {
        int cols = 4, zoom = 7;
        int cw = 64, ch = 64, cx = 100, cy = 128;   // 画布上头部区域 (130,155) 附近，放大 7 倍
        int cwz = cw * zoom, chz = ch * zoom;
        int rows = (panels.Count + cols - 1) / cols;
        using var sheet = new Bitmap(cwz * cols, chz * rows, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 12, 12, 14));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int i = 0; i < panels.Count; i++)
            {
                int r = i / cols, c = i % cols;
                g.DrawImage(panels[i].bmp, new Rectangle(c * cwz, r * chz, cwz, chz), new Rectangle(cx, cy, cw, ch), GraphicsUnit.Pixel);
            }
        }
        sheet.Save(path, ImageFormat.Png);
    }

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

    // ---------------- Lib 读取 ----------------
    private sealed class Lib
    {
        private byte[] _data;
        private int[] _index;

        public int Count => _index.Length;

        public static Lib Load(string path)
        {
            var lib = new Lib { _data = File.ReadAllBytes(path) };
            using var ms = new MemoryStream(lib._data);
            using var reader = new BinaryReader(ms);
            int version = reader.ReadInt32();
            int count = reader.ReadInt32();
            if (version >= 3) reader.ReadInt32();
            lib._index = new int[count];
            for (int i = 0; i < count; i++) lib._index[i] = reader.ReadInt32();
            return lib;
        }

        public Bitmap Decode(int frame)
        {
            if (frame < 0 || frame >= _index.Length) return null;
            int off = _index[frame];
            if (off <= 0 || off + 17 > _data.Length) return null;
            using var ms = new MemoryStream(_data) { Position = off };
            using var reader = new BinaryReader(ms);
            short w = reader.ReadInt16();
            short h = reader.ReadInt16();
            reader.ReadInt16(); reader.ReadInt16(); reader.ReadInt16(); reader.ReadInt16();
            reader.ReadByte();
            int length = reader.ReadInt32();
            if (w <= 0 || h <= 0 || length <= 0) return null;
            long pos = (long)off + 17;
            if (pos + length > _data.Length) return null;
            byte[] raw = new byte[length];
            Array.Copy(_data, pos, raw, 0, length);
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

        public short FrameX(int frame) { return FrameXY(frame, true); }
        public short FrameY(int frame) { return FrameXY(frame, false); }

        private short FrameXY(int frame, bool isX)
        {
            if (frame < 0 || frame >= _index.Length) return 0;
            int off = _index[frame];
            if (off <= 0 || off + 17 > _data.Length) return 0;
            using var ms = new MemoryStream(_data) { Position = off };
            using var reader = new BinaryReader(ms);
            reader.ReadInt16(); reader.ReadInt16();
            short fx = reader.ReadInt16();
            short fy = reader.ReadInt16();
            return isX ? fx : fy;
        }

        // 画到画布：与客户端 MLibrary.Draw(index, Location) 一致 → 落点 = 帧XY
        public void Draw(Graphics g, int frame, int dx, int dy)
        {
            if (frame < 0 || frame >= _index.Length) return;
            int off = _index[frame];
            if (off <= 0 || off + 17 > _data.Length) return;
            using var ms = new MemoryStream(_data) { Position = off };
            using var reader = new BinaryReader(ms);
            reader.ReadInt16(); reader.ReadInt16();
            short fx = reader.ReadInt16();
            short fy = reader.ReadInt16();
            reader.ReadInt16(); reader.ReadInt16(); reader.ReadByte();
            int length = reader.ReadInt32();
            Bitmap bmp = Decode(frame);
            if (bmp == null) return;
            g.DrawImage(bmp, dx + fx, dy + fy, bmp.Width, bmp.Height);
            bmp.Dispose();
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
