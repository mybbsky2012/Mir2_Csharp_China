using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using LibraryEditor;

namespace LibV5Test
{
    /// <summary>
    /// MLibraryV2 的 V5 分支验证：
    ///   1) 打开 V5 库（桌面 UI.Lib）：帧数、版本、逐帧解码尺寸
    ///   2) 未改动直接保存：必须与原文件字节级一致（证明表/负载/派生字段回写无误）
    ///   3) 替换一帧后保存再重开：新帧生效、其它帧不受影响
    ///   4) 增删帧后保存再重开：帧数、派生字段、文件长度校验都还成立
    ///   5) 回归：V3 库（UI_32bit.Lib）仍按旧路径打开
    /// </summary>
    internal static class Program
    {
        private static int _pass, _fail;

        private static void Check(bool ok, string what)
        {
            if (ok) { _pass++; }
            else { _fail++; Console.WriteLine("  [FAIL] " + what); }
        }

        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            string v5Path = args.Length > 0 ? args[0] : @"D:\Program Files\desktop\UI.Lib";
            string v3Path = args.Length > 1 ? args[1] : @"D:\Program Files\desktop\UI_32bit.Lib";

            string tempDir = Path.Combine(Path.GetTempPath(), "libv5test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TestV5Open(v5Path, tempDir);
                TestV5Replace(v5Path, tempDir);
                TestV5InsertRemove(v5Path, tempDir);
                TestV3Regression(v3Path);

                if (args.Length > 2) DumpFrames(v5Path, args[2]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION: " + ex);
                _fail++;
            }

            Console.WriteLine();
            Console.WriteLine("PASS " + _pass + "  FAIL " + _fail);
            return _fail == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- 1
        private static void TestV5Open(string src, string tempDir)
        {
            Console.WriteLine("[1] 打开 V5 库");

            string work = Path.Combine(tempDir, "UI.Lib");
            File.Copy(src, work, true);

            var lib = new MLibraryV2(work);

            Check(lib.IsV5, "IsV5 == true");
            Check(lib.ReadVersion == 5, "ReadVersion == 5（实际 " + lib.ReadVersion + "）");
            Check(lib.Count == 697, "Count == 697（实际 " + lib.Count + "）");
            Check(lib.Images.Count == 697, "Images.Count == 697");
            Check(lib.Frames == null || lib.Frames.Keys.Count == 0, "V5 没有动作帧段");

            // 逐帧解码：尺寸与帧头一致、位图非空。
            // 宽或高 < 2 的帧是占位空帧（1×1 / 464×1 之类），所有格式都按空帧处理，不算失败。
            int badSize = 0, badImage = 0, tiny = 0;
            for (int i = 0; i < lib.Count; i++)
            {
                var mi = lib.GetMImage(i);
                if (mi == null) { badImage++; Console.WriteLine("    帧 " + i + " 为 null"); continue; }
                if (mi.Width < 2 || mi.Height < 2) { tiny++; continue; }

                if (mi.Image == null) { badImage++; Console.WriteLine("    帧 " + i + " " + mi.Width + "×" + mi.Height + " 解码失败：" + lib.LastEnsureError); continue; }
                if (mi.Image.Width != mi.Width || mi.Image.Height != mi.Height) badSize++;
            }
            Console.WriteLine("    空帧（<2px）" + tiny + " 帧");
            Check(badImage == 0, "全部可解码帧都出位图（失败 " + badImage + "）");
            Check(badSize == 0, "位图尺寸与帧头一致（不符 " + badSize + "）");

            // 像素基准：帧 0 是 337×285 的 INVENTORY 背包窗
            var m0 = lib.GetMImage(0);
            Check(m0.Width == 337 && m0.Height == 285, "帧 0 尺寸 337×285（实际 " + m0.Width + "×" + m0.Height + "）");

            if (m0.Image != null)
            {
                Color c = m0.Image.GetPixel(100, 100);
                Check(c.B == 19 && c.G == 21 && c.R == 23 && c.A == 255,
                      "帧 0 (100,100) 像素 = B19 G21 R23 A255（实际 B" + c.B + " G" + c.G + " R" + c.R + " A" + c.A + "）");
            }

            // 未改动保存：字节级一致
            string beforeMd5 = Md5(work);
            long beforeLen = new FileInfo(work).Length;
            lib.Save();

            long afterLen = new FileInfo(work).Length;
            string afterMd5 = Md5(work);

            Check(afterLen == beforeLen, "原样保存后文件长度不变（" + beforeLen + " → " + afterLen + "）");
            Check(beforeMd5 == afterMd5, "原样保存后字节级一致（md5 " + beforeMd5 + " → " + afterMd5 + "）");

            // 重开确认结构没坏
            var again = new MLibraryV2(work);
            Check(again.IsV5 && again.Count == 697, "重开后仍是 V5 / 697 帧");
            var againMi = again.GetMImage(0);
            Check(againMi != null && againMi.Image != null, "重开后帧 0 仍能解码");
        }

        // ---------------------------------------------------------------- 2
        private static void TestV5Replace(string src, string tempDir)
        {
            Console.WriteLine("[2] 替换一帧后保存");

            string work = Path.Combine(tempDir, "replace.Lib");
            File.Copy(src, work, true);

            var lib = new MLibraryV2(work);

            using (var bmp = new Bitmap(40, 30))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Red);
                lib.ReplaceImage(1, bmp, 5, -7);
            }

            lib.Save();

            var re = new MLibraryV2(work);
            Check(re.IsV5, "替换后仍是 V5");
            Check(re.Count == 697, "替换后帧数不变（实际 " + re.Count + "）");

            var mi = re.GetMImage(1);
            Check(mi != null && mi.Width == 40 && mi.Height == 30, "帧 1 尺寸变成 40×30（实际 " +
                  (mi == null ? "null" : mi.Width + "×" + mi.Height) + "）");
            Check(mi != null && mi.X == 5 && mi.Y == -7, "帧 1 偏移变成 (5,-7)");

            if (mi != null && mi.Image != null)
            {
                Color c = mi.Image.GetPixel(20, 15);
                Check(c.R == 255 && c.G == 0 && c.B == 0, "帧 1 像素为纯红（实际 R" + c.R + " G" + c.G + " B" + c.B + "）");
            }

            // 其它帧不受影响：帧 0 还是原来的 INVENTORY
            var m0 = re.GetMImage(0);
            Check(m0 != null && m0.Width == 337 && m0.Height == 285, "帧 0 未受替换影响");

            int bad = 0;
            for (int i = 0; i < re.Count; i++)
            {
                var x = re.GetMImage(i);
                if (x == null) { bad++; continue; }
                if (x.Width < 2 || x.Height < 2) continue;
                if (x.Image == null) { bad++; Console.WriteLine("    帧 " + i + " 解码失败"); }
            }
            Check(bad == 0, "替换保存后全部帧仍可解码（失败 " + bad + "）");
        }

        // ---------------------------------------------------------------- 3
        private static void TestV5InsertRemove(string src, string tempDir)
        {
            Console.WriteLine("[3] 增帧 / 删帧后保存");

            string work = Path.Combine(tempDir, "insert.Lib");
            File.Copy(src, work, true);

            var lib = new MLibraryV2(work);

            using (var bmp = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Lime);
                lib.InsertImage(10, bmp, 0, 0);
            }

            Check(lib.Count == 698, "插入后内存帧数 698（实际 " + lib.Count + "）");
            lib.Save();

            var re = new MLibraryV2(work);
            Check(re.IsV5, "增帧后仍是 V5");
            Check(re.Count == 698, "增帧保存后帧数 698（实际 " + re.Count + "）");

            var mi = re.GetMImage(10);
            Check(mi != null && mi.Width == 16 && mi.Height == 16, "新帧落在索引 10 且尺寸 16×16");

            int bad = CountBlank(re);
            Check(bad == 0, "增帧保存后全部帧可解码（失败 " + bad + "）");

            // 派生字段随帧数更新（B = 帧数×256，A = (17×帧数+8)×256）
            int u1, u2;
            ReadFields(work, out u1, out u2);
            Check(u2 == 698 * 256, "派生字段 B = 698×256（实际 " + u2 + "）");
            Check(u1 == (698 * 17 + 8) * 256, "派生字段 A = (17×698+8)×256（实际 " + u1 + "）");

            // 删回原帧数
            re.RemoveImage(10);
            Check(re.Count == 697, "删除后内存帧数 697");
            re.Save();

            var re2 = new MLibraryV2(work);
            Check(re2.IsV5 && re2.Count == 697, "删帧保存后 697 帧");

            int bad2 = CountBlank(re2);
            Check(bad2 == 0, "删帧保存后全部帧可解码（失败 " + bad2 + "）");

            ReadFields(work, out u1, out u2);
            Check(u2 == 697 * 256, "删帧后派生字段 B 回到 697×256（实际 " + u2 + "）");
        }

        // ---------------------------------------------------------------- 4
        private static void TestV3Regression(string src)
        {
            Console.WriteLine("[4] V3 库回归");

            if (!File.Exists(src))
            {
                Console.WriteLine("  [SKIP] 找不到 " + src);
                return;
            }

            var lib = new MLibraryV2(src);
            Check(!lib.IsV5, "V3 库未被误判成 V5");
            Check(lib.Count > 0, "V3 库帧数 > 0（实际 " + lib.Count + "）");

            int bad = CountBlank(lib);
            Check(bad == 0, "V3 库全部" + lib.Count + " 帧可解码（失败 " + bad + "，0×0 空帧不计）");
        }

        /// <summary>统计「应该出图却出不了图」的帧数（宽高 &lt; 2 的占位空帧跳过）。</summary>
        private static int CountBlank(MLibraryV2 lib)
        {
            int bad = 0;
            for (int i = 0; i < lib.Count; i++)
            {
                var mi = lib.GetMImage(i);
                if (mi == null) { bad++; continue; }
                if (mi.Width < 2 || mi.Height < 2) continue;
                if (mi.Image == null)
                {
                    bad++;
                    if (bad <= 5) Console.WriteLine("    帧 " + i + " " + mi.Width + "×" + mi.Height + " 解码失败：" + lib.LastEnsureError);
                }
            }
            return bad;
        }

        /// <summary>读文件头两个派生字段（0x04 = A，0x08 = B；0x00 是版本号）。</summary>
        private static void ReadFields(string path, out int a, out int b)
        {
            byte[] head = new byte[12];
            using (var fs = File.OpenRead(path)) fs.ReadExactly(head, 0, 12);
            a = BitConverter.ToInt32(head, 4);
            b = BitConverter.ToInt32(head, 8);
        }

        /// <summary>把前若干帧导出成 PNG，用于肉眼核对解码结果。</summary>
        private static void DumpFrames(string src, string outDir)
        {
            Console.WriteLine("[5] 导出前几帧 PNG → " + outDir);
            Directory.CreateDirectory(outDir);

            var lib = new MLibraryV2(src);
            int[] picks = { 0, 1, 2, 60, 373 };

            foreach (int i in picks)
            {
                var mi = lib.GetMImage(i);
                if (mi == null || mi.Image == null) { Console.WriteLine("  帧 " + i + " 无位图（空帧）"); continue; }
                string p = Path.Combine(outDir, "v5_frame" + i + ".png");
                mi.Image.Save(p, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("  帧 " + i + " " + mi.Width + "×" + mi.Height + " → " + p);
            }
        }

        private static string Md5(string path)
        {
            using (var md5 = MD5.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "");
        }
    }
}
