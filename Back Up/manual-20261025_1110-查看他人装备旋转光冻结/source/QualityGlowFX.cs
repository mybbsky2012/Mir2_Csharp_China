using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SlimDX;
using SlimDX.Direct3D9;
using Color = System.Drawing.Color;

namespace Client.MirGraphics
{
    /// <summary>
    /// 品质物品动态发光（宝物紫 / 圣物蓝 / 神物金）。
    /// 效果 = 中心光核 + 旋转放射激光束 + 底层光晕，加法混合叠加在物品图标下面。
    /// 实现：首次用到时用 GDI+ 生成「60 帧 × 3 档」的旋转序列纹理（含在纹理里做渐变衰减，按档懒生成），
    /// 之后每帧只按 CMain.Time 取帧、以加法混合画一张图——无逐帧旋转计算、无状态、无分配。
    /// MirItemCell 每帧直接画到后备缓冲（未走 base.DrawControl），所以这里也不需要失效驱动。
    /// 开关：Mir2Config.ini [Game] QualityEffect（默认 True）。
    /// </summary>
    public static class QualityGlowFX
    {
        private const int FrameCount = 60;    // 每档帧数（60 帧 × 60ms = 3.6 秒一圈，每步 6°，顺滑）
        private const int TexSize = 72;       // 单帧纹理边长（光束半径约 34px，略溢出格子更有光感）
        private const int FrameDelay = 60;    // 毫秒/帧

        private static Texture[][] _frames;   // [3 档][FrameCount]，按档懒生成
        private static bool _failed;          // 生成失败就永久停用，不再反复尝试

        // 每档：内色（中心亮）/ 外色（束尾）
        private static readonly Color[][] Palette =
        {
            new[] { Color.FromArgb(215, 165, 255), Color.FromArgb(140, 90, 230) },   // 宝物 紫
            new[] { Color.FromArgb(150, 200, 255), Color.FromArgb(70, 140, 240) },   // 圣物 蓝
            new[] { Color.FromArgb(255, 235, 150), Color.FromArgb(255, 190, 60) },   // 神物 金
        };

        // 每档光束数量：神物最密
        private static readonly int[] BeamCounts = { 10, 14, 18 };

        /// <summary>
        /// 在格子中心画当前帧。grade 传 ItemInfo.Grade 的字节值（宝物=2 圣物=3 神物=4）。
        /// 调用时机：MirItemCell.DrawControl 画图标之前（光压在图标下面）。
        /// </summary>
        public static void Draw(byte grade, Point centre)
        {
            if (!Settings.QualityEffectOn || _failed || grade < 2 || grade > 4) return;

            int gi = grade - 2;
            if (_frames == null) _frames = new Texture[3][];
            if (_frames[gi] == null && !CreateFrames(gi)) return;

            Texture tex = _frames[gi][(int)(CMain.Time / FrameDelay % FrameCount)];
            if (tex == null || tex.Disposed) return;

            //★实际画了旋转光：标记需要每帧重绘，否则对话框缓存贴图不刷新，
            //查看他人装备（InspectDialog）等面板开着时光效会整帧冻结（用户实测）。
            QualityOutlineGlow.NeedAnimate = true;

            bool oldBlend = DXManager.Blending;
            float oldRate = DXManager.BlendingRate;
            BlendMode oldMode = DXManager.BlendingMode;

            DXManager.SetBlend(true, 1F);
            DXManager.Draw(tex, null,
                new Vector3(centre.X - TexSize / 2f, centre.Y - TexSize / 2f, 0F),
                new Color4(1F, 1F, 1F, 1F));

            DXManager.SetBlend(oldBlend, oldRate, oldMode);
        }

        /// <summary>
        /// 释放序列纹理（客户端关闭图库/重置设备时调用；留 null 让下次用到时重建）。
        /// </summary>
        public static void DisposeAll()
        {
            if (_frames == null) return;
            for (int g = 0; g < 3; g++)
            {
                if (_frames[g] == null) continue;
                for (int f = 0; f < FrameCount; f++)
                    if (_frames[g][f] != null && !_frames[g][f].Disposed)
                        _frames[g][f].Dispose();
                _frames[g] = null;
            }
            _frames = null;
        }

        private static bool CreateFrames(int gradeIdx)
        {
            try
            {
                _frames[gradeIdx] = new Texture[FrameCount];
                for (int f = 0; f < FrameCount; f++)
                    _frames[gradeIdx][f] = CreateFrame(gradeIdx, f * 360f / FrameCount);
                return true;
            }
            catch (Exception ex)
            {
                CMain.SaveError("QualityGlowFX 纹理生成失败: " + ex);
                _failed = true;
                return false;
            }
        }

        /// <summary>
        /// 生成单帧：参照 DXManager.CreateLights 的「锁纹 + GDI+ 位图」写法。
        /// 锁纹理期间只用 GDI+ 画位图、只 Unlock 不 Dispose DataRectangle。
        /// </summary>
        private static Texture CreateFrame(int gradeIdx, float rotationOffset)
        {
            Texture tex = new Texture(DXManager.Device, TexSize, TexSize, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);

            DataRectangle dr = tex.LockRectangle(0, LockFlags.Discard);
            using (Bitmap bmp = new Bitmap(TexSize, TexSize, TexSize * 4, PixelFormat.Format32bppArgb, dr.Data.DataPointer))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color inner = Palette[gradeIdx][0];
                Color outer = Palette[gradeIdx][1];
                float cx = TexSize / 2f, cy = TexSize / 2f;
                float maxR = TexSize / 2f - 2f;
                int count = BeamCounts[gradeIdx];

                // ① 底层光晕：柔和的品质色圆晕
                using (GraphicsPath halo = new GraphicsPath())
                {
                    halo.AddEllipse(new RectangleF(cx - 27, cy - 27, 54, 54));
                    using (PathGradientBrush b = new PathGradientBrush(halo))
                    {
                        b.CenterColor = Color.FromArgb(64, inner);
                        b.SurroundColors = new[] { Color.FromArgb(0, outer) };
                        g.FillPath(b, halo);
                    }
                }

                // ② 放射激光束：中心亮、尾端透明的细长三角，长短粗细按序号波动
                for (int i = 0; i < count; i++)
                {
                    float ang = (rotationOffset + i * 360f / count) * (float)Math.PI / 180f;
                    float len = maxR * (0.55f + 0.4f * (0.5f + 0.5f * (float)Math.Sin(i * 2.4)));
                    float w = 1.6f + 1.6f * (0.5f + 0.5f * (float)Math.Sin(i * 1.7));

                    float dx = (float)Math.Cos(ang), dy = (float)Math.Sin(ang);
                    float px = -dy, py = dx;

                    using (GraphicsPath beam = new GraphicsPath())
                    {
                        beam.AddPolygon(new[]
                        {
                            new PointF(cx, cy),
                            new PointF(cx + dx * len + px * w, cy + dy * len + py * w),
                            new PointF(cx + dx * len - px * w, cy + dy * len - py * w),
                        });
                        using (PathGradientBrush b = new PathGradientBrush(beam))
                        {
                            b.CenterColor = Color.FromArgb(215, inner);
                            b.SurroundColors = new[] { Color.FromArgb(0, outer) };
                            g.FillPath(b, beam);
                        }
                    }
                }

                // ③ 中心亮核：让图标后面有一团亮光
                using (GraphicsPath core = new GraphicsPath())
                {
                    core.AddEllipse(new RectangleF(cx - 15, cy - 15, 30, 30));
                    using (PathGradientBrush b = new PathGradientBrush(core))
                    {
                        b.CenterColor = Color.FromArgb(150, inner);
                        b.SurroundColors = new[] { Color.FromArgb(0, outer) };
                        g.FillPath(b, core);
                    }
                }
            }

            tex.UnlockRectangle(0);
            return tex;
        }
    }
}
