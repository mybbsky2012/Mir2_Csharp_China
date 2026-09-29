using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace Client.Utils
{
    /// <summary>
    /// 微端资源下载器（真正的"边玩边加载"）。
    ///
    /// 核心原则：**资源只在被真正用到的那一刻才去下载**。
    /// 玩家看不到"加载 10 分钟"，启动速度只跟核心包体积有关，剩下的资源在游戏过程中悄悄补齐。
    ///
    /// 三个关键机制：
    ///   1. 批量初始化抑制（BeginBulkLoad / EndBulkLoad）
    ///      LoadGameLibraries() 会在启动时把上千个 .Lib 图库目录"登记"一遍。那些只是数组占位，
    ///      绝大多数玩家当前地图根本用不到。抑制期间只查本地文件、绝不入队，避免启动瞬间
    ///      把整个资源库塞进下载队列 —— 那就退化成"一次性全量加载"了。
    ///
    ///   2. 非阻塞排队（EnsureLocalFile / RequestLocalFile）
    ///      任何加载点发现本地缺文件时，只把请求丢进后台队列并立即返回"现在没有"。
    ///      调用方按原本"缺文件就跳过"的逻辑走，下一帧/下一次调用自然重试，等到后台下完就生效。
    ///      渲染线程永远不会被网络 IO 卡住。
    ///
    ///   3. 必须就绪时的小范围等待（EnsureLocalFileBlocking）
    ///      只有"缺了就没法继续"的资源才等待，例如进入某张地图的 .map 文件、
    ///      以及 DX 像素着色器。等待上限很小，且这些资源一次性、体积可控。
    ///
    /// 其它保证：
    ///   - 永不抛异常 —— 下载失败就当作资源不存在，绝不让网络问题把客户端搞崩。
    ///   - 负缓存 —— 失败过的文件本次运行不再重试，避免反复打不通的服务端拖慢游戏。
    ///   - 原子写入 —— 先写 .tmp 再改名，避免断线留下的半截文件被当成有效资源加载。
    ///   - 去重 —— 同一文件被多个加载点/多帧反复请求时只下载一次。
    /// </summary>
    internal static class ResourceDownloader
    {
        private static readonly object _sync = new object();

        // 文件状态（全部为相对客户端根目录的正斜杠路径）
        private static readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // 已入队或已完成

        private static readonly Queue<string> _queue = new Queue<string>();
        private static readonly Dictionary<string, List<ManualResetEventSlim>> _waiters =
            new Dictionary<string, List<ManualResetEventSlim>>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<Thread> _workers = new List<Thread>();
        private static bool _workersStarted;

        /// <summary>
        /// 批量登记阶段（LoadGameLibraries）的抑制标记。
        /// 用 ThreadStatic 保证只影响执行批量初始化的那个线程：
        /// 即使同一时刻渲染线程真的要读某个图库，它仍然能正常排队下载。
        /// </summary>
        [ThreadStatic]
        private static int _bulkDepth;

        // 统计信息，供游戏内角标显示
        private static int _pending;          // 队列中 + 下载中
        private static int _completed;        // 成功下载的文件数
        private static int _failedCount;      // 失败的文件数
        private static long _bytes;           // 累计下载字节数
        private static volatile string _lastFile = string.Empty;

        // 服务端资源清单：目录(小写、正斜杠相对路径) -> 该目录下的文件名集合
        private static Dictionary<string, HashSet<string>> _remoteIndex;
        private static bool _indexLoading;
        private static int _indexNextTry;                       // 拉取失败的冷却截止（Environment.TickCount）
        private const int IndexRetryMs = 30000;                 // 失败后 30 秒才允许再试，避免没起服务端时空转刷日志

        /// <summary>
        /// 资源清单到手时触发一次（仅成功时）。
        /// Libraries 用它把"降级初始化"的图库数组按正确长度重建。
        /// </summary>
        internal static Action RemoteIndexReady;

        /// <summary>清单是否已经到手（图库数组据此判断初始化是否降级过）。</summary>
        internal static bool IsIndexAvailable
        {
            get { lock (_sync) return _remoteIndex != null; }
        }

        #region 对外状态

        /// <summary>当前还有多少个资源在排队/下载中（0 表示后台已空闲）。</summary>
        internal static int PendingCount
        {
            get { lock (_sync) return _pending; }
        }

        /// <summary>后台是否正在补资源（用于游戏内角标）。</summary>
        internal static bool IsDownloading
        {
            get { return PendingCount > 0; }
        }

        internal static int CompletedCount
        {
            get { lock (_sync) return _completed; }
        }

        internal static long DownloadedBytes
        {
            get { lock (_sync) return _bytes; }
        }

        internal static string LastDownloadedFile
        {
            get { return _lastFile; }
        }

        #endregion

        #region 批量初始化抑制

        /// <summary>
        /// 进入"批量登记"阶段：期间所有下载请求只查本地文件，不入队。
        /// LoadGameLibraries() 必须用它包住，否则启动时会排队上千个用不到的图库。
        /// </summary>
        internal static void BeginBulkLoad()
        {
            _bulkDepth++;
        }

        internal static void EndBulkLoad()
        {
            if (_bulkDepth > 0) _bulkDepth--;
        }

        private static bool BulkLoading
        {
            get { return _bulkDepth > 0; }
        }

        #endregion

        #region 按需加载入口

        /// <summary>
        /// 确保文件在本地可用（非阻塞）。返回 true 表示现在就能打开它。
        ///
        /// 文件不在本地时会把它排进后台下载队列并立即返回 false，调用方按原逻辑
        /// "缺文件就跳过"处理即可 —— 后台下完之后，下一次调用就会返回 true。
        /// </summary>
        internal static bool EnsureLocalFile(string localPath)
        {
            bool queued;
            return EnsureLocalFile(localPath, out queued);
        }

        /// <summary>
        /// 同上，并通过 queued 告知"本次是否已安排后台下载"。
        /// 调用方（如 MLibrary）可据此记住"已提交过请求"，避免每帧重复检查磁盘。
        /// </summary>
        internal static bool EnsureLocalFile(string localPath, out bool queued)
        {
            queued = false;

            try
            {
                if (File.Exists(localPath)) return true;

                if (!Settings.MicroClient) return false;

                string relative = ToRelativePath(localPath);
                if (relative == null) return false;   // 不在客户端目录内，无法映射到服务端资源

                lock (_sync)
                {
                    if (_failed.Contains(relative)) return false;
                    if (_known.Contains(relative)) return false;   // 已经在队列里/下载中，等它自己完成
                }

                // 批量登记阶段：只登记"本地没有"，不产生任何下载请求
                if (BulkLoading) return false;

                // 服务端也没有这个文件时直接记失败，省掉一次注定 404 的 HTTP 往返。
                // 音效这类"按扩展名逐个试探"的加载点收益最大。
                if (!ExistsOnServer(relative))
                {
                    lock (_sync) _failed.Add(relative);
                    return false;
                }

                queued = Enqueue(relative);
                return false;
            }
            catch (Exception ex)
            {
                Log("EnsureLocalFile 异常 " + localPath + " : " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 阻塞式确保文件就绪。只用于"缺了就没法继续"的场景：
        /// 进入地图的 .map、DX 像素着色器、音效索引表。
        /// 超时后返回 false，调用方走原有的缺资源兜底分支。
        /// </summary>
        internal static bool EnsureLocalFileBlocking(string localPath, int timeoutMs)
        {
            try
            {
                if (File.Exists(localPath)) return true;
                if (!Settings.MicroClient) return false;

                bool queued;
                if (EnsureLocalFile(localPath, out queued)) return true;
                if (!queued) return false;      // 已失败过或不在客户端目录内，等也没用

                string relative = ToRelativePath(localPath);
                if (relative == null) return false;

                var handle = new ManualResetEventSlim(false);

                lock (_sync)
                {
                    // 极端情况下文件在入队与注册之间就下完了
                    if (!_known.Contains(relative))
                        return File.Exists(localPath);

                    if (!_waiters.TryGetValue(relative, out var list))
                    {
                        list = new List<ManualResetEventSlim>();
                        _waiters[relative] = list;
                    }
                    list.Add(handle);
                }

                if (timeoutMs < 1000) timeoutMs = 1000;
                handle.Wait(timeoutMs);

                lock (_sync)
                {
                    if (_waiters.TryGetValue(relative, out var list))
                    {
                        list.Remove(handle);
                        if (list.Count == 0) _waiters.Remove(relative);
                    }
                }

                bool ok = File.Exists(localPath);
                if (!ok) Log("等待资源超时 " + relative);
                return ok;
            }
            catch (Exception ex)
            {
                Log("EnsureLocalFileBlocking 异常 " + localPath + " : " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 忘掉某文件之前的下载失败记录，允许它重新入队。
        /// 用于这种场景：清单未就绪时首次尝试被误判「服务端没有」而进了负缓存，
        /// 等清单真正到手后需要给它一次重新同步的机会（如音效索引表 SoundList.lst）。
        /// 注意下载失败过的文件仍留在 _known 里，必须一并移除才能重新入队。
        /// </summary>
        internal static void ForgetFailure(string localPath)
        {
            try
            {
                string relative = ToRelativePath(localPath);
                if (relative == null) return;

                lock (_sync)
                {
                    _failed.Remove(relative);
                    _known.Remove(relative);
                }
            }
            catch
            {
            }
        }
        /// <summary>
        /// 依据已加载的资源清单判断服务端是否真的有这个文件（入参为相对客户端根目录的路径）。
        /// 清单尚未就绪或不可用时返回 true（乐观放行），交给真实的 HTTP 请求去判断。
        /// </summary>
        private static bool ExistsOnServer(string relative)
        {
            try
            {
                if (!Settings.MicroClient) return true;

                var index = GetRemoteIndex();
                if (index == null) return true;      // 清单不可用，不做预判

                string key = relative.Replace('\\', '/');
                int slash = key.LastIndexOf('/');
                string dir = slash > 0 ? key.Substring(0, slash).ToLowerInvariant() : string.Empty;
                string name = slash >= 0 ? key.Substring(slash + 1) : key;

                return index.TryGetValue(dir, out var files) && files.Contains(name);
            }
            catch
            {
                return true;
            }
        }

        #endregion

        #region 下载队列

        /// <summary>把一个文件排进后台下载队列；返回 false 表示"已有请求/已失败/已存在"，无需重复入队。</summary>
        private static bool Enqueue(string relative)
        {
            lock (_sync)
            {
                if (_failed.Contains(relative) || _known.Contains(relative)) return false;

                _known.Add(relative);
                _queue.Enqueue(relative);
                _pending++;

                EnsureWorkersLocked();
                Monitor.Pulse(_sync);
                return true;
            }
        }

        private static void EnsureWorkersLocked()
        {
            if (_workersStarted) return;
            _workersStarted = true;

            int count = Settings.MicroConcurrency;
            if (count < 1) count = 1;
            if (count > 8) count = 8;

            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "MicroClientDownloader" + i
                };
                thread.Start();
                _workers.Add(thread);
            }

            Log("微端后台下载线程已启动，并发数 " + count);
        }

        private static void WorkerLoop()
        {
            while (true)
            {
                string relative;

                lock (_sync)
                {
                    while (_queue.Count == 0)
                        Monitor.Wait(_sync);

                    relative = _queue.Dequeue();
                }

                bool ok = false;
                try
                {
                    ok = DownloadFile(relative);
                }
                catch (Exception ex)
                {
                    Log("下载线程异常 " + relative + " : " + ex.Message);
                }

                lock (_sync)
                {
                    _pending--;
                    if (ok)
                    {
                        _completed++;
                        _lastFile = relative;
                    }
                    else
                    {
                        _failed.Add(relative);
                        _failedCount++;
                    }

                    if (_waiters.TryGetValue(relative, out var list))
                    {
                        foreach (var handle in list)
                        {
                            try { handle.Set(); } catch { }
                        }
                    }
                }
            }
        }

        private static bool DownloadFile(string relative)
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

        /// <summary>可选的下载限速。玩家带宽紧张时可以把 RateLimit 调小，让补资源尽量不抢网速。</summary>
        private static void Throttle(Stopwatch watch, long total, int limitKBps)
        {
            if (limitKBps <= 0) return;

            double expectedMs = total / 1024.0 * 1000.0 / limitKBps;
            double actualMs = watch.Elapsed.TotalMilliseconds;
            double sleepMs = expectedMs - actualMs;

            if (sleepMs > 1)
            {
                int wait = (int)Math.Min(sleepMs, 200);
                Thread.Sleep(wait);
            }
        }

        #endregion

        #region 资源清单

        /// <summary>
        /// 提前在后台把服务端资源清单拉下来。
        /// 清单只有几十 KB，但 InitLibrary 推算图库数组长度时必须要它，
        /// 先预热可以让启动阶段完全不阻塞在网络上。
        /// </summary>
        internal static void WarmUp()
        {
            if (!Settings.MicroClient) return;

            var thread = new Thread(delegate()
            {
                try
                {
                    // 服务端还没起/网络抖动时拉不到清单：隔 30 秒再试，直到拿到为止。
                    // 拿到后 GetRemoteIndex 内部会触发 RemoteIndexReady，图库数组自动重建。
                    while (true)
                    {
                        if (GetRemoteIndex() != null) return;
                        Thread.Sleep(IndexRetryMs);
                    }
                }
                catch { }
            })
            {
                IsBackground = true,
                Name = "MicroClientIndex"
            };
            thread.Start();
        }

        /// <summary>
        /// 从服务端资源清单推算某个图库目录应有的文件数量。
        ///
        /// 微端下本地目录可能是空的，而 Libraries.InitLibrary 是靠扫描本地目录
        /// 决定 MLibrary 数组长度的，目录为空会导致索引越界，所以必须向服务端要这个数。
        /// 返回 0 表示无法判断（调用方沿用本地结果）。
        /// </summary>
        internal static int GetExpectedLibraryCount(string dirPath, string suffix)
        {
            try
            {
                if (!Settings.MicroClient) return 0;

                var index = GetRemoteIndex();
                if (index == null) return 0;

                string dirKey = ToRelativePath(dirPath);
                if (dirKey == null) return 0;

                // 目录相对路径可能带结尾斜杠，统一去掉
                dirKey = dirKey.TrimEnd('/').ToLowerInvariant();

                if (!index.TryGetValue(dirKey, out var files) || files.Count == 0) return 0;

                string tail = (suffix ?? string.Empty) + ".lib";
                int max = -1;

                foreach (var name in files)
                {
                    if (!name.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) continue;

                    // 直接对完整文件名取结尾数字：100.Lib -> 100、"00 S.Lib" -> 0。
                    // 注意不能先截掉 tail 再解析，否则 100.Lib 会被当成 1。
                    int value = TrailingNumber(name);
                    if (value > max) max = value;
                }

                return max + 1;
            }
            catch (Exception ex)
            {
                Log("GetExpectedLibraryCount 异常 " + dirPath + " : " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// 拉取服务端资源清单（每个目录下的文件名），用于推算图库数组长度。
        /// 拉取失败后进入 30 秒冷却，冷却期过后由 WarmUp 线程自动重试 ——
        /// 绝不能像旧版那样失败一次就永久放弃：客户端比服务端先启动是很常见的
        /// （服务端在重启、网络抖动），放弃意味着整局图库数组长度全错、
        /// NPC/装备资源就算下载成功也用不上。
        /// </summary>
        private static Dictionary<string, HashSet<string>> GetRemoteIndex()
        {
            lock (_sync)
            {
                if (_remoteIndex != null) return _remoteIndex;

                if (_indexLoading)
                {
                    // 已有线程在拉，等它（预热线程通常已经拉好了）
                    Monitor.Wait(_sync, 10000);
                    return _remoteIndex;
                }

                if (unchecked(Environment.TickCount - _indexNextTry) < 0)
                    return null;      // 冷却中：不重试也不报错，交给 WarmUp 稍后再来

                _indexLoading = true;
            }

            Dictionary<string, HashSet<string>> index = null;

            try
            {
                string url = BuildUrl("resindex", null, null);

                // 复用共享实例：连接走连接池，不再每个文件都新建一条 TCP 连接
                var client = SharedClient();

                // 清单体积小，给它一个固定上限（共享的 HttpClient 本身已不设超时）
                int indexTimeoutMs = Settings.MicroTimeout;
                if (indexTimeoutMs < 30000) indexTimeoutMs = 30000;

                using (var indexCts = new CancellationTokenSource(indexTimeoutMs))
                using (var response = client.GetAsync(url, indexCts.Token).GetAwaiter().GetResult())
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Log("资源清单获取失败 -> " + (int)response.StatusCode);
                    }
                    else
                    {
                        string text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        index = ParseIndex(text);

                        // 空清单不能当成有效结果缓存：服务端「微端资源服务」未开启或资源目录
                        // 未配置时同样返回 200 + 空内容。若把空清单缓存下来，
                        // ExistsOnServer 会对所有文件返回 false（包括音效索引表 SoundList.lst），
                        // 索引表从此再也同步不下来 —— 整局音效全部落到默认命名上，声音全错。
                        if (CountEntries(index) == 0)
                        {
                            Log("资源清单为空（服务端未开启微端资源服务或资源目录为空），稍后重试");
                            index = null;
                        }
                        else
                        {
                            Log("资源清单加载完成，共 " + CountEntries(index) + " 个文件");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("资源清单获取异常 : " + ex.Message);
            }

            lock (_sync)
            {
                if (index != null) _remoteIndex = index;
                else _indexNextTry = Environment.TickCount + IndexRetryMs;

                _indexLoading = false;
                Monitor.PulseAll(_sync);
            }

            // 清单成功到手：通知图库数组重建（在锁外触发，回调自己保证线程安全）
            if (index != null && RemoteIndexReady != null)
            {
                try { RemoteIndexReady(); } catch { }
            }

            return IsIndexAvailable ? _remoteIndex : null;
        }

        /// <summary>解析清单文本：每行一个「相对路径」，如 Data/Monster/000.Lib。</summary>
        private static Dictionary<string, HashSet<string>> ParseIndex(string text)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) return result;

            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim().Replace('\\', '/');
                if (line.Length == 0) continue;

                int slash = line.LastIndexOf('/');
                string dir = slash > 0 ? line.Substring(0, slash) : string.Empty;
                string name = slash >= 0 ? line.Substring(slash + 1) : line;

                if (!result.TryGetValue(dir, out var list))
                {
                    list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[dir] = list;
                }

                list.Add(name);
            }

            return result;
        }

        private static int CountEntries(Dictionary<string, HashSet<string>> index)
        {
            int n = 0;
            if (index != null)
                foreach (var kv in index) n += kv.Value.Count;
            return n;
        }

        #endregion

        #region 路径与 HTTP

        /// <summary>把本地绝对路径换算成相对客户端根目录的正斜杠路径；不在客户端目录内返回 null。</summary>
        internal static string ToRelativePath(string localPath)
        {
            try
            {
                string root = Path.GetFullPath(Settings.P_Client);
                if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    root += Path.DirectorySeparatorChar;

                string full = Path.GetFullPath(localPath);
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;

                return full.Substring(root.Length).Replace('\\', '/');
            }
            catch
            {
                return null;
            }
        }

        /// <summary>把清单里的相对路径还原成本地绝对路径（客户端目录内）。</summary>
        private static string ToLocalPath(string relative)
        {
            try
            {
                if (string.IsNullOrEmpty(relative)) return null;
                if (relative.IndexOf(':') >= 0) return null;

                string root = Path.GetFullPath(Settings.P_Client);
                string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
                return full;
            }
            catch
            {
                return null;
            }
        }

        private static HttpClient _sharedClient;
        private static readonly object _clientLock = new object();

        /// <summary>
        /// 共享的 HttpClient。
        ///
        /// 原来是每下载一个文件就 new 一个 HttpClient —— 那等于每个请求都重新
        /// 建一条 TCP 连接，用完连接就丢。上百人同时补资源时，服务端会堆满
        /// TIME_WAIT 连接（默认要等好几分钟才释放），端口和内存都被白白吃掉。
        /// 复用同一个实例后连接走连接池，下完一个文件接着下下一个。
        /// </summary>
        private static HttpClient SharedClient()
        {
            if (_sharedClient != null) return _sharedClient;

            lock (_clientLock)
            {
                if (_sharedClient != null) return _sharedClient;

                var handler = new HttpClientHandler
                {
                    // 单机同时下载的并发上限，和后台下载线程数保持一致即可
                    MaxConnectionsPerServer = Math.Max(2, Settings.MicroConcurrency),
                };

                var client = new HttpClient(handler);

                // 刻意不用 HttpClient.Timeout：它无法区分「服务器连不上」和「文件本来就大」。
                // 而且在 ResponseHeadersRead 模式下，这个超时会一直管到响应体读完为止 ——
                // 最大的地砖图库有 588MB，外网玩家以 2.5MB/s 下载要 4 分钟，
                // 任何几十秒的固定超时都会让它必然失败（这就是外网微端最坑的一处）。
                // 超时改由调用方按场景自己控制：
                //   · 清单这类小请求 → CancellationTokenSource 给固定上限
                //   · 资源下载       → 「多久没收到数据」的静默超时（见 DownloadFile）
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

                _sharedClient = client;
                return _sharedClient;
            }
        }

        private static string BuildUrl(string path, string key, string value)
        {
            string host = Settings.MicroHost ?? string.Empty;
            if (host.Length == 0) host = "http://127.0.0.1:5679/";
            host = host.TrimEnd('/');

            string url = host + "/" + path;
            if (key != null && value != null)
            {
                // 保留 / 分隔符便于服务端识别目录层级，同时保证中文/空格安全
                string encoded = Uri.EscapeDataString(value).Replace("%2F", "/");
                url += "?" + key + "=" + encoded;
            }

            return url;
        }

        /// <summary>取字符串结尾的数字，例如 "000" -> 0、"100 R" -> 100、"0012" -> 12。</summary>
        private static int TrailingNumber(string stem)
        {
            stem = stem.Trim();
            if (stem.Length == 0) return -1;

            int end = stem.Length - 1;
            while (end >= 0 && !char.IsDigit(stem[end])) end--;
            if (end < 0) return -1;

            int start = end;
            while (start >= 0 && char.IsDigit(stem[start])) start--;

            string digits = stem.Substring(start + 1, end - start);
            return int.TryParse(digits, out int value) ? value : -1;
        }

        internal static void Log(string message)
        {
            if (!Settings.MicroLog) return;
            try
            {
                File.AppendAllText(Path.Combine(System.Windows.Forms.Application.StartupPath, "MicroClient.log"),
                    string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine),
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        #endregion
    }
}
