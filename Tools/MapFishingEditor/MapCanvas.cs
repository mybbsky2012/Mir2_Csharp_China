using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MapFishingEditor
{
    /// <summary>地图画布：自绘整图，支持缩放、笔刷预览、坐标拾取。</summary>
    internal sealed class MapCanvas : Panel
    {
        public MapDoc Doc;
        public Bitmap BaseBitmap;
        public int Zoom = 2;

        public bool ShowStand;
        public bool[] StandCache;

        public Rectangle BrushCell = Rectangle.Empty;  // 笔刷预览（格坐标）
        public Rectangle RectSelect = Rectangle.Empty; // 矩形模式预览
        public Point Hover = new Point(-1, -1);

        public event Action<Point> Hovered;
        public event Action<Point, MouseButtons> StrokeBegin;
        public event Action<Point, MouseButtons> StrokeMove;
        public event Action StrokeEnd;
        public event Action ZoomChanged;

        private bool _dragging;

        public MapCanvas()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoScroll = true;
            BackColor = Color.FromArgb(48, 50, 56);
            TabStop = true;
        }

        public void UpdateScrollSize()
        {
            if (Doc == null) return;
            AutoScrollMinSize = new Size(Doc.Width * Zoom + 2, Doc.Height * Zoom + 2);
        }

        public Point ToCell(Point p)
        {
            if (Doc == null) return new Point(-1, -1);
            int x = (p.X - AutoScrollPosition.X) / Zoom;
            int y = (p.Y - AutoScrollPosition.Y) / Zoom;
            return new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            if (Doc == null || BaseBitmap == null)
            {
                g.DrawString("请先「打开地图」载入一个 .map 文件", new Font("Microsoft YaHei UI", 11f),
                    Brushes.Gainsboro, new PointF(24, 24));
                return;
            }

            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            var dest = new Rectangle(0, 0, Doc.Width * Zoom, Doc.Height * Zoom);
            g.DrawImage(BaseBitmap, dest);

            // 可站位置覆盖层
            if (ShowStand && StandCache != null && Zoom >= 2)
            {
                using (var b = new SolidBrush(Color.FromArgb(170, 255, 226, 110)))
                {
                    for (int y = 0; y < Doc.Height; y++)
                        for (int x = 0; x < Doc.Width; x++)
                        {
                            if (!StandCache[y * Doc.Width + x]) continue;
                            g.FillRectangle(b, x * Zoom, y * Zoom, Zoom, Zoom);
                        }
                }
            }

            // 网格（放大到 6 倍以上才画，避免糊屏）
            if (Zoom >= 6)
            {
                using (var pen = new Pen(Color.FromArgb(38, 255, 255, 255)))
                {
                    for (int x = 0; x <= Doc.Width; x++)
                        g.DrawLine(pen, x * Zoom, 0, x * Zoom, Doc.Height * Zoom);
                    for (int y = 0; y <= Doc.Height; y++)
                        g.DrawLine(pen, 0, y * Zoom, Doc.Width * Zoom, y * Zoom);
                }
            }

            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255)))
                g.DrawRectangle(pen, 0, 0, dest.Width - 1, dest.Height - 1);

            // 矩形选区预览
            if (RectSelect.Width > 0 && RectSelect.Height > 0)
            {
                var r = new Rectangle(RectSelect.X * Zoom, RectSelect.Y * Zoom,
                                      RectSelect.Width * Zoom, RectSelect.Height * Zoom);
                using (var fill = new SolidBrush(Color.FromArgb(60, 255, 235, 60)))
                using (var pen = new Pen(Color.FromArgb(255, 255, 220, 40), 1.6f))
                {
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(pen, r);
                }
            }

            // 笔刷预览
            if (BrushCell.Width > 0 && BrushCell.Height > 0)
            {
                var r = new Rectangle(BrushCell.X * Zoom, BrushCell.Y * Zoom,
                                      BrushCell.Width * Zoom, BrushCell.Height * Zoom);
                using (var fill = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 1.2f))
                {
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(pen, r);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (Doc == null) return;
            var c = ToCell(e.Location);
            if (!Doc.InRange(c.X, c.Y)) return;
            _dragging = true;
            Capture = true;
            StrokeBegin?.Invoke(c, e.Button);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Doc == null) return;
            var c = ToCell(e.Location);
            bool valid = Doc.InRange(c.X, c.Y);
            Hover = valid ? c : new Point(-1, -1);
            Hovered?.Invoke(c);

            if (_dragging && valid)
                StrokeMove?.Invoke(c, e.Button);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                StrokeEnd?.Invoke();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Hover = new Point(-1, -1);
            Hovered?.Invoke(new Point(-1, -1));
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Doc == null) return;

            // 只有按住 Ctrl 才缩放，否则交给 Panel 走正常的滚动（符合常规习惯）
            if ((ModifierKeys & Keys.Control) == Keys.None)
            {
                base.OnMouseWheel(e);
                return;
            }

            float cx = (e.X - AutoScrollPosition.X) / (float)Zoom;
            float cy = (e.Y - AutoScrollPosition.Y) / (float)Zoom;

            int nz = Zoom + (e.Delta > 0 ? 1 : -1);
            nz = Math.Max(1, Math.Min(12, nz));
            if (nz == Zoom) return;
            Zoom = nz;
            UpdateScrollSize();
            AutoScrollPosition = new Point(
                Math.Max(0, (int)(cx * Zoom - e.X)),
                Math.Max(0, (int)(cy * Zoom - e.Y)));
            ZoomChanged?.Invoke();
            Invalidate();
        }

        /// <summary>全量重建底图</summary>
        public void RenderAll()
        {
            if (Doc == null) return;
            if (BaseBitmap == null || BaseBitmap.Width != Doc.Width || BaseBitmap.Height != Doc.Height)
            {
                BaseBitmap?.Dispose();
                BaseBitmap = new Bitmap(Doc.Width, Doc.Height, PixelFormat.Format32bppArgb);
            }
            RenderArea(0, 0, Doc.Width, Doc.Height);
            Invalidate();
        }

        /// <summary>局部重建（笔刷用，避免整图重算）</summary>
        public void RenderArea(int cx, int cy, int cw, int ch)
        {
            if (Doc == null || BaseBitmap == null) return;
            cx = Math.Max(0, cx); cy = Math.Max(0, cy);
            cw = Math.Min(cw, Doc.Width - cx); ch = Math.Min(ch, Doc.Height - cy);
            if (cw <= 0 || ch <= 0) return;

            var rect = new Rectangle(cx, cy, cw, ch);
            BitmapData bd = BaseBitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = bd.Stride;
                int[] row = new int[cw];
                for (int y = 0; y < ch; y++)
                {
                    for (int x = 0; x < cw; x++)
                        row[x] = ColorOf(cx + x, cy + y);
                    Marshal.Copy(row, 0, IntPtr.Add(bd.Scan0, y * stride), cw);
                }
            }
            finally
            {
                BaseBitmap.UnlockBits(bd);
            }
        }

        public static readonly Color CLand = Color.FromArgb(206, 202, 192);
        public static readonly Color CWater = Color.FromArgb(38, 74, 124);
        public static readonly Color COther = Color.FromArgb(120, 112, 102);
        public static readonly Color CFish = Color.FromArgb(60, 220, 230);

        public int ColorOf(int x, int y)
        {
            Color c;
            if (Doc.IsFish(x, y)) c = CFish;
            else if (Doc.IsWater(x, y)) c = CWater;
            else if (Doc.IsBlocked(x, y)) c = COther;
            else c = CLand;
            return c.ToArgb();
        }
    }
}
