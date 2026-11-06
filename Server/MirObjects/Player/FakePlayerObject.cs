using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using System.Diagnostics;
using System.Drawing;

namespace Server.MirObjects
{
    /// <summary>
    /// 假人（AI 玩家）。
    ///
    /// 它就是一个真的 PlayerObject —— 所以客户端**不需要改一行代码**就能把它当普通玩家渲染出来：
    /// 外观/装备/等级/头顶名字/行会/喊话气泡/组队/交易/攻城……所有走 PlayerObject 的系统全都天然可用。
    ///
    /// 与真人的区别只有两点：
    ///   1. 身后的 Connection 是 BotConnection：不占 socket、所有下行包直接丢弃；
    ///   2. Process 末尾多跑一次 AI 大脑（FakePlayerBrain.Tick）。
    /// </summary>
    public class FakePlayerObject : PlayerObject
    {
        public FakePlayerBrain Brain;

        /// <summary>死亡后自动回城复活的时间点（假人没人点「复活」）</summary>
        public long NextReviveTime;

        /// <summary>
        /// 驻地：这个假人「常驻」在哪张图的哪个点。
        /// 由 FakePlayerManager.Login 按配置的登录点设置；「回城补给」会回到这里，
        /// 而不是像原来那样一律传送回主城出生点 —— 那样玩家在别的图上就再也见不到假人了。
        /// </summary>
        public bool HasHome;
        public int HomeMapIndex;
        public Point HomeLocation;

        public FakePlayerObject(CharacterInfo info, MirConnection connection) : base(info, connection) { }

        public override void Process()
        {
            // 20261106 休眠地图（无真人在线）：跳过 AI 大脑 —— 没人看的假人不寻路、不打怪、不拾取，
            //   只保留低频的 base.Process（状态/复活/巡检节拍）兜底。
            bool idleMap = CurrentMap == null || CurrentMap.RealPlayers.Count == 0;

            long t0 = Stopwatch.GetTimestamp();
            base.Process();
            FakePlayerPerf.NoteBotProcess(Stopwatch.GetTimestamp() - t0);

            if (Brain == null || idleMap)
            {
                // 休眠地图也要自动复活：Brain 不跑的话没人执行 TownRevive，
                //   否则真人下次进图会看到一地假人尸体（复活点在 Brain 的正常逻辑里）
                if (idleMap && Dead && NextReviveTime > 0 && Envir.Time >= NextReviveTime)
                    TownRevive();

                return;
            }

            long t1 = Stopwatch.GetTimestamp();
            Brain.Tick();
            FakePlayerPerf.NoteBrain(Stopwatch.GetTimestamp() - t1);
        }

        /// <summary>
        /// 假人的主循环节拍。
        ///
        /// 真人是「主循环每扫一遍就被 Process 一次」（HumanObject 把 OperateTime 直接设成 Envir.Time），
        /// 一个真人如此、一百个真人也如此 —— 这是原版设计。但假人没必要这么勤：
        ///   · 它身后没有客户端在等位置更新，晚 100~200ms 处理一次，玩家肉眼看不出来；
        ///   · AI 大脑自身本来就是 380~700ms 才决策一次，处理频率再高也是空转。
        ///
        /// 所以这里给它 130~280ms 的**随机**间隔，两件事一起解决：
        ///   1. 把「50 个假人冒充 50 个玩家」占掉的主循环时间直接压到 1/5 上下；
        ///   2. 随机错开落点，避免 50 个假人挤在同一帧里一起醒来 ——
        ///      那才是真正会把主循环顶出毛刺的写法（也是客户端跑起来一顿一顿的常见来源）。
        /// </summary>
        public override void SetOperateTime()
        {
            // 20261106 分级节拍：无真人的地图（休眠）拉长到 IdleMs~IdleMs*2 随机间隔，
            //   有真人的地图维持原来的 130~280ms 正常表现。
            if (CurrentMap == null || CurrentMap.RealPlayers.Count == 0)
            {
                OperateTime = Envir.Time + Envir.Random.Next(Settings.FakePlayerIdleMs, Settings.FakePlayerIdleMs * 2);
                return;
            }

            OperateTime = Envir.Time + Envir.Random.Next(130, 280);
        }

        public override void Die()
        {
            base.Die();

            NextReviveTime = Envir.Time + Envir.Random.Next(4000, 12000);
        }

        // ==================================================================================
        //  20261106 同屏可见上限 + 渐显：进真人视野时先问注册表要名额。
        //  拿不到名额（该真人视野内假人已满 VisibleCap）→ 不发出现包，客户端看不到这个假人；
        //  拿到名额也不立刻发 —— 排进渐显队列，由 FakePlayerManager 每秒放一波
        //  （1→2→4→…→ShowRampMax 封顶），避免进图瞬间收到上百个出现包把客户端卡死几十秒。
        // ==================================================================================
        public override void Add(HumanObject player)
        {
            if (player == this) return;

            // 名额判定 + 入队/立即显示（组队成员立即出现）都在 Manager 里做
            FakePlayerManager.TryShowToPlayer(this, player);
        }

        public override void Remove(HumanObject player)
        {
            FakePlayerManager.RemoveFromSight(this, player);   // 先让出可见名额
            base.Remove(player);                               // 照常给真人发 ObjectRemove
        }

        // 被打 → 通知大脑（还手 / 躲避）。两个重载对应「被人打」和「被怪打」。
        public override int Attacked(HumanObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            int result = base.Attacked(attacker, damage, type, damageWeapon);

            if (damage > 0 && Brain != null) Brain.OnAttacked(attacker);

            return result;
        }

        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            int result = base.Attacked(attacker, damage, type);

            if (damage > 0 && Brain != null) Brain.OnAttacked(attacker);

            return result;
        }
    }
}
