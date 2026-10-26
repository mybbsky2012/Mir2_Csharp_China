using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace HeroAutoPlayTest;

/// <summary>
/// 内挂「装备保护（自动使用修复物品）」与「自动续气血石」支持英雄 —— 验证（只读，不连服务器）。
///
///  A. 服务端能力：英雄走 HumanObject 的石头处理、卷轴 Shape=23 / 特殊消耗品 Shape=84 修的是英雄自己的装备、
///     英雄背包的使用包与装备包都认得
///  B. 客户端结构：玩家/英雄两套入口方法都在，状态是两组
///  C. 客户端 IL ：两个入口确实对「玩家 + 英雄」各调一次；发包的 Grid 来自参数（英雄＝HeroInventory）
///  D. 客户端行为：挑石逻辑对英雄背包同样成立；宝石位 0 电不会被当成「要修的装备」
///  E. 界面：提示文案与位置
///
/// 用法：HeroAutoPlayTest.exe [Client产物目录] [Server源码目录]
/// </summary>
internal static class Program
{
    private static int _fail;

    private static Assembly _client;
    private static Assembly _shared;
    private static Type _mapControl;
    private static Type _itemTypeEnum;
    private static Type _itemInfoType;
    private static Type _userItemType;
    private static Type _settingsType;
    private static Type _equipSlotEnum;

    private static string _clientDir = @"D:\mir2-20241027\Build\Client\Release";
    private static string _srcRoot = @"D:\mir2-20241027";

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string[] plain = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (plain.Length > 0) _clientDir = Path.GetFullPath(plain[0]);
        if (plain.Length > 1) _srcRoot = Path.GetFullPath(plain[1]);

        string dll = Path.Combine(_clientDir, "Client.dll");
        if (!File.Exists(dll)) { Console.WriteLine("找不到 Client.dll: " + dll); return 2; }

        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string path = Path.Combine(_clientDir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };

        Console.WriteLine("=== Client：" + dll);
        Console.WriteLine("=== 源码根：" + _srcRoot);

        _client = Assembly.LoadFrom(dll);
        _shared = Assembly.LoadFrom(Path.Combine(_clientDir, "Shared.dll"));

        _mapControl = _client.GetType("Client.MirScenes.MapControl", false);
        _settingsType = _client.GetType("Client.Settings", false);
        _itemTypeEnum = _shared.GetType("ItemType", true);
        _itemInfoType = _shared.GetType("ItemInfo", true);
        _userItemType = _shared.GetType("UserItem", true);
        _equipSlotEnum = _shared.GetType("EquipmentSlot", true);

        Section("A. 服务端：英雄本来就能用");
        CheckServer();

        Section("B. 客户端结构");
        CheckStructure();

        Section("C. 客户端发包（IL）");
        CheckIl();

        Section("D. 客户端行为");
        CheckBehaviour();

        Section("E. 界面");
        CheckUi();

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过" : $"有 {_fail} 项失败");
        return _fail == 0 ? 0 : 1;
    }

    // ════════════════════════════════════════════════
    // A. 服务端
    // ════════════════════════════════════════════════
    private static void CheckServer()
    {
        string hero = Src("Server/MirObjects/HeroObject.cs");
        string human = Src("Server/MirObjects/HumanObject.cs");
        string player = Src("Server/MirObjects/PlayerObject.cs");
        string conn = Src("Server/MirNetwork/MirConnection.cs");
        string charInfo = Src("Server/MirDatabase/CharacterInfo.cs");
        string heroInfo = Src("Server/MirDatabase/HeroInfo.cs");

        Check("HeroObject 继承 HumanObject（石头处理在基类，英雄自动共享）",
            hero.Contains("class HeroObject : HumanObject"));

        Check("HeroObject.Process() 先调 base.Process()（否则基类回血/石头全被跳过）",
            System.Text.RegularExpressions.Regex.IsMatch(hero, @"public override void Process\(\)\s*\{\s*base\.Process\(\);"));
        Check("HumanObject.Process 里有 ProcessRegen()", human.Contains("ProcessRegen();"));
        Check("ProcessRegen 里调 ProcessStones(ref healthRegen, ref manaRegen)",
            human.Contains("ProcessStones(ref healthRegen, ref manaRegen);"));
        Check("ProcessStones 读的是 Info.Equipment[(int)EquipmentSlot.守护石]（= 英雄也用这一份）",
            human.Contains("Info.Equipment[(int)EquipmentSlot.守护石]"));

        Check("英雄装备数组长度 = Info.Equipment.Length（CharacterInfo 里是 14 格，含宝石位 12）",
            charInfo.Contains("Equipment = new UserItem[14]") && hero.Contains("Equipment = new UserItem[Info.Equipment.Length]"));
        Check("HeroInfo 继承 CharacterInfo", heroInfo.Contains("class HeroInfo : CharacterInfo"));

        // 卷轴 Shape=23（全身修复神水）在英雄侧
        int i23 = hero.IndexOf("case 23: //RepairAllOil", StringComparison.Ordinal);
        Check("英雄 UseItem 有 case 23（全身修复神水）", i23 > 0);
        Check("case 23 调的是英雄自己的 RepairAllEquipment()",
            i23 > 0 && hero.Substring(i23, Math.Min(400, hero.Length - i23)).Contains("RepairAllEquipment()"));
        Check("英雄 RepairAllEquipment 修的是 Info.Equipment（英雄身上）",
            System.Text.RegularExpressions.Regex.IsMatch(hero, @"RepairAllEquipmentCore\(Info\.Equipment"));

        int i84 = hero.IndexOf("if (item.Info.Shape == 84)", StringComparison.Ordinal);
        Check("英雄特殊消耗品 Shape=84（修复神水）也修英雄装备",
            i84 > 0 && hero.Substring(i84, Math.Min(300, hero.Length - i84)).Contains("RepairAllEquipment()"));

        Check("PlayerObject.HeroUseItem 转发到 Hero.UseItem",
            System.Text.RegularExpressions.Regex.IsMatch(player, @"public void HeroUseItem\(ulong id\)[\s\S]{0,200}Hero\.UseItem\(id\)"));
        Check("MirConnection.UseItem：Grid=HeroInventory → Player.HeroUseItem",
            System.Text.RegularExpressions.Regex.IsMatch(conn, @"case MirGridType\.HeroInventory:\s*Player\.HeroUseItem\(p\.UniqueID\)"));

        int heroEquip = player.IndexOf("case MirGridType.HeroInventory:", StringComparison.Ordinal);
        Check("Player.EquipItem 支持 HeroInventory（英雄装备包）", heroEquip > 0);
        Check("英雄装备包目标数组 = CurrentHero.Equipment，回包 Grid = HeroEquipment",
            System.Text.RegularExpressions.Regex.IsMatch(player,
                @"case MirGridType\.HeroInventory:[\s\S]{0,300}toArray = CurrentHero\.Equipment[\s\S]{0,200}toGrid = MirGridType\.HeroEquipment"));

        // 英雄侧装备条件：宝石位接受气血石/魔血石（CanEquipItem 在基类，英雄共用）
        Check("CanEquipItem 的宝石位接受 气血石 / 魔血石（英雄共用基类判定）",
            System.Text.RegularExpressions.Regex.IsMatch(human,
                @"case EquipmentSlot\.守护石:[\s\S]{0,300}item\.Info\.Type != ItemType\.气血石[\s\S]{0,200}item\.Info\.Type != ItemType\.魔血石"));
    }

    // ════════════════════════════════════════════════
    // B. 结构
    // ════════════════════════════════════════════════
    private static void CheckStructure()
    {
        Check("MapControl 类型存在", _mapControl != null);
        if (_mapControl == null) return;

        Check("AutoPlayRepairActor(装备,背包,Grid,who) 存在",
            FindInstance("AutoPlayRepairActor", 4) != null);
        Check("AutoPlayRenewStoneActor(装备,背包,Grid,who) 存在",
            FindInstance("AutoPlayRenewStoneActor", 4) != null);
        Check("AutoPlayReadyHero() 存在（英雄未召唤就不管）",
            FindStatic("AutoPlayReadyHero", 0) != null);
        Check("AutoPlayEquipmentNeedRepair(装备) 存在",
            FindStatic("AutoPlayEquipmentNeedRepair", 1) != null);

        MethodInfo findByName = FindStatic("AutoPlayFindItemByName", 2);
        Check("AutoPlayFindItemByName(背包,名字) 改成按背包查（玩家/英雄通用）", findByName != null);
        Check("AutoPlayFindAnyRepairItem(背包) 改成按背包查", FindStatic("AutoPlayFindAnyRepairItem", 1) != null);

        // 状态必须是两组，否则玩家和英雄互相打断节流
        string[] arrFields =
        {
            "_autoPlayNextRepair", "_autoPlayNextStone",
            "_autoStoneWantShape", "_autoStoneZeroSince", "_autoStoneLastID", "_autoStoneLastTime",
        };

        foreach (string f in arrFields)
        {
            FieldInfo fi = _mapControl.GetField(f, BindingFlags.NonPublic | BindingFlags.Static);
            Check("状态字段 " + f + " 是数组", fi != null && fi.FieldType.IsArray,
                fi == null ? "不存在" : fi.FieldType.Name);

            if (fi != null && fi.FieldType.IsArray)
            {
                Array a = fi.GetValue(null) as Array;
                Check("  └ 长度 2（[0]=玩家 [1]=英雄）", a != null && a.Length == 2,
                    a == null ? "null" : "长度 " + a.Length);
            }
        }
    }

    // ════════════════════════════════════════════════
    // C. IL
    // ════════════════════════════════════════════════
    private static void CheckIl()
    {
        if (_mapControl == null) return;

        MethodInfo repair = FindInstance("AutoPlayRepairEquipment", 0);
        MethodInfo renew = FindInstance("AutoPlayRenewStone", 0);
        MethodInfo repairActor = FindInstance("AutoPlayRepairActor", 4);
        MethodInfo renewActor = FindInstance("AutoPlayRenewStoneActor", 4);
        MethodInfo ready = FindStatic("AutoPlayReadyHero", 0);

        Check("AutoPlayRepairEquipment 存在", repair != null);
        Check("AutoPlayRenewStone 存在", renew != null);

        if (repair != null)
        {
            int n = CallsTo(repair, "AutoPlayRepairActor");
            Check("装备保护对「玩家 + 英雄」各调一次 RepairActor", n == 2, "实际 " + n + " 次");
            Check("装备保护会先问 AutoPlayReadyHero（英雄没召唤就不发）", CallsTo(repair, "AutoPlayReadyHero") >= 1);
        }

        if (renew != null)
        {
            int n = CallsTo(renew, "AutoPlayRenewStoneActor");
            Check("续石对「玩家 + 英雄」各调一次 RenewStoneActor", n == 2, "实际 " + n + " 次");
            Check("续石也会先问 AutoPlayReadyHero", CallsTo(renew, "AutoPlayReadyHero") >= 1);
        }

        if (ready != null)
        {
            // 源码里是 GameScene.HeroObject（静态属性）→ IL 是 call get_HeroObject
            Check("AutoPlayReadyHero 真的读了 HeroObject（未召唤返回 null）",
                CallsTo(ready, "get_HeroObject") >= 1);
        }

        if (repairActor != null)
        {
            Check("RepairActor 第 3 个参数是 MirGridType（决定发玩家包还是英雄包）",
                repairActor.GetParameters()[2].ParameterType.Name == "MirGridType",
                repairActor.GetParameters()[2].ParameterType.Name);
            Check("RepairActor 发包的 Grid 来自该参数（不是写死 Inventory）",
                AssignsGridFromArg(repairActor));
            Check("RepairActor 会调 AutoPlayEquipmentNeedRepair（先判耐久再发）",
                CallsTo(repairActor, "AutoPlayEquipmentNeedRepair") >= 1);
            Check("RepairActor 两种物品查找都传了背包（按名字 → 任意修复类）",
                CallsTo(repairActor, "AutoPlayFindItemByName") >= 1 &&
                CallsTo(repairActor, "AutoPlayFindAnyRepairItem") >= 1);
        }

        if (renewActor != null)
        {
            Check("RenewStoneActor 第 3 个参数是 MirGridType",
                renewActor.GetParameters()[2].ParameterType.Name == "MirGridType",
                renewActor.GetParameters()[2].ParameterType.Name);
            Check("RenewStoneActor 换装的 Grid 来自该参数（英雄发 HeroInventory）", AssignsGridFromArg(renewActor));
            Check("RenewStoneActor 用 AutoPlayPickLifeStone（同一份挑石逻辑）",
                CallsTo(renewActor, "AutoPlayPickLifeStone") >= 1);
        }
    }

    /// <summary>方法里是否把「第 3 个参数（grid）」直接赋给了包的 Grid 字段</summary>
    private static bool AssignsGridFromArg(MethodInfo m)
    {
        List<Ins> il = Parse(m);
        for (int i = 0; i < il.Count; i++)
        {
            if (il[i].Op != OpCodes.Stfld) continue;

            FieldInfo f = ResolveField(m, il[i].Operand);
            if (f == null || f.Name != "Grid" || f.DeclaringType == null) continue;
            if (!f.DeclaringType.Name.Contains("UseItem") && !f.DeclaringType.Name.Contains("EquipItem")) continue;

            // 前一条指令应该是 ldarg.3 / ldarg.s 3
            // （实例方法的 IL 参数序号：arg0=this, arg1=equipment, arg2=inventory, arg3=grid）
            if (i == 0) continue;
            Ins prev = il[i - 1];
            if (prev.Op == OpCodes.Ldarg_3) return true;
            if (prev.Op == OpCodes.Ldarg_S && prev.Operand == 3) return true;
        }

        return false;
    }

    private static int CallsTo(MethodBase m, string name)
    {
        int n = 0;
        foreach (Ins ins in Parse(m))
        {
            if (ins.Op != OpCodes.Call && ins.Op != OpCodes.Callvirt) continue;
            MethodBase target = ResolveMethod(m, ins.Operand);
            if (target != null && target.Name == name) n++;
        }
        return n;
    }

    private static bool UsesField(MethodBase m, string declaring, string field)
    {
        foreach (Ins ins in Parse(m))
        {
            if (ins.Op != OpCodes.Ldsfld && ins.Op != OpCodes.Ldfld) continue;
            FieldInfo f = ResolveField(m, ins.Operand);
            if (f == null || f.Name != field) continue;
            if (f.DeclaringType == null || f.DeclaringType.Name != declaring) continue;
            return true;
        }
        return false;
    }

    // ════════════════════════════════════════════════
    // D. 行为
    // ════════════════════════════════════════════════
    private static void CheckBehaviour()
    {
        if (_mapControl == null) return;

        MethodInfo pick = FindStatic("AutoPlayPickLifeStone", 2);
        MethodInfo need = FindStatic("AutoPlayEquipmentNeedRepair", 1);
        if (pick == null || need == null) return;

        // 挑石：英雄背包与玩家背包走同一份逻辑
        object heroHp = MakeItem(1, 60000, 60000, 501);
        object heroMp = MakeItem(2, 60000, 60000, 502);

        Check("英雄背包 want=1（原来装气血石）→ 续气血石",
            Picked(pick, Pack(heroMp, heroHp), 1) == 501);
        Check("英雄背包 want=2（原来装魔血石）→ 续魔血石",
            Picked(pick, Pack(heroHp, heroMp), 2) == 502);

        object deadStone = MakeItem(1, 60000, 0, 503);
        Check("英雄背包里的 0 电石头不会被续上",
            Picked(pick, Pack(deadStone, heroHp), 1) == 501);

        object plainGuard = MakeItem(0, 60000, 60000, 504);   // ItemType.守护石 Shape=0
        Check("普通守护石（芷心石类）不会被当气血石续",
            Picked(pick, Pack(plainGuard), 0) == 0);

        // 装备保护：宝石位不算「要修的装备」
        int pct = 5;
        try
        {
            FieldInfo p = _settingsType?.GetField("AutoRepairPercent", BindingFlags.Public | BindingFlags.Static);
            if (p != null) pct = (int)p.GetValue(null);
        }
        catch { }

        int slotStone = Convert.ToInt32(Enum.Parse(_equipSlotEnum, "守护石"));

        Check("满耐久的装备 → 不需要修",
            !(bool)need.Invoke(null, new object[] { EquipArray(slotStone, 0, MakeEquip(0, 10000, 10000)) }));
        Check("耐久 15% 以上（阈值 " + pct + "%）→ 不需要修",
            !(bool)need.Invoke(null, new object[] { EquipArray(slotStone, 0, MakeEquip(0, 10000, 1500)) }));
        Check("耐久低于阈值 → 需要修",
            (bool)need.Invoke(null, new object[] { EquipArray(slotStone, 0, MakeEquip(0, 10000, 100)) }));
        Check("耐久 1 点（快碎）→ 立即需要修",
            (bool)need.Invoke(null, new object[] { EquipArray(slotStone, 0, MakeEquip(0, 10000, 1)) }));
        Check("只有宝石位是 0 电石头 → 不当成要修（交给续石）",
            !(bool)need.Invoke(null, new object[] { EquipArray(slotStone, slotStone, MakeItem(1, 60000, 0, 700)) }));
        Check("全空的装备数组 → 不需要修",
            !(bool)need.Invoke(null, new object[] { EquipArray(slotStone, 0) }));
    }

    private static ulong Picked(MethodInfo pick, Array inv, int want)
    {
        object r = pick.Invoke(null, new object[] { inv, (short)want });
        if (r == null) return 0;
        return (ulong)_userItemType.GetField("UniqueID").GetValue(r);
    }

    // ════════════════════════════════════════════════
    // E. 界面
    // ════════════════════════════════════════════════
    private static void CheckUi()
    {
        string dialog = Src("Client/MirScenes/Dialogs/AutoPlayDialog.cs");
        if (dialog.Length == 0) { Check("能读到 AutoPlayDialog.cs", false); return; }

        Check("保护页提示写明「玩家/英雄都管」", dialog.Contains("玩家/英雄都管"));

        // 提示框：y=386，h=40 → 底 426，不越过内容区下沿（430）
        System.Text.RegularExpressions.Match m =
            System.Text.RegularExpressions.Regex.Match(dialog, @"CreateHint\(386,\s*""([^""]+)""\s*,\s*(\d+)\)");
        Check("提示框位置/高度取到（y=386, h=40）", m.Success, m.Success ? m.Groups[2].Value : "没匹配到");

        if (m.Success)
        {
            string text = m.Groups[1].Value;
            int h = int.Parse(m.Groups[2].Value);
            int chars = text.Count(c => c > 0x2E80);      // 粗略数中文字数

            Check("提示框底边 386+" + h + " ≤ 430（不压底部装饰）", 386 + h <= 430, "底边 " + (386 + h));
            Check("提示文字不超过 56 个中文（2 行内）", chars <= 56, "中文 " + chars + " 字");
        }

        // 两个勾选框仍在，且提示提到"续石/耐久"
        Check("保护页仍有「自动续气血石」勾选框", dialog.Contains("RenewStoneBox"));
        Check("保护页仍有「装备保护」勾选框", dialog.Contains("RepairBox"));
    }

    // ════════════════════════════════════════════════
    // 反射小工具
    // ════════════════════════════════════════════════
    private static MethodInfo FindInstance(string name, int argc)
    {
        return _mapControl?.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == argc);
    }

    private static MethodInfo FindStatic(string name, int argc)
    {
        return _mapControl?.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == argc);
    }

    private static object MakeItem(short shape, ushort maxDura, ushort curDura, ulong uid)
    {
        object info = Activator.CreateInstance(_itemInfoType);
        string typeName = shape == 0 ? "守护石" : (shape == 2 ? "魔血石" : "气血石");
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

    /// <summary>造一件普通装备（武器），耐久可指定</summary>
    private static object MakeEquip(int slot, ushort maxDura, ushort curDura)
    {
        object info = Activator.CreateInstance(_itemInfoType);
        _itemInfoType.GetField("Type").SetValue(info, Enum.Parse(_itemTypeEnum, "武器"));
        _itemInfoType.GetField("Shape").SetValue(info, (short)0);
        _itemInfoType.GetField("Durability").SetValue(info, maxDura);
        _itemInfoType.GetField("Name").SetValue(info, "测试武器_" + slot);

        object item = Activator.CreateInstance(_userItemType, new[] { info });
        _userItemType.GetField("CurrentDura").SetValue(item, curDura);
        _userItemType.GetField("MaxDura").SetValue(item, maxDura);
        _userItemType.GetField("UniqueID").SetValue(item, (ulong)(9000 + slot));

        return item;
    }

    private static Array Pack(params object[] items)
    {
        Array arr = Array.CreateInstance(_userItemType, items.Length);
        for (int i = 0; i < items.Length; i++) arr.SetValue(items[i], i);
        return arr;
    }

    private static Array EquipArray(int stoneSlot, int atIndex, params object[] wearables)
    {
        Array arr = Array.CreateInstance(_userItemType, stoneSlot + 1);
        for (int i = 0; i < wearables.Length; i++) arr.SetValue(wearables[i], atIndex + i);
        return arr;
    }

    private static string Src(string relative)
    {
        string p = Path.Combine(_srcRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(p) ? File.ReadAllText(p) : string.Empty;
    }

    // ───── IL 解析 ─────
    private readonly struct Ins
    {
        public readonly int Offset;
        public readonly OpCode Op;
        public readonly int Operand;

        public Ins(int offset, OpCode op, int operand) { Offset = offset; Op = op; Operand = operand; }
    }

    private static Dictionary<short, OpCode> _opcodes;

    private static Dictionary<short, OpCode> OpCodeMap()
    {
        if (_opcodes != null) return _opcodes;

        _opcodes = new Dictionary<short, OpCode>();
        foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.GetValue(null) is not OpCode oc) continue;
            _opcodes[oc.Value] = oc;
        }
        return _opcodes;
    }

    private static List<Ins> Parse(MethodBase m)
    {
        List<Ins> list = new List<Ins>();
        byte[] il;
        try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch { return list; }
        if (il == null) return list;

        Dictionary<short, OpCode> map = OpCodeMap();
        int i = 0;

        while (i < il.Length)
        {
            int start = i;
            short code = il[i];

            if (code == 0xFE && i + 1 < il.Length)
            {
                code = (short)(0xFE00 | il[i + 1]);
                i += 2;
            }
            else i++;

            if (!map.TryGetValue(code, out OpCode op)) break;

            int operand = 0;
            int size = OperandSize(op.OperandType);

            if (op.OperandType == OperandType.InlineSwitch)
            {
                if (i + 4 > il.Length) break;
                int count = BitConverter.ToInt32(il, i);
                i += 4 + 4 * count;
            }
            else
            {
                if (i + size > il.Length) break;

                if (size == 1) operand = il[i];
                else if (size == 2) operand = BitConverter.ToUInt16(il, i);
                else if (size == 4) operand = BitConverter.ToInt32(il, i);
                else if (size == 8) operand = 0;

                i += size;
            }

            list.Add(new Ins(start, op, operand));
        }

        return list;
    }

    private static int OperandSize(OperandType t)
    {
        switch (t)
        {
            case OperandType.InlineNone: return 0;
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
            case OperandType.ShortInlineBrTarget: return 1;
            case OperandType.InlineVar: return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR: return 8;
            case OperandType.InlineSwitch: return 4;
            default: return 4;
        }
    }

    private static MethodBase ResolveMethod(MethodBase host, int token)
    {
        try { return host.Module.ResolveMethod(token); } catch { return null; }
    }

    private static FieldInfo ResolveField(MethodBase host, int token)
    {
        try { return host.Module.ResolveField(token); } catch { return null; }
    }

    // ───── 输出 ─────
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
