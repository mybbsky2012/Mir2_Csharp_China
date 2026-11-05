using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

// 图像小工具（只读源文件，写新文件）：
//   ImgTool zoom  <in> <out> <倍数>                     最近邻放大，便于肉眼看清像素
//   ImgTool flat  <in> <out> <R,G,B>                    把透明区域铺成实色背景，检查"哪里被抠掉了"
//   ImgTool alpha <in>                                  打印 alpha 直方图（0 / 1-254 / 255）
//   ImgTool opaque <in> <out> <阈值>                    把「alpha=0 但带有颜色」的像素恢复成不透明
//                                                       （黑底被抠掉留下的洞，颜色还在，就能救回来）
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        if (args.Length < 3) { Console.WriteLine("用法：ImgTool zoom|flat ..."); return 2; }

        string cmd = args[0].ToLowerInvariant();
        using var src = new Bitmap(args[1]);

        if (cmd == "zoom")
        {
            int s = int.Parse(args[3]);
            using var dst = new Bitmap(src.Width * s, src.Height * s, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, 0, 0, dst.Width, dst.Height);
            }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}  {dst.Width}x{dst.Height}");
            return 0;
        }

        if (cmd == "flat")
        {
            var parts = args[3].Split(',');
            var bg = Color.FromArgb(255, int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
            using var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
            {
                g.Clear(bg);
                g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}（背景 {bg.R},{bg.G},{bg.B}）");
            return 0;
        }

        if (cmd == "opaque")
        {
            int th = int.Parse(args[3]);
            using var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            int fixedCount = 0;
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    Color c = src.GetPixel(x, y);
                    if (c.A == 0 && Math.Max(c.R, Math.Max(c.G, c.B)) >= th)
                    {
                        c = Color.FromArgb(255, c.R, c.G, c.B);
                        fixedCount++;
                    }
                    dst.SetPixel(x, y, c);
                }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}，恢复不透明像素 {fixedCount} 个（阈值 {th}）");
            return 0;
        }

        if (cmd == "holes")
        {
            // 从四边洪水填充（4 连通）标记「真背景」，未被访问到的透明像素 = 被身体包住的洞，填成不透明
            int w = src.Width, h = src.Height;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = src.GetPixel(x, y);

            var outside = new bool[w * h];
            var st = new Stack<int>();
            void Push(int i) { if (!outside[i] && px[i].A == 0) { outside[i] = true; st.Push(i); } }
            for (int x = 0; x < w; x++) { Push(x); Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Push(y * w); Push(y * w + w - 1); }
            while (st.Count > 0)
            {
                int c = st.Pop(); int cx = c % w, cy = c / w;
                if (cx > 0) Push(c - 1);
                if (cx < w - 1) Push(c + 1);
                if (cy > 0) Push(c - w);
                if (cy < h - 1) Push(c + w);
            }

            int filled = 0;
            using var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = px[y * w + x];
                    if (c.A == 0 && !outside[y * w + x]) { c = Color.FromArgb(255, c.R, c.G, c.B); filled++; }
                    dst.SetPixel(x, y, c);
                }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}，填平封闭空洞 {filled} 个像素");
            return 0;
        }

        if (cmd == "close")
        {
            // 形态学闭运算（先膨胀再腐蚀）：把散在身体里的 1~2 像素透明小洞补上，
            // 外形轮廓不会被撑大（膨胀后被腐蚀回去）。补上的像素取周围不透明像素的平均色。
            int w = src.Width, h = src.Height, r = int.Parse(args[3]);
            var px = new Color[w * h];
            var mask = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = src.GetPixel(x, y);
                    px[y * w + x] = c;
                    mask[y * w + x] = c.A > 0;
                }

            bool[] Dilate(bool[] m)
            {
                var o = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (!m[y * w + x]) continue;
                        for (int dy = -r; dy <= r; dy++)
                        {
                            int ny = y + dy; if (ny < 0 || ny >= h) continue;
                            for (int dx = -r; dx <= r; dx++)
                            {
                                int nx = x + dx; if (nx < 0 || nx >= w) continue;
                                o[ny * w + nx] = true;
                            }
                        }
                    }
                return o;
            }
            bool[] Erode(bool[] m)
            {
                var o = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool all = true;
                        for (int dy = -r; dy <= r && all; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) { all = false; break; }
                            for (int dx = -r; dx <= r; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w || !m[ny * w + nx]) { all = false; break; }
                            }
                        }
                        o[y * w + x] = all;
                    }
                return o;
            }

            bool[] closed = Erode(Dilate(mask));

            int filled = 0;
            using var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = px[i];
                    if (!mask[i] && closed[i])
                    {
                        int rs = 0, gs = 0, bs = 0, cnt = 0;
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int ny = y + dy; if (ny < 0 || ny >= h) continue;
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx; if (nx < 0 || nx >= w) continue;
                                int j = ny * w + nx;
                                if (!mask[j]) continue;
                                rs += px[j].R; gs += px[j].G; bs += px[j].B; cnt++;
                            }
                        }
                        c = cnt > 0 ? Color.FromArgb(255, rs / cnt, gs / cnt, bs / cnt) : Color.FromArgb(255, c);
                        filled++;
                    }
                    dst.SetPixel(x, y, c);
                }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}，闭运算半径 {r}，补洞像素 {filled} 个");
            return 0;
        }

        if (cmd == "fill2")
        {
            // 四方向填充：一个透明像素若在上下左右各 k 格内都能碰到不透明像素，
            // 就说明它被身体夹在中间（黑底被抠出的裂缝/碎点），补成不透明。
            // 轮廓上的背景像素至少有一个方向是空的，不会被误填，所以外形不会变形。
            int w = src.Width, h = src.Height, k = int.Parse(args[3]);
            var px = new Color[w * h];
            var mask = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = src.GetPixel(x, y);
                    px[y * w + x] = c;
                    mask[y * w + x] = c.A > 0;
                }

            int filled = 0;
            using var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = px[i];
                    if (!mask[i])
                    {
                        bool l = false, r = false, u = false, d = false;
                        for (int t = 1; t <= k; t++)
                        {
                            if (!l && x - t >= 0 && mask[y * w + x - t]) l = true;
                            if (!r && x + t < w && mask[y * w + x + t]) r = true;
                            if (!u && y - t >= 0 && mask[(y - t) * w + x]) u = true;
                            if (!d && y + t < h && mask[(y + t) * w + x]) d = true;
                            if (l && r && u && d) break;
                        }
                        if (l && r && u && d)
                        {
                            int rs = 0, gs = 0, bs = 0, cnt = 0;
                            for (int dy = -2; dy <= 2; dy++)
                            {
                                int ny = y + dy; if (ny < 0 || ny >= h) continue;
                                for (int dx = -2; dx <= 2; dx++)
                                {
                                    int nx = x + dx; if (nx < 0 || nx >= w) continue;
                                    int j = ny * w + nx;
                                    if (!mask[j]) continue;
                                    rs += px[j].R; gs += px[j].G; bs += px[j].B; cnt++;
                                }
                            }
                            c = cnt > 0 ? Color.FromArgb(255, rs / cnt, gs / cnt, bs / cnt) : Color.FromArgb(255, c);
                            filled++;
                        }
                    }
                    dst.SetPixel(x, y, c);
                }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}，四方向补洞 k={k}，补了 {filled} 个像素");
            return 0;
        }

        if (cmd == "bg")
        {
            // 背景重建（形态学开运算 + 洪水填充）：
            //   1) 把「透明」当背景掩码，先腐蚀 r 次 —— 比 2r+1 更窄的透明通道会消失
            //   2) 从四边洪水填充，只有和画布边缘连通的才算真背景
            //   3) 再把背景膨胀 r 次还原
            // 剩下的透明像素 = 被身体夹住/被细缝连出去的黑底残留，补成不透明。
            int w = src.Width, h = src.Height, r = int.Parse(args[3]);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[y * w + x] = src.GetPixel(x, y);

            bool[] Erode(bool[] m)
            {
                var o = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool all = true;
                        for (int dy = -1; dy <= 1 && all; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) { all = false; break; }
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w || !m[ny * w + nx]) { all = false; break; }
                            }
                        }
                        o[y * w + x] = all;
                    }
                return o;
            }
            bool[] Dilate(bool[] m)
            {
                var o = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (!m[y * w + x]) continue;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = y + dy; if (ny < 0 || ny >= h) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx; if (nx < 0 || nx >= w) continue;
                                o[ny * w + nx] = true;
                            }
                        }
                    }
                return o;
            }

            // 外扩 r 圈「透明」边界，否则腐蚀会把画布边缘啃掉、洪水填充就没有起点了
            int pad = r + 2;
            int pw = w + 2 * pad, ph = h + 2 * pad;
            var bg = new bool[pw * ph];
            for (int i = 0; i < pw * ph; i++) bg[i] = true;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    bg[(y + pad) * pw + (x + pad)] = px[y * w + x].A == 0;

            bool[] ErodeP(bool[] m)
            {
                var o = new bool[pw * ph];
                for (int y = 0; y < ph; y++)
                    for (int x = 0; x < pw; x++)
                    {
                        bool all = true;
                        for (int dy = -1; dy <= 1 && all; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= ph) { all = false; break; }
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= pw || !m[ny * pw + nx]) { all = false; break; }
                            }
                        }
                        o[y * pw + x] = all;
                    }
                return o;
            }
            bool[] DilateP(bool[] m)
            {
                var o = new bool[pw * ph];
                for (int y = 0; y < ph; y++)
                    for (int x = 0; x < pw; x++)
                    {
                        if (!m[y * pw + x]) continue;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = y + dy; if (ny < 0 || ny >= ph) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx; if (nx < 0 || nx >= pw) continue;
                                o[ny * pw + nx] = true;
                            }
                        }
                    }
                return o;
            }

            var core = bg;
            for (int t = 0; t < r; t++) core = ErodeP(core);

            var outside = new bool[pw * ph];
            var st = new Stack<int>();
            void Seed(int i) { if (core[i] && !outside[i]) { outside[i] = true; st.Push(i); } }
            // 直接以整圈外扩区为起点（腐蚀会把最外两圈吃掉，不能只取第一行/列）
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                    if (x < pad || x >= pw - pad || y < pad || y >= ph - pad)
                        Seed(y * pw + x);
            while (st.Count > 0)
            {
                int c = st.Pop(); int cx = c % pw, cy = c / pw;
                if (cx > 0) Seed(c - 1);
                if (cx < pw - 1) Seed(c + 1);
                if (cy > 0) Seed(c - pw);
                if (cy < ph - 1) Seed(c + pw);
            }

            var realBgP = outside;
            for (int t = 0; t < r; t++) realBgP = DilateP(realBgP);

            var realBg = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    realBg[y * w + x] = realBgP[(y + pad) * pw + (x + pad)];

            int filled = 0;
            using var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = px[i];
                    if (c.A == 0 && !realBg[i])
                    {
                        int rs = 0, gs = 0, bs = 0, cnt = 0;
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int ny = y + dy; if (ny < 0 || ny >= h) continue;
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx; if (nx < 0 || nx >= w) continue;
                                Color q = px[ny * w + nx];
                                if (q.A == 0) continue;
                                rs += q.R; gs += q.G; bs += q.B; cnt++;
                            }
                        }
                        c = cnt > 0 ? Color.FromArgb(255, rs / cnt, gs / cnt, bs / cnt) : Color.FromArgb(255, c);
                        filled++;
                    }
                    dst.SetPixel(x, y, c);
                }
            dst.Save(args[2], ImageFormat.Png);
            Console.WriteLine($"已写出 {args[2]}，背景重建 r={r}，补洞 {filled} 像素");
            return 0;
        }

        if (cmd == "grid")
        {
            int gx = int.Parse(args[3]), gy = int.Parse(args[4]);
            int gw = int.Parse(args[5]), gh = int.Parse(args[6]);
            Console.WriteLine($"区域 x={gx}..{gx + gw - 1} y={gy}..{gy + gh - 1}（字符：'.'=透明  '#'=不透明  数字=半透明档）");
            for (int y = gy; y < gy + gh && y < src.Height; y++)
            {
                var sb = new System.Text.StringBuilder();
                for (int x = gx; x < gx + gw && x < src.Width; x++)
                {
                    int a = src.GetPixel(x, y).A;
                    sb.Append(a == 0 ? '.' : a == 255 ? '#' : (char)('0' + Math.Min(9, a / 26)));
                }
                Console.WriteLine($"{y,4} {sb}");
            }
            return 0;
        }

        if (cmd == "alpha")
        {
            int zero = 0, semi = 0, full = 0;
            long sum = 0;
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    int a = src.GetPixel(x, y).A;
                    sum += a;
                    if (a == 0) zero++; else if (a == 255) full++; else semi++;
                }
            int n = src.Width * src.Height;
            Console.WriteLine($"{args[1]}: {src.Width}x{src.Height}  A=0:{zero}  半透明:{semi}  不透明:{full}  平均A:{(double)sum / n:F1}");
            return 0;
        }

        Console.WriteLine("未知命令");
        return 2;
    }
}
