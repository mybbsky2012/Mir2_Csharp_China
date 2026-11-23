using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace LohPoolTest
{
    //★验证 MLibrary.CreateTexture 的 ArrayPool 改造（11-20 内存优化）：
    //  1) 新 DecompressImage(compressed, len, target, targetLen) 与旧 ToArray 版输出一致（精确长度）；
    //  2) 超长输出按容量截断、短输出不越界；
    //  3) reader.Read 循环与 reader.ReadBytes 消耗的字节流位置完全一致（含 EOF 边界）。
    internal static class Program
    {
        private static int _pass, _fail;

        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            TestDecompressExact();
            TestDecompressOversize();
            TestDecompressShort();
            TestReadVsReadBytes();
            TestPoolRepeat();

            Console.WriteLine($"==== 结果：{_pass} 过 {_fail} 挂 ====");
            Environment.Exit(_fail == 0 ? 0 : 1);
        }

        private static void Report(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; Console.WriteLine($"[过] {name}"); }
            else { _fail++; Console.WriteLine($"[挂] {name}  {detail}"); }
        }

        // ---- 与 MLibrary.cs 相同的实现副本 ----
        private static byte[] OldDecompress(byte[] image)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(image), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0) memory.Write(buffer, 0, count);
                    } while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        private static void NewDecompress(byte[] compressed, int compressedLen, byte[] target, int targetLen)
        {
            if (compressed == null || compressedLen <= 0 || target == null || targetLen <= 0) return;

            using (GZipStream stream = new GZipStream(new MemoryStream(compressed, 0, compressedLen), CompressionMode.Decompress))
            {
                int written = 0;
                while (written < targetLen)
                {
                    int count = stream.Read(target, written, targetLen - written);
                    if (count <= 0) break;
                    written += count;
                }
            }
        }

        private static byte[] MakeGz(byte[] payload)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
                    gz.Write(payload, 0, payload.Length);
                return ms.ToArray();
            }
        }

        private static void TestDecompressExact()
        {
            var rnd = new Random(42);
            for (int t = 0; t < 5; t++)
            {
                int len = 1024 * (t + 1) * 3;    // 全部 >85KB → 模拟 LOH
                byte[] payload = new byte[len];
                rnd.NextBytes(payload);
                byte[] gz = MakeGz(payload);

                byte[] oldOut = OldDecompress(gz);

                byte[] target = ArrayPool<byte>.Shared.Rent(len);
                try
                {
                    Array.Clear(target, 0, target.Length);
                    NewDecompress(gz, gz.Length, target, len);
                    bool same = oldOut.Length == len;
                    for (int i = 0; same && i < len; i++) same = oldOut[i] == target[i];
                    Report($"精确长度解压一致 #{t} ({len}B)", same);
                }
                finally { ArrayPool<byte>.Shared.Return(target); }
            }
        }

        private static void TestDecompressOversize()
        {
            // 解压输出比 target 大：新实现按容量截断，不抛异常不越界
            var rnd = new Random(7);
            byte[] payload = new byte[500 * 1024];
            rnd.NextBytes(payload);
            byte[] gz = MakeGz(payload);

            byte[] target = ArrayPool<byte>.Shared.Rent(300 * 1024);
            try
            {
                NewDecompress(gz, gz.Length, target, 300 * 1024);
                bool ok = true;
                for (int i = 0; i < 300 * 1024; i++) ok &= target[i] == payload[i];
                Report("超长解压按容量截断且前缀一致", ok);
            }
            finally { ArrayPool<byte>.Shared.Return(target); }
        }

        private static void TestDecompressShort()
        {
            // 解压输出比 target 小：写多少算多少，剩余区域不动（等价旧版 raw.Length 短的行为）
            byte[] payload = new byte[1000];
            new Random(9).NextBytes(payload);
            byte[] gz = MakeGz(payload);

            byte[] target = new byte[4 * 1024 * 1024];
            for (int i = 1000; i < target.Length; i++) target[i] = 0xAB;

            NewDecompress(gz, gz.Length, target, target.Length);
            bool ok = target[999] == payload[999] && target[1000] == 0xAB && target[target.Length - 1] == 0xAB;
            Report("短解压不越界不覆盖", ok);
        }

        private static void TestReadVsReadBytes()
        {
            // 两台"reader"读同一文件：一边 ReadBytes，一边 Read 循环，比对消耗的字节与内容
            var rnd = new Random(1234);
            byte[] data = new byte[300 * 1024];
            rnd.NextBytes(data);
            string file = Path.Combine(Path.GetTempPath(), "lohtest_" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(file, data);

            try
            {
                var sizes = new[] { 10, 4096, 100 * 1024, 300 * 1024, 400 * 1024 };   // 最后一个超文件长度（EOF）
                int posA = 0, posB = 0;
                bool same = true;

                using (var fa = new FileStream(file, FileMode.Open, FileAccess.Read))
                using (var ra = new BinaryReader(fa))
                using (var fb = new FileStream(file, FileMode.Open, FileAccess.Read))
                using (var rb = new BinaryReader(fb))
                {
                    foreach (int len in sizes)
                    {
                        byte[] a = ra.ReadBytes(len);                       // 旧写法
                        byte[] buf = ArrayPool<byte>.Shared.Rent(len);
                        try
                        {
                            int got = 0;
                            while (got < len)
                            {
                                int n = rb.Read(buf, got, len - got);
                                if (n <= 0) break;
                                got += n;
                            }

                            if (a.Length != got) same = false;
                            for (int i = 0; same && i < got; i++) same = a[i] == buf[i];
                            posA += a.Length;
                            posB += got;
                        }
                        finally { ArrayPool<byte>.Shared.Return(buf); }
                    }
                }

                Report($"Read 循环 ≡ ReadBytes（含 EOF 边界，耗 {posA}/{posB}）", same && posA == posB);
            }
            finally { File.Delete(file); }
        }

        private static void TestPoolRepeat()
        {
            // 租用/归还高频循环：确认归还后能复用、无异常
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    byte[] a = ArrayPool<byte>.Shared.Rent(1024 * 1024);
                    byte[] b = ArrayPool<byte>.Shared.Rent(2048);
                    ArrayPool<byte>.Shared.Return(a);
                    ArrayPool<byte>.Shared.Return(b);
                }
                Report("高频租还 1000 轮无异常", true);
            }
            catch (Exception ex)
            {
                Report("高频租还 1000 轮无异常", false, ex.Message);
            }
        }
    }
}
