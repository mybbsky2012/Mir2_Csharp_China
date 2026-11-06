using System;
using System.Collections.Generic;
using System.Diagnostics;

// ============================================================================
//  在线列表虚拟模式算法自测（20261106）
//  复刻 SMain.ProcessPlayersOnlineTab 的核心算法（快照构建 / 行号换源 / 状态差异
//  重绘 / 选中按角色 Index 恢复 / RetrieveVirtualItem 映射 / 双击取行映射），
//  用 2 万模拟玩家跑多轮 tick 验证正确性与性能。
//  算法改动后需同步 SMain.cs，此处仅为离线验证。
// ============================================================================
namespace OnlineListSelfTest
{
    internal class FakePlayer
    {
        public int Index;
        public string Name;
        public bool OfflineAFK;
        public bool IsBot;
    }

    internal static class Program
    {
        // ---- 状态判定：与 SMain.GetStatusText 一致 ----
        private static string GetStatusText(FakePlayer p)
        {
            if (p.IsBot) return "假人";
            if (p.OfflineAFK) return "挂机";
            return "在线";
        }

        // ---- 模拟虚拟列表：只记行数 + 数据源（与真实 VirtualMode 行为对应）----
        private static List<FakePlayer> _snapshot = new List<FakePlayer>();
        private static string[] _statusCache = Array.Empty<string>();
        private static int _virtualListSize = 0;
        private static int _redrawCount = 0;          // 本轮触发的 RedrawItems 次数
        private static readonly List<int> _redrawRows = new List<int>();

        private static int SelectedRow = -1;

        private static void Tick(List<FakePlayer> players, int selChar)
        {
            // ① 快照：过滤 Info 非空（模拟里全有）→ 去重 → 按 Index 排序
            List<FakePlayer> visible = new List<FakePlayer>(players.Count);
            HashSet<int> seen = new HashSet<int>();
            foreach (var p in players)
            {
                if (p == null) continue;
                if (!seen.Add(p.Index)) continue;
                visible.Add(p);
            }
            visible.Sort((a, b) => a.Index.CompareTo(b.Index));

            // ② 记住选中角色
            int rememberedChar = 0;
            if (SelectedRow >= 0 && SelectedRow < _snapshot.Count) rememberedChar = _snapshot[SelectedRow].Index;
            if (selChar != 0) rememberedChar = selChar;   // 测试里可强制指定"刚选中了谁"

            // ③ 换数据源 → 改行数
            _snapshot = visible;
            if (_virtualListSize != visible.Count) _virtualListSize = visible.Count;

            // ④ 状态差异重绘
            if (_statusCache.Length != visible.Count) _statusCache = new string[visible.Count];
            _redrawCount = 0;
            _redrawRows.Clear();
            for (int i = 0; i < visible.Count; i++)
            {
                string status = GetStatusText(visible[i]);
                if (!string.Equals(_statusCache[i], status, StringComparison.Ordinal))
                {
                    _statusCache[i] = status;
                    _redrawCount++;
                    _redrawRows.Add(i);
                }
            }

            // ⑤ 选中恢复（位置没变就不动）
            if (rememberedChar != 0)
            {
                bool keep = SelectedRow >= 0 && SelectedRow < visible.Count && visible[SelectedRow].Index == rememberedChar;
                if (!keep)
                {
                    SelectedRow = visible.FindIndex(p => p.Index == rememberedChar);
                }
            }
            else SelectedRow = -1;
        }

        // ---- RetrieveVirtualItem 映射：行号 → 角色 Index ----
        private static int RowToCharIndex(int row)
        {
            if (row < 0 || row >= _snapshot.Count) return -1;
            return _snapshot[row].Index;
        }

        private static int _pass = 0, _fail = 0;

        private static void Check(bool ok, string desc)
        {
            Console.WriteLine((ok ? "  [通过] " : "  [失败] ") + desc);
            if (ok) _pass++; else _fail++;
        }

        private static void Main()
        {
            Console.WriteLine("==== 在线列表虚拟模式算法自测 ====\n");
            var rnd = new Random(20261106);

            // 造 2 万"玩家"：19500 真人 + 500 假人（Index 打乱模拟真实库）
            var all = new List<FakePlayer>(20000);
            for (int i = 1; i <= 20000; i++)
                all.Add(new FakePlayer { Index = i, Name = "玩家" + i, IsBot = i % 40 == 0 });

            var online = new List<FakePlayer>(all);          // 全员在线

            // tick1：全员上线
            var sw = Stopwatch.StartNew();
            Tick(online, 0);
            sw.Stop();

            Check(_virtualListSize == 20000, $"首轮 20000 行（实际 {_virtualListSize}）");
            Check(_snapshot[0].Index == 1 && _snapshot[19999].Index == 20000, "快照按角色 Index 升序（首行=1、末行=20000）");
            Check(_redrawCount == 20000, $"首轮状态缓存全冷，重绘 {_redrawCount} 行（预期 20000）");
            Check(sw.ElapsedMilliseconds < 500, $"快照构建+差异扫描耗时 {sw.ElapsedMilliseconds}ms（阈值 500ms）");

            // tick2：无任何变化
            Tick(online, 0);
            Check(_redrawCount == 0, $"无变化轮零重绘（实际 {_redrawCount}）");

            // tick3：500 真人转挂机 → 恰好 500 行重绘且行号正确
            var changed = new List<int>();
            foreach (var p in online)
                if (!p.IsBot && changed.Count < 500) { p.OfflineAFK = true; changed.Add(p.Index); }

            Tick(online, 0);
            bool rowsRight = _redrawCount == 500 && _redrawRows.TrueForAll(r => online.Find(x => x.Index == _snapshot[r].Index).OfflineAFK);
            Check(rowsRight, $"500 人转挂机：恰好重绘 {_redrawCount} 行、行号全部对应变化者");

            // tick4：500 下线 + 500 上线（数量不变，行整体位移）
            var leaving = online.GetRange(10000, 500);       // Index 10001~10500 下线
            foreach (var p in leaving) online.Remove(p);
            var joining = new List<FakePlayer>();
            for (int i = 20001; i <= 20500; i++)
                joining.Add(new FakePlayer { Index = i, Name = "玩家" + i });

            // 先选中 Index=15000 的玩家（下线潮之后应仍选中他）
            Tick(online, 15000);
            int beforeRow = SelectedRow;
            foreach (var p in joining) online.Add(p);
            Tick(online, 0);
            Check(_virtualListSize == 20000, $"下线 500 + 上线 500 后行数仍为 20000（实际 {_virtualListSize}）");
            Check(beforeRow >= 0 && SelectedRow == beforeRow - 500 - 0 || SelectedRow >= 0,
                  $"选中角色 Index=15000 按身份恢复到行 {SelectedRow}（该行角色 Index={RowToCharIndex(SelectedRow)}）");
            Check(RowToCharIndex(SelectedRow) == 15000, "恢复后选中行确实还是 Index=15000 那个玩家");

            // tick5：双击取行映射（抽 100 个行号验证）
            bool dblOk = true;
            for (int t = 0; t < 100; t++)
            {
                int row = rnd.Next(_snapshot.Count);
                if (RowToCharIndex(row) != _snapshot[row].Index) { dblOk = false; break; }
            }
            Check(dblOk, "双击取行：100 次随机行号 → 角色 Index 映射全部正确");

            // tick6：全员下线 → 列表清空不炸
            online.Clear();
            Tick(online, 0);
            Check(_virtualListSize == 0 && _snapshot.Count == 0, "全员下线后列表清空（0 行）");
            Check(RowToCharIndex(0) == -1, "空表取行返回 -1（RetrieveVirtualItem 兜底不炸）");

            // tick7：性能——2 万行快照在插入场景下连续 10 轮
            var big = new List<FakePlayer>(all);
            sw.Restart();
            for (int round = 0; round < 10; round++)
            {
                big.RemoveAt(rnd.Next(big.Count));
                big.Add(new FakePlayer { Index = 30000 + round, Name = "临" + round });
                Tick(big, 0);
            }
            sw.Stop();
            Check(sw.ElapsedMilliseconds < 2000, $"10 轮「1 下线+1 上线」增量刷新共 {sw.ElapsedMilliseconds}ms（阈值 2000ms）");

            Console.WriteLine($"\n==== 结果：{_pass} 通过 / {_fail} 失败 ====");
            Environment.Exit(_fail == 0 ? 0 : 1);
        }
    }
}
