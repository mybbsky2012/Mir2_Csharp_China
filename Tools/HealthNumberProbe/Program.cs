using System.Reflection;
using System.Text;

namespace HealthNumberProbe;

/// <summary>
/// 只读验证「数字显血」编译进了真实的 Client.dll：
/// 1) Settings.HealthNumber 存在且默认 false；
/// 2) AutoPlayDialog 有 HealthNumberBox / ItemFilterBox 字段；
/// 3) MapObject 有私有的 DrawHealthNumber 方法与 CurrentHP/MaxHP 字段；
/// 4) 共享程序集里的 S.ObjectHealth 带 CurrentHP/MaxHP。
/// 不实例化 UI、不写配置、不连服务器。
/// 用法：HealthNumberProbe.exe "E:\micromir2"
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                                      ?? Directory.GetCurrentDirectory());
        string dll = Path.Combine(dir, "Client.dll");
        if (!File.Exists(dll)) { Console.WriteLine("找不到 Client.dll: " + dll); return 2; }

        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string path = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };

        Assembly client = Assembly.LoadFrom(dll);

        int fail = 0;

        void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "[OK]   " : "[FAIL] ") + name);
            if (!ok) fail++;
        }

        Type settings = client.GetType("Client.Settings", false);
        Check("Client.Settings 类型存在", settings != null);

        FieldInfo healthNumber = settings?.GetField("HealthNumber", BindingFlags.Public | BindingFlags.Static);
        Check("Settings.HealthNumber 字段存在", healthNumber != null);
        if (healthNumber != null)
        {
            // 只读字段值；Settings 的静态构造在第一次访问静态成员时已触发，
            // 这里不调用 Load/Save，不改用户配置
            object v = healthNumber.GetValue(null);
            Check("Settings.HealthNumber 默认 false（未勾选）", v is bool b && !b);
        }

        Type dialog = client.GetType("Client.MirScenes.Dialogs.AutoPlayDialog", false);
        Check("AutoPlayDialog 类型存在", dialog != null);
        if (dialog != null)
        {
            Check("AutoPlayDialog.HealthNumberBox 字段存在",
                  dialog.GetField("HealthNumberBox", BindingFlags.Public | BindingFlags.Instance) != null);
            Check("AutoPlayDialog.ItemFilterBox 字段存在",
                  dialog.GetField("ItemFilterBox", BindingFlags.Public | BindingFlags.Instance) != null);
        }

        Type mapObject = client.GetType("Client.MirObjects.MapObject", false);
        Check("MapObject 类型存在", mapObject != null);
        if (mapObject != null)
        {
            Check("MapObject.DrawHealthNumber 私有方法存在",
                  mapObject.GetMethod("DrawHealthNumber", BindingFlags.NonPublic | BindingFlags.Instance) != null);
            Check("MapObject.CurrentHP/MaxHP 字段存在",
                  mapObject.GetField("CurrentHP") != null && mapObject.GetField("MaxHP") != null);
            Check("MapObject.HealthNumberLabel 字段存在",
                  mapObject.GetField("HealthNumberLabel") != null);

            // 共享程序集里的 ObjectHealth 必须带上绝对值血量字段
            Assembly shared = Assembly.LoadFrom(Path.Combine(dir, "Shared.dll"));
            Type health = shared.GetType("ServerPackets.ObjectHealth", true);
            Check("S.ObjectHealth 有 CurrentHP/MaxHP 字段",
                  health != null && health.GetField("CurrentHP") != null && health.GetField("MaxHP") != null);
        }

        Console.WriteLine(fail == 0 ? "全部通过" : $"有 {fail} 项失败");
        return fail == 0 ? 0 : 1;
    }
}
