using System;
using System.Collections.Generic;
using System.Reflection;
using Client.MirObjects;

namespace CrashFixTest
{
    /// <summary>
    /// 验证 11-16 的三处崩溃修复（反射真实 Client.dll）：
    ///   ① RankingDialog.RecieveRanks/UpdateRanks：服务端 Count 与本页列表长度不一致时不再越界
    ///   ② Count &lt;= 20（不足一页）时 GapPerRow 不再除零
    ///   ③ rankType 超界时直接丢弃
    ///   ④ DXManager.IsReady 在未初始化时为 false（RenderEnvironment 不再裸空引用）
    /// </summary>
    internal static class Program
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static int _pass, _fail;

        private static void Check(bool ok, string name, string detail = "")
        {
            if (ok) { _pass++; Console.WriteLine("  [PASS] " + name); }
            else { _fail++; Console.WriteLine("  [FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : "  | " + detail)); }
        }

        private static void Section(string s) => Console.WriteLine("\n## " + s);

        [STAThread]
        private static int Main()
        {
            var asm = Assembly.Load("Client");

            // RankingDialog 构造时要按屏幕 DPI 缩放字体（CMain.Graphics.DpiX），
            // 控制台进程里没有渲染窗体，给一个离屏 Graphics 顶上。
            using (var bmp = new System.Drawing.Bitmap(4, 4))
            {
                var cmain = asm.GetType("Client.CMain", true);
                cmain.GetField("Graphics", Any).SetValue(null, System.Drawing.Graphics.FromImage(bmp));

                var dlgType = asm.GetType("Client.MirScenes.Dialogs.RankingDialog", true);
                var rankType = Type.GetType("RankCharacterInfo, Shared");

                object dlg = Activator.CreateInstance(dlgType);
                var recieve = dlgType.GetMethod("RecieveRanks", Any);
                var update = dlgType.GetMethod("UpdateRanks", Any);

                var gapField = dlgType.GetField("GapPerRow", Any);
                var rankCountField = dlgType.GetField("RankCount", Any);
                var rowsField = dlgType.GetField("Rows", Any);

                Console.WriteLine("RankCharacterInfo 类型: " + rankType);
                Console.WriteLine();

                Func<int, object> makeInfo = delegate (int i)
                {
                var info = Activator.CreateInstance(rankType);
                rankType.GetField("Name", Any)?.SetValue(info, "玩家" + i);
                return info;
                };

                // ================= ① Count 与列表长度不一致 =================
                Section("① Count=100 但本页只回 20 条（原版必抛 IndexOutOfRange）");
                var page = new List<object>();
                for (int i = 0; i < 20; i++) page.Add(makeInfo(i));

                bool threw1 = false;
                try
                {
                recieve.Invoke(dlg, new object[] { page.CastToRankList(rankType), (byte)0, 5, 100 });
                }
                catch (Exception ex) { threw1 = true; Console.WriteLine("   异常: " + ex.InnerException); }

                Check(!threw1, "RecieveRanks + UpdateRanks 不抛异常");
                int rankCount = (int)rankCountField.GetValue(dlg);
                Check(rankCount == 100, "RankCount = " + rankCount);
                Check(gapField.GetValue(dlg) is float g && g > 0, "GapPerRow = " + gapField.GetValue(dlg));

                // ================= ② 不足一页 =================
                Section("② Count=10（不足一页 20 行，原版除零）");
                var small = new List<object>();
                for (int i = 0; i < 10; i++) small.Add(makeInfo(i));

                bool threw2 = false;
                try
                {
                recieve.Invoke(dlg, new object[] { small.CastToRankList(rankType), (byte)1, 0, 10 });
                }
                catch (Exception ex) { threw2 = true; Console.WriteLine("   异常: " + ex.InnerException); }

                Check(!threw2, "Count=10 不抛除零异常");
                Check(gapField.GetValue(dlg) is float g2 && g2 == 0f, "GapPerRow = " + gapField.GetValue(dlg) + "（应为 0）");

                // ================= ③ rankType 超界 =================
                Section("③ rankType=9（超出 6 个榜单）");
                bool threw3 = false;
                try
                {
                recieve.Invoke(dlg, new object[] { small.CastToRankList(rankType), (byte)9, 0, 10 });
                }
                catch (Exception ex) { threw3 = true; Console.WriteLine("   异常: " + ex.InnerException); }

                Check(!threw3, "超界 rankType 被丢弃，不写坏数组");

                // ================= ④ 空列表 / null =================
                Section("④ 空列表与 null 列表");
                bool threw4 = false;
                try
                {
                recieve.Invoke(dlg, new object[] { new List<object>().CastToRankList(rankType), (byte)2, 0, 0 });
                recieve.Invoke(dlg, new object[] { null, (byte)3, 0, 30 });
                update.Invoke(dlg, null);
                }
                catch (Exception ex) { threw4 = true; Console.WriteLine("   异常: " + ex.InnerException); }

                Check(!threw4, "空列表 / null 列表都能安全处理");

                // ================= ⑤ DXManager 就绪检查 =================
                Section("⑤ DXManager.IsReady（未初始化时应为 false）");
                var dx = asm.GetType("Client.MirGraphics.DXManager", true);
                bool ready = (bool)dx.GetProperty("IsReady", Any).GetValue(null);
                Check(!ready, "初始化前 IsReady = " + ready + "（RenderEnvironment 会跳帧而不是空引用）");

                Console.WriteLine(string.Format("\n==== 通过 {0} 项，失败 {1} 项 ====", _pass, _fail));
                return _fail == 0 ? 0 : 1;
            }
        }

        private static object CastToRankList(this IEnumerable<object> source, Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType);
            var add = listType.GetMethod("Add");
            if (source != null)
                foreach (var item in source) add.Invoke(list, new[] { item });
            return list;
        }
    }
}
