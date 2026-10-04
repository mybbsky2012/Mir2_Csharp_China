using System.Drawing;
using Client.MirControls;
using Client.MirGraphics;

namespace Client.MirScenes
{
    /// <summary>
    /// 「载入中」画面：全屏黑底 + 金色 MIR2 标志 + Loading 动画。
    ///
<<<<<<< HEAD
    /// 绘制时机与层级：<b>不挂到任何场景的控件树上</b>，而是由 `CMain.RenderEnvironment()`
    /// 在 `ActiveScene.Draw()` 之后直接画一遍（见 `DrawOverlay()`）。这样它永远在所有
    /// 场景内容之上 —— 主面板、技能条、物品栏、聊天框都不会再盖在载入背景前面，
    /// 也不受场景切换/子控件排序/场景纹理缓存的影响。
=======
    /// 用途（玩家看到的时机）：在选人界面点「开始游戏 / 进入游戏」之后，一直到
    /// 新地图底板和角色对象都真正画出来之前，用它盖住这段黑屏；画面挂在当前
    /// ActiveScene 上，所以从 SelectScene 切到 GameScene 也不会丢。
    ///
    /// Armed 语义：Show() 表示「这段载入期需要盖着」，由 GameScene.Process 每帧检查，
    /// 直到世界真的能画了才 Hide()；中途场景切换（SelectScene → GameScene）会重建控件，
    /// 只要 Armed 还是 true，GameScene 会自己把它重新挂回去。
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
    ///
    /// 素材（实测 E:\micromir2\Data 图库，注意 ChrSel[17]/[18] 是近乎全黑的过渡帧，不能用）：
    ///  · 底图 —— Prguse 第 931~940 帧（Index 930~939，十张 800x600 整屏插画）里随机一张；
    ///  · Loading 动画 —— Prguse 第 941~949 帧（Index 940 起 9 帧，92x20 的「Loading...」）。
    /// </summary>
    internal static class LoadingScreen
    {
        private const int Timeout = 30000;   // 一直等不到世界画出来就自动撤掉，避免永远卡在载入画面

        /// <summary>默认最少展示时长：世界哪怕瞬间就绪，背景图也先亮 2 秒，太快一闪而过反而像卡了。</summary>
        public const long MinShowTime = 2000;

        /// <summary>
        /// 背景图候选：Prguse 第 931~940 帧（Index 930~939，0 基），每张都是 800x600 的整屏插画，
        /// 每次进游戏随机挑一张，避免每次都是同一幅画面。
        /// </summary>
        private static readonly int[] BackgroundIndices = { 930, 931, 932, 933, 934, 935, 936, 937, 938, 939 };

        private static MirImageControl _root;
<<<<<<< HEAD
        private static MirLabel _statusLabel;
=======
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
        private static long _shownTime;

        /// <summary>true = 当前处于「进游戏载入期」，GameScene 需要一直把载入画面盖着。</summary>
        public static bool Armed { get; private set; }

        /// <summary>本次载入画面的点亮时间（配合 MinShowTime 做「至少展示 2 秒」）。</summary>
        public static long ShownTime
        {
            get { return _shownTime; }
        }

        public static bool Visible
        {
            get { return _root != null && !_root.IsDisposed; }
        }

<<<<<<< HEAD
        /// <summary>开始载入（点「开始游戏」时调用）：点亮画面，由 CMain 每帧画在最上层。</summary>
        public static void Show()
        {
            DisposeRoot();   // 只清掉旧画面，不影响 Armed
            _statusLabel = null;
=======
        /// <summary>开始载入（点「开始游戏」时调用），并立即在当前场景上显示载入画面。</summary>
        public static void Show()
        {
            MirScene scene = MirScene.ActiveScene;

            if (scene == null || scene.IsDisposed) return;

            DisposeRoot();   // 只清掉旧画面，不影响 Armed
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01

            Armed = true;
            _shownTime = CMain.Time;

<<<<<<< HEAD
            // 注意：不设置 Parent —— 载入画面不进任何场景的控件树，
            // 由 CMain.RenderEnvironment() 在场景画完之后单独调用 DrawOverlay() 画在最上层。
=======
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
            _root = new MirImageControl
            {
                AutoSize = false,
                DrawControlTexture = true,
                BackColour = Color.Black,
                Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight),
                Location = new Point(0, 0),
                NotControl = true,
<<<<<<< HEAD
=======
                Parent = scene,
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
            };

            // 每次随机挑一张整屏插画做背景（图库没就绪时退回 800x600 居中）
            int bgIndex = BackgroundIndices[CMain.Random.Next(BackgroundIndices.Length)];
            Size bgSize = Libraries.Prguse.GetTrueSize(bgIndex);

            if (bgSize.Width <= 0 || bgSize.Height <= 0)
                bgSize = new Size(800, 600);

            new MirImageControl
            {
                AutoSize = false,
                Library = Libraries.Prguse,
                Index = bgIndex,
                Size = bgSize,
                Location = new Point((Settings.ScreenWidth - bgSize.Width) / 2, (Settings.ScreenHeight - bgSize.Height) / 2),
                NotControl = true,
                Parent = _root,
            };

<<<<<<< HEAD
            _statusLabel = new MirLabel
=======
            new MirLabel
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
            {
                AutoSize = false,
                Size = new Size(Settings.ScreenWidth, 22),
                Location = new Point(0, Settings.ScreenHeight - 64),
                Text = "正在进入游戏，请稍候...",
                ForeColour = Color.White,
                OutLine = true,
                DrawFormat = TextFormatFlags.HorizontalCenter,
                NotControl = true,
                Parent = _root,
            };

            new MirAnimatedControl
            {
                Library = Libraries.Prguse,
                Index = 940,
                Animated = true,
                AnimationCount = 9,
                AnimationDelay = 100,
                Loop = true,
                NotControl = true,
                Parent = _root,
                Location = new Point((Settings.ScreenWidth - 92) / 2, Settings.ScreenHeight - 36),
            };
        }

<<<<<<< HEAD
        /// <summary>
        /// 每帧最后调用一次：把载入画面直接画在**所有场景内容之上**。
        /// 由 `CMain.RenderEnvironment()` 在 `ActiveScene.Draw()` 之后调用。
        /// 画面没点亮时什么都不做。
        /// </summary>
        public static void DrawOverlay()
        {
            if (_root == null || _root.IsDisposed) return;

            _root.Draw();
        }

=======
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
        /// <summary>超时保护：显示超过 Timeout 还没等到下一步（地图/角色数据），自动撤掉。</summary>
        public static void CheckTimeout()
        {
            if (Visible && CMain.Time - _shownTime > Timeout) Hide();
        }

<<<<<<< HEAD
        /// <summary>
        /// 更新载入画面底部的状态文字（不重建画面）。
        /// 点「开始游戏」后先显示「正在加载游戏资源 x/y...」，
        /// 资源就绪、发出进入请求后再换回「正在进入游戏，请稍候...」。
        /// </summary>
        public static void SetStatus(string text)
        {
            if (!Visible) return;

            if (_statusLabel == null || _statusLabel.IsDisposed)
            {
                _statusLabel = new MirLabel
                {
                    AutoSize = false,
                    Size = new Size(Settings.ScreenWidth, 22),
                    Location = new Point(0, Settings.ScreenHeight - 64),
                    ForeColour = Color.White,
                    OutLine = true,
                    DrawFormat = TextFormatFlags.HorizontalCenter,
                    NotControl = true,
                    Parent = _root,
                };
            }

            _statusLabel.Text = text;
        }

=======
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
        /// <summary>撤掉载入画面，并结束这一段载入期（世界已经可以正常绘制）。</summary>
        public static void Hide()
        {
            Armed = false;
<<<<<<< HEAD
            _statusLabel = null;
=======
>>>>>>> 387da1057651bb867b9fab9d85af1be42ce16c01
            DisposeRoot();
        }

        private static void DisposeRoot()
        {
            if (_root == null) return;

            if (!_root.IsDisposed) _root.Dispose();
            _root = null;
        }
    }
}
