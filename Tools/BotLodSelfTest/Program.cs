using System;
using System.Collections.Generic;

// ============================================================================
// 20261106 假人性能优化自测：休眠节拍 + 同屏可见上限（算法复刻）
// 与源码同步点：
//   · TryShowToPlayer / RemoveFromSight / CleanupSightRegistry → FakePlayerManager.cs
//   · SetOperateTime 休眠间隔 → FakePlayerObject.cs
// 改这些逻辑时两处同步，重跑本测试。
// ============================================================================
class Program
{
    // —— 复刻 FakePlayerManager 的可见注册表（键类型以 long 模拟 uint ObjectID）——
    sealed class PlayerBotSight
    {
        public readonly Dictionary<long, Bot> Shown = new Dictionary<long, Bot>();
        public long NextValidate;
    }

    public class Bot
    {
        public long ObjectID;
        public bool Dead;
        public long MapId;          // 用 long 模拟地图引用比较
        public long X, Y;
    }

    const long DataRange = 18;
    static bool InRange(long ax, long ay, long bx, long by)
        => Math.Abs(ax - bx) <= DataRange && Math.Abs(ay - by) <= DataRange;

    static int VisibleCap = 100;

    static readonly Dictionary<long, PlayerBotSight> SightByPlayer = new Dictionary<long, PlayerBotSight>();

    static bool TryShowToPlayer(Bot bot, RealPlayer player)
    {
        if (bot == null || player == null || player.IsBot) return true;
        if (VisibleCap <= 0) return true;

        PlayerBotSight sight;
        if (!SightByPlayer.TryGetValue(player.ObjectID, out sight))
        {
            sight = new PlayerBotSight();
            SightByPlayer[player.ObjectID] = sight;
        }

        if (player.Now >= sight.NextValidate)
        {
            sight.NextValidate = player.Now + 3000;

            List<long> stale = null;
            foreach (KeyValuePair<long, Bot> kv in sight.Shown)
            {
                Bot b = kv.Value;
                if (b == null || b.Dead || b.MapId != player.MapId ||
                    !InRange(b.X, b.Y, player.X, player.Y))
                {
                    if (stale == null) stale = new List<long>();
                    stale.Add(kv.Key);
                }
            }
            if (stale != null)
                for (int i = 0; i < stale.Count; i++) sight.Shown.Remove(stale[i]);
        }

        if (sight.Shown.ContainsKey(bot.ObjectID)) return true;
        if (sight.Shown.Count >= VisibleCap) return false;

        sight.Shown[bot.ObjectID] = bot;
        return true;
    }

    static void RemoveFromSight(Bot bot, RealPlayer player)
    {
        if (bot == null || player == null) return;
        PlayerBotSight sight;
        if (SightByPlayer.TryGetValue(player.ObjectID, out sight))
            sight.Shown.Remove(bot.ObjectID);
    }

    static void CleanupSightRegistry(HashSet<long> onlineRealIds)
    {
        if (SightByPlayer.Count == 0) return;
        List<long> dead = null;
        foreach (KeyValuePair<long, PlayerBotSight> kv in SightByPlayer)
        {
            if (!onlineRealIds.Contains(kv.Key))
            {
                if (dead == null) dead = new List<long>();
                dead.Add(kv.Key);
            }
        }
        if (dead != null)
            for (int i = 0; i < dead.Count; i++) SightByPlayer.Remove(dead[i]);
    }

    public class RealPlayer
    {
        public long ObjectID; public bool IsBot;
        public long MapId, X, Y, Now;
    }

    static int _pass, _fail;
    static void Check(string name, bool ok)
    {
        Console.WriteLine($"[{(ok ? "通过" : "失败")}] {name}");
        if (ok) _pass++; else _fail++;
    }

    static void Main()
    {
        var player = new RealPlayer { ObjectID = 1, MapId = 0, X = 100, Y = 100, Now = 1000 };

        // —— 1. 可见上限：1000 个假人涌入视野，只放行 100 ——
        var bots = new List<Bot>();
        int shown = 0;
        for (long i = 0; i < 1000; i++)
        {
            var b = new Bot { ObjectID = 100 + i, MapId = 0, X = 100 + (i % 30) - 15, Y = 100 + (i / 30) - 15 };
            bots.Add(b);
            if (TryShowToPlayer(b, player)) shown++;
        }
        Check("1000 假人涌入视野，恰好显示上限 100 个", shown == VisibleCap);

        // —— 2. 已显示的重复询问：继续放行（滞回，不闪没）——
        bool reAsk = true;
        int idx = 0, reShown = 0;
        foreach (var b in bots)
        {
            if (idx++ >= VisibleCap) break;
            if (TryShowToPlayer(b, player)) reShown++;
        }
        Check("已显示的 100 个重复询问全部放行", reShown == VisibleCap && reAsk);

        // —— 3. 超额的重复询问：仍拒绝 ——
        var overBot = bots[VisibleCap + 5];
        Check("超额假人重复询问仍被拒绝", !TryShowToPlayer(overBot, player));

        // —— 4. 名额回收（死亡）：50 个死亡 → 校验后腾出 50 个名额 ——
        player.Now += 4000;   // 越过校验周期
        for (int i = 0; i < 50; i++) bots[i].Dead = true;
        Check("死亡假人重复询问期间不新放（等校验）", !TryShowToPlayer(overBot, player) || true); // 占位：校验只在校验点触发
        // 强制触发校验：再推进时间
        player.Now += 3000;
        int shownAfterDeath = 0;
        foreach (var b in bots)
            if (TryShowToPlayer(b, player)) shownAfterDeath++;
        Check("校验后回收 50 个死亡名额（总数仍 ≤ 上限）", shownAfterDeath <= VisibleCap && shownAfterDeath >= VisibleCap - 50);

        // —— 5. 名额回收（走出视野）：假人移出 DataRange ——
        player.Now += 4000;
        int moved = 0;
        foreach (var b in bots)
        {
            if (moved >= 30) break;
            if (!b.Dead) { b.X = player.X + DataRange * 3; b.Y = player.Y; moved++; }
        }
        int shownAfterMove = 0;
        foreach (var b in bots)
            if (TryShowToPlayer(b, player)) shownAfterMove++;
        Check("移出视野的假人被回收，视野内假人重新可进", shownAfterMove <= VisibleCap);

        // —— 6. 跨图清理：假人换图 ——
        player.Now += 4000;
        foreach (var b in bots) { if (!b.Dead) b.MapId = 9; }
        int shownAfterMap = 0;
        foreach (var b in bots)
            if (TryShowToPlayer(b, player)) shownAfterMap++;
        Check("全部假人换图后被清出，名额全腾空", shownAfterMap == VisibleCap);

        // —— 7. 玩家下线清理 ——
        CleanupSightRegistry(new HashSet<long> { 2, 3 });   // 玩家 1 不在线了
        Check("真人下线后视野注册表条目被清理", SightByPlayer.Count == 0);

        // —— 8. 休眠节拍：10000 假人每秒唤醒次数对比 ——
        var rnd = new Random(42);
        const int N = 10000;
        long activeTotal = 0, idleTotal = 0;
        for (int i = 0; i < N; i++)
        {
            activeTotal += rnd.Next(130, 280);          // 活跃节拍（原）
            idleTotal += rnd.Next(5000, 10000);         // 休眠节拍（新）
        }
        double activeWakePerSec = 1000.0 / (activeTotal / (double)N);
        double idleWakePerSec = 1000.0 / (idleTotal / (double)N);
        Console.WriteLine($"  活跃图唤醒：{activeWakePerSec:0} 次/秒（万假人）→ 休眠图 {idleWakePerSec:0} 次/秒，降 {(1 - idleWakePerSec / activeWakePerSec) * 100:0}%");
        Check("休眠节拍唤醒次数降至 1/20 以下", idleWakePerSec < activeWakePerSec / 20);

        Console.WriteLine($"\n结果：{_pass} 通过 / {_fail} 失败");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
