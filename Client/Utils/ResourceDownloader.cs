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
    ///   - 负缓存分级 —— 404（服务端确实没有）本次运行不再重试；偶发失败（网络抖动/断连/5xx）
    ///     只冷却 30 秒就允许重试，绝不让一次网络抖动把资源「判死刑」到重启为止。
    ///   - 原子写入 —— 先写 .tmp 再改名，避免断线留下的半截文件被当成有效资源加载。
    ///   - 去重 —— 同一文件被多个加载点/多帧反复请求时只下载一次。
    /// </summary>
    internal static class ResourceDownloader
    {
        private static readonly object _sync = new object();

        // 文件状态（全部为相对客户端根目录的正斜杠路径）
        //
        // ★负缓存分级（10-23 修「微端有时候突然不加载」）：
        //   以前只要下载失败一次（网络抖动/服务器重启/读取超时）就永久拉黑，本次运行再也不重试
        //   —— 玩家玩着玩着网络抖一下，正在补的那批资源就集体「死」了，只能重启客户端。
        //   现在：404（服务端确实没有）→ 永久；其余（超时/断连/5xx/写盘失败）→ 冷却 30 秒后自动允许重试。
        private const int PermanentFail = int.MaxValue;   // 404 等确定性失败：本次运行不再重试
        private const int TransientRetryMs = 30000;       // 偶发失败的冷却时长
        private static readonly Dictionary<string, int> _failedUntil =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // 文件 -> 冷却截止(TickCount) / PermanentFail

        //★11-14 失败次数：偶发失败每次 +1，达到 Settings.MicroMaxRetry 就永久放弃并通知上层。
        //  没有这个计数时，网络一直不通的文件会被「冷却 30 秒 → 重试 → 再冷却」无限循环，
        //  地图加载也就永远等不到「结束本次加载」的时机。
        private static readonly Dictionary<string, int> _attempts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // 文件 -> 已失败次数

        private static readonly HashSet<string> _known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // 已入队或已完成

        private static readonly Queue<string> _queue = new Queue<string>();
        // 紧急队列：切图等"缺了就走不下去"的资源走这里，永远先于普通队列被 worker 取走
        private static readonly Queue<string> _urgentQueue = new Queue<string>();
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

        // 等清单的总预算：服务端正常时 WarmUp 的请求几百毫秒就回来，这点预算足够拿到正确长度；
        // 服务器连不上时请求要挂到超时（≥30 秒），预算耗尽后所有调用立即返回 null，
        // 绝不让登录界面为了一份「拿不到的清单」干等 —— 这正是「点开始卡半分钟才出登录界面」的根源之一。
        private static int _indexWaitBudget = IndexStartWaitMs;
        private const int IndexStartWaitMs = 2500;
        private const int IndexWaitStepMs = 250;

        // 清单还没拿到时，阻塞等文件的上限：此时无法判断服务端有没有这个文件，
        // 等满原来的超时（15~30 秒）也几乎不会有结果，只会把启动卡住。
        private const int IndexUnavailableWaitMs = 1500;

        /// <summary>阻塞等待时回调 onTick 的间隔（毫秒）。100ms 足够让心跳和进度都不掉帧。</summary>
        private const int WaitTickMs = 100;

        /// <summary>
        /// 服务端明确拒绝了本机 IP（返回 "notrusted:&lt;ip&gt;"）。
        /// 这种情况下所有资源请求都会拿到同样的 200 + 错误文本 ——
        /// 既不该把它写进本地文件，也不该反复发请求刷日志。
        /// 等清单重新拉取成功（说明服务端已放行）时自动清除。
        /// </summary>
        private static volatile bool _serverNotTrusted;

        /// <summary>
        /// 资源清单到手时触发一次（仅成功时）。
        /// Libraries 用它把"降级初始化"的图库数组按正确长度重建。
        /// </summary>
        internal static Action RemoteIndexReady;

        /// <summary>
        /// 某文件重试到上限仍失败时触发一次（参数 = 相对客户端根目录的正斜杠路径）。
        ///
        /// ★11-14：地图加载靠它「结束本次加载」——以前只有 30 秒冷却重试，没有终点，
        ///   玩家会一直卡在「地图还没下好」的状态里。现在次数用完就明确放弃，
        ///   由 MapReader 收尾（撤载入画面 + 提示），玩家至少能正常待在游戏里。
        /// 回调在下载线程上触发，实现方必须自己切回主线程再动界面/场景。
        /// </summary>
        internal static Action<string> FileGivenUp;

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

        // 当前正在下载的单个文件及其进度：供「主线程阻塞等待」时把进度显示到载入画面上。
        private static volatile string _activeFile = string.Empty;
        private static long _activeBytes;

        /// <summary>当前正在下载的文件（相对路径）；没有下载在进行时为空串。</summary>
        internal static string ActiveFile
        {
            get { return _activeFile; }
        }

        /// <summary>当前文件已收到的字节数。</summary>
        internal static long ActiveBytes
        {
            get { return Interlocked.Read(ref _activeBytes); }
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

                // 服务端已明确拒绝本机 IP：再发请求也只会拿到同样的错误文本，
                // 静默返回即可（日志已在首次拒绝时写过一次）。
                if (_serverNotTrusted) return false;

                string relative = ToRelativePath(localPath);
                if (relative == null) return false;   // 不在客户端目录内，无法映射到服务端资源

                // ★11-16：「Sound/.wav」「Sound/.mp3」这类只有扩展名的占位路径，是音效
                //   加载器按扩展名试探时留下的（目录里根本没有这个文件）。服务端不可能有，
                //   直接记永久失败，省掉一次注定 404 的 HTTP 往返 —— 以前每个都白跑一趟。
                int slash = relative.LastIndexOf('/');
                if (slash >= 0 && relative.Length > slash + 1 && relative[slash + 1] == '.')
                {
                    lock (_sync) _failedUntil[relative] = PermanentFail;
                    return false;
                }

                lock (_sync)
                {
                    if (_failedUntil.TryGetValue(relative, out int until))
                    {
                        if (until == PermanentFail) return false;
                        if (unchecked(Environment.TickCount - until) < 0) return false;   // 冷却期内，先不重试
                        _failedUntil.Remove(relative);   // 冷却已过，给一次重新入队的机会
                    }

                    if (_known.Contains(relative)) return false;   // 已经在队列里/下载中，等它自己完成
                }

                // 批量登记阶段：只登记"本地没有"，不产生任何下载请求
                if (BulkLoading) return false;

                // 服务端也没有这个文件时直接记永久失败，省掉一次注定 404 的 HTTP 往返。
                // 音效这类"按扩展名逐个试探"的加载点收益最大。
                if (!ExistsOnServer(relative))
                {
                    lock (_sync) _failedUntil[relative] = PermanentFail;
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
            return EnsureLocalFileBlocking(localPath, timeoutMs, null);
        }

        /// <summary>
        /// 同上，并在等待期间每隔 <see cref="WaitTickMs"/> 回调一次 onTick。
        ///
        /// 回调的意义：等待是在主线程上发生的，主循环此时完全停摆 ——
        /// 只有靠回调才能「一边等一边泵心跳 + 刷新画面」。
        /// 见 MapReader.WaitForMapFile（微端切图时服务端 10 秒收不到心跳就会踢人）。
        ///
        /// onTick 里抛异常会被吞掉：进度显示失败绝不能把地图加载搞挂。
        /// </summary>
        internal static bool EnsureLocalFileBlocking(string localPath, int timeoutMs, Action onTick)
        {
            try
            {
                if (File.Exists(localPath)) return true;
                if (!Settings.MicroClient) return false;

                bool queued;
                if (EnsureLocalFile(localPath, out queued)) return true;

                string relative = ToRelativePath(localPath);
                if (relative == null) return false;

                // 玩家正在等这个文件：把它提到紧急队列头，别让它排在预取任务后面
                PromoteToUrgent(relative);

                var handle = new ManualResetEventSlim(false);

                lock (_sync)
                {
                    // 永久失败 / 偶发失败冷却期内：等也没用
                    if (_failedUntil.TryGetValue(relative, out int until) &&
                        (until == PermanentFail || unchecked(Environment.TickCount - until) < 0))
                        return false;

                    // ★queued=false 有两种含义：永久失败（上面已挡掉），或者「文件已被别处
                    //   （后台预取/更早的调用）先入了队」。后者必须照样等待 —— 以前这里直接
                    //   return false，导致预取先入队的 .map 一毫秒都不等就报超时，进图一片黑。
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

                // 清单还没拿到时没法判断服务端有没有这个文件，等满整个超时几乎不会有结果，
                // 只会把调用线程（往往是启动阶段）卡住 —— 收缩到短等待，拿不到就按缺文件继续，
                // 后台下载线程拿到清单后仍会继续把它补上。
                // 例外：调用方带了 onTick（说明它愿意把等待显示成「载入画面」），
                // 这时不能砍时长 —— 清单没到时 ExistsOnServer 会乐观放行，文件照样能下，
                // 砍成 1.5 秒等于让学生刚坐下就收卷（首次登录时地图必然判缺失）。
                if (!IsIndexAvailable && timeoutMs > IndexUnavailableWaitMs && onTick == null)
                    timeoutMs = IndexUnavailableWaitMs;

                // 分段等待：每 WaitTickMs 醒一次跑一次 onTick（泵心跳 / 刷进度）。
                // 没有回调时行为与原来的一次性 Wait 完全一致。
                if (onTick == null)
                {
                    handle.Wait(timeoutMs);
                }
                else
                {
                    int waited = 0;
                    while (!handle.Wait(WaitTickMs))
                    {
                        waited += WaitTickMs;

                        try { onTick(); }
                        catch { }

                        if (waited >= timeoutMs) break;
                    }
                }

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
                    _failedUntil.Remove(relative);
                    _known.Remove(relative);
                    _attempts.Remove(relative);   // 连失败次数一起清，否则「重下一次」立刻又撞到上限
                }
            }
            catch
            {
            }
        }
        /// <summary>
        /// 只清「失败冷却 / 已入队」记录，**保留**失败次数。
        /// 用于「文件下坏了（0 字节/半截）」这类本地问题：清掉状态才能重新入队，
        /// 但次数要留着 —— 否则坏文件会被无限删了重下，永远到不了重试上限。
        /// </summary>
        internal static void ResetFileState(string localPath)
        {
            try
            {
                string relative = ToRelativePath(localPath);
                if (relative == null) return;

                lock (_sync)
                {
                    _failedUntil.Remove(relative);
                    _known.Remove(relative);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 某文件是否已被判定「本次运行不再下载」（404 服务端确实没有 / 重试次数用完）。
        /// 地图加载用它结束本次加载，不再无限等一张永远下不来的图。
        /// </summary>
        internal static bool IsGivenUp(string localPath)
        {
            try
            {
                string relative = ToRelativePath(localPath);
                if (relative == null) return false;

                lock (_sync)
                {
                    int until;
                    return _failedUntil.TryGetValue(relative, out until) && until == PermanentFail;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>某文件已经失败过几次（0 = 没失败过）。供日志/诊断使用。</summary>
        internal static int GetAttemptCount(string localPath)
        {
            try
            {
                string relative = ToRelativePath(localPath);
                if (relative == null) return 0;

                lock (_sync)
                {
                    int n;
                    return _attempts.TryGetValue(relative, out n) ? n : 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 依据已加载的资源清单判断服务端是否真的有这个文件（入参为相对客户端根目录的路径）。
        ///
        /// ★11-14：只查**已经拿到的**清单，绝不在调用线程上发起网络请求。
        ///   以前这里调 GetRemoteIndex()，清单还没到手时会在当前线程同步拉取（多台地址
        ///   逐个超时，最长 30 秒 × N 台）—— 而这正是切图路径，于是「切到没下过的图」
        ///   会卡几十秒不进游戏。现在拿不到清单就乐观放行，交给真实 HTTP 请求判断。
        /// </summary>
        private static bool ExistsOnServer(string relative)
        {
            try
            {
                if (!Settings.MicroClient) return true;

                Dictionary<string, HashSet<string>> index;
                lock (_sync) index = _remoteIndex;
                if (index == null) return true;      // 清单还没到手，不做预判

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
                if (_failedUntil.ContainsKey(relative) || _known.Contains(relative)) return false;

                _known.Add(relative);
                _queue.Enqueue(relative);
                _pending++;

                EnsureWorkersLocked();
                Monitor.Pulse(_sync);
                return true;
            }
        }

        /// <summary>
        /// 把已在普通队列里的文件提到紧急队列头。
        /// 用于"玩家正在等"的场景（切图 .map 等）：它可能早被后台预取排进了队尾，
        /// 不提前的话就要排在所有预取任务后面慢慢轮 —— 带宽越紧等得越久。
        /// 文件若已出队（正在下载中）或不在队列里，则无事可做。
        /// </summary>
        private static void PromoteToUrgent(string relative)
        {
            lock (_sync)
            {
                if (_urgentQueue.Contains(relative)) return;
                if (!_queue.Contains(relative)) return;

                // Queue 不支持随机删除：重建一次普通队列
                var keep = new List<string>(_queue.Count);
                foreach (var item in _queue)
                    if (!string.Equals(item, relative, StringComparison.OrdinalIgnoreCase))
                        keep.Add(item);

                _queue.Clear();
                foreach (var item in keep) _queue.Enqueue(item);
                _urgentQueue.Enqueue(relative);
                Monitor.PulseAll(_sync);
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

        /// <summary>单次下载结果：Success=成功；NotFound=服务端确定没有（404/路径非法），永久拉黑；Transient=偶发失败（超时/断连/5xx/写盘冲突），冷却后允许重试。</summary>
        private enum DownloadResult { Success, NotFound, Transient }

        private static void WorkerLoop()
        {
            while (true)
            {
                string relative;
                string givenUp = null;   // 本次下载若达到重试上限，记下文件路径，锁外通知

                lock (_sync)
                {
                    while (_queue.Count == 0 && _urgentQueue.Count == 0)
                        Monitor.Wait(_sync);

                    // 紧急资源（切图等）优先；普通队列维持先来后到
                    relative = _urgentQueue.Count > 0 ? _urgentQueue.Dequeue() : _queue.Dequeue();
                }

                DownloadResult result;
                try
                {
                    result = DownloadFile(relative);
                }
                catch (Exception ex)
                {
                    Log("下载线程异常 " + relative + " : " + ex.Message);
                    result = DownloadResult.Transient;
                }

                lock (_sync)
                {
                    _pending--;
                    if (result == DownloadResult.Success)
                    {
                        _completed++;
                        _lastFile = relative;
                        _attempts.Remove(relative);
                    }
                    else
                    {
                        _failedCount++;

                        if (result == DownloadResult.NotFound)
                        {
                            // 服务端确定没有：本次运行不再重试
                            _failedUntil[relative] = PermanentFail;
                            _attempts.Remove(relative);
                        }
                        else
                        {
                            // ★11-14：记录失败次数，到上限就永久放弃（并通知上层「本次加载结束」）。
                            int tries;
                            if (!_attempts.TryGetValue(relative, out tries)) tries = 0;
                            tries++;
                            _attempts[relative] = tries;

                            int maxRetry = Settings.MicroMaxRetry;

                            if (maxRetry > 0 && tries >= maxRetry)
                            {
                                _failedUntil[relative] = PermanentFail;
                                _known.Remove(relative);
                                givenUp = relative;
                            }
                            else
                            {
                                // ★偶发失败（网络抖动/服务器重启/读取超时等）：
                                // 只冷却 30 秒，之后允许重新入队。以前这里是永久拉黑 ——
                                // 一次网络抖动就把正在补的资源集体判死刑，表现就是
                                // 「微端玩着玩着突然不加载了，只能重启客户端」。
                                // 服务端拒绝本机 IP 时也走这里（不算「这个文件不存在」），
                                // _serverNotTrusted 会统一挡住请求，IP 放行后自动恢复。
                                _failedUntil[relative] = unchecked(Environment.TickCount + TransientRetryMs);
                                _known.Remove(relative);   // 必须移出「已入队」集合，冷却后才能重新入队
                            }
                        }
                    }

                    if (_waiters.TryGetValue(relative, out var list))
                    {
                        foreach (var handle in list)
                        {
                            try { handle.Set(); } catch { }
                        }
                    }
                }

                // 到上限放弃：在锁外通知（回调可能回头调 EnsureLocalFile 取同一把锁）
                if (givenUp != null)
                {
                    Log("已重试 " + Settings.MicroMaxRetry + " 次仍未成功，本次放弃 " + givenUp);
                    var cb = FileGivenUp;
                    if (cb != null)
                    {
                        try { cb(givenUp); } catch { }
                    }
                }
            }
        }

        //★（11-13）微端接口「连接不通」时的纯 HTTP 目录兜底。
        //  确认不通后在冷却期内直接走目录，避免每个文件都先白等一次连接超时
        //  （几千个文件各等 15 秒，足以把启动拖到几十分钟）。
        //★11-16 改 volatile：清单一拉不到就把「接口不可用」写到这个字段，
        //  但拉清单的是启动线程/WarmUp 线程，读它的是下载线程 —— 不加 volatile 时
        //  下载线程可能长时间读到旧值（0），于是每个文件都还要先去撞一次死主机。
        private static volatile int _apiDownUntil;
        //★11-16 5 分钟 → 30 分钟：接口主机如果是「黑洞」（TCP 能连上但永不回包，例如挂掉的 frp 隧道），
        //  每次冷却到期都会再白等一次超时。5 分钟一轮意味着玩着玩着每 5 分钟卡 8 秒；
        //  而接口真恢复时玩家多半会重启客户端，30 分钟足够覆盖一局。代价只是恢复得慢一点。
        private const int ApiDownCooldownMs = 30 * 60 * 1000;

        /// <summary>
        /// 下载单个文件。优先微端接口（/resource?f=），**只有在接口连不上**时才改走
        /// 纯 HTTP 目录（DirHost/相对路径）。接口回 404 表示确实没这个文件，不兜底。
        /// </summary>
        private static DownloadResult DownloadFile(string relative)
        {
            bool pureStatic = Settings.MicroFileAtRoot && string.IsNullOrEmpty(Settings.MicroDirHost);
            string dirUrl = BuildDirUrl(relative);
            string apiUrl = pureStatic ? null : BuildUrl("resource", "f", relative);

            if (apiUrl == null) return TryDownload(relative, dirUrl, out _);   // 纯静态模式：只走目录
            if (dirUrl == null) return TryDownload(relative, apiUrl, out _);   // 未配目录兜底：只走接口

            // 刚确认过接口连不上：先走目录，别再白等超时
            if (unchecked(Environment.TickCount - _apiDownUntil) < 0)
            {
                var rd = TryDownload(relative, dirUrl, out bool dirDown);
                if (!dirDown) return rd;
                // 目录也不通：继续试接口（可能已经恢复）
            }

            var r = TryDownload(relative, apiUrl, out bool unreachable);
            if (r == DownloadResult.Success || !unreachable) return r;

            Log("微端服务器连接不通，改用 HTTP 目录直取: " + relative);
            _apiDownUntil = Environment.TickCount + ApiDownCooldownMs;
            return TryDownload(relative, dirUrl, out _);
        }

        /// <summary>纯 HTTP 目录地址（网站根 = 资源根）。未配置 DirHost 且非纯静态模式时返回 null。</summary>
        private static string BuildDirUrl(string relative)
        {
            string dirBase = (Settings.MicroDirHost ?? "").Trim().TrimEnd('/');
            if (dirBase.Length == 0 && Settings.MicroFileAtRoot) dirBase = CurrentHost.TrimEnd('/');
            if (dirBase.Length == 0) return null;
            return dirBase + "/" + Uri.EscapeDataString(relative).Replace("%2F", "/");
        }

        /// <summary>是否地图文件（相对路径以 .map 结尾）。地图走 2 秒短超时，其它资源走 30 秒。</summary>
        private static bool IsMapFile(string relative)
        {
            return !string.IsNullOrEmpty(relative) &&
                   relative.EndsWith(".map", StringComparison.OrdinalIgnoreCase);
        }

        private static DownloadResult TryDownload(string relative, string url, out bool unreachable)
        {
            unreachable = false;

            //★11-16：以前这里把非地图文件的响应头超时一律抬到 30 秒，
            //  配置里的 Timeout=8000 等于完全失效。结果就是：接口主机不通时，
            //  **每一个**资源都要先白等 30 秒才轮到目录兜底 —— 玩家侧看到的就是
            //  「读哪个都加载不出来，半天不动」。等响应头不是传输时长，5 秒足够判定可达性；
            //  大文件真正耗的是后面的数据传输，由静默超时（下面的 ReadAsync）管。
            int headerTimeoutMs = Settings.MicroTimeout;
            if (headerTimeoutMs < 5000) headerTimeoutMs = 5000;

            //★11-14 地图文件用「短连接超时」：2 秒内拿不到响应头就当这次连不上，
            //  立刻把机会让给玩家进游戏（后台继续重试）。
            //  其它资源必须保留 30 秒上限 —— 588MB 的地砖图库在慢速链路上，
            //  任何短超时都会把正常下载误判成失败。
            if (IsMapFile(relative))
            {
                int mapMs = Settings.MicroMapTimeout;
                if (mapMs < 500) mapMs = 2000;
                if (mapMs < headerTimeoutMs) headerTimeoutMs = mapMs;
            }

            // 进度：让阻塞等待方（切图载入画面）能显示「已收到多少」
            _activeFile = relative;
            Interlocked.Exchange(ref _activeBytes, 0);

            // ---- 断点续传（10-23）：本地有半截 .tmp 就从它的长度继续要 ----
            string localPath = ToLocalPath(relative);
            if (localPath == null) return DownloadResult.NotFound;

            string temp = localPath + ".tmp";
            long resumeFrom = 0;
            try
            {
                if (File.Exists(temp)) resumeFrom = new FileInfo(temp).Length;
            }
            catch
            {
                resumeFrom = 0;
            }

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
                        var req = new HttpRequestMessage(HttpMethod.Get, url);
                        if (resumeFrom > 0)
                            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);

                        // ResponseHeadersRead：拿到响应头就开始写盘，大图库也不会整块堆在内存里
                        response = client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                                         .GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        Log("连接超时（" + (headerTimeoutMs / 1000) + " 秒未收到响应头）: " + relative);
                        unreachable = true;
                        NextHost();   // 连不上当前服务器：地址池换下一个
                        return DownloadResult.Transient;
                    }

                    using (response)
                    {
                        // 响应头已经拿到了，撤掉总超时：大文件的传输时长不该被它限制
                        cts.CancelAfter(System.Threading.Timeout.Infinite);

                        if (!response.IsSuccessStatusCode)
                        {
                            // 416 = 续传起点越界：多半是上次 .tmp 已经下完整
                            //（写完还没来得及改名就断了）。直接把半截文件转正。
                            if ((int)response.StatusCode == 416 && resumeFrom > 0)
                                return FinalizeTemp(localPath, temp, relative);

                            int code = (int)response.StatusCode;
                            Log("资源不存在或被拒绝 " + relative + " -> " + code);

                            //★502/503/504 = 网关/上游不可用，等价于「微端服务器连不上」，
                            //  可用于触发 HTTP 目录兜底（玩家开着系统代理/加速器时，
                            //  服务器明明挂了却回 502，光看异常是识别不出来的）
                            if (code == 502 || code == 503 || code == 504) unreachable = true;

                            // 404 = 服务端确实没有这个文件，永久拉黑没有争议；
                            // 5xx/其它状态多半是服务端正在重启或临时故障，按偶发失败处理
                            return code == 404
                                ? DownloadResult.NotFound
                                : DownloadResult.Transient;
                        }

                        long offset = resumeFrom;

                        // 服务端不支持 Range（回的是 200 全量）：丢弃旧 tmp 重头下
                        if ((int)response.StatusCode != 206 && resumeFrom > 0)
                            offset = 0;

                        // 续传起点超出服务端文件大小（服务端换过文件）：扔掉 tmp，冷却后重头下
                        var contentRange = response.Content.Headers.ContentRange;
                        if (offset > 0 && contentRange != null && contentRange.Length.HasValue &&
                            offset > contentRange.Length.Value)
                        {
                            Log("续传起点越界（服务端文件已变化），重头下载 " + relative);
                            try { File.Delete(temp); } catch { }
                            return DownloadResult.Transient;
                        }

                        string dir = Path.GetDirectoryName(localPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        // 续传用 Append 追加；否则建新文件（先写临时文件再改名，避免半截文件被后续加载当成有效资源）
                        long startOffset = offset;
                        long received = 0;
                        bool notTrusted = false;

                        using (var source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        using (var target = new FileStream(temp, offset > 0 ? FileMode.Append : FileMode.Create,
                                                           FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[64 * 1024];
                            int limit = Settings.MicroRateLimit;      // KB/s，0 = 不限速
                            int stallMs = headerTimeoutMs;            // 多久收不到数据算卡死
                            var watch = Stopwatch.StartNew();

                            int read;
                            bool firstChunk = true;
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
                                    unreachable = true;
                                    NextHost();   // 传输中途断流：多半也是当前服务器网络问题，换下一个
                                    return DownloadResult.Transient;
                                }

                                if (read <= 0) break;

                                if (firstChunk)
                                {
                                    firstChunk = false;

                                    // 服务端拒绝来源 IP 时也是 HTTP 200，但内容只有 "notrusted:<ip>" 二十来字节。
                                    // 这种响应绝不能落地 —— 写进 .Lib/.wav 等于把资源文件污染成垃圾。
                                    if (IsNotTrustedBuffer(buffer, read))
                                    {
                                        notTrusted = true;
                                        break;
                                    }
                                }

                                target.Write(buffer, 0, read);
                                received += read;
                                Interlocked.Exchange(ref _activeBytes, startOffset + received);
                                Throttle(watch, received, limit);
                            }

                            target.Flush();
                        }

                        if (notTrusted)
                        {
                            if (startOffset == 0)
                            {
                                try { File.Delete(temp); } catch { }
                            }
                            // 续传场景下 tmp 里的旧数据是干净的，只截掉本次追加的垃圾不现实 ——
                            // 但 notrusted 只回 200 全量（startOffset=0），理论到不了这里；保险起见只记状态。

                            // 只在第一次拒绝时写日志：后面每个文件都会撞同一堵墙，不该刷屏
                            if (!_serverNotTrusted)
                                Log("微端资源服务拒绝了本机 IP：服务端「网络」设置里未允许任意 IP 访问微端资源，"
                                    + "已暂停资源下载（本机 IP 放行后会自动恢复，也可重启客户端）");
                            _serverNotTrusted = true;
                            return DownloadResult.Transient;
                        }

                        if (received == 0)
                        {
                            if (startOffset == 0)
                            {
                                try { File.Delete(temp); } catch { }
                                Log("资源内容为空 " + relative);
                                return DownloadResult.Transient;
                            }

                            // 续传请求但服务端没发任何字节：tmp 已是完整文件（长度正好等于起点），转正
                            return FinalizeTemp(localPath, temp, relative);
                        }

                        if (File.Exists(localPath)) File.Delete(localPath);
                        File.Move(temp, localPath);

                        lock (_sync)
                        {
                            _bytes += received;
                        }

                        Log((startOffset > 0 ? "已续传 " : "已下载 ") + relative + " (" + received + " 字节"
                            + (startOffset > 0 ? "，从第 " + startOffset + " 字节继续" : "") + ")");
                        return DownloadResult.Success;
                    }
                }
            }
            catch (Exception ex)
            {
                // 断连/写盘冲突（比如旧文件还被图库的读句柄占着）等都算偶发失败，
                // 冷却后重试 —— 写盘冲突尤其常见于「重新下载已存在的文件」。
                // 续传时 .tmp 还在，下次自动从断点继续。
                Log("下载失败 " + relative + " : " + ex.Message);

                // 连接层异常（DNS/拒绝连接/断连）才算「微端服务器不通」，可用于触发目录兜底；
                // 写盘冲突这类本地错误不能误判成服务器挂了。
                unreachable = IsConnectionFailure(ex);

                // 连接层异常时地址池换下一个服务器。
                // 写盘冲突这类本地异常换地址也无妨：下次成功下载继续用新地址。
                NextHost();
                return DownloadResult.Transient;
            }
            finally
            {
                // 无论成功失败都清掉进度，避免载入画面一直显示一个早就结束的下载
                _activeFile = string.Empty;
                Interlocked.Exchange(ref _activeBytes, 0);
            }
        }

        /// <summary>是否为「连不上服务器」类异常（DNS/拒绝连接/连接中断/超时）。</summary>
        private static bool IsConnectionFailure(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Net.Http.HttpRequestException) return true;
                if (e is System.Net.Sockets.SocketException) return true;
                if (e is System.IO.IOException) return true;
                if (e is TimeoutException) return true;
                if (e is TaskCanceledException) return true;
                if (e is OperationCanceledException) return true;
            }
            return false;
        }

        /// <summary>把下载完的 .tmp 半截文件转正（断点续传收尾）。</summary>
        private static DownloadResult FinalizeTemp(string localPath, string temp, string relative)
        {
            try
            {
                if (!File.Exists(temp))
                    return DownloadResult.Transient;

                if (File.Exists(localPath)) File.Delete(localPath);
                File.Move(temp, localPath);

                Log("已续传收尾 " + relative + "（本地半截文件已是完整内容，直接转正）");
                return DownloadResult.Success;
            }
            catch (Exception ex)
            {
                Log("续传收尾失败 " + relative + " : " + ex.Message);
                return DownloadResult.Transient;
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
        /// 清单缓存文件：随客户端分发的「已知资源列表」。
        /// 放在 Data 下，玩家/运维可以直接覆盖（"缺清单先塞一个进去"）。
        /// </summary>
        private static string IndexCachePath
        {
            get { return Path.Combine(Settings.P_Client, "Data", "MicroIndex.cache"); }
        }

        private static bool _indexCacheTried;
        private static bool _indexRefreshStarted;
        private static int _indexFails;
        private const int IndexRetryMaxMs = 5 * 60 * 1000;
        private const int IndexCacheMaxDays = 30;

        /// <summary>
        /// 拉取服务端资源清单（每目录下的文件名集合），用于推算图库数组长度。
        ///
        /// ★11-16 三级顺序：
        ///   ① 本地缓存 Data\MicroIndex.cache —— 启动即可用，不依赖任何服务器在线。
        ///      清单只在增删资源时才变，而清单源（frp 隧道 / 本机 nginx）却经常不可用；
        ///      没有清单时图库数组按「本地已有文件数」初始化，高编号图库压根不存在，
        ///      资源就算下下来了也没有代码去用它 —— 这正是「素材半天加载不出来」的根因。
        ///   ② 缓存用上之后，5 秒再后台刷一次网络（服务端加了资源也不至于永久用旧清单）。
        ///   ③ 网络来源顺序：IndexHost → Host → DirHost，每台先 /resindex 再 /resindex.txt。
        ///
        /// 拉取失败按次数退避（30 秒起、最长 5 分钟），绝不放弃：客户端比服务端先启动很常见。
        /// </summary>
        private static Dictionary<string, HashSet<string>> GetRemoteIndex()
        {
            if (_remoteIndex != null) return _remoteIndex;

            // ① 本地缓存优先
            if (TryLoadIndexCache())
            {
                ScheduleIndexRefresh();
                return _remoteIndex;
            }

            lock (_sync)
            {
                if (_remoteIndex != null) return _remoteIndex;

                if (_indexLoading)
                {
                    // 已有线程在拉：本线程最多等一小段（总预算见 _indexWaitBudget）。
                    // 以前这里是死等 10 秒，而 Libraries 初始化要调用本方法三十多次，
                    // 服务器连不上时启动就会卡在几十秒的黑屏上。
                    int budget = _indexWaitBudget;

                    if (budget <= 0) return null;

                    int step = Math.Min(budget, IndexWaitStepMs);

                    Monitor.Wait(_sync, step);
                    _indexWaitBudget = Math.Max(0, budget - step);

                    return _remoteIndex;
                }

                if (unchecked(Environment.TickCount - _indexNextTry) < 0)
                    return null;      // 冷却中：不重试也不报错，交给 WarmUp 稍后再来

                _indexLoading = true;
            }

            Dictionary<string, HashSet<string>> index = FetchIndexFromNetwork();

            lock (_sync)
            {
                if (index != null)
                {
                    _remoteIndex = index;
                    _indexFails = 0;
                }
                else
                {
                    //★11-16 指数退避：以前固定 30 秒重试一次，死主人的情形下每 30 秒撞一轮
                    //  （每轮 2 个地址各挂 10~12 秒），后台下载线程全被清单拉取占着，
                    //  真正的资源反而排不上队。现在 30s → 60 → 120 → 240 → 300 封顶。
                    _indexFails++;
                    int wait = IndexRetryMs << Math.Min(_indexFails - 1, 4);
                    if (wait > IndexRetryMaxMs || wait <= 0) wait = IndexRetryMaxMs;
                    _indexNextTry = Environment.TickCount + wait;
                }

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

        /// <summary>
        /// 从网络拉清单（候选地址见 GetRemoteIndex 注释）。成功会落盘缓存；
        /// 连接层失败（超时/拒绝/断连）会把「资源接口」标记为不可用 5 分钟，
        /// 让按需下载直接走 DirHost —— 否则每个文件都要先在死主机上白等一次超时。
        /// </summary>
        private static Dictionary<string, HashSet<string>> FetchIndexFromNetwork()
        {
            string rawText = null;

            try
            {
                var indexUrls = new List<string>();

                foreach (string baseHost in new[] { Settings.MicroIndexHost, CurrentHost, Settings.MicroDirHost })
                {
                    if (string.IsNullOrWhiteSpace(baseHost)) continue;

                    string b = baseHost.Trim().TrimEnd('/');

                    foreach (string u in new[] { b + "/resindex", b + "/resindex.txt" })
                        if (!indexUrls.Contains(u)) indexUrls.Add(u);
                }

                // 复用共享实例：连接走连接池，不再每个文件都新建一条 TCP 连接
                var client = SharedClient();

                // 清单只有几十 KB，给它一个固定上限。以前抬到 30 秒，主机不通时
                // 等于每个候选地址都要干等 30 秒 ×4 个地址 = 两分钟一轮。
                int indexTimeoutMs = Settings.MicroTimeout;
                if (indexTimeoutMs < 10000) indexTimeoutMs = 10000;

                foreach (string url in indexUrls)
                {
                    // ★每台单独 try：某一台连不上（超时/拒绝）只跳过它，不能整轮放弃 ——
                    //   否则「本机清单源挂了、远端有 resindex.txt」时永远拿不到清单
                    try
                    {
                        using (var indexCts = new CancellationTokenSource(indexTimeoutMs))
                        using (var response = client.GetAsync(url, indexCts.Token).GetAwaiter().GetResult())
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                Log("资源清单获取失败 -> " + (int)response.StatusCode + "  " + url);
                                continue;
                            }

                            string text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                            // 服务端「受信 IP」校验不通过时返回的是 HTTP 200 + 单行文本
                            // "notrusted:<本机IP>"，而不是清单。必须当成获取失败：
                            // 否则它会被解析成一份「只有 1 个文件」的清单并被缓存下来，
                            // ExistsOnServer 判断一切文件都是「服务端没有」，
                            // 微端一个资源都下不到，而且日志里只显示"清单加载完成，共 1 个文件"。
                            if (IsNotTrustedText(text))
                            {
                                Log("微端资源服务拒绝了本机 IP（" + text.Trim() + "）：服务端「网络」设置里未允许任意 IP 访问微端资源，"
                                    + "把本机 IP 填进“受信 IP”或勾选“允许任意 IP 访问微端资源”后会自动恢复");
                                continue;
                            }

                            var parsed = ParseIndex(text);

                            // 空清单不能当成有效结果缓存：服务端「微端资源服务」未开启或资源目录
                            // 未配置时同样返回 200 + 空内容。若把空清单缓存下来，
                            // ExistsOnServer 会对所有文件返回 false（包括音效索引表 SoundList.lst），
                            // 索引表从此再也同步不下来 —— 整局音效全部落到默认命名上，声音全错。
                            if (CountEntries(parsed) == 0)
                            {
                                Log("资源清单为空（服务端未开启微端资源服务或资源目录为空），稍后重试");
                                continue;
                            }

                            // 清单能正常拿到，说明之前“服务端拒绝本机 IP”的状态已经解除，
                            // 允许资源下载重新开始（否则要重启客户端才能恢复声音/图库）
                            if (_serverNotTrusted)
                            {
                                _serverNotTrusted = false;
                                Log("微端资源服务已放行本机 IP，恢复资源下载");
                            }

                            Log("资源清单加载完成，共 " + CountEntries(parsed) + " 个文件   " + url);

                            //★11-16 接口主机真的回了包 → 它已经恢复，立刻解除「接口不可用」标记。
                            //  否则黑名单要挂满 30 分钟，微端服务器修好了也要等半天才恢复走接口。
                            //  只认 CurrentHost（接口主机）的命中；从 IndexHost / DirHost 拿到清单不算。
                            if (url.StartsWith(CurrentHost.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                                _apiDownUntil = 0;

                            rawText = text;
                            return parsed;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("资源清单获取异常 : " + ex.Message + "   " + url);

                        //★11-16：连不上＝这台的资源接口基本也没戏。标记 5 分钟不可用，
                        //  让按需下载从"先撞 10 秒"变成"直接走目录"。
                        if (ex is OperationCanceledException || IsConnectionFailure(ex))
                            _apiDownUntil = Environment.TickCount + ApiDownCooldownMs;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("资源清单获取异常 : " + ex.Message);
                NextHost();   // 连不上当前服务器：地址池换下一个，冷却后对新地址重试
            }
            finally
            {
                if (rawText != null) SaveIndexCache(rawText);
            }

            return null;
        }

        /// <summary>用上缓存后，隔 5 秒在后台刷一次网络清单；失败就继续用缓存，什么都不影响。</summary>
        private static void ScheduleIndexRefresh()
        {
            if (_indexRefreshStarted) return;
            _indexRefreshStarted = true;

            var thread = new Thread(delegate ()
            {
                try
                {
                    Thread.Sleep(5000);

                    var fresh = FetchIndexFromNetwork();
                    if (fresh == null) return;

                    int before, after;

                    lock (_sync)
                    {
                        before = CountEntries(_remoteIndex);
                        after = CountEntries(fresh);
                        _remoteIndex = fresh;
                        _indexFails = 0;
                    }

                    // 数量变了说明服务端加减过资源：图库数组要按新数量重建
                    if (after != before && RemoteIndexReady != null)
                    {
                        Log("清单已刷新（" + before + " → " + after + " 个文件），重建图库数组");
                        try { RemoteIndexReady(); } catch { }
                    }
                }
                catch
                {
                }
            })
            { IsBackground = true, Name = "MicroIndexRefresh" };

            thread.Start();
        }

        /// <summary>读本地清单缓存。成功返回 true（_remoteIndex 已就绪，不必等网络）。</summary>
        private static bool TryLoadIndexCache()
        {
            lock (_sync)
            {
                if (_remoteIndex != null) return true;
                if (_indexCacheTried) return false;
                _indexCacheTried = true;
            }

            try
            {
                string path = IndexCachePath;

                if (!File.Exists(path)) return false;

                var info = new FileInfo(path);

                if ((DateTime.Now - info.LastWriteTime).TotalDays > IndexCacheMaxDays)
                {
                    Log("清单缓存超过 " + IndexCacheMaxDays + " 天，忽略：" + path);
                    return false;
                }

                var index = ParseIndex(File.ReadAllText(path));

                if (CountEntries(index) == 0) return false;

                lock (_sync) _remoteIndex = index;

                Log("使用本地清单缓存，共 " + CountEntries(index) + " 个文件   " + path);
                return true;
            }
            catch (Exception ex)
            {
                Log("读取清单缓存失败 : " + ex.Message);
                return false;
            }
        }

        /// <summary>把清单原文落盘（无 BOM：带 BOM 时第一行会被解析成 "\uFEFFData/..."，那个文件永远 404）。</summary>
        private static void SaveIndexCache(string text)
        {
            try
            {
                string path = IndexCachePath;
                string dir = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log("写入清单缓存失败 : " + ex.Message);
            }
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

                // 相对路径里不会出现冒号。服务端拒绝来源 IP 时回的是单行文本
                // "notrusted:192.168.2.53"，若不挡掉就会被解析成「服务端只有这一个文件」，
                // 于是所有资源都判定为服务端没有 —— 微端整局静默失效。
                if (line.IndexOf(':') >= 0) continue;

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

        /// <summary>响应文本是否为服务端的「来源 IP 不受信」提示（notrusted:&lt;ip&gt;）。</summary>
        private static bool IsNotTrustedText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            string head = text.TrimStart();
            return head.StartsWith("notrusted", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>响应内容是否以「来源 IP 不受信」提示开头（用于流式下载的头部字节判断）。</summary>
        private static bool IsNotTrustedBuffer(byte[] buffer, int count)
        {
            const string tag = "notrusted";
            if (count < tag.Length) return false;

            for (int i = 0; i < tag.Length; i++)
            {
                char c = (char)buffer[i];
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                if (c != tag[i]) return false;
            }

            return true;
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
            bool isIndex = path == "resindex";

            //★（11-13）控制面/数据面分离：清单可以单独指向主服，文件全部走 Host（远端）
            //  主服带宽小或外网链路慢时，302 中转的每一个文件都要多一次主服往返，
            //  几千个文件累计起来很可观；把文件直连远端后主服只出 76KB 清单。
            string host = isIndex && !string.IsNullOrWhiteSpace(Settings.MicroIndexHost)
                ? Settings.MicroIndexHost
                : CurrentHost;
            if (host.Length == 0) host = "http://127.0.0.1:5679";
            host = host.TrimEnd('/');

            if (isIndex) return host + "/resindex";

            //★（11-13）FileAtRoot：文件直接落在资源服务器根目录
            //  远端只要「网站根 = 资源根」即可，不需要 rewrite/脚本/接口
            if (Settings.MicroFileAtRoot && key != null && value != null)
            {
                string rootPath = Uri.EscapeDataString(value).Replace("%2F", "/");
                return host + "/" + rootPath;
            }

            string url = host + "/" + path;
            if (key != null && value != null)
            {
                // 保留 / 分隔符便于服务端识别目录层级，同时保证中文/空格安全
                string encoded = Uri.EscapeDataString(value).Replace("%2F", "/");
                url += "?" + key + "=" + encoded;
            }

            return url;
        }

        #region 资源服务器地址池

        //★地址池（10-25）：Host 可填多个资源服务器地址，连接失败自动换下一个。
        //换过去就一直用（不成功不回跳主地址，避免主地址抖动时每个文件都先撞一次超时）；
        //当前地址也失败就继续轮换，循环使用。池只有一个地址时行为与原来完全一致。
        private static int _hostIndex;

        /// <summary>当前使用的资源服务器地址（含 scheme，无结尾斜杠）。</summary>
        private static string CurrentHost
        {
            get
            {
                var pool = Settings.MicroHostPool;
                if (pool == null || pool.Count == 0) return "http://127.0.0.1:5679";
                int i = _hostIndex;
                if (i < 0 || i >= pool.Count) i = 0;
                return pool[i];
            }
        }

        /// <summary>当前地址连接失败：换下一个（循环）。池只有一个地址时无事可做。</summary>
        private static void NextHost()
        {
            var pool = Settings.MicroHostPool;
            if (pool == null || pool.Count < 2) return;

            _hostIndex = (_hostIndex + 1) % pool.Count;
            Log("微端资源服务器连接失败，切换到下一个地址: " + pool[_hostIndex]);

            // 「服务端拒绝本机 IP」是按服务器算的：换地址后给新服务器一次机会，
            // 否则旧服务器的拒绝状态会把所有请求挡死，备用地址形同虚设。
            if (_serverNotTrusted)
            {
                _serverNotTrusted = false;
                Log("已切换资源服务器，重置本机 IP 受信状态");
            }
        }

        #endregion

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
