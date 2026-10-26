using System.Reflection;
using System.Text;

namespace StoneRenewTest;

/// <summary>
/// 内挂「自动续气血石」验证（只读，不实例化 UI、不写配置、不连服务器）：
///  A. 结构：Settings.AutoRenewStone、AutoPlayDialog.RenewStoneBox、GameScene 的四个方法都在
///  B. IL  ：AutoPlayRenewStone 里确实发了 C.EquipItem 且 To = (int)EquipmentSlot.守护石(12)
///  C. 行为：AutoPlayPickLifeStone 的挑选优先级（同款 &gt; 幻血 &gt; 另一款，同档挑电量最大；0 电/Shape=0/普通守护石排除）
///  D. 源码：Process() 调用点、「装备保护」不再修宝石位、面板 358 行复选框、Settings 落盘键
/// 用法：StoneRenewTest.exe "D:\mir2-20241027\Build\Client\Release"
/// </summary>
internal static class Program
{
    private static int _fail;
    private static readonly string[] SourceRoots =
    {
        @"D:\mir2-20241027\Client\",
    };

    private static Type _itemTypeEnum;      // ItemType
    private static Type _itemInfoType;      // ItemInfo
    private static Type _userItemType;      // UserItem
    private static MethodInfo _pick;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                                      ?? @"D:\mir2-20241027\Build\Client\Release");
        string dll = Path.Combine(dir, "Client.dll");
        if (!File.Exists(dll)) { Console.WriteLine("找不到 Client.dll: " + dll); return 2; }

        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string path = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };

        Console.WriteLine("=== 被测程序集：" + dll);
        Assembly client = Assembly.LoadFrom(dll);
        Assembly shared = Assembly.LoadFrom(Path.Combine(dir, "Shared.dll"));

        _itemTypeEnum = shared.GetType("ItemType", true);
        _itemInfoType = shared.GetType("ItemInfo", true);
        _userItemType = shared.GetType("UserItem", true);

        Section("A. 结构");
        CheckStructure(client);

        Section("B. 发包（IL）");
        CheckIl(client);

        Section("C. 挑石逻辑（行为）");
        CheckPick(client);

        Section("D. 源码/界面");
        CheckSource(client);

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过" : $"有 {_fail} 项失败");
        return _fail == 0 ? 0 : 1;
    }

    // ────────────────────────────────────────────────
    private static void CheckStructure(Assembly client)
    {
        Type settings = client.GetType("Client.Settings", false);
        Check("Client.Settings 类型存在", settings != null);

        FieldInfo renew = settings?.GetField("AutoRenewStone", BindingFlags.Public | BindingFlags.Static);
        Check("Settings.AutoRenewStone 字段存在", renew != null);
        if (renew != null)
        {
            object v = renew.GetValue(null);
            Check("AutoRenewStone 默认 false（不打扰现有玩家）", v is bool b && !b, "实际=" + v);
        }

        Type dialog = client.GetType("Client.MirScenes.Dialogs.AutoPlayDialog", false);
        Check("AutoPlayDialog 类型存在", dialog != null);
        if (dialog != null)
        {
            FieldInfo box = dialog.GetField("RenewStoneBox", BindingFlags.Public | BindingFlags.Instance);
            Check("AutoPlayDialog.RenewStoneBox 字段存在", box != null);
            Check("RenewStoneBox 类型是 MirCheckBox", box != null && box.FieldType.Name == "MirCheckBox",
                  box == null ? "" : box.FieldType.Name);
        }

        // ★内挂逻辑全在 MapControl 里（与 GameScene 同文件，10849 行起是另一个类）
        Type scene = client.GetType("Client.MirScenes.MapControl", false);
        Check("MapControl 类型存在（内挂逻辑宿主）", scene != null);

        MethodInfo renewMethod = scene?.GetMethod("AutoPlayRenewStone", BindingFlags.NonPublic | BindingFlags.Instance);
        Check("MapControl.AutoPlayRenewStone 方法存在", renewMethod != null);

        MethodInfo isLife = scene?.GetMethod("AutoPlayIsLifeStone", BindingFlags.NonPublic | BindingFlags.Static);
        Check("MapControl.AutoPlayIsLifeStone 方法存在", isLife != null);

        MethodInfo find = scene?.GetMethod("AutoPlayFindLifeStone", BindingFlags.NonPublic | BindingFlags.Instance);
        Check("MapControl.AutoPlayFindLifeStone 方法存在", find != null);

        _pick = scene?.GetMethod("AutoPlayPickLifeStone", BindingFlags.NonPublic | BindingFlags.Static);
        Check("MapControl.AutoPlayPickLifeStone 方法存在（纯逻辑）", _pick != null);
    }

    private static void CheckIl(Assembly client)
    {
        Type scene = client.GetType("Client.MirScenes.MapControl", false);
        MethodInfo m = scene?.GetMethod("AutoPlayRenewStone", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Check("AutoPlayRenewStone 可取到方法体", false); return; }

        byte[] il = m.GetMethodBody()?.GetILAsByteArray();
        if (il == null) { Check("AutoPlayRenewStone 可取到方法体", false); return; }
        Check("AutoPlayRenewStone 可取到方法体", true, il.Length + " 字节");

        // 1) 必须发 C.EquipItem
        bool hasEquip = false;
        // 2) 必须出现常量 12（守护石槽）
        bool hasSlot12 = false;
        // 3) 必须写 EquipItem.To
        bool hasToField = false;

        for (int i = 0; i < il.Length; i++)
        {
            byte op = il[i];

            if (op == 0x73 && i + 4 < il.Length)          // newobj
            {
                int token = BitConverter.ToInt32(il, i + 1);
                try
                {
                    MethodBase ctor = m.Module.ResolveMethod(token);
                    if (ctor != null && ctor.DeclaringType != null && ctor.DeclaringType.Name == "EquipItem") hasEquip = true;
                }
                catch { }
            }
            else if (op == 0x7D && i + 4 < il.Length)     // stfld
            {
                int token = BitConverter.ToInt32(il, i + 1);
                try
                {
                    FieldInfo f = m.Module.ResolveField(token);
                    if (f != null && f.Name == "To" && f.DeclaringType != null && f.DeclaringType.Name == "EquipItem") hasToField = true;
                }
                catch { }
            }
            else if (op == 0x1F && i + 1 < il.Length && il[i + 1] == 12) hasSlot12 = true;   // ldc.i4.s 12
            else if (op == 0x20 && i + 4 < il.Length && BitConverter.ToInt32(il, i + 1) == 12) hasSlot12 = true; // ldc.i4 12
        }

        Check("用的是 EquipItem 发包（不是 UseItem）", hasEquip);
        Check("写着 EquipItem.To（指定装备槽）", hasToField);
        Check("装备槽常量 = 12（EquipmentSlot.守护石）", hasSlot12);
    }

    private static void CheckPick(Assembly client)
    {
        if (_pick == null) { Check("AutoPlayPickLifeStone 可获得", false); return; }

        // 1) 不知道原款时（第一次自动装）：优先回血类
        var a = MakeItem("气血石", 1, 60000, 60000, 101);
        var b = MakeItem("魔血石", 2, 60000, 60000, 102);
        Check("want=0：优先回血类（魔血石排前面也不选它）", Picked(Pack(b, a), 0) == 101);

        var magicOnly = MakeItem("魔血石", 2, 60000, 60000, 201);
        Check("want=0：背包只有魔血石也照装（不挑三拣四）", Picked(Pack(magicOnly), 0) == 201);

        // 2) 原款是气血石：跳过排在前面的魔血石
        Check("want=1(气血)：跳过前排魔血石，选后面的气血石", Picked(Pack(b, a), 1) == 101);

        // 3) 原款是魔血石：跳过排在前面的气血石
        Check("want=2(魔血)：跳过前排气血石，选后面的魔血石", Picked(Pack(a, b), 2) == 102);

        // 4) 原款是气血石，背包只有幻血石 → 兜底用幻血石
        var f = MakeItem("气血石", 3, 60000, 60000, 103);
        Check("want=1：没有同款时用幻血石顶", Picked(Pack(f, b), 1) == 103);

        // 5) 原款是幻血石：优先同款
        Check("want=3(幻血)：优先同款幻血石", Picked(Pack(a, f), 3) == 103);

        // 6) 0 电的排前面也要跳过
        var dead = MakeItem("气血石", 1, 60000, 0, 104);
        Check("0 电的石头被跳过，选有电的那块", Picked(Pack(dead, a), 1) == 101);

        // 7) 全是 0 电 → 不选
        Check("全 0 电 → 返回 null（不乱发换装）", Picked(Pack(dead), 0) == 0);

        // 8) Shape=0 的气血石不算（库里没填 Shape 的脏数据）
        var shape0 = MakeItem("气血石", 0, 60000, 60000, 105);
        Check("Shape=0 的气血石不算（避免换上不生效的石头）", Picked(Pack(shape0), 0) == 0);

        // 9) 普通守护石（芷心石）不碰
        var guard = MakeItem("守护石", 0, 60000, 60000, 106);
        Check("普通守护石（芷心石一类）不参与续石", Picked(Pack(guard), 0) == 0);

        // 10) 同款两块：取电量大的
        var small = MakeItem("气血石", 1, 60000, 1234, 107);
        var big = MakeItem("气血石", 1, 60000, 59000, 108);
        Check("同款多块：取电量最大的", Picked(Pack(small, big), 1) == 108);

        // 11) 空背包
        Check("空背包 → null", Picked(Pack(), 0) == 0);

        // 12) 混合：气血石(0电) + 魔血石(有电) + 幻血石(有电)，want=1 → 幻血石（score2 胜过魔血 score1）
        var mixDead = MakeItem("气血石", 1, 60000, 0, 109);
        var mixMagic = MakeItem("魔血石", 2, 60000, 40000, 110);
        var mixAll = MakeItem("气血石", 3, 60000, 30000, 111);
        Check("want=1：0电气血 → 幻血(2) 胜过魔血(1)", Picked(Pack(mixDead, mixMagic, mixAll), 1) == 111);
    }

    private static void CheckSource(Assembly client)
    {
        string scene = ReadFirst("MirScenes\\GameScene.cs");
        string dialog = ReadFirst("MirScenes\\Dialogs\\AutoPlayDialog.cs");
        string settings = ReadFirst("Settings.cs");

        Check("AutoPlayRenewStone 首行有开关判断", scene.Contains("if (!Settings.AutoRenewStone) return;"));
        Check("Process() 里调用了 AutoPlayRenewStone",
              scene.Contains("AutoPlayRepairEquipment();") &&
              scene.IndexOf("AutoPlayRenewStone();", StringComparison.Ordinal) >
              scene.IndexOf("AutoPlayRepairEquipment();", StringComparison.Ordinal));
        Check("装备保护不再把宝石位当装备去修",
              scene.Contains("if (i == (int)EquipmentSlot.守护石) continue;"));
        Check("续石只看宝石位（EquipmentSlot.守护石）",
              scene.Contains("User.Equipment[(int)EquipmentSlot.守护石]"));
        Check("续石有 3 秒防重复发包", scene.Contains("_autoStoneLastID") && scene.Contains("_autoStoneLastTime"));
        Check("面板保护页有复选框（y=358）",
              dialog.Contains("CreateBox(14, 358, \"自动续气血石"));
        Check("面板 UpdateState 会同步勾选状态",
              dialog.Contains("RenewStoneBox.Checked = Settings.AutoRenewStone"));
        Check("设置里有落盘键 AutoRenewStone",
              settings.Contains("Reader.Write(\"AutoPlay\", \"AutoRenewStone\", AutoRenewStone)") &&
              settings.Contains("Reader.ReadBoolean(\"AutoPlay\", \"AutoRenewStone\", AutoRenewStone)"));
    }

    // ────────────────────────────────────────────────
    private static object MakeItem(string typeName, short shape, ushort maxDura, ushort curDura, ulong uid)
    {
        object info = Activator.CreateInstance(_itemInfoType);
        _itemInfoType.GetField("Type").SetValue(info, Enum.Parse(_itemTypeEnum, typeName));
        _itemInfoType.GetField("Shape").SetValue(info, shape);
        _itemInfoType.GetField("Durability").SetValue(info, maxDura);
        _itemInfoType.GetField("Name").SetValue(info, typeName + "_" + uid);

        object item = Activator.CreateInstance(_userItemType, new[] { info });
        _userItemType.GetField("CurrentDura").SetValue(item, curDura);
        _userItemType.GetField("MaxDura").SetValue(item, maxDura);
        _userItemType.GetField("UniqueID").SetValue(item, uid);

        return item;
    }

    private static Array Pack(params object[] items)
    {
        Array arr = Array.CreateInstance(_userItemType, items.Length);
        for (int i = 0; i < items.Length; i++) arr.SetValue(items[i], i);
        return arr;
    }

    private static ulong Picked(Array inv, int want)
    {
        object r = _pick.Invoke(null, new object[] { inv, (short)want });
        if (r == null) return 0;
        return (ulong)_userItemType.GetField("UniqueID").GetValue(r);
    }

    private static string ReadFirst(string relative)
    {
        foreach (string root in SourceRoots)
        {
            string p = Path.Combine(root, relative);
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        return string.Empty;
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("── " + title + " " + new string('─', Math.Max(0, 46 - title.Length * 2)));
    }

    private static void Check(string name, bool ok, string detail = null)
    {
        if (!ok) _fail++;
        Console.WriteLine((ok ? "  [OK]   " : "  [FAIL] ") + name + (string.IsNullOrEmpty(detail) ? "" : "  → " + detail));
    }
}
