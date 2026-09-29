# -*- coding: utf-8 -*-
"""替换 Client/Utils/ResourceDownloader.cs 里的 DownloadFile 方法。

背景：原来用 HttpClient.Timeout 固定 32 秒，而资源体积跨度极大
（音效几百 KB ~ 地砖图库 588MB）。固定总超时会让外网玩家在大文件上必然失败。
新实现：等响应头用固定上限，响应头到手后撤掉总超时，读数据时改成
「多久没收到数据」的静默超时。
"""

import io

PATH = 'Client/Utils/ResourceDownloader.cs'

NEW = r'''        private static bool DownloadFile(string relative)
        {
            string url = BuildUrl("resource", "f", relative);

            int headerTimeoutMs = Settings.MicroTimeout;
            if (headerTimeoutMs < 30000) headerTimeoutMs = 30000;

            try
            {
                // 复用共享实例：连接走连接池，不再每个文件都新建一条 TCP 连接
                var client = SharedClient();

                // 先给「等响应头」一个上限：服务器没起来或者卡住时不能无限挂着。
                // 响应头一到就把这个总超时撤掉，之后的时长交给下面的「静默超时」控制 ——
                // 最大的地砖图库有 588MB，用任何固定的短总超时都会把它误判成失败。
                using (var cts = new CancellationTokenSource(headerTimeoutMs))
                {
                    HttpResponseMessage response;
                    try
                    {
                        // ResponseHeadersRead：拿到响应头就开始写盘，大图库也不会整块堆在内存里
                        response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                                         .GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        Log("连接超时（" + (headerTimeoutMs / 1000) + " 秒未收到响应头）: " + relative);
                        return false;
                    }

                    using (response)
                    {
                        // 响应头已经拿到了，撤掉总超时：大文件的传输时长不该被它限制
                        cts.CancelAfter(System.Threading.Timeout.Infinite);

                        if (!response.IsSuccessStatusCode)
                        {
                            Log("资源不存在或被拒绝 " + relative + " -> " + (int)response.StatusCode);
                            return false;
                        }

                        string localPath = ToLocalPath(relative);
                        if (localPath == null) return false;

                        string dir = Path.GetDirectoryName(localPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        // 先写临时文件再改名，避免半截文件被后续加载当成有效资源
                        string temp = localPath + ".tmp";
                        long total = 0;

                        using (var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[64 * 1024];
                            int limit = Settings.MicroRateLimit;      // KB/s，0 = 不限速
                            int stallMs = headerTimeoutMs;            // 多久收不到数据算卡死
                            var watch = Stopwatch.StartNew();

                            int read;
                            while (true)
                            {
                                try
                                {
                                    using (var readCts = new CancellationTokenSource(stallMs))
                                    {
                                        read = source.ReadAsync(buffer, 0, buffer.Length, readCts.Token)
                                                     .GetAwaiter().GetResult();
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Log("读取超时（" + (stallMs / 1000) + " 秒未收到数据）: " + relative);
                                    return false;
                                }

                                if (read <= 0) break;

                                target.Write(buffer, 0, read);
                                total += read;
                                Throttle(watch, total, limit);
                            }

                            target.Flush();
                        }

                        if (total == 0)
                        {
                            try { File.Delete(temp); } catch { }
                            Log("资源内容为空 " + relative);
                            return false;
                        }

                        if (File.Exists(localPath)) File.Delete(localPath);
                        File.Move(temp, localPath);

                        lock (_sync)
                        {
                            _bytes += total;
                        }

                        Log("已下载 " + relative + " (" + total + " 字节)");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("下载失败 " + relative + " : " + ex.Message);
                return false;
            }
        }

'''

s = io.open(PATH, encoding='utf-8-sig').read()

start_marker = '        private static bool DownloadFile(string relative)'
end_marker = '        /// <summary>可选的下载限速'

start = s.index(start_marker)
end = s.index(end_marker)

s = s[:start] + NEW + s[end:]

io.open(PATH, 'w', encoding='utf-8-sig', newline='').write(s)
print('DownloadFile 已替换')
