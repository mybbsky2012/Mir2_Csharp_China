using System.Diagnostics;
using System.Drawing;
using Client.MirObjects;
using Client.MirScenes;

namespace Client.MirScenes
{
    /// <summary>测试用替身：真 CellInfo 在客户端里就这两个字段被寻路读到。</summary>
    public struct CellInfo
    {
        public int BackImage;
        public int FrontImage;
    }

    /// <summary>测试用替身 MapControl：只实现 PathFinder 会碰的那几个成员。</summary>
    public class MapControl
    {
        public int Width, Height;
        public CellInfo[,] M2CellInfo;
        public List<Point> Blockers = new List<Point>();

        public MapControl(int w, int h)
        {
            Width = w;
            Height = h;
            M2CellInfo = new CellInfo[w, h];
        }

        public void SetWall(int x, int y)
        {
            M2CellInfo[x, y].BackImage |= 0x20000000;
        }

        public bool EmptyCell(Point p)
        {
            if ((M2CellInfo[p.X, p.Y].BackImage & 0x20000000) != 0) return false;
            if ((M2CellInfo[p.X, p.Y].FrontImage & 0x8000) != 0) return false;
            return !Blockers.Contains(p);
        }

        public void SnapshotBlockingCells(HashSet<long> into)
        {
            into.Clear();
            for (int i = 0; i < Blockers.Count; i++)
            {
                Point p = Blockers[i];
                if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height) continue;
                into.Add((((long)p.X) << 32) | ((uint)p.Y));
            }
        }
    }
}

namespace PathTest
{
    internal static class Program
    {
        static int _fail;

        static void Check(bool ok, string name, string extra = "")
        {
            Console.WriteLine((ok ? "  OK   " : "  FAIL ") + name + (extra.Length > 0 ? "  " + extra : ""));
            if (!ok) _fail++;
        }

        static bool ContiguousAndWalkable(MapControl map, List<Node> path, Point start, Point goal, string label)
        {
            if (path == null || path.Count == 0) { Check(false, label + " 有路径"); return false; }

            Check(path[0].Location == start, label + " 起点一致", path.Count > 0 ? path[0].Location.ToString() : "-");

            bool ok = true;
            for (int i = 1; i < path.Count; i++)
            {
                Point a = path[i - 1].Location;
                Point b = path[i].Location;
                if (Math.Abs(a.X - b.X) > 1 || Math.Abs(a.Y - b.Y) > 1 || (a.X == b.X && a.Y == b.Y)) ok = false;
                if (!map.EmptyCell(b)) ok = false;
            }
            Check(ok, label + " 路径连续且可走");
            Check(path[path.Count - 1].Location == goal, label + " 终点命中", path[path.Count - 1].Location.ToString());
            return ok;
        }

        static void Main()
        {
            // ---- 1. 空地：同一张图连续寻路 6 次，每次都必须成功（旧实现第二次就返回 null）----
            Console.WriteLine("[1] 100x100 空地，重复寻路 6 次");
            {
                var map = new MapControl(100, 100);
                var pf = new PathFinder(map);
                var start = new Point(2, 2);
                var goal = new Point(95, 95);

                int firstLen = -1;
                for (int i = 0; i < 6; i++)
                {
                    var p = pf.FindPath(start, goal);
                    if (p == null) { Check(false, $"第 {i + 1} 次有路径"); continue; }
                    if (i == 0) firstLen = p.Count;
                    Check(p.Count == firstLen, $"第 {i + 1} 次长度一致", $"count={p.Count} first={firstLen}");
                }
                var path = pf.FindPath(start, goal);
                ContiguousAndWalkable(map, path, start, goal, "空地");
            }

            // ---- 2. 第二次寻路换终点 + 中间加墙（模拟「先点 A 再点 B」）----
            Console.WriteLine("[2] 连续两次不同终点 + 中途加墙");
            {
                var map = new MapControl(120, 120);
                var pf = new PathFinder(map);
                var start = new Point(5, 5);

                var p1 = pf.FindPath(start, new Point(100, 100));
                Check(p1 != null && p1.Count > 0, "第一次成功");

                for (int y = 0; y < 120; y++)
                    if (y != 60) map.SetWall(60, y);   // 竖墙，只留 y=60 的门

                var p2 = pf.FindPath(start, new Point(100, 100));
                Check(p2 != null && p2.Count > 0, "第二次（绕墙）成功", p2 == null ? "null" : "count=" + p2.Count);

                if (p2 != null)
                {
                    bool throughDoor = p2.Exists(n => n.Location == new Point(60, 60));
                    Check(throughDoor, "路径从墙上的门 y=60 穿过");
                    ContiguousAndWalkable(map, p2, start, new Point(100, 100), "绕墙");
                }
            }

            // ---- 3. 完全封闭的目标：不能卡死，要快速返回 ----
            Console.WriteLine("[3] 1000x800 大地图，目标被完全封死（旧实现会翻整张图，定住几秒）");
            {
                var map = new MapControl(1000, 800);
                var pf = new PathFinder(map);
                var start = new Point(10, 10);
                var goal = new Point(900, 700);

                for (int x = goal.X - 2; x <= goal.X + 2; x++)
                    for (int y = goal.Y - 2; y <= goal.Y + 2; y++)
                        if (x >= 0 && y >= 0 && x < 1000 && y < 800) map.SetWall(x, y);

                var sw = Stopwatch.StartNew();
                var p = pf.FindPath(start, goal);
                sw.Stop();

                Console.WriteLine($"       耗时 {sw.ElapsedMilliseconds} ms，返回 {(p == null ? "null" : "count=" + p.Count)}");
                Check(sw.ElapsedMilliseconds < 1200, "未卡死（<1.2s）", sw.ElapsedMilliseconds + "ms");
                Check(p == null || p.Count > 0, "返回 null 或部分路径（玩家会往前走，不会原地不动）");
            }

            // ---- 4. 大地图正常远距离寻路：性能 + 正确性 ----
            Console.WriteLine("[4] 1000x800 大地图，横向 900 格长路");
            {
                var map = new MapControl(1000, 800);
                var pf = new PathFinder(map);
                var sw = Stopwatch.StartNew();
                var p = pf.FindPath(new Point(50, 400), new Point(950, 400));
                sw.Stop();
                Console.WriteLine($"       耗时 {sw.ElapsedMilliseconds} ms，count={(p == null ? -1 : p.Count)}");
                Check(p != null && p.Count > 0, "成功找到长路");
                Check(sw.ElapsedMilliseconds < 800, "耗时 <800ms", sw.ElapsedMilliseconds + "ms");
                if (p != null) ContiguousAndWalkable(map, p, new Point(50, 400), new Point(950, 400), "长路");
            }

            // ---- 5. 终点被占（怪物/NPC 站着）→ 就近落脚 ----
            Console.WriteLine("[5] 终点被怪占住");
            {
                var map = new MapControl(60, 60);
                var pf = new PathFinder(map);
                map.Blockers.Add(new Point(30, 30));

                var p = pf.FindPath(new Point(5, 5), new Point(30, 30));
                Check(p != null && p.Count > 0, "仍能走到旁边");
                if (p != null)
                {
                    Point end = p[p.Count - 1].Location;
                    int dist = Math.Max(Math.Abs(end.X - 30), Math.Abs(end.Y - 30));
                    Check(dist >= 1 && dist <= 3, "落在 3 格内", end.ToString());
                    Check(map.EmptyCell(end), "落点可站");
                }
            }

            // ---- 6. 越界坐标不抛异常 ----
            Console.WriteLine("[6] 越界坐标");
            {
                var map = new MapControl(50, 50);
                var pf = new PathFinder(map);

                bool threw = false;
                try
                {
                    Check(pf.FindPath(new Point(-1, 10), new Point(20, 20)) == null, "起点越界返回 null");
                    Check(pf.FindPath(new Point(10, 10), new Point(999, 20)) == null, "终点越界返回 null");
                }
                catch (Exception ex)
                {
                    threw = true;
                    Console.WriteLine("       " + ex.GetType().Name + ": " + ex.Message);
                }
                Check(!threw, "未抛异常");
            }

            // ---- 7. 迷宫：随机 30% 墙，连续寻路 200 次不崩、结果稳定 ----
            Console.WriteLine("[7] 迷宫压力：200 次寻路");
            {
                var rnd = new Random(1234);
                var map = new MapControl(200, 200);
                for (int x = 0; x < 200; x++)
                    for (int y = 0; y < 200; y++)
                        if (rnd.Next(100) < 30) map.SetWall(x, y);

                var pf = new PathFinder(map);
                int ok = 0, nul = 0;
                bool threw = false;
                var sw = Stopwatch.StartNew();
                try
                {
                    for (int i = 0; i < 200; i++)
                    {
                        var s = new Point(rnd.Next(1, 199), rnd.Next(1, 199));
                        var g = new Point(rnd.Next(1, 199), rnd.Next(1, 199));
                        var sn = pf.FindPath(new Point(s.X, s.Y), new Point(s.X, s.Y));
                        // 起点被墙里时直接跳过（真实场景不会发生）
                        if (sn == null) { nul++; continue; }
                        var p = pf.FindPath(s, g);
                        if (p == null) nul++;
                        else
                        {
                            ok++;
                            if (p.Count > 1)
                            {
                                for (int k = 1; k < p.Count; k++)
                                {
                                    Point a = p[k - 1].Location, b = p[k].Location;
                                    if (Math.Abs(a.X - b.X) > 1 || Math.Abs(a.Y - b.Y) > 1) { threw = true; break; }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    threw = true;
                    Console.WriteLine("       " + ex.GetType().Name + ": " + ex.Message);
                }
                sw.Stop();
                Console.WriteLine($"       200 次共 {sw.ElapsedMilliseconds} ms（success={ok} null={nul}）");
                Check(!threw, "无异常/无非法步进");
                Check(ok > 150, "大部分请求能算出路", "ok=" + ok);
                Check(sw.ElapsedMilliseconds < 4000, "总耗时 <4s", sw.ElapsedMilliseconds + "ms");
            }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "=== 全部通过 ===" : $"=== 失败 {_fail} 项 ===");
            Environment.Exit(_fail == 0 ? 0 : 1);
        }
    }
}
