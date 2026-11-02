using Client.MirScenes;

namespace Client.MirObjects
{
    public class Heap<T> where T : IHeapItem<T>
    {
        private T[] items;
        private int currentItemCount;

        public Heap(int maxHeapSize)
        {
            items = new T[maxHeapSize];
        }

        public void Add(T item)
        {
            //容量不够就翻倍，避免调用方为了安全一次性把整个地图的格子数都分配出来
            if (currentItemCount >= items.Length)
                Array.Resize(ref items, Math.Max(4, items.Length * 2));

            item.HeapIndex = currentItemCount;
            items[currentItemCount] = item;
            SortUp(item);
            currentItemCount++;
        }

        public T RemoveFirst()
        {
            T firstItem = items[0];
            currentItemCount--;

            items[0] = items[currentItemCount];
            items[0].HeapIndex = 0;

            SortDown(items[0]);

            return firstItem;
        }

        public void UpdateItem(T item)
        {
            SortUp(item);
        }

        public int Count { get { return currentItemCount; } }

        public bool Contains(T item)
        {
            return Equals(items[item.HeapIndex], item);
        }

        private void SortDown(T item)
        {
            while (true)
            {
                int childIndexLeft = (item.HeapIndex * 2) + 1;
                int childIndexRight = (item.HeapIndex * 2) + 2;
                int swapIndex = 0;

                if (childIndexLeft < currentItemCount)
                {
                    swapIndex = childIndexLeft;
                    if (childIndexRight < currentItemCount)
                    {
                        if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0)
                        {
                            swapIndex = childIndexRight;
                        }
                    }

                    if (item.CompareTo(items[swapIndex]) < 0)
                    {
                        Swap(item, items[swapIndex]);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private void SortUp(T item)
        {
            int parentIndex = (item.HeapIndex - 1) / 2;

            while (true)
            {
                T parentItem = items[parentIndex];
                if (item.CompareTo(parentItem) > 0)
                {
                    Swap(item, parentItem);
                }
                else
                    break;

                parentIndex = (item.HeapIndex - 1) / 2;
            }
        }

        private void Swap(T itemA, T itemB)
        {
            items[itemA.HeapIndex] = itemB;
            items[itemB.HeapIndex] = itemA;

            int itemAIndex = itemA.HeapIndex;
            itemA.HeapIndex = itemB.HeapIndex;
            itemB.HeapIndex = itemAIndex;
        }
    }

    public interface IHeapItem<T> : IComparable<T>
    {
        int HeapIndex
        {
            get; set;
        }
    }

    public class Node : IHeapItem<Node>
    {
        public bool Walkable
        {
            get { return Map.EmptyCell(Location); }
        }

        public MapControl Map;
        public Point Location;
        public Node Parent;

        public int GCost, HCost;

        public int FCost
        {
            get { return GCost + HCost; }
        }

        //★11-03：这一代寻路的代号。同一张地图的 Node 会被反复复用，
        //  上一轮留下的 GCost/Parent/HeapIndex 必须在新的寻路开始前清掉，
        //  否则第二次寻路所有格子都「已经更优」→ 一个节点都不入队 → 直接返回 null，
        //  表现就是「大地图右键寻路没任何反应」。
        public int Stamp;
        public bool InOpen;
        public bool Closed;

        private int _heapIndex;

        public int HeapIndex
        {
            get { return _heapIndex; }
            set { _heapIndex = value; }
        }

        public int CompareTo(Node nodeToCompare)
        {
            int compare = FCost.CompareTo(nodeToCompare.FCost);
            if (compare == 0)
            {
                compare = HCost.CompareTo(nodeToCompare.HCost);
            }
            return -compare;
        }

        public Node(MapControl map, int x, int y)
        {
            Map = map;
            Location = new Point(x, y);
        }
    }

    public class PathFinder
    {
        private Node[,] Grid;

        public MapControl Map;

        //★11-03：寻路代数。每调用一次 FindPath +1，Node 靠它判断「自己是不是这一轮的数据」。
        private int _stamp;

        //本轮寻路的阻挡物快照（一次遍历 Objects，寻路过程中 O(1) 查询）
        private readonly HashSet<long> _blockers = new HashSet<long>();

        /// <summary>
        /// ★展开上限（11-03）：目标点在墙里/隔着一片过不去的区域时，A* 会把整张图翻一遍。
        /// 1000×800 的地图就是 80 万格 × 8 邻格 —— 客户端直接定住几秒，然后告诉你「无路线」。
        /// 超过上限就提前收手，退回「已经走到的最接近目标的那一格」的路径，至少人是往前走的。
        /// </summary>
        private const int MaxExpansions = 60000;

        public int MaxSize
        {
            get { return Map.Width * Map.Height; }
        }

        public PathFinder(MapControl map)
        {
            Map = map;

            CreateGrid();
        }

        /// <summary>
        /// ★惰性建格（10-31）：原来进图时这里会按 W×H 逐格 new Node —— 900×760 的地图就是
        /// 68 万个对象（约 40MB）一次性分配，主线程直接停住几百毫秒，这就是「进图卡一会儿」的主体之一。
        /// 改成只分配引用数组（约 5MB，一次性，很快），Node 真正被 A* 用到时才创建：
        /// 玩家一次寻路只碰几千个格子，绝大多数格子这辈子都不会有 Node。
        /// </summary>
        private void CreateGrid()
        {
            Grid = new Node[Map.Width, Map.Height];
        }

        public List<Node> FindPath(Point start, Point target, int MaxNodes = 0)
        {
            if (Map == null || Grid == null || Map.M2CellInfo == null) return null;

            int width = Grid.GetLength(0);
            int height = Grid.GetLength(1);

            if (width <= 0 || height <= 0) return null;
            if (start.X < 0 || start.Y < 0 || start.X >= width || start.Y >= height) return null;
            if (target.X < 0 || target.Y < 0 || target.X >= width || target.Y >= height) return null;

            //新的一轮：代数 +1（让所有 Node 的数据作废重建），并快照当前阻挡物。
            _stamp++;
            Map.SnapshotBlockingCells(_blockers);

            //点到怪/NPC/玩家身上时那一格是「不可站」的，直接当终点会必然失败。
            //就近挑一个能站的格子当终点（最多 3 格内），寻路体验才正常。
            Point goal;
            if (!TryResolveGoal(target, out goal)) return null;

            Node startNode = GetNode(start);
            Node targetNode = GetNode(goal);

            //★起始容量给小、按需扩容（10-31）：原来传 MaxSize（W×H，90 万格就是 90 万槽位 ≈ 7MB），
            //  而一次寻路实际只用到几千个节点 —— 每点一次地面都白分配一次 7MB，GC 压力全砸在画面帧上。
            Heap<Node> openSet = new Heap<Node>(512);

            startNode.GCost = 0;
            startNode.HCost = GetDistance(startNode, targetNode);
            startNode.InOpen = true;
            openSet.Add(startNode);

            //走到哪一格离目标最近（超过展开上限时用它兜底，而不是直接「无路线」）
            Node closest = startNode;

            int expansions = 0;

            while (openSet.Count > 0)
            {
                Node currentNode = openSet.RemoveFirst();
                currentNode.InOpen = false;
                currentNode.Closed = true;

                if (currentNode == targetNode)
                    return BuildResult(startNode, targetNode, MaxNodes);

                if (currentNode.HCost < closest.HCost)
                    closest = currentNode;

                if (++expansions >= MaxExpansions)
                    break;

                //★不再 List<Node> GetNeighbours（每个节点 new 一个 List，几十万次 → GC 抖成幻灯片）
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = currentNode.Location.X + dx;
                        int ny = currentNode.Location.Y + dy;

                        if (!Walkable(nx, ny)) continue;

                        Node neighbor = GetNode(new Point(nx, ny));
                        if (neighbor.Closed) continue;

                        int newMovementCostToNeighbor = currentNode.GCost + ((dx != 0 && dy != 0) ? 14 : 10);
                        if (neighbor.InOpen && newMovementCostToNeighbor >= neighbor.GCost) continue;

                        neighbor.GCost = newMovementCostToNeighbor;
                        neighbor.HCost = GetDistance(neighbor, targetNode);
                        neighbor.Parent = currentNode;

                        if (!neighbor.InOpen)
                        {
                            //★用 InOpen 标记代替 openSet.Contains()：原来的 Contains 靠 HeapIndex 反查，
                            //  跨寻路残留的 HeapIndex 会越界/误判，是「第二条路直接算不出来」的另一个原因。
                            neighbor.InOpen = true;
                            openSet.Add(neighbor);
                        }
                        else
                        {
                            openSet.UpdateItem(neighbor);
                        }
                    }
                }
            }

            //没能走到终点：如果已经摸到过比起点更接近目标的位置，就把那段路给它（至少往前走，别原地不动）
            if (closest != startNode && closest.HCost < startNode.HCost)
                return BuildResult(startNode, closest, MaxNodes);

            return null;
        }

        private List<Node> BuildResult(Node startNode, Node endNode, int MaxNodes)
        {
            List<Node> path = RetracePath(startNode, endNode);

            if (path.Count == 0) return null;
            if (MaxNodes != 0 && path.Count >= MaxNodes) return null;

            return path;
        }

        public List<Node> RetracePath(Node startNode, Node endNode)
        {
            List<Node> path = new List<Node>();
            Node currentNode = endNode;

            //兜底：Parent 链断了也不能死循环（跨寻路残留时出现过）
            int guard = 0;
            int limit = Grid.GetLength(0) * Grid.GetLength(1) + 4;

            while (currentNode != null && currentNode != startNode && guard++ < limit)
            {
                path.Add(currentNode);
                currentNode = currentNode.Parent;
            }

            if (currentNode != startNode) return new List<Node>();

            path.Add(startNode);
            path.Reverse();

            return path;
        }

        private int GetDistance(Node nodeA, Node nodeB)
        {
            int distX = Math.Abs(nodeA.Location.X - nodeB.Location.X);
            int distY = Math.Abs(nodeA.Location.Y - nodeB.Location.Y);

            if (distX > distY)
                return 14 * distY + (10 * (distX - distY));

            return 14 * distX + (10 * (distY - distX));
        }

        /// <summary>该格能不能走：地图阻挡标记 + 本轮的阻挡物快照。全程 O(1)。</summary>
        private bool Walkable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height) return false;
            if ((Map.M2CellInfo[x, y].BackImage & 0x20000000) != 0) return false;
            if ((Map.M2CellInfo[x, y].FrontImage & 0x8000) != 0) return false;

            return !_blockers.Contains((((long)x) << 32) | ((uint)y));
        }

        /// <summary>终点被占（怪/NPC/玩家站着）时，在 3 格内就近找一个能站的格子。</summary>
        private bool TryResolveGoal(Point target, out Point goal)
        {
            goal = target;

            if (Walkable(target.X, target.Y)) return true;

            for (int r = 1; r <= 3; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;

                        int x = target.X + dx;
                        int y = target.Y + dy;

                        if (!Walkable(x, y)) continue;

                        goal = new Point(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        private Node GetNode(Point location)
        {
            Node node = Grid[location.X, location.Y];

            if (node == null)
            {
                //★按需创建：只有 A* 真正走到的格子才产生 Node
                node = new Node(Map, location.X, location.Y);
                Grid[location.X, location.Y] = node;
            }

            if (node.Stamp != _stamp)
            {
                //★新一轮寻路：把上一轮留下的代价/父节点/堆位置全部清掉（见 Node.Stamp 的注释）
                node.Stamp = _stamp;
                node.GCost = 0;
                node.HCost = 0;
                node.Parent = null;
                node.InOpen = false;
                node.Closed = false;
                node.HeapIndex = 0;
            }

            return node;
        }
    }
}
