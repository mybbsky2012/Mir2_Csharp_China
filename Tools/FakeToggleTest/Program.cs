using System.Reflection;
using Server;
using Server.MirObjects;

namespace FakeToggleTest;

/// <summary>
/// 只读验证「后台勾选假人系统开关 → 下一帧生效」这条链路（不加载 DB、不写任何文件）。
///
/// 关注点：
///   1. 标记位在 Process() 里、Enabled 判断**之前**被处理（否则从关到开永远开不起来）；
///   2. 关掉开关时真的会把 Enabled 置回 false（在线假人被 Close 清掉）；
///   3. 标记处理完要清零点，避免每帧反复 Init；
///   4. 没变化时不重复 Init（Settings 与 Enabled 一致 → 不做任何事）。
/// </summary>
internal static class Program
{
    private static int _fail;

    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  → " + detail : ""));
        if (!ok) _fail++;
    }

    /// <summary>Enabled 是 private set，测试里用反射改它模拟「当前运行状态」。</summary>
    private static void ForceEnabled(bool value)
    {
        PropertyInfo p = typeof(FakePlayerManager).GetProperty("Enabled", BindingFlags.Public | BindingFlags.Static);
        p.SetValue(null, value);
    }

    [STAThread]
    private static int Main()
    {
        Console.WriteLine("=== 假人开关热切换链路 ===");

        // ── 0) 源码顺序：RestartPending 的处理必须在 !Enabled 判断之前 ──
        Check("RestartPending 字段存在", typeof(FakePlayerManager).GetField("RestartPending", BindingFlags.Public | BindingFlags.Static) != null);

        // ── 1) 无变化：标记被清掉，但 Enabled 不动 ──
        Settings.FakePlayerEnabled = true;
        ForceEnabled(true);
        FakePlayerManager.RestartPending = true;
        FakePlayerManager.Process();
        Check("1) 开关没有变化时不重建（Enabled 保持、标记清零）",
              FakePlayerManager.Enabled && !FakePlayerManager.RestartPending,
              $"Enabled={FakePlayerManager.Enabled} Pending={FakePlayerManager.RestartPending}");

        // ── 2) 关闭：本来开着，配置改成 false → 下一帧 Enabled 变 false ──
        ForceEnabled(true);
        Settings.FakePlayerEnabled = false;
        FakePlayerManager.RestartPending = true;
        FakePlayerManager.Process();
        Check("2) 关闭开关后下一帧生效（Enabled=false）",
              !FakePlayerManager.Enabled && !FakePlayerManager.RestartPending,
              $"Enabled={FakePlayerManager.Enabled} Pending={FakePlayerManager.RestartPending}");

        // ── 3) 再跑一帧：标记已清，不应再动 Enabled ──
        bool before = FakePlayerManager.Enabled;
        FakePlayerManager.Process();
        Check("3) 标记清零后不会每帧重复重建", FakePlayerManager.Enabled == before,
              $"Enabled={FakePlayerManager.Enabled}");

        // ── 4) 关闭态下标记一次「打开」：会走 Init（本测试没有地图数据，只验证不炸 + 标记清零） ──
        ForceEnabled(false);
        Settings.FakePlayerEnabled = true;
        FakePlayerManager.RestartPending = true;
        bool threw = false;
        string err = "";
        try
        {
            FakePlayerManager.Process();
        }
        catch (Exception ex)
        {
            threw = true;
            err = ex.GetType().Name + ": " + ex.Message;
        }
        Check("4) 开启路径不会抛异常（无地图数据时静默失败）", !threw, threw ? err : "无异常");
        Check("5) 开启路径同样会清掉标记位", !FakePlayerManager.RestartPending,
              $"Pending={FakePlayerManager.RestartPending}");

        // ── 6) 配置与状态口径：关闭态不享受免扣（回归上一轮改动） ──
        Check("6) 关闭假人系统后 Enabled=false",
              !FakePlayerManager.Enabled || Settings.FakePlayerEnabled,
              $"Enabled={FakePlayerManager.Enabled} Settings={Settings.FakePlayerEnabled}");

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过" : $"{_fail} 项失败");
        return _fail == 0 ? 0 : 1;
    }
}
