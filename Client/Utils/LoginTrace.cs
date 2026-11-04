using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Client.Utils
{
    /// <summary>
    /// ★11-04 进图计时（临时诊断用）：只在关键节点往 LoadTrace.log 追加一行，带毫秒时间戳与相对耗时。
    /// 目的：定位「进游戏黑屏十几秒」到底卡在哪一段 —— 是等封包、等地图文件、还是首次建地板纹理。
    /// 开销：每次一行 Append，全程不到 10 次，可忽略；定位完可整类删除。
    /// </summary>
    internal static class LoginTrace
    {
        private static readonly System.Diagnostics.Stopwatch Watch = System.Diagnostics.Stopwatch.StartNew();
        private static string _lastTag;
        private static long _lastMs;

        internal static void Mark(string tag)
        {
            try
            {
                long ms = Watch.ElapsedMilliseconds;
                long delta = _lastTag == null ? ms : ms - _lastMs;

                _lastTag = tag;
                _lastMs = ms;

                File.AppendAllText(
                    Path.Combine(Application.StartupPath, "LoadTrace.log"),
                    string.Format("[{0:HH:mm:ss.fff}] +{1,6} ms (上段 {2,6} ms)  {3}{4}",
                        DateTime.Now, ms, delta, tag, Environment.NewLine),
                    Encoding.UTF8);
            }
            catch
            {
                // 诊断代码本身绝不影响游戏
            }
        }
    }
}
