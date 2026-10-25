using System.Drawing;
using Server.MirEnvir;

namespace Server.MirObjects
{
    /// <summary>
    /// 有界 A* 网格寻路（当前仅用于英雄自动拾取补给等近距离场景）。
    /// 只把「地形 / 未开的门 / 阻挡单位」当障碍；玩家、怪物等移动单位的位置随时会变，
    /// 规划出的路径仅作参考——走的时候撞上单位由 MoveTo 的转向逻辑自行避让。
    /// </summary>
    internal static class PathFinder
    {
        private const int MaxNodes = 300;   // 单次展开节点上限，防卡顿
        private const int MaxRange = 12;    // 起点与终点的最大距离（切比雪夫距离）

        private sealed class Node
        {
            public Point P;
            public Node Parent;
            public int G;
            public int F;
        }

        /// <summary>求 start 到 goal 的路径；返回路径点队列（不含起点、含终点），找不到返回 null。</summary>
        public static List<Point> FindPath(Map map, Point start, Point goal)
        {
            if (map == null || start == goal) return null;
            if (Functions.MaxDistance(start, goal) > MaxRange) return null;

            List<Node> open = new List<Node>();
            Dictionary<Point, Node> openSet = new Dictionary<Point, Node>();
            HashSet<Point> closed = new HashSet<Point>();

            Node first = new Node { P = start, G = 0, F = H(start, goal) };
            open.Add(first);
            openSet[start] = first;

            int expansions = 0;

            while (open.Count > 0)
            {
                // 取 F 值最小的节点（节点数上限 300，线性扫描足够快）
                int best = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].F < open[best].F) best = i;

                Node cur = open[best];
                open.RemoveAt(best);
                openSet.Remove(cur.P);
                closed.Add(cur.P);

                if (cur.P == goal)
                {
                    List<Point> path = new List<Point>();
                    for (Node n = cur; n.Parent != null; n = n.Parent) path.Add(n.P);
                    path.Reverse();
                    return path;
                }

                if (++expansions > MaxNodes) return null;   // 超预算：放弃本次寻路

                for (int d = 0; d < 8; d++)
                {
                    MirDirection dir = (MirDirection)d;
                    Point np = Functions.PointMove(cur.P, dir, 1);

                    if (closed.Contains(np)) continue;
                    if (np != goal && !Walkable(map, np)) continue;   // 终点格放行（地面物品不挡路）

                    // 禁止斜穿墙角：斜行必须两个正交邻格都可走
                    if (d % 2 == 1)
                    {
                        MirDirection a = (MirDirection)((d + 7) % 8);
                        MirDirection b = (MirDirection)((d + 1) % 8);
                        if (!Walkable(map, Functions.PointMove(cur.P, a, 1)) ||
                            !Walkable(map, Functions.PointMove(cur.P, b, 1))) continue;
                    }

                    int g = cur.G + (d % 2 == 0 ? 10 : 14);   // 直行 10 / 斜行 14

                    if (openSet.TryGetValue(np, out Node exist) && exist.G <= g) continue;

                    Node node = new Node { P = np, Parent = cur, G = g, F = g + H(np, goal) };
                    if (exist != null) open.Remove(exist);
                    open.Add(node);
                    openSet[np] = node;
                }
            }

            return null;
        }

        /// <summary>启发函数：切比雪夫距离（允许斜线移动）。</summary>
        private static int H(Point a, Point b)
        {
            int dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
            return 10 * Math.Max(dx, dy);
        }

        /// <summary>单格可走判定：地形有效 + 门已开 + 没有阻挡单位。</summary>
        private static bool Walkable(Map map, Point p)
        {
            if (!map.ValidPoint(p)) return false;
            if (!map.CheckDoorOpen(p)) return false;

            Cell cell = map.GetCell(p);
            if (cell.Objects == null) return true;

            for (int i = 0; i < cell.Objects.Count; i++)
                if (cell.Objects[i].Blocking) return false;

            return true;
        }
    }
}
