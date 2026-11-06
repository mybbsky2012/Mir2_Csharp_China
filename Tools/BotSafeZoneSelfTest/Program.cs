using System;
using System.Collections.Generic;

// 复刻 FakePlayerManager 登录落点算法（20261106 秒飞版）做纯逻辑自测。
// 注意：改动算法时这里要同步（文件头注释标明）。
class Program
{
    const int MapSize = 200;                     // 模拟 200x200 地图
    static readonly Random rnd = new Random(42);
    static readonly HashSet<long> walkable = new HashSet<long>();
    // 安全区：中心 (100,100)，半径 10
    static bool InSafeZone(int x, int y) => Math.Abs(x - 100) <= 10 && Math.Abs(y - 100) <= 10;

    static void Main()
    {
        // 铺可走格子（留 5% 墙）
        for (int x = 0; x < MapSize; x++)
            for (int y = 0; y < MapSize; y++)
                if (rnd.Next(100) >= 5) walkable.Add((long)x * MapSize + y);

        bool ValidPoint(int x, int y) => walkable.Contains((long)x * MapSize + y);

        // === 复刻 PickSpawnPoint（随机可走点，避开安全区）===
        Point PickSpawnPoint()
        {
            for (int i = 0; i < 200; i++)
            {
                int x = rnd.Next(MapSize), y = rnd.Next(MapSize);
                if (!ValidPoint(x, y) || InSafeZone(x, y)) continue;
                return new Point(x, y);
            }
            return new Point(-1, -1);
        }

        // === 复刻 ShouldStayInSafeZone（配额 3~5，计数在线且在安全区的假人）===
        List<Point> online = new List<Point>();
        bool ShouldStay()
        {
            int quota = 3 + rnd.Next(3);
            int count = 0;
            foreach (var p in online)
            {
                if (InSafeZone(p.X, p.Y)) { count++; if (count >= quota) return false; }
            }
            return true;
        }

        // === 复刻登录落点决策 ===
        Point LoginSpot()
        {
            if (ShouldStay()) return new Point(100, 100);      // 留守：安全区中心
            return PickSpawnPoint();                            // 秒飞：随机全图
        }

        int stayed = 0, flown = 0, fail = 0;
        for (int i = 0; i < 2000; i++)
        {
            Point p = LoginSpot();
            if (p.IsEmpty) { fail++; continue; }
            online.Add(p);
            if (InSafeZone(p.X, p.Y)) stayed++; else flown++;
        }

        Console.WriteLine($"[模拟] 2000 次登录：留守安全区 {stayed}，秒飞散图 {flown}，落点失败 {fail}");
        Console.WriteLine(stayed >= 3 && stayed <= 12 && fail == 0
            ? "[通过] 安全区留守人数在 3~5 配额的稳定范围（含上下浮动），其余全部散到图上"
            : "[失败] 配额行为异常");
        Console.WriteLine(flown > 1900 ? "[通过] 绝大多数假人秒飞随机落图" : "[失败] 秒飞比例异常");
    }

    struct Point { public int X, Y; public static readonly Point Empty = new Point(-1, -1); public bool IsEmpty => X == -1 && Y == -1; public Point(int x, int y) { X = x; Y = y; } }
}
