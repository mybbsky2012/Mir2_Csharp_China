using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Client.MirControls;
using Client.MirScenes;
using Client.Utils;
using SlimDX;
using SlimDX.Direct3D9;
using Blend = SlimDX.Direct3D9.Blend;

namespace Client.MirGraphics
{
    class DXManager
    {
        public static List<MImage> TextureList = new List<MImage>();
        public static List<MirControl> ControlList = new List<MirControl>();

        public static Device Device;
        public static Sprite Sprite;
        public static Line Line;

        public static Surface CurrentSurface;
        public static Surface MainSurface;
        public static PresentParameters Parameters;
        public static bool DeviceLost;
        public static float Opacity = 1F;
        public static bool Blending;
        public static float BlendingRate;
        public static BlendMode BlendingMode;


        public static Texture RadarTexture;
        public static List<Texture> Lights = new List<Texture>();
        public static Texture PoisonDotBackground;

        public static Texture FloorTexture, LightTexture;
        public static Surface FloorSurface, LightSurface;

        public static PixelShader GrayScalePixelShader;
        public static PixelShader NormalPixelShader;
        public static PixelShader MagicPixelShader;

        public static bool GrayScale;

        public static Point[] LightSizes =
        {
            new Point(125,95),
            new Point(205,156),
            new Point(285,217),
            new Point(365,277),
            new Point(445,338),
            new Point(525,399),
            new Point(605,460),
            new Point(685,521),
            new Point(765,581),
            new Point(845,642),
            new Point(925,703)
        };

        public static void Create()
        {
            Parameters = new PresentParameters
            {
                BackBufferFormat = Format.X8R8G8B8,
                PresentFlags = PresentFlags.LockableBackBuffer,
                BackBufferWidth = Settings.ScreenWidth,
                BackBufferHeight = Settings.ScreenHeight,
                SwapEffect = SwapEffect.Discard,
                PresentationInterval = Settings.FPSCap ? PresentInterval.One : PresentInterval.Immediate,
                Windowed = !Settings.FullScreen,
            };


            Direct3D d3d = new Direct3D();

            Capabilities devCaps = d3d.GetDeviceCaps(0, DeviceType.Hardware);
            DeviceType devType = DeviceType.Reference;
            CreateFlags devFlags = CreateFlags.HardwareVertexProcessing;

            if (devCaps.VertexShaderVersion.Major >= 2 && devCaps.PixelShaderVersion.Major >= 2)
                devType = DeviceType.Hardware;

            if ((devCaps.DeviceCaps & DeviceCaps.HWTransformAndLight) != 0)
                devFlags = CreateFlags.HardwareVertexProcessing;


            if ((devCaps.DeviceCaps & DeviceCaps.PureDevice) != 0)
                devFlags |= CreateFlags.PureDevice;


            Device = new Device(d3d, d3d.Adapters.DefaultAdapter.Adapter, devType, Program.Form.Handle, devFlags, Parameters);

            Device.SetDialogBoxMode(true);

            LoadTextures();
            LoadPixelsShaders();
        }

        private static unsafe void LoadPixelsShaders()
        {
            var shaderNormalPath = Settings.ShadersPath + "normal.ps";
            var shaderGrayScalePath = Settings.ShadersPath + "grayscale.ps";
            var shaderMagicPath = Settings.ShadersPath + "magic.ps";

            // 着色器只有几百字节，但这里以前用 15 秒的阻塞等待：
            // 服务端资源目录里根本没有 normal.ps / magic.ps（只有 grayscale.ps），
            // 服务器一连不上，这里就会为两个「永远下不来的文件」各白等 15 秒，
            // 登录界面出来前先卡半分钟。
            // 着色器缺失只影响个别特效（灰度/魔法滤镜）的呈现，SetNormal/SetGrayscale/SetBlendMagic
            // 对 null 都有保护，不影响登录与进游戏 —— 所以改成非阻塞：
            // 本地有就编译；没有就排进后台下载队列立刻返回，下次启动客户端自然就有了。
            TryLoadShader(shaderNormalPath);
            TryLoadShader(shaderGrayScalePath);
            TryLoadShader(shaderMagicPath);
        }

        private static unsafe void TryLoadShader(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                // 非阻塞：只是把请求排进后台队列；拿不到也照常继续（本局该特效不生效）
                ResourceDownloader.EnsureLocalFile(path);
                return;
            }

            PixelShader shader;

            using (var gs = ShaderBytecode.AssembleFromFile(path, ShaderFlags.None))
                shader = new PixelShader(Device, gs);

            if (path.EndsWith("normal.ps")) NormalPixelShader = shader;
            else if (path.EndsWith("grayscale.ps")) GrayScalePixelShader = shader;
            else if (path.EndsWith("magic.ps")) MagicPixelShader = shader;
        }

        private static unsafe void LoadTextures()
        {
            Sprite = new Sprite(Device);
            Line = new Line(Device) { Width = 1F };

            MainSurface = Device.GetBackBuffer(0, 0);
            CurrentSurface = MainSurface;
            Device.SetRenderTarget(0, MainSurface);

            if (RadarTexture == null || RadarTexture.Disposed)
            {
                RadarTexture = new Texture(Device, 2, 2, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);

                DataRectangle stream = RadarTexture.LockRectangle(0, LockFlags.Discard);
                using (Bitmap image = new Bitmap(2, 2, 8, PixelFormat.Format32bppArgb, stream.Data.DataPointer))
                using (Graphics graphics = Graphics.FromImage(image))
                    graphics.Clear(Color.White);
                RadarTexture.UnlockRectangle(0);
            }
            if (PoisonDotBackground == null || PoisonDotBackground.Disposed)
            {
                PoisonDotBackground = new Texture(Device, 5, 5, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);

                DataRectangle stream = PoisonDotBackground.LockRectangle(0, LockFlags.Discard);
                using (Bitmap image = new Bitmap(5, 5, 20, PixelFormat.Format32bppArgb, stream.Data.DataPointer))
                using (Graphics graphics = Graphics.FromImage(image))
                    graphics.Clear(Color.White);
                PoisonDotBackground.UnlockRectangle(0);
            }
            CreateLights();
        }

        private unsafe static void CreateLights()
        {

            for (int i = Lights.Count - 1; i >= 0; i--)
                Lights[i].Dispose();

            Lights.Clear();

            for (int i = 1; i < LightSizes.Length; i++)
            {
                // int width = 125 + (57 *i);
                //int height = 110 + (57 * i);
                int width = LightSizes[i].X;
                int height = LightSizes[i].Y;

                Texture light = new Texture(Device, width, height, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);

                DataRectangle stream = light.LockRectangle(0, LockFlags.Discard);
                using (Bitmap image = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, stream.Data.DataPointer))
                {
                    using (Graphics graphics = Graphics.FromImage(image))
                    {
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            //path.AddEllipse(new Rectangle(0, 0, width, height));
                            //using (PathGradientBrush brush = new PathGradientBrush(path))
                            //{
                            //    graphics.Clear(Color.FromArgb(0, 0, 0, 0));
                            //    brush.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                            //    brush.CenterColor = Color.FromArgb(255, 255, 255, 255);
                            //    graphics.FillPath(brush, path);
                            //    graphics.Save();
                            //}

                            path.AddEllipse(new Rectangle(0, 0, width, height));
                            using (PathGradientBrush brush = new PathGradientBrush(path))
                            {
                                Color[] blendColours = { Color.White,
                                                     Color.FromArgb(255,210,210,210),
                                                     Color.FromArgb(255,160,160,160),
                                                     Color.FromArgb(255,70,70,70),
                                                     Color.FromArgb(255,40,40,40),
                                                     Color.FromArgb(0,0,0,0)};

                                float[] radiusPositions = { 0f, .20f, .40f, .60f, .80f, 1.0f };

                                ColorBlend colourBlend = new ColorBlend();
                                colourBlend.Colors = blendColours;
                                colourBlend.Positions = radiusPositions;

                                graphics.Clear(Color.FromArgb(0, 0, 0, 0));
                                brush.InterpolationColors = colourBlend;
                                brush.SurroundColors = blendColours;
                                brush.CenterColor = Color.White;
                                graphics.FillPath(brush, path);
                                graphics.Save();
                            }
                        }
                    }
                }

                light.UnlockRectangle(0);
                //light.Disposing += (o, e) => Lights.Remove(light);
                Lights.Add(light);
            }
        }

        public static void SetSurface(Surface surface)
        {
            if (surface == null || surface.Disposed)
                return;

            if (CurrentSurface == surface)
                return;

            Sprite.Flush();
            CurrentSurface = surface;
            Device.SetRenderTarget(0, surface);
        }
        public static void SetGrayscale(bool value)
        {
            GrayScale = value;

            if (value == true)
            {
                if (Device.PixelShader == GrayScalePixelShader) return;
                Sprite.Flush();
                Device.PixelShader = GrayScalePixelShader;
            }
            else
            {
                if (Device.PixelShader == null) return;
                Sprite.Flush();
                Device.PixelShader = null;
            }
        }

        public static void DrawOpaque(Texture texture, Rectangle? sourceRect, Vector3? position, Color4 color, float opacity)
        {
            color.Alpha = opacity;
            Draw(texture, sourceRect, position, color);
        }

        public static void Draw(Texture texture, Rectangle? sourceRect, Vector3? position, Color4 color)
        {
            //★别把「一张图的纹理还没准备好」升级成「整个渲染循环崩掉」（11-09）：
            //  纹理可能刚被 DXManager.Clean 回收（TextureValid=false，下一帧 CheckImage 才重建），
            //  也可能是旧设备上创建的。这时 Sprite.Draw 抛的正是 D3DERR_INVALIDCALL，
            //  会把整帧打断、Sprite/Device 停在 Begin 状态 → 之后每帧都抛同一个错。
            //  直接跳过这一张，画面最多缺一帧，不会连锁。
            if (texture == null || texture.Disposed) return;
            if (Sprite == null || Sprite.Disposed) return;
            if (DeviceLost) return;

            Sprite.Draw(texture, sourceRect, Vector3.Zero, position, color);
            CMain.DPSCounter++;
        }

        /// <summary>
        /// 一帧的开始（Clear → BeginScene → Sprite.Begin → 复位绘制状态）。
        /// 抽出来是为了让「出错时的收尾」有唯一对应的位置，见 AbortFrame。
        /// </summary>
        /// <summary>
        /// 渲染前置条件是否齐备（11-16）。
        /// Device/Sprite/MainSurface 任一为空或已释放时画帧必然空引用 —— 以前这个 NRE
        /// 因为 BeginFrame 被 JIT 内联，日志里只记到 RenderEnvironment 一层，查不到真因。
        /// 典型时机：退出客户端途中、Reset 失败后的 CleanUp、窗口关闭竞态。
        /// </summary>
        public static bool IsReady
        {
            get
            {
                return Device != null && !Device.Disposed &&
                       Sprite != null && !Sprite.Disposed &&
                       MainSurface != null && !MainSurface.Disposed;
            }
        }

        public static void BeginFrame()
        {
            Device.Clear(ClearFlags.Target, Color.Black, 0, 0);
            Device.BeginScene();
            Sprite.Begin(SpriteFlags.AlphaBlend);
            ResetDrawState();
            SetSurface(MainSurface);
        }

        /// <summary>一帧的结束（Sprite.End → EndScene → Present）。</summary>
        public static void EndFrame()
        {
            Sprite.End();
            Device.EndScene();
            Device.Present();
        }

        /// <summary>
        /// 一帧画到一半抛异常时的现场清理（11-09）。
        ///
        /// D3D 是状态机：Sprite.Begin / Device.BeginScene 之后如果中途抛异常跳过了 End，
        /// 状态就一直挂在「已在 Scene 里」，之后每一帧都会在 BeginScene/Begin/Draw 上继续抛
        /// D3DERR_INVALIDCALL。而 TestCooperativeLevel 此时返回 Success（设备确实没丢），
        /// 于是 AttemptReset 把 DeviceLost 清掉、下一帧再抛、再置位 —— 无限循环刷日志、
        /// 画面彻底不动（11/8 11:14:20 那一整片 INVALIDCALL 就是这个循环）。
        /// 出错后必须把状态机推回干净状态：成对补 End、重建后台缓冲引用、复位混合与着色器。
        /// </summary>
        public static void AbortFrame()
        {
            try { if (Sprite != null && !Sprite.Disposed) Sprite.End(); } catch { }
            try { if (Device != null) Device.EndScene(); } catch { }

            try
            {
                if (Device != null)
                {
                    MainSurface = Device.GetBackBuffer(0, 0);
                    CurrentSurface = MainSurface;
                    Device.SetRenderTarget(0, MainSurface);
                    Device.PixelShader = null;
                }
            }
            catch
            {
            }

            GrayScale = false;
            Blending = false;
            BlendingRate = 1F;
            Opacity = 1F;
        }

        /// <summary>
        /// 连续多帧渲染失败后的重手段：整设备重建（Sprite / Line / 后台缓冲 / 纹理全部重建）。
        /// 用于状态机已经脏到 AbortFrame 也救不回来的情况（比如 Reset 之后纹理全废、
        /// Sprite 内部状态与设备不同步）。
        /// </summary>
        public static void ForceRebuild()
        {
            try
            {
                ResetDevice();
            }
            catch
            {
                DeviceLost = true;
            }
        }

        public static void AttemptReset()
        {
            try
            {
                if (Device == null) return;

                Result result = DXManager.Device.TestCooperativeLevel();

                if (result.Code == ResultCode.DeviceLost.Code) return;

                if (result.Code == ResultCode.DeviceNotReset.Code)
                {
                    DXManager.ResetDevice();
                    return;
                }

                if (result.Code != ResultCode.Success.Code) return;

                //★设备本身没问题，但绘制对象可能是空的：上一次 ResetDevice 在 CleanUp 之后
                //  中途失败（比如窗口正好被最小化、Reset 返回 INVALIDCALL），Sprite/后台缓冲
                //  已经被 Dispose 掉了。这时候只清 DeviceLost 标志，下一帧就会在 Sprite.Begin
                //  上空引用/抛 INVALIDCALL —— 必须先重建再说。
                if (Sprite == null || Sprite.Disposed || MainSurface == null || MainSurface.Disposed)
                {
                    DXManager.ResetDevice();
                    return;
                }

                DXManager.DeviceLost = false;
            }
            catch
            {
            }
        }

        public static void ResetDevice()
        {
            if (DXManager.Parameters == null || Program.Form == null)
            {
                DXManager.DeviceLost = true;
                return;
            }

            Size clientSize = Program.Form.ClientSize;

            //★先在 CleanUp 之前判尺寸（11-09）：
            //  窗口最小化时客户区是 0×0。原来的顺序是「先 CleanUp（Sprite/Surface 全 Dispose）
            //  → 再判尺寸为 0 → return」，于是设备处于「Sprite 为 null 但没人重建」的半死状态，
            //  之后每帧 INVALIDCALL。现在尺寸不对就原地不动，等窗口回来再重建。
            if (clientSize.Width <= 0 || clientSize.Height <= 0)
            {
                DXManager.DeviceLost = true;
                return;
            }

            DXManager.CleanUp();
            DXManager.DeviceLost = true;

            DXManager.Parameters.Windowed = !Settings.FullScreen;
            DXManager.Parameters.BackBufferWidth = clientSize.Width;
            DXManager.Parameters.BackBufferHeight = clientSize.Height;
            DXManager.Parameters.PresentationInterval = Settings.FPSCap ? PresentInterval.Default : PresentInterval.Immediate;

            DXManager.Device.Reset(DXManager.Parameters);

            DXManager.LoadTextures();
        }

        public static void AttemptRecovery()
        {
            try
            {
                Sprite.End();
            }
            catch
            {
            }

            try
            {
                Device.EndScene();
            }
            catch
            {
            }

            try
            {
                MainSurface = Device.GetBackBuffer(0, 0);
                CurrentSurface = MainSurface;
                Device.SetRenderTarget(0, MainSurface);
            }
            catch
            {
            }
        }
        /// <summary>
        /// 每帧绘制开始前强制复位绘制状态：正常「纹理 alpha」混合、透明度 1。
        /// 兜底防闪烁：任何 SetOpacity/SetBlend 的泄漏（例如隐身怪 BodyLibrary 未就绪时
        /// 提前 return）最多只影响当帧，不会把整屏拖成持续的半透明发暗。
        /// </summary>
        public static void ResetDrawState()
        {
            Blending = false;
            BlendingRate = 1F;
            Opacity = 1F;

            if (Device == null || DeviceLost) return;

            try
            {
                Device.SetRenderState(RenderState.AlphaBlendEnable, true);
                Device.SetRenderState(RenderState.SourceBlend, SlimDX.Direct3D9.Blend.SourceAlpha);
                Device.SetRenderState(RenderState.DestinationBlend, SlimDX.Direct3D9.Blend.InverseSourceAlpha);
                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb(255, 255, 255, 255).ToArgb());
            }
            catch
            {
            }
        }

        public static void SetOpacity(float opacity)
        {
            if (Opacity == opacity)
                return;

            Sprite.Flush();
            Device.SetRenderState(RenderState.AlphaBlendEnable, true);
            if (opacity >= 1 || opacity < 0)
            {
                Device.SetRenderState(RenderState.SourceBlend, SlimDX.Direct3D9.Blend.SourceAlpha);
                Device.SetRenderState(RenderState.DestinationBlend, SlimDX.Direct3D9.Blend.InverseSourceAlpha);
                Device.SetRenderState(RenderState.SourceBlendAlpha, Blend.One);
                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb(255, 255, 255, 255).ToArgb());
            }
            else
            {
                Device.SetRenderState(RenderState.SourceBlend, Blend.BlendFactor);
                Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseBlendFactor);
                Device.SetRenderState(RenderState.SourceBlendAlpha, Blend.SourceAlpha);
                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb((byte)(255 * opacity), (byte)(255 * opacity), (byte)(255 * opacity), (byte)(255 * opacity)).ToArgb());
            }
            Opacity = opacity;
            Sprite.Flush();
        }
        public static void SetBlend(bool value, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            if (value == Blending && BlendingRate == rate && BlendingMode == mode) return;

            Blending = value;
            BlendingRate = rate;
            BlendingMode = mode;

            Sprite.Flush();

            Sprite.End();

            if (Blending)
            {
                Sprite.Begin(SpriteFlags.DoNotSaveState);
                Device.SetRenderState(RenderState.AlphaBlendEnable, true);

                switch (BlendingMode)
                {
                    case BlendMode.INVLIGHT:
                        Device.SetRenderState(RenderState.BlendOperation, BlendOperation.Add);
                        Device.SetRenderState(RenderState.SourceBlend, Blend.BlendFactor);
                        Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceColor);
                        break;
                    default:
                        Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
                        Device.SetRenderState(RenderState.DestinationBlend, Blend.One);
                        break;
                }

                Device.SetRenderState(RenderState.BlendFactor, Color.FromArgb((byte)(255 * BlendingRate), (byte)(255 * BlendingRate),
                                                                (byte)(255 * BlendingRate), (byte)(255 * BlendingRate)).ToArgb());
            }
            else
                Sprite.Begin(SpriteFlags.AlphaBlend);

            Device.SetRenderTarget(0, CurrentSurface);
        }

        public static void SetNormal(float blend, Color tintcolor)
        {
            if (Device.PixelShader == NormalPixelShader)
                return;

            Sprite.Flush();
            Device.PixelShader = NormalPixelShader;
            Device.SetPixelShaderConstant(0, new Vector4[] { new Vector4(1.0F, 1.0F, 1.0F, blend) });
            Device.SetPixelShaderConstant(1, new Vector4[] { new Vector4(tintcolor.R / 255, tintcolor.G / 255, tintcolor.B / 255, 1.0F) });
            Sprite.Flush();
        }

        public static void SetGrayscale(float blend, Color tintcolor)
        {
            if (Device.PixelShader == GrayScalePixelShader)
                return;

            Sprite.Flush();
            Device.PixelShader = GrayScalePixelShader;
            Device.SetPixelShaderConstant(0, new Vector4[] { new Vector4(1.0F, 1.0F, 1.0F, blend) });
            Device.SetPixelShaderConstant(1, new Vector4[] { new Vector4(tintcolor.R / 255, tintcolor.G / 255, tintcolor.B / 255, 1.0F) });
            Sprite.Flush();
        }

        public static void SetBlendMagic(float blend, Color tintcolor)
        {
            if (Device.PixelShader == MagicPixelShader || MagicPixelShader == null)
                return;

            Sprite.Flush();
            Device.PixelShader = MagicPixelShader;
            Device.SetPixelShaderConstant(0, new Vector4[] { new Vector4(1.0F, 1.0F, 1.0F, blend) });
            Device.SetPixelShaderConstant(1, new Vector4[] { new Vector4(tintcolor.R / 255, tintcolor.G / 255, tintcolor.B / 255, 1.0F) });
            Sprite.Flush();
        }

        public static void Clean()
        {
            for (int i = TextureList.Count - 1; i >= 0; i--)
            {
                MImage m = TextureList[i];

                if (m == null)
                {
                    TextureList.RemoveAt(i);
                    continue;
                }

                if (CMain.Time <= m.CleanTime) continue;

                m.DisposeTexture();
            }

            for (int i = ControlList.Count - 1; i >= 0; i--)
            {
                MirControl c = ControlList[i];

                if (c == null)
                {
                    ControlList.RemoveAt(i);
                    continue;
                }

                if (CMain.Time <= c.CleanTime) continue;

                c.DisposeTexture();
            }
        }


        private static void CleanUp()
        {
            if (Sprite != null)
            {
                if (!Sprite.Disposed)
                {
                    Sprite.Dispose();
                }

                Sprite = null;
            }

            if (Line != null)
            {
                if (!Line.Disposed)
                {
                    Line.Dispose();
                }

                Line = null;
            }

            if (CurrentSurface != null)
            {
                if (!CurrentSurface.Disposed)
                {
                    CurrentSurface.Dispose();
                }

                CurrentSurface = null;
            }

            if (PoisonDotBackground != null)
            {
                if (!PoisonDotBackground.Disposed)
                {
                    PoisonDotBackground.Dispose();
                }

                PoisonDotBackground = null;
            }

            if (RadarTexture != null)
            {
                if (!RadarTexture.Disposed)
                {
                    RadarTexture.Dispose();
                }

                RadarTexture = null;
            }

            if (FloorTexture != null)
            {
                if (!FloorTexture.Disposed)
                {
                    FloorTexture.Dispose();
                }

                DXManager.FloorTexture = null;

                // 进游戏前（还在登录界面）Scene/MapControl 都是 null；这里不能无条件解引用，
                // 否则 CleanUp 抛 NRE → ResetDevice 整体失败 → 客户端卡在半初始化状态。
                if (GameScene.Scene != null && GameScene.Scene.MapControl != null)
                    GameScene.Scene.MapControl.FloorValid = false;

                if (DXManager.FloorSurface != null && !DXManager.FloorSurface.Disposed)
                {
                    DXManager.FloorSurface.Dispose();
                }

                DXManager.FloorSurface = null;
            }

            if (LightTexture != null)
            {
                if (!LightTexture.Disposed)
                    LightTexture.Dispose();

                DXManager.LightTexture = null;

                if (DXManager.LightSurface != null && !DXManager.LightSurface.Disposed)
                {
                    DXManager.LightSurface.Dispose();
                }

                DXManager.LightSurface = null;
            }

            if (Lights != null)
            {
                for (int i = 0; i < Lights.Count; i++)
                {
                    if (Lights[i] == null || Lights[i].Disposed) continue;

                    Lights[i].Dispose();
                }
                Lights.Clear();
            }

            for (int i = TextureList.Count - 1; i >= 0; i--)
            {
                MImage m = TextureList[i];

                if (m == null) continue;

                m.DisposeTexture();
            }
            TextureList.Clear();


            for (int i = ControlList.Count - 1; i >= 0; i--)
            {
                MirControl c = ControlList[i];

                if (c == null) continue;

                c.DisposeTexture();
            }
            ControlList.Clear();
        }

        public static void Dispose()
        {
            CleanUp();

            Device?.Direct3D?.Dispose();

            Device?.Dispose();

            NormalPixelShader?.Dispose();
            GrayScalePixelShader?.Dispose();
            MagicPixelShader?.Dispose();
        }
    }
}