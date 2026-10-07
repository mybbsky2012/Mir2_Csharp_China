namespace Server.MirNetwork
{
    /// <summary>
    /// 假人专用连接。
    ///
    /// 假人（AI 玩家）在服务端就是真实的 PlayerObject —— 这样真人客户端不用改一行代码，
    /// 看到的假人和真人完全一样（外观、等级、装备、头顶名字、行会、喊话、交易、组队、PK 全通）。
    ///
    /// 但假人身后没有客户端，所以给它一个「空连接」：
    ///   1. 不申请 socket、不进 Envir 的网络轮询表；
    ///   2. 所有发往它的下行包在 Enqueue 里直接丢掉（不占内存、不占带宽）；
    ///   3. Connection 非空，于是 PlayerObject/HumanObject 里那些
    ///      `if (Connection == null) return;` 的守卫会正常通过，逻辑与真人一致。
    /// </summary>
    public class BotConnection : MirConnection
    {
        public BotConnection(int sessionID) : base(sessionID, null)
        {
            Stage = GameStage.Game;
        }

        public override void Enqueue(Packet p)
        {
            // 假人没有客户端：全部丢弃（含给观察者转发的那部分）。
        }

        public override void Process()
        {
            // 不参与网络收发轮询。
        }

        /// <summary>
        /// 管理界面的「踢下线」按钮会对在线假人调用 SendDisconnect ——
        /// 假人是空连接，没有 socket，不能走原版收发/断连流程。
        /// 假人的下线统一由 FakePlayerManager 管理，这里安全地什么都不做。
        /// </summary>
        public override void SendDisconnect(byte reason)
        {
        }
    }
}
