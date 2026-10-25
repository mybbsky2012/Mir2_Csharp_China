using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MapFishingEditor
{
    /// <summary>无界面自检：验证地图解析、计数、染色与渲染，结果写日志文件。</summary>
    internal static class SelfTest
    {
        public static void Run(string mapPath, string logPath)
        {
            var sb = new StringBuilder();
            try
            {
                var doc = MapDoc.Load(mapPath);
                sb.AppendLine("OK 载入 " + mapPath);
                sb.AppendLine(string.Format("   尺寸 {0} x {1}   xor=0x{2:X4}   文件 {3} 字节",
                    doc.Width, doc.Height, doc.Xor, doc.Raw.Length));

                long fish = doc.CountFish();
                sb.AppendLine("   已有钓鱼点 " + fish + " 格");

                // 地形分类统计
                long water = 0, other = 0, land = 0;
                for (int y = 0; y < doc.Height; y++)
                    for (int x = 0; x < doc.Width; x++)
                    {
                        if (doc.IsWater(x, y)) water++;
                        else if (doc.IsBlocked(x, y)) other++;
                        else land++;
                    }
                sb.AppendLine(string.Format("   水面 {0}   其他阻挡 {1}   陆地 {2}", water, other, land));

                // 模拟一次矩形填充（只在内存里，不写文件）
                int bx = doc.Width / 2, by = doc.Height / 2;
                int painted = 0, skipped = 0;
                int fx0 = -1, fy0 = -1;
                for (int y = by - 10; y <= by + 10; y++)
                    for (int x = bx - 10; x <= bx + 10; x++)
                    {
                        if (!doc.InRange(x, y)) continue;
                        if (!doc.IsWater(x, y)) { skipped++; continue; }
                        doc.SetLight(x, y, 100);
                        if (fx0 < 0) { fx0 = x; fy0 = y; }
                        painted++;
                    }
                sb.AppendLine(string.Format("   模拟画笔：中心 ({0},{1}) 21x21 区域，染色 {2} 格，非水面跳过 {3} 格",
                    bx, by, painted, skipped));

                // 渲染预览图
                string png = Path.ChangeExtension(logPath, ".png");
                RenderPreview(doc, png);
                sb.AppendLine("   预览图 " + png);

                // 校验：读回被染的格
                if (painted > 0)
                {
                    byte l = doc.GetLight(fx0, fy0);
                    sb.AppendLine(string.Format("   读回染色格 ({0},{1}) light = {2} {3}",
                        fx0, fy0, l, l == 100 ? "OK" : "异常!"));
                    if (l != 100) throw new InvalidDataException("light 写入后读回不一致");
                }
                else
                {
                    sb.AppendLine("   注意：中心区域没有水面格（可能是城区地图），画笔未染色");
                }

                sb.AppendLine("SELFTEST PASSED");
            }
            catch (Exception ex)
            {
                sb.AppendLine("SELFTEST FAILED: " + ex);
            }
            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>把主界面渲染成 PNG（窗口建在屏幕外，不打扰桌面）</summary>
        public static void Shot(string mapPath, string pngPath)
        {
            var form = new MainForm(mapPath);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-6000, -6000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            System.Threading.Thread.Sleep(600);
            Application.DoEvents();

            using (var bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(pngPath, ImageFormat.Png);
            }
            form.Close();
        }

        private static void RenderPreview(MapDoc doc, string path)
        {
            using (var bmp = new Bitmap(doc.Width, doc.Height, PixelFormat.Format32bppArgb))
            {
                var bd = bmp.LockBits(new Rectangle(0, 0, doc.Width, doc.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int[] row = new int[doc.Width];
                    for (int y = 0; y < doc.Height; y++)
                    {
                        for (int x = 0; x < doc.Width; x++)
                        {
                            Color c;
                            if (doc.IsFish(x, y)) c = MapCanvas.CFish;
                            else if (doc.IsWater(x, y)) c = MapCanvas.CWater;
                            else if (doc.IsBlocked(x, y)) c = MapCanvas.COther;
                            else c = MapCanvas.CLand;
                            row[x] = c.ToArgb();
                        }
                        Marshal.Copy(row, 0, IntPtr.Add(bd.Scan0, y * bd.Stride), doc.Width);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bd);
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
