using System;
using System.Drawing;

namespace Client.Resolution
{
    /// <summary>
    /// 分辨率与窗口尺寸的唯一映射来源。
    ///
    /// 改造前尺寸是硬编码散落在 SelectScene / GameScene 的 switch 里，
    /// 增删一档要改多处、极易漏改（1280 那档枚举名写 720 实际却用 800）。
    /// 现在全部收口到这里，新增分辨率只要改 eSupportedResolution 与本表。
    /// </summary>
    internal static class ResolutionHelper
    {
        /// <summary>默认分辨率（未知值兜底）。</summary>
        internal const int DefaultResolution = 1024;

        /// <summary>登录/选人界面使用的窗口尺寸。</summary>
        internal static Size LoginSize => new Size(800, 600);

        /// <summary>
        /// 按分辨率宽度取窗口尺寸。
        /// Resolution 存的是宽度值（见 eSupportedResolution），这里换算成实际窗口高宽。
        /// </summary>
        internal static Size GetSize(int resolution)
        {
            switch (resolution)
            {
                case 800:
                    return new Size(800, 600);
                case 1024:
                    return new Size(1024, 768);
                case 1280:
                    return new Size(1280, 720);
                case 1366:
                    return new Size(1366, 768);
                case 1600:
                    return new Size(1600, 900);
                case 1920:
                    return new Size(1920, 1080);
                case 2560:
                    return new Size(2560, 1440);
                default:
                    return new Size(1024, 768);
            }
        }

        internal static Size GetSize(eSupportedResolution resolution)
        {
            return GetSize((int)resolution);
        }

        /// <summary>
        /// 把任意分辨率归一化成受支持的档位宽度，避免配置里塞了非法值时窗口尺寸错乱。
        /// </summary>
        internal static int Normalize(int resolution)
        {
            switch (resolution)
            {
                case 800:
                case 1024:
                case 1280:
                case 1366:
                case 1600:
                case 1920:
                case 2560:
                    return resolution;
                default:
                    return DefaultResolution;
            }
        }

        /// <summary>
        /// 该分辨率是否属于宽屏布局（宽度大于 1024）。
        /// UI 各面板用这个判断是否启用加宽底图与两侧装饰，替代原先零散的 Resolution &gt; 1024 判断。
        /// </summary>
        internal static bool IsWide(int resolution)
        {
            return resolution > DefaultResolution;
        }

        /// <summary>是否为 800x600 老式布局。</summary>
        internal static bool IsLegacySmall(int resolution)
        {
            return resolution <= 800;
        }

        /// <summary>
        /// 当前分辨率是否有「整宽底栏」支持（PrguseEx.Lib 里按分辨率预生成的整宽图）。
        /// 有则游戏里在 MainDialog 背后垫一层 BottomBarFill，把底栏栏杆拉满整个窗口宽度，
        /// 同时 MainDialog 自己的两侧端帽要藏起来（填充层已经把侧边接上了）。
        /// 没有这个图库文件就返回 false，退回原版「居中 1024 底栏 + 两侧端帽」。
        /// </summary>
        internal static bool HasFullWidthBar(int resolution)
        {
            switch (resolution)
            {
                case 1280:
                case 1366:
                case 1600:
                case 1920:
                case 2560:
                    return System.IO.File.Exists(System.IO.Path.Combine(Settings.DataPath, "PrguseEx.Lib"));
                default:
                    return false;
            }
        }

        /// <summary>整宽底栏在 PrguseEx.Lib 里的帧索引（1280/1366/1600/1920/2560 → 0..4）。</summary>
        internal static int FullWidthBarIndex(int resolution)
        {
            switch (resolution)
            {
                case 1280: return 0;
                case 1366: return 1;
                case 1600: return 2;
                case 1920: return 3;
                case 2560: return 4;
                default: return 3;
            }
        }
    }
}
