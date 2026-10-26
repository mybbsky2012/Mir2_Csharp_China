using System;
using System.IO;
using System.Linq;
using LibraryEditor.Formats;

string dir = args.Length > 0 ? args[0] : @"E:/BaiduNetdiskDownload/eurmir2/Data/Item";
int pass = 0, fail = 0;

foreach (string fn in Directory.GetFiles(dir, "*.EuRe"))
{
    byte[] orig = File.ReadAllBytes(fn);
    try
    {
        var c = EuReContainer.Parse(orig);
        byte[] rebuilt = c.Rebuild();

        bool identical = orig.SequenceEqual(rebuilt);
        int diffAt = -1;
        if (!identical)
        {
            int n = Math.Min(orig.Length, rebuilt.Length);
            for (int i = 0; i < n; i++)
                if (orig[i] != rebuilt[i]) { diffAt = i; break; }
            if (diffAt < 0) diffAt = n;
        }

        // 二次往返：重建 -> 再解析 -> 再重建，必须幂等
        var c2 = EuReContainer.Parse(rebuilt);
        byte[] rebuilt2 = c2.Rebuild();
        bool idem = rebuilt.SequenceEqual(rebuilt2);

        Console.WriteLine($"{Path.GetFileName(fn)}: 帧 {c.Frames.Count} (原 {c.OriginalCount}) | " +
            $"字节一致={identical} | 幂等={idem} | 原始 {orig.Length}B -> 重建 {rebuilt.Length}B" +
            (identical ? "" : $" 首个差异@{diffAt}"));

        if (identical && idem) pass++; else fail++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{Path.GetFileName(fn)}: 失败 - {ex.Message}");
        fail++;
    }
}

// 空库
var empty = EuReContainer.Parse(EuReContainer.BuildEmptyFile());
Console.WriteLine("空库: " + (empty.Rebuild().Length == 8 && empty.Frames.Count == 0 ? "OK" : "FAIL"));

Console.WriteLine($"通过 {pass} / 失败 {fail}");

int ffail = FuncTest.Run(Path.Combine(dir, "EuReStateHair.EuRe"));
return (fail == 0 && ffail == 0) ? 0 : 1;
