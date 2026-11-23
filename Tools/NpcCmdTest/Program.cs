using Server;
using Server.MirDatabase;
using Server.MirObjects;

// BUFF 配置解析冒烟：确认羽化 11 个节能被识别（Shared.dll 必须含 159~169）
if (args.Length > 0 && args[0] == "--buff")
{
    Settings.Load();
    BuffConfig.Load();

    var types = new List<BuffType> { BuffType.羽化登仙 };
    for (int i = 0; i < 10; i++) types.Add((BuffType)((int)BuffType.羽化一重 + i));

    int ok = 0;
    foreach (var t in types)
    {
        var def = BuffConfig.Get(t);
        if (def == null || def.Stats.Count == 0)
        {
            Console.WriteLine($"MISS  {t}  def={(def != null)}  stats={(def == null ? -1 : def.Stats.Count)}");
            continue;
        }
        ok++;
        Console.WriteLine($"OK    {t,-8} {def.Name}  属性 {def.Stats.Count} 条  Stack={def.Stack}");
    }
    Console.WriteLine($"识别成功 {ok}/{types.Count}   配置目录 {Settings.ConfigPath}");
    Environment.Exit(ok == types.Count ? 0 : 1);
}

// NPC 脚本命令解析冒烟测试：只验证「命令名 → ActionType/CheckType + 参数」的映射，
// 不启动服务端、不碰存档。用于 CHECKHUMUP / HUMP / HUMDOWN / RESETHUMUP 的语法自检。
var page = new NPCPage("@test");
var seg = new NPCSegment(page, new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>());

void ParseAct(string line)
{
    seg.ActList.Clear();
    seg.ParseAct(seg.ActList, line);
    if (seg.ActList.Count == 0)
    {
        Console.WriteLine($"ACT  {line,-22} -> (无动作)");
        return;
    }
    var a = seg.ActList[0];
    Console.WriteLine($"ACT  {line,-22} -> {a.Type}  params=[{string.Join(",", a.Params)}]");
}

void ParseCheck(string line)
{
    seg.CheckList.Clear();
    seg.ParseCheck(line);
    if (seg.CheckList.Count == 0)
    {
        Console.WriteLine($"CHK  {line,-22} -> (无条件)");
        return;
    }
    var c = seg.CheckList[0];
    Console.WriteLine($"CHK  {line,-22} -> {c.Type}  params=[{string.Join(",", c.Params)}]");
}

Console.WriteLine("=== 条件：检测登仙状态 ===");
ParseCheck("CHECKHUMUP");
ParseCheck("CHECKHUMUP 3");
ParseCheck("CHECKHUMUP >= 3");
ParseCheck("CHECKHUMUP < 10");

Console.WriteLine();
Console.WriteLine("=== 条件：装备栏空位（整栏 + 指定槽位）===");
ParseCheck("CHECKEMPTYEQUIP");
ParseCheck("CHECKEMPTYEQUIP 武器");
ParseCheck("CHECKEMPTYEQUIP 0,4");
ParseCheck("CHECKHEROEMPTYEQUIP");
ParseCheck("CHECKHEROEMPTYEQUIP 武器,头盔");
ParseCheck("CHECKHEROEMPTYEQUIP 2");

Console.WriteLine();
Console.WriteLine("=== 动作：升重 / 降重 / 还原 ===");
ParseAct("HUMP");
ParseAct("HUMP 3");
ParseAct("HUMDOWN");
ParseAct("HUMDOWN 2");
ParseAct("RESETHUMUP");
ParseAct("RESETHUMUP 1");

Console.WriteLine();
Console.WriteLine("=== 运行时：装备槽空位检测（IsEquipSlotEmpty 反射）===");
var mIs = typeof(NPCSegment).GetMethod("IsEquipSlotEmpty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
if (mIs == null) { Console.WriteLine("FAIL 找不到 IsEquipSlotEmpty"); Environment.Exit(1); }

UserItem[] MakeEquip(int filledSlot)
{
    var eq = new UserItem[14];
    if (filledSlot >= 0) eq[filledSlot] = new UserItem(new ItemInfo { Index = 1, Name = "test" });
    return eq;
}
bool Call(object[] args) => (bool)mIs.Invoke(null, args);

Console.WriteLine(Call(new object[] { MakeEquip(-1), null }) == true ? "OK 全空槽 + 无参 = 成立" : "FAIL 全空 + 无参");
Console.WriteLine(Call(new object[] { MakeEquip(0), null }) == false ? "OK 武器(0)有货 + 无参 = 不成立" : "FAIL 有货 + 无参");
Console.WriteLine(Call(new object[] { MakeEquip(0), "武器" }) == false ? "OK 武器有货 + 查武器 = 不成立" : "FAIL 武器有货查武器");
Console.WriteLine(Call(new object[] { MakeEquip(0), "盔甲" }) == true ? "OK 武器有货 + 查盔甲 = 成立" : "FAIL 武器有货查盔甲");
Console.WriteLine(Call(new object[] { MakeEquip(4), "0,4" }) == false ? "OK 项链(4)有货 + 查 0,4 = 不成立" : "FAIL 项链有货查0,4");
Console.WriteLine(Call(new object[] { MakeEquip(4), "0,2" }) == true ? "OK 项链有货 + 查 0,2 = 成立" : "FAIL 项链有货查0,2");
Console.WriteLine(Call(new object[] { MakeEquip(7), "左戒指" }) == false ? "OK 左戒指(7)有货 + 查名 = 不成立" : "FAIL 左戒指有货");
Console.WriteLine(Call(new object[] { MakeEquip(13), "坐骑" }) == false ? "OK 坐骑(13)有货 + 查名 = 不成立" : "FAIL 坐骑有货");
Console.WriteLine(Call(new object[] { MakeEquip(-1), "武器,头盔,坐骑" }) == true ? "OK 全空 + 多槽位 = 成立" : "FAIL 全空多槽位");
Console.WriteLine(Call(new object[] { MakeEquip(0), "不存在的槽" }) == false ? "OK 槽名错误 = 不成立" : "FAIL 槽名错误应不成立");
Console.WriteLine(Call(new object[] { MakeEquip(0), "99" }) == false ? "OK 索引越界 = 不成立" : "FAIL 越界应不成立");
Console.WriteLine(Call(new object[] { null, null }) == true ? "OK 装备数组 null = 成立（缺槽算空口径）" : "FAIL null 数组");

Console.WriteLine();
Console.WriteLine("=== 旧名字仍可用（向后兼容）===");
ParseAct("HUMUPUP 2");
ParseAct("HUMUPDOWN 1");
ParseAct("HUMUPRESET");
