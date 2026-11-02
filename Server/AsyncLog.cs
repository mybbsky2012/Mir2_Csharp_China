using System.Collections.Concurrent;
using System.Threading;

namespace Server
{
    /// <summary>
    /// 日志异步落盘（11-02）。
    ///
    /// 背景：原来 <see cref="MessageQueue"/> 里的每条消息/异常都在**游戏工作线程**上
    /// 直接调 log4net 写文件。RollingFileAppender 配的是 MinimalLock，每写一条都要
    /// 打开→写→关闭→flush 一次日志文件；一旦某个逻辑开始连续报错（坏对象、脏数据、
    /// 召唤物异常……），主循环就有相当一部分时间耗在磁盘 IO 和异常栈格式化上，
    /// 表现为「服务器在刷错误 → 客户端进图/走动明显变慢」。
    ///
    /// 这里把落盘搬到一条独立的后台线程：游戏线程只做一次入队（纳秒级），
    /// 队列有上限，溢出只累加丢弃计数（绝不反过来拖慢游戏线程）。
    /// </summary>
    public static class AsyncLog
    {
        private sealed class Entry
        {
            public LogType Type;
            public bool IsError;
            public string Message;
            public System.Exception Ex;
        }

        private const int MaxQueueLength = 5000;

        private static readonly ConcurrentQueue<Entry> _queue = new ConcurrentQueue<Entry>();
        private static readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private static readonly object _startLock = new object();

        private static Thread _worker;
        private static volatile bool _running;
        private static long _dropped;

        /// <summary>队列溢出被丢弃的条数（只计数，不再为丢弃本身写日志，避免二次刷屏）。</summary>
        public static long Dropped
        {
            get { return Interlocked.Read(ref _dropped); }
        }

        private static void EnsureStarted()
        {
            if (_running) return;

            lock (_startLock)
            {
                if (_running) return;

                _running = true;

                _worker = new Thread(Loop)
                {
                    IsBackground = true,
                    Name = "AsyncLog",
                    // 日志永远不该跟游戏线程抢 CPU
                    Priority = ThreadPriority.BelowNormal
                };
                _worker.Start();
            }
        }

        private static void Loop()
        {
            while (true)
            {
                try
                {
                    Entry entry;
                    while (_queue.TryDequeue(out entry))
                    {
                        WriteToFile(entry);
                    }

                    _signal.WaitOne(200);
                }
                catch
                {
                    // 日志线程自身绝不允许抛出去把进程带崩
                    try { Thread.Sleep(50); } catch { }
                }
            }
        }

        private static void WriteToFile(Entry entry)
        {
            var logger = Logger.GetLogger(entry.Type);

            if (entry.IsError)
            {
                if (entry.Ex != null) logger.Error(entry.Message, entry.Ex);
                else logger.Error(entry.Message);
                return;
            }

            if (entry.Type == LogType.Debug) logger.Debug(entry.Message);
            else logger.Info(entry.Message);
        }

        private static void Post(LogType type, bool isError, string message, System.Exception ex)
        {
            EnsureStarted();

            if (_queue.Count >= MaxQueueLength)
            {
                Interlocked.Increment(ref _dropped);
                return;
            }

            _queue.Enqueue(new Entry
            {
                Type = type,
                IsError = isError,
                Message = message,
                Ex = ex
            });

            if (_queue.Count <= 8) _signal.Set();
        }

        public static void Info(LogType type, string message)
        {
            Post(type, false, message, null);
        }

        public static void Debug(LogType type, string message)
        {
            Post(type, false, message, null);
        }

        public static void Error(LogType type, string message, System.Exception ex = null)
        {
            Post(type, true, message, ex);
        }

        /// <summary>关机前尽量把队列写光（最多等 timeoutMs，超时就算了，绝不卡住停服）。</summary>
        public static void Flush(int timeoutMs = 2000)
        {
            if (!_running) return;

            var deadline = System.Environment.TickCount + timeoutMs;

            while (!_queue.IsEmpty && unchecked(System.Environment.TickCount - deadline) < 0)
            {
                try { Thread.Sleep(20); } catch { }
            }
        }
    }
}
