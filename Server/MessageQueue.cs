using System.Collections.Concurrent;

namespace Server
{
    public class MessageQueue
    {
        private static readonly MessageQueue instance = new MessageQueue();

        public static MessageQueue Instance
        {
            get { return instance; }
        }

        public readonly ConcurrentQueue<string> MessageLog = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> DebugLog = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> ChatLog = new ConcurrentQueue<string>();

        public MessageQueue() { }

        // ── 11-02：落盘一律走 AsyncLog（后台线程写文件） ──────────────────────
        // 原来这里同步调 log4net，等于把「打开→写→关闭日志文件」算进游戏主循环。
        // 服务器一但开始连续报错，进图/走路的卡顿就是这么来的。
        // 现在游戏线程只入队，UI 上看到的消息列表（MessageLog 等）行为完全不变。
        public void Enqueue(string msg)
        {
            if (MessageLog.Count < 100)
                MessageLog.Enqueue(String.Format("[{0}]: {1}" + Environment.NewLine, DateTime.Now, msg));

            AsyncLog.Info(LogType.Server, msg);
        }

        public void Enqueue(Exception ex)
        {
            if (MessageLog.Count < 100)
                MessageLog.Enqueue(String.Format("[{0}]: {1} - {2}" + Environment.NewLine, DateTime.Now, ex.TargetSite, ex));

            AsyncLog.Error(LogType.Server, ex.TargetSite + " - " + ex.Message, ex);
        }

        public void EnqueueDebugging(string msg)
        {
            if (DebugLog.Count < 100)
                DebugLog.Enqueue(String.Format("[{0}]: {1}" + Environment.NewLine, DateTime.Now, msg));

            AsyncLog.Debug(LogType.Debug, msg);
        }

        public void EnqueueChat(string msg)
        {
            if (ChatLog.Count < 100)
                ChatLog.Enqueue(String.Format("[{0}]: {1}" + Environment.NewLine, DateTime.Now, msg));

            AsyncLog.Info(LogType.Chat, msg);
        }
    }
}
