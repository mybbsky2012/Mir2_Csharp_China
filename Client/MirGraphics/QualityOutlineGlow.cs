using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Client;
using SlimDX;
using SlimDX.Direct3D9;

namespace Client.MirGraphics
{
    /// <summary>
    /// 装备品质光效（宝物紫 / 圣物蓝 / 神物金）。
    ///
    /// ★v4（10-24 凌晨，按用户确认的效果图实现）：
    ///   衣服/头盔 = 发光：装备本体加法染色（亮部透出品质色）+ 沿剪影上升的火苗
    ///   武器     = 魔幻火焰：沿武器真实剪影（VisibleMask 采样）向上舔的火舌 + 上升火星
    ///
    /// 火苗贴的是「武器/装备自己的剪影点」，不再是在包围盒里乱飘；
    /// 每帧 sprite 数固定（火焰 12 + 火星 10），纹理只预生成一张柔光圆点。
    /// 开关：Mir2Config.ini [Game] QualityEffect。
    /// </summary>
    internal static class QualityOutlineGlow
    {
        // 品质三色（加深版，与效果图一致）
        private static readonly Color[] Colors =
        {
            Color.FromArgb(150, 70, 235),    // 宝物 紫
            Color.FromArgb(50, 130, 250),    // 圣物 蓝
            Color.FromArgb(255, 165, 25),    // 神物 金
        };

        /// <summary>轮廓光内圈偏移（像素）。</summary>
        private const int InnerSpread = 2;
        /// <summary>轮廓光外圈偏移（像素）。</summary>
        private const int OuterSpread = 4;

        /// <summary>火苗圆点纹理边长。</summary>
        private const int BlobSize = 20;

        /// <summary>装备本体染色透明度（加法混合，越高越亮）。</summary>
        private const int TintAlpha = 55;

        private static readonly Dictionary<int, Texture> _blobs = new Dictionary<int, Texture>();
        private static bool _blobFailed;

        /// <summary>
        /// ★本帧内画过品质光效（呼吸动画的时间驱动绘制）。
        /// GameScene.Process 据此决定是否每帧强制场景重绘——
        /// 场景/对话框渲染走缓存贴图，没有 Redraw 时整个画面冻结（用户实测：不动鼠标发光就不动）。
        /// </summary>
        internal static bool NeedAnimate;

        private static Texture Blob(int size)
        {
            Texture tex;
            if (_blobs.TryGetValue(size, out tex) && tex != null && !tex.Disposed) return tex;
            if (_blobFailed) return null;

            try
            {
                tex = new Texture(DXManager.Device, size, size, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);

                DataRectangle dr = tex.LockRectangle(0, LockFlags.Discard);
                using (Bitmap bmp = new Bitmap(size, size, size * 4, PixelFormat.Format32bppArgb, dr.Data.DataPointer))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(0, 0, 0, 0));
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    using (GraphicsPath p = new GraphicsPath())
                    {
                        p.AddEllipse(size * 0.08f, size * 0.08f, size * 0.84f, size * 0.84f);
                        using (PathGradientBrush b = new PathGradientBrush(p))
                        {
                            b.CenterColor = Color.FromArgb(255, Color.White);
                            b.SurroundColors = new[] { Color.FromArgb(0, Color.White) };
                            g.FillPath(b, p);
                        }
                    }
                }

                tex.UnlockRectangle(0);
                _blobs[size] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                CMain.SaveError("QualityOutlineGlow 圆点纹理生成失败: " + ex);
                _blobFailed = true;
                return null;
            }
        }

        // ---- 剪影采样点缓存：lib → frameIndex → 底边采样点（纹理坐标） ----
        private static readonly Dictionary<MLibrary, Dictionary<int, List<Point>>> _silhouetteCache =
            new Dictionary<MLibrary, Dictionary<int, List<Point>>>();

        // ---- 诊断日志 ----
        private static int _drawLogCount;

        internal static void AppendLog(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(Application.StartupPath, "QualityGlow.log"),
                    string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine),
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        internal static void DebugLoad(byte armourGrade, byte weaponGrade)
        {
            if (armourGrade < 2 && weaponGrade < 2) return;
            AppendLog(string.Format("收到玩家包 盔甲品质={0} 武器品质={1}", armourGrade, weaponGrade));
        }

        /// <summary>
        /// 衣服（及头饰）品质发光：本体染色 + 贴边亮线 + 沿剪影上升的火苗。
        /// </summary>
        public static void Draw(MLibrary lib, int frameIndex, Point location, byte grade)
        {
            if (!Settings.QualityEffectOn || lib == null || grade < 2 || grade > 4) return;

            Color colour = Colors[grade - 2];

            if (_drawLogCount < 12)
            {
                _drawLogCount++;
                AppendLog(string.Format("衣服发光 grade={0} 帧={1}", grade, frameIndex));
            }

            // ★描边已去掉（用户要求：不要轮廓线，只要「本身在发光」）
            DrawTint(lib, frameIndex, location, colour, TintAlpha);

            DrawSilhouetteFlames(lib, frameIndex, location, colour, 8, 22, 70, sparks: 4);
        }

        /// <summary>
        /// 内观立绘（静态图）★v4（11-06）：
        ///   - 去掉 8 方向偏移描边（DrawRim），只保留染色 —— 与外观 Draw() 口径统一；
        ///   - 有武器时沿剪影冒火苗，衣服/头盔只染色；
        ///   - 染色透明度随时间「呼吸」，需要场景每帧重绘（NeedAnimate）。
        /// </summary>
        public static void DrawPortrait(MLibrary lib, int frameIndex, Point location, byte grade, bool withFlame)
        {
            if (!Settings.QualityEffectOn || lib == null || grade < 2 || grade > 4) return;

            NeedAnimate = true;

            Color colour = Colors[grade - 2];

            // 呼吸节奏：约 1.9 秒一个周期，0→1→0 平滑往复
            double pulse = (Math.Sin(CMain.Time / 300.0) + 1.0) / 2.0;

            //★描边已去掉（11-06，与外观 Draw() 统一）：不要轮廓线，只要「本身在发光」。
            //  原 DrawRim 是把整张立绘在圆周 8 个方向各偏移一次、两层共叠 16 遍，
            //  内观素材是硬边（alpha 只有 0/255），叠出来就是一圈锯齿重影；
            //  顶点色又走乘法调制，金色装备×蓝色品质色会变成暗绿碎片 —— 用户截图里的「绿色花边」。
            DrawTint(lib, frameIndex, location, colour, 56 + (int)(26 * pulse));

            if (withFlame)
                DrawSilhouetteFlames(lib, frameIndex, location, colour, 13, 32, 135 + (int)(50 * pulse), sparks: 13);
        }

        /// <summary>
        /// 武器魔幻火焰：沿武器剪影向上舔的火舌 + 上升火星（无描边）。
        /// </summary>
        public static void DrawFlame(MLibrary lib, int frameIndex, Point location, byte grade)
        {
            if (!Settings.QualityEffectOn || lib == null || grade < 2 || grade > 4) return;

            Color colour = Colors[grade - 2];

            DrawSilhouetteFlames(lib, frameIndex, location, colour, 12, 30, 90, sparks: 10);
        }

        /// <summary>
        /// 装备本体染色：同一帧用品质色加法混合再画一次。
        /// 亮部（盔甲高光、金属）被染上品质色 → 看起来「装备在发光」，暗部几乎不受影响。
        /// </summary>
        private static void DrawTint(MLibrary lib, int frameIndex, Point location, Color colour, int alpha)
        {
            bool oldBlend = DXManager.Blending;
            float oldRate = DXManager.BlendingRate;
            BlendMode oldMode = DXManager.BlendingMode;

            DXManager.SetBlend(true, 1F);
            try
            {
                lib.Draw(frameIndex, location, Color.FromArgb(alpha, colour), true);
            }
            finally
            {
                DXManager.SetBlend(oldBlend, oldRate, oldMode);
            }
        }

        //★DrawRim（8 方向偏移叠画）已删除（11-06）：
        //  内观素材是硬边，16 层偏移副本叠出来是一圈锯齿重影，且顶点色乘法调制会让金饰变暗绿，
        //  正是用户反馈的「内观描边透明花边」。内观现在只做 DrawTint 染色。
        //  世界外观 Draw() 早在 10-24 也已经是「只染色 + 剪影火苗」，两边口径现在一致。

        /// <summary>
        /// 沿真实剪影向上舔的火舌 + 火星。
        /// 剪影点来自图库的 VisibleMask（每列取最低可见像素 = 底边），火焰贴着装备烧。
        /// </summary>
        private static void DrawSilhouetteFlames(MLibrary lib, int frameIndex, Point location,
                                                 Color colour, int flameCount, int peakHeight, int peakAlpha, int sparks)
        {
            Point offset = lib.GetOffSet(frameIndex);
            Size size = lib.GetTrueSize(frameIndex);
            if (size.Width <= 0 || size.Height <= 0) return;

            List<Point> pts = SilhouettePoints(lib, frameIndex);
            if (pts == null || pts.Count == 0)
            {
                // 图库没有可用剪影（未加载/纯色块）时退回包围盒方案
                Texture boxTex = Blob(BlobSize);
                if (boxTex == null) return;
                DrawBoxFlames(location, offset, size, colour, flameCount, peakHeight, peakAlpha, boxTex);
                return;
            }

            // 剪影左上角（与 lib.Draw(..., offSet:true) 同源）
            int left = location.X + offset.X;
            int top = location.Y + offset.Y;

            bool oldBlend = DXManager.Blending;
            float oldRate = DXManager.BlendingRate;
            BlendMode oldMode = DXManager.BlendingMode;

            DXManager.SetBlend(true, 1F);

            try
            {
                // ---- 火舌：贴着剪影底边往上舔 ----
                for (int i = 0; i < flameCount; i++)
                {
                    Point p = pts[(i * 5 + (int)((CMain.Time / 900) % pts.Count)) % pts.Count];

                    // 每颗火苗沿自己的相位循环上升
                    double t = ((CMain.Time / 700.0) + i * (1.0 / flameCount)) % 1.0;

                    int x = left + p.X + (int)Math.Round(Math.Sin((CMain.Time / 260.0) + i * 1.7) * 2.5);
                    int y = top + p.Y - (int)(peakHeight * t);

                    int sz = 12 + (i % 3) * 4;                        // 12/16/20 三档大小
                    int alpha = (int)(peakAlpha * (1.0 - t * 0.7));
                    if (alpha <= 6) continue;

                    Texture tex = Blob(sz);
                    if (tex == null) return;

                    var pos = new SlimDX.Vector3(x - sz / 2f, y - sz / 2f, 0F);
                    var col = new SlimDX.Color4(alpha / 255f, colour.R / 255f, colour.G / 255f, colour.B / 255f);

                    DXManager.Draw(tex, new Rectangle(0, 0, sz, sz), pos, col);
                }

                // ---- 火星：小亮点，飘得更高 ----
                Texture sparkTex = Blob(6);
                if (sparkTex == null) return;

                for (int i = 0; i < sparks; i++)
                {
                    Point p = pts[(i * 7 + 3) % pts.Count];
                    double t = ((CMain.Time / 1100.0) + i * (1.0 / sparks)) % 1.0;

                    int x = left + p.X + (int)Math.Round(Math.Sin((CMain.Time / 300.0) + i * 2.1) * 5.0);
                    int y = top + p.Y - (int)((peakHeight + 26) * t);

                    int alpha = (int)(150 * (1.0 - t));
                    if (alpha <= 8) continue;

                    var pos = new SlimDX.Vector3(x - 3f, y - 3f, 0F);
                    var col = new SlimDX.Color4(alpha / 255f,
                        Math.Min(255, colour.R + 90) / 255f,
                        Math.Min(255, colour.G + 90) / 255f,
                        Math.Min(255, colour.B + 90) / 255f);

                    DXManager.Draw(sparkTex, new Rectangle(0, 0, 6, 6), pos, col);
                }
            }
            finally
            {
                DXManager.SetBlend(oldBlend, oldRate, oldMode);
            }
        }

        /// <summary>没有剪影数据时的兜底：在包围盒中段 60% 宽度上冒火。</summary>
        private static void DrawBoxFlames(Point location, Point offset, Size size, Color colour,
                                          int count, int peakHeight, int peakAlpha, Texture tex)
        {
            int left = location.X + offset.X;
            int top = location.Y + offset.Y;

            int bodySpan = Math.Max(8, (int)(size.Width * 0.6));
            int baseX = left + (size.Width - bodySpan) / 2;
            int baseY = top + (int)(size.Height * 0.55);

            bool oldBlend = DXManager.Blending;
            float oldRate = DXManager.BlendingRate;
            BlendMode oldMode = DXManager.BlendingMode;

            DXManager.SetBlend(true, 1F);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    double t = ((CMain.Time / 780.0) + i * (1.0 / count)) % 1.0;

                    int x = baseX + (int)(bodySpan * (0.15 + 0.7 * ((i * 0.37) % 1.0)))
                            + (int)Math.Round(Math.Sin((CMain.Time / 260.0) + i * 1.7) * 3.0);
                    int y = baseY - (int)(peakHeight * t);

                    int alpha = (int)(peakAlpha * (1.0 - t * 0.75));
                    if (alpha <= 6) continue;

                    var pos = new SlimDX.Vector3(x - BlobSize / 2f, y - BlobSize / 2f, 0F);
                    var col = new SlimDX.Color4(alpha / 255f, colour.R / 255f, colour.G / 255f, colour.B / 255f);
                    DXManager.Draw(tex, new Rectangle(0, 0, BlobSize, BlobSize), pos, col);
                }
            }
            finally
            {
                DXManager.SetBlend(oldBlend, oldRate, oldMode);
            }
        }

        /// <summary>
        /// 采样剪影底边点（纹理坐标）：把宽度分成 ~14 列，每列取最低的可见像素。
        /// 缓存按 (lib, frameIndex)，lib 是静态实例不会泄漏；缓存总量超限就清空。
        /// </summary>
        private static List<Point> SilhouettePoints(MLibrary lib, int frameIndex)
        {
            Dictionary<int, List<Point>> perLib;
            if (!_silhouetteCache.TryGetValue(lib, out perLib))
            {
                if (_silhouetteCache.Count > 24)
                    _silhouetteCache.Clear();
                perLib = new Dictionary<int, List<Point>>();
                _silhouetteCache[lib] = perLib;
            }

            List<Point> pts;
            if (perLib.TryGetValue(frameIndex, out pts))
                return pts;

            pts = new List<Point>();
            try
            {
                // ★必须用 VisibleBounds（可见区在整张画布里的坐标）来采样。
                //   之前用 GetTrueSize 的尺寸当「从 (0,0) 开始」的框，只有帧本身贴边时才对；
                //   内观 StateItem 帧四周有大量透明边，结果采样落在画布左上角的空白区，
                //   火苗就飘在人物左上方（用户实测截图）。世界外观的 CArmours/CWeapons 帧基本贴边，
                //   所以那边看起来一直正常。
                var mi = lib.GetMImage(frameIndex);
                Rectangle vb = lib.GetVisibleBounds(frameIndex);

                if (mi != null && vb.Width > 0 && vb.Height > 0)
                {
                    int columns = Math.Min(14, vb.Width);
                    for (int c = 0; c < columns; c++)
                    {
                        int x = vb.Left + (int)((c + 0.5) * vb.Width / columns);
                        int bottom = -1;
                        for (int y = vb.Bottom - 1; y >= vb.Top; y--)
                        {
                            if (mi.VisiblePixel(new Point(x, y)))
                            {
                                bottom = y;
                                break;
                            }
                        }
                        if (bottom >= 0)
                            pts.Add(new Point(x, bottom));
                    }
                }
            }
            catch
            {
                pts.Clear();
            }

            perLib[frameIndex] = pts;
            return pts;
        }
    }
}
