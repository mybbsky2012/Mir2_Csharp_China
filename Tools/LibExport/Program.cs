using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace LibExport
{
    /// <summary>
    /// 把客户端 Data 目录下的 .Lib 图库展开成 PNG，文件名就是图库索引号。
    ///
    /// 用途：做 UI 皮肤包时，先导出对应的库看索引，改好再放回
    ///       UI\Skin\<皮肤>\<库名>\<索引>.png
    ///
    /// 用法：
    ///   LibExport.exe &lt;数据目录&gt; &lt;输出目录&gt; [库名 ...]
    ///   LibExport.exe "E:\micromir2\Data" "E:\UI导出" Prguse Title Prguse2 Background
    ///   不给库名就把数据目录下所有 .Lib 全导一遍。
    ///
    /// .Lib 结构（与 Client/MirGraphics/MLibrary.cs 保持一致）：
    ///   int32 version / int32 count / [version>=3] int32 frameSeek / int32[count] indexList
    ///   每个条目的偏移处：Width,Height,X,Y,ShadowX,ShadowY (各 int16) + Shadow (byte) + Length (int32)
    ///   紧随其后 Length 字节 = gzip 压缩的 Width*Height*4 裸像素（BGRA）
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: LibExport <数据目录> <输出目录> [库名 ...]");
                Console.WriteLine(@"示例: LibExport ""E:\micromir2\Data"" ""E:\UI导出"" Prguse Title Background");
                Console.WriteLine("不给库名 = 导出数据目录下全部 .Lib");
                return 1;
            }

            string dataDir = args[0];
            string outDir = args[1];

            if (!Directory.Exists(dataDir))
            {
                Console.WriteLine("数据目录不存在: " + dataDir);
                return 2;
            }

            var libs = new List<string>();
            if (args.Length > 2)
            {
                for (int i = 2; i < args.Length; i++)
                    libs.Add(args[i]);
            }
            else
            {
                foreach (string f in Directory.GetFiles(dataDir, "*.Lib", SearchOption.TopDirectoryOnly))
                    libs.Add(Path.GetFileNameWithoutExtension(f));
            }

            int totalOk = 0;

            foreach (string lib in libs)
            {
                string path = Path.Combine(dataDir, lib + ".Lib");
                if (!File.Exists(path))
                {
                    Console.WriteLine("跳过（找不到）: " + path);
                    continue;
                }

                try
                {
                    totalOk += Export(path, Path.Combine(outDir, lib));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("{0}: 失败 - {1}", lib, ex.Message));
                }
            }

            Console.WriteLine();
            Console.WriteLine("完成，共导出 " + totalOk + " 张图到 " + outDir);
            return 0;
        }

        private static int Export(string file, string outDir)
        {
            Directory.CreateDirectory(outDir);

            byte[] all = File.ReadAllBytes(file);

            using (var ms = new MemoryStream(all))
            using (var reader = new BinaryReader(ms))
            {
                int version = reader.ReadInt32();
                if (version < 2)
                {
                    Console.WriteLine(Path.GetFileName(file) + ": 版本不支持 " + version);
                    return 0;
                }

                int count = reader.ReadInt32();
                if (version >= 3) reader.ReadInt32();   // frameSeek，导出用不到

                var indexList = new int[count];
                for (int i = 0; i < count; i++)
                    indexList[i] = reader.ReadInt32();

                int ok = 0, empty = 0, fail = 0;
                var sb = new StringBuilder();
                sb.AppendLine("索引,宽,高,偏移X,偏移Y,文件");

                for (int i = 0; i < count; i++)
                {
                    int off = indexList[i];
                    if (off <= 0 || off + 17 > all.Length) { empty++; continue; }

                    ms.Position = off;
                    short w = reader.ReadInt16();
                    short h = reader.ReadInt16();
                    short ox = reader.ReadInt16();
                    short oy = reader.ReadInt16();
                    reader.ReadInt16();                     // ShadowX
                    reader.ReadInt16();                     // ShadowY
                    byte shadow = reader.ReadByte();
                    int length = reader.ReadInt32();

                    if (w <= 0 || h <= 0 || length <= 0) { empty++; continue; }

                    long dataPos = (long)off + 17;
                    if (dataPos + length > all.Length) { fail++; continue; }

                    try
                    {
                        byte[] raw = new byte[length];
                        Array.Copy(all, dataPos, raw, 0, length);

                        byte[] pixels = Gunzip(raw, w * h * 4);
                        if (pixels == null) { fail++; continue; }

                        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                        {
                            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                                                         ImageLockMode.WriteOnly,
                                                         PixelFormat.Format32bppArgb);
                            try
                            {
                                for (int y = 0; y < h; y++)
                                    Marshal.Copy(pixels, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4);
                            }
                            finally
                            {
                                bmp.UnlockBits(bd);
                            }

                            bmp.Save(Path.Combine(outDir, i + ".png"), ImageFormat.Png);
                        }

                        sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{0}.png", i, w, h, ox, oy));
                        ok++;
                    }
                    catch
                    {
                        fail++;
                    }
                }

                File.WriteAllText(Path.Combine(outDir, "_index.csv"), sb.ToString(), new UTF8Encoding(true));
                Console.WriteLine(string.Format("{0}: 导出 {1} 张，空图 {2}，失败 {3}",
                    Path.GetFileNameWithoutExtension(file), ok, empty, fail));
                return ok;
            }
        }

        private static byte[] Gunzip(byte[] data, int expect)
        {
            using (var input = new MemoryStream(data))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                byte[] result = output.ToArray();
                return result.Length < expect ? null : result;
            }
        }
    }
}
