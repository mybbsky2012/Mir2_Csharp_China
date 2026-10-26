using System;
using System.IO;
using System.Drawing;
using System.Linq;
using LibraryEditor;

internal static class FuncTest
{
    public static int Run(string srcFile)
    {
        int fails = 0;
        string sandbox = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_sandbox");
        Directory.CreateDirectory(sandbox);
        string testFile = Path.Combine(sandbox, "EuReStateHair.EuRe");
        File.Copy(srcFile, testFile, true);
        byte[] beforeMd5Src = File.ReadAllBytes(testFile);

        // 1. 加载
        var lib = new EuReLibrary(testFile);
        Console.WriteLine("[加载] Count=" + lib.Count);
        if (lib.Count != 32) { Console.WriteLine("FAIL 帧数应为 32"); fails++; }

        // 2. 解码第 1 帧
        var mi = lib.GetMImage(1);
        Console.WriteLine($"[帧1] {mi.Width}x{mi.Height} offset=({mi.X},{mi.Y}) HasMask={mi.HasMask}");
        if (mi.Width <= 0 || mi.Height <= 0) { Console.WriteLine("FAIL 帧1尺寸异常"); fails++; }
        if (mi.Image == null) { Console.WriteLine("FAIL 帧1未解码出位图"); fails++; }
        else if (mi.Image.Width != mi.Width || mi.Image.Height != mi.Height)
        { Console.WriteLine("FAIL 位图尺寸与帧头不符"); fails++; }

        // 3. 未改动保存 → 必须字节级一致
        lib.Save();
        byte[] after = File.ReadAllBytes(testFile);
        bool identical = beforeMd5Src.SequenceEqual(after);
        Console.WriteLine("[未改动保存] 字节一致=" + identical);
        if (!identical) { Console.WriteLine("FAIL 未改动保存字节不一致"); fails++; }

        // 4. 替换第 0 帧为红色测试图 → 保存 → 重读验证
        using (Bitmap red = new Bitmap(7, 5))
        using (Graphics g = Graphics.FromImage(red))
        {
            g.Clear(Color.FromArgb(255, 200, 30, 40));
            lib.ReplaceImage(0, red, 3, 4, false);
        }
        lib.Save();
        lib.Close();

        var lib2 = new EuReLibrary(testFile);
        var mi0 = lib2.GetMImage(0);
        Console.WriteLine($"[替换后] 帧0={mi0.Width}x{mi0.Height} offset=({mi0.X},{mi0.Y}) " +
            $"像素=({mi0.Image.GetPixel(0,0).R},{mi0.Image.GetPixel(0,0).G},{mi0.Image.GetPixel(0,0).B})");
        if (mi0.Width != 7 || mi0.Height != 5 || mi0.X != 3 || mi0.Y != 4)
        { Console.WriteLine("FAIL 替换后帧0属性不符"); fails++; }
        var c0 = mi0.Image.GetPixel(0, 0);
        if (c0.R != 200 || c0.G != 30 || c0.B != 40) { Console.WriteLine("FAIL 替换后颜色不符"); fails++; }

        // 5. 其余帧未被破坏：帧1尺寸与之前一致
        var mi1b = lib2.GetMImage(1);
        if (mi1b.Width != mi.Width || mi1b.Height != mi.Height)
        { Console.WriteLine("FAIL 替换后帧1被破坏"); fails++; }

        // 6. 增删帧
        int cnt = lib2.Count;
        using (Bitmap b = new Bitmap(2, 2)) lib2.AddImage(b, 0, 0, false);
        lib2.RemoveImage(5);
        lib2.Save();
        lib2.Close();
        var lib3 = new EuReLibrary(testFile);
        Console.WriteLine($"[增删] 原 {cnt} -> 现 {lib3.Count} (期望 {cnt})");
        if (lib3.Count != cnt) { Console.WriteLine("FAIL 增删后帧数不符"); fails++; }
        lib3.Close();

        Console.WriteLine(fails == 0 ? "功能测试: 全部通过" : $"功能测试: {fails} 项失败");
        return fails;
    }
}
