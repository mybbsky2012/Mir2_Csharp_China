using System.Reflection;
using System.Text;

namespace ItemFilterProbe;

/// <summary>
/// 只读验证客户端「物品过滤」的判定逻辑：直接反射调用真实的 Client.dll 里的 ItemFilter.ShouldShow。
/// 不改配置、不连服务器，只读 Data\ItemFilter.txt。
/// 用法：ItemFilterProbe.exe "E:\micromir2"
/// </summary>
internal static class Program
{
    private static string ClientDir;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        ClientDir = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Directory.GetCurrentDirectory());
        if (!File.Exists(Path.Combine(ClientDir, "Client.dll"))) { Console.WriteLine("找不到 Client.dll: " + ClientDir); return 2; }

        Directory.SetCurrentDirectory(ClientDir); // Settings 用相对路径 .\Mir2Config.ini / .\Data\

        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            string path = Path.Combine(ClientDir, name);
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };

        Assembly client;
        try
        {
            client = Assembly.LoadFrom(Path.Combine(ClientDir, "Client.dll"));
        }
        catch (Exception ex)
        {
            Console.WriteLine("加载 Client.dll 失败: " + ex.Message);
            return 2;
        }

        Type settingsType = client.GetType("Client.Settings", true);
        Type filterType = client.GetType("Client.ItemFilter", true);
        Type itemObjectType = client.GetType("Client.MirObjects.ItemObject", true);
        if (settingsType == null || filterType == null || itemObjectType == null)
        {
            Console.WriteLine("找不到 Settings / ItemFilter / ItemObject 类型");
            return 2;
        }

        // 打开开关（等价于内挂里勾选「物品过滤」）
        settingsType.GetField("ItemFilterOn", BindingFlags.Public | BindingFlags.Static).SetValue(null, true);

        MethodInfo reload = filterType.GetMethod("Reload", BindingFlags.Public | BindingFlags.Static);
        MethodInfo shouldShow = filterType.GetMethod("ShouldShow", BindingFlags.Public | BindingFlags.Static);
        reload.Invoke(null, null);

        int names = (int)filterType.GetProperty("NameCount", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        object minGrade = filterType.GetProperty("MinGrade", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        string file = (string)filterType.GetProperty("CurrentFile", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        Console.WriteLine($"[列表] {file}");
        Console.WriteLine($"[列表] 名字 {names} 条，品质下限 = {minGrade}");
        Console.WriteLine();

        if (names == 0 && minGrade.ToString() == "None")
        {
            Console.WriteLine("!! 列表为空，过滤会自动失效（不过滤），请检查 ItemFilter.txt");
            return 1;
        }

        int fail = 0;

        // 样例：普通 / 宝物 / 圣物 / 金币 / 名字白名单
        fail += Check(itemObjectType, shouldShow, "肉", 1, false, "普通品质应被过滤掉");
        fail += Check(itemObjectType, shouldShow, "井中月", 2, true, "宝物品质应显示");
        fail += Check(itemObjectType, shouldShow, "魔血剑", 3, true, "圣物品质应显示");
        fail += Check(itemObjectType, shouldShow, "独孤九剑", 4, true, "神物品质应显示");
        fail += Check(itemObjectType, shouldShow, "英雄封印", 5, true, "英雄品质应显示");
        fail += Check(itemObjectType, shouldShow, "金币 (1,234)", 0, true, "金币在示例列表里，应显示");
        fail += Check(itemObjectType, shouldShow, "金创药(大) (3)", 1, false, "不在列表里的普通物品应隐藏");

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "全部通过 ✓ 客户端过滤判定正确" : $"失败 {fail} 项 ✗");
        return fail == 0 ? 0 : 1;
    }

    private static int Check(Type itemObjectType, MethodInfo shouldShow, string name, int grade, bool expect, string note)
    {
        object ob = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(itemObjectType);
        itemObjectType.GetField("Name").SetValue(ob, name);

        FieldInfo gradeField = itemObjectType.GetField("Grade");
        gradeField.SetValue(ob, Enum.ToObject(gradeField.FieldType, grade));

        bool actual = (bool)shouldShow.Invoke(null, new[] { ob });
        bool ok = actual == expect;
        Console.WriteLine($"[{(ok ? "OK" : "!!")}] {name} 品质={gradeField.GetValue(ob)} → 显示={actual}（期望 {expect}）  {note}");
        return ok ? 0 : 1;
    }
}
