using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;

namespace MailDeleteAllTest;

/// <summary>
/// 邮箱「一键删除全部邮件」验证（只读：不实例化 UI、不连服务器、不动数据库）。A~D 四段：
///  A. 协议：新包 DeleteAllMail 加在枚举最末（不能中间插，否则旧包序号整体平移）、无载荷、能收发往返
///  B. 服务端：RemoveUnlockedMail 只剔未锁定的；DeleteAllMail 有 removed&gt;0 守卫；MirConnection 有 Game 阶段守卫
///  C. 客户端：按钮字段/图标/坐标；CountForDeleteAll、NormalizePage 的纯逻辑行为
///  D. 界面：按钮排在行动作按钮最右边，27px 一格不重叠，不超出面板内边框
/// 用法：MailDeleteAllTest.exe [客户端产物目录] [服务端产物目录]
/// 基线：Tools\MailDeleteAllTest\baseline\Shared-before.dll（改动前线上那份，用来确认旧包序号没被平移）
/// </summary>
internal static class Program
{
    private static int _fail;

    private static Assembly _shared, _client, _server;
    private static Type _clientMail, _userItem, _mailInfo;

    private const string Root = @"D:\mir2-20241027";

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string clientDir = Path.GetFullPath(Arg(args, 0, Path.Combine(Root, @"Build\Client\Release")));
        string serverDir = Path.GetFullPath(Arg(args, 1, Path.Combine(Root, @"Build\Server\Release")));

        if (!File.Exists(Path.Combine(clientDir, "Client.dll"))) { Console.WriteLine("找不到 Client.dll: " + clientDir); return 2; }
        if (!File.Exists(Path.Combine(serverDir, "Server.dll"))) { Console.WriteLine("找不到 Server.dll: " + serverDir); return 2; }

        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            foreach (string d in new[] { clientDir, serverDir })
            {
                string p = Path.Combine(d, name);
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };

        Console.WriteLine("=== 客户端：" + clientDir);
        Console.WriteLine("=== 服务端：" + serverDir);

        _shared = Assembly.LoadFrom(Path.Combine(clientDir, "Shared.dll"));
        _client = Assembly.LoadFrom(Path.Combine(clientDir, "Client.dll"));
        _server = Assembly.LoadFrom(Path.Combine(serverDir, "Server.dll"));

        _clientMail = _shared.GetType("ClientMail", true);
        _userItem = _shared.GetType("UserItem", true);
        _mailInfo = FindType("Server.MirEnvir.MailInfo");

        Section("A. 协议与枚举");
        CheckProtocol();

        Section("B. 服务端");
        CheckServer();

        Section("C. 客户端逻辑");
        CheckClient();

        Section("D. 界面布局");
        CheckLayout();

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "全部通过" : $"有 {_fail} 项失败");
        return _fail == 0 ? 0 : 1;
    }

    // ─────────────────────────── A. 协议 ───────────────────────────
    private static void CheckProtocol()
    {
        Type ids = _shared.GetType("ClientPacketIds", true);

        Check("ClientPacketIds.DeleteAllMail 存在", Enum.IsDefined(ids, "DeleteAllMail"));

        int newId = Convert.ToInt32(Enum.Parse(ids, "DeleteAllMail"));
        int sortBag = Convert.ToInt32(Enum.Parse(ids, "SortBag"));

        int max = Enum.GetValues(ids).Cast<object>().Select(Convert.ToInt32).Max();
        Check("新包加在枚举最末（序号最大），不会平移旧包", newId == max, $"DeleteAllMail={newId} 最大={max}");
        Check("紧跟在 SortBag 后面", newId == sortBag + 1, $"SortBag={sortBag} DeleteAllMail={newId}");

        CheckOldIdsUnchanged(ids);

        Type pkt = _shared.GetType("ClientPackets+DeleteAllMail", false) ?? _shared.GetType("ClientPackets.DeleteAllMail", false);
        Check("ClientPackets.DeleteAllMail 类型存在", pkt != null);
        if (pkt == null) return;

        object inst = Activator.CreateInstance(pkt);
        short idx = (short)pkt.GetProperty("Index").GetValue(inst);
        Check("包 Index = ClientPacketIds.DeleteAllMail", idx == newId, $"Index={idx}");

        Type packetType = _shared.GetType("Packet", true);

        // 无载荷：2 字节长度 + 2 字节包号 = 4 字节
        MethodInfo getBytes = packetType.GetMethod("GetPacketBytes");
        byte[] raw = ((IEnumerable<byte>)getBytes.Invoke(inst, null)).ToArray();
        Check("包体为空（共 4 字节：长度+包号）", raw.Length == 4, "实际 " + raw.Length + " 字节");

        // 往返：按服务端口径反序列化回来
        FieldInfo isServer = packetType.GetField("IsServer", BindingFlags.Public | BindingFlags.Static);
        object oldIsServer = isServer.GetValue(null);
        try
        {
            isServer.SetValue(null, true);

            object[] callArgs = { raw, null };
            object back = packetType.GetMethod("ReceivePacket").Invoke(null, callArgs);

            Check("服务端能把这包还原成 DeleteAllMail", back != null && back.GetType() == pkt,
                back == null ? "还原结果 null" : back.GetType().Name);
        }
        catch (Exception ex)
        {
            Check("服务端能把这包还原成 DeleteAllMail", false, ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            isServer.SetValue(null, oldIsServer);
        }

        // 分派开关里认得这个号
        MethodInfo factory = packetType.GetMethod("GetClientPacket", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (factory != null)
        {
            object made = factory.Invoke(null, new object[] { (short)newId });
            Check("Packet.GetClientPacket 认得新包号", made != null && made.GetType() == pkt);
        }
        else
        {
            Check("Packet.GetClientPacket 认得新包号", false, "找不到工厂方法");
        }
    }

    /// <summary>
    /// 用「改动前线上那份 Shared.dll」做基线，逐个比对旧包序号。
    /// 这是中间插包最狠的坑：后面所有包号会整体 +1，新老端一混就错位。
    /// </summary>
    private static void CheckOldIdsUnchanged(Type ids)
    {
        string baselinePath = Path.Combine(Root, @"Tools\MailDeleteAllTest\baseline\Shared-before.dll");

        if (!File.Exists(baselinePath))
        {
            Console.WriteLine("  ~ 跳过：找不到基线 " + baselinePath);
            return;
        }

        // 同一个程序集身份不能重复 LoadFrom，另开 ALC 载入旧的那份
        AssemblyLoadContext alc = new AssemblyLoadContext("baseline", true);
        Type oldIds = null;

        try
        {
            Assembly oldShared = alc.LoadFromAssemblyPath(baselinePath);
            oldIds = oldShared.GetType("ClientPacketIds", false);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  ~ 跳过：基线程序集载入失败 " + ex.Message);
        }

        if (oldIds == null) { if (alc.IsCollectible) alc.Unload(); return; }

        List<string> shifted = new List<string>();
        int compared = 0;

        foreach (string name in Enum.GetNames(oldIds))
        {
            compared++;

            if (!Enum.IsDefined(ids, name)) { shifted.Add(name + "(新版已移除)"); continue; }

            int before = Convert.ToInt32(Enum.Parse(oldIds, name));
            int now = Convert.ToInt32(Enum.Parse(ids, name));

            if (before != now) shifted.Add($"{name}: {before}->{now}");
        }

        Check($"旧包序号全部未变（比对基线 {compared} 项）", shifted.Count == 0,
            shifted.Count == 0 ? null : string.Join("; ", shifted.Take(5)));

        Check("基线里确实没有 DeleteAllMail（确认基线是改动前的）", !Enum.IsDefined(oldIds, "DeleteAllMail"));

        if (alc.IsCollectible) alc.Unload();
    }

    // ─────────────────────────── B. 服务端 ───────────────────────────
    private static void CheckServer()
    {
        Check("Server.MirEnvir.MailInfo 类型存在", _mailInfo != null);
        if (_mailInfo == null) return;

        Type player = FindType("Server.MirObjects.PlayerObject");
        Check("PlayerObject 类型存在", player != null);
        if (player == null) return;

        MethodInfo remove = player.GetMethod("RemoveUnlockedMail", BindingFlags.Public | BindingFlags.Static);
        Check("PlayerObject.RemoveUnlockedMail 静态方法存在", remove != null);

        MethodInfo delAll = player.GetMethod("DeleteAllMail", BindingFlags.Public | BindingFlags.Instance);
        Check("PlayerObject.DeleteAllMail 实例方法存在", delAll != null && delAll.ReturnType == typeof(int),
            delAll == null ? null : "返回 " + delAll.ReturnType.Name);

        Type conn = FindType("Server.MirNetwork.MirConnection");
        Check("MirConnection 类型存在", conn != null);
        Check("MirConnection.DeleteAllMail 处理方法存在", conn?.GetMethod("DeleteAllMail", BindingFlags.Public | BindingFlags.Instance) != null);

        if (remove == null) return;

        IList mail = NewMailInfoList();
        mail.Add(Mail(1, false));
        mail.Add(Mail(2, true));
        mail.Add(Mail(3, false));
        mail.Add(Mail(4, true));
        mail.Add(Mail(5, false));

        int removed = InvokeRemove(remove, mail);
        Check("混合 5 封（3 未锁 + 2 锁）：删掉 3 封", removed == 3, "实际=" + removed);
        Check("锁定 2 封保留", mail.Count == 2 && AllLocked(mail), "剩 " + mail.Count + " 封");
        Check("保留项顺序不变（2 在前 4 在后）",
            mail.Count == 2 && IdOf(mail[0]) == 2 && IdOf(mail[1]) == 4,
            mail.Count == 2 ? IdOf(mail[0]) + "," + IdOf(mail[1]) : "数量不对");

        IList allLocked = NewMailInfoList();
        allLocked.Add(Mail(11, true));
        allLocked.Add(Mail(12, true));
        Check("全部锁定：删 0 封、列表不动", InvokeRemove(remove, allLocked) == 0 && allLocked.Count == 2);

        IList allFree = NewMailInfoList();
        allFree.Add(Mail(21, false));
        allFree.Add(Mail(22, false));
        allFree.Add(Mail(23, false));
        Check("全部未锁定：全删", InvokeRemove(remove, allFree) == 3 && allFree.Count == 0);

        Check("空列表：返回 0", InvokeRemove(remove, NewMailInfoList()) == 0);
        Check("null 列表：返回 0 不抛异常", InvokeRemove(remove, null) == 0);

        // 源码守卫：没删到东西就不回列表（避免空包刷客户端）
        string src = ReadSource(@"Server\MirObjects\PlayerObject.cs");
        string body = ExtractBody(src, "public int DeleteAllMail()");
        Check("DeleteAllMail 有 removed > 0 守卫才 GetMail",
            body != null && Regex.IsMatch(body, @"if\s*\(\s*removed\s*>\s*0\s*\)") && body.Contains("GetMail()"));
        Check("DeleteAllMail 不碰锁定邮件（走 RemoveUnlockedMail）", body != null && body.Contains("RemoveUnlockedMail"));

        string connSrc = ReadSource(@"Server\MirNetwork\MirConnection.cs");
        string connBody = ExtractBody(connSrc, "public void DeleteAllMail()");
        Check("MirConnection.DeleteAllMail 有 Game 阶段守卫",
            connBody != null && connBody.Contains("GameStage.Game") && connBody.Contains("Player.DeleteAllMail"));
        Check("协议分派里注册了 DeleteAllMail", Regex.IsMatch(connSrc, @"case\s*\(short\)ClientPacketIds\.DeleteAllMail:"));
    }

    // ─────────────────────────── C. 客户端逻辑 ───────────────────────────
    private static void CheckClient()
    {
        Type dlg = _client.GetType("Client.MirScenes.Dialogs.MailListDialog", false);
        Check("MailListDialog 类型存在", dlg != null);
        if (dlg == null) return;

        // 字段没有修饰符 = private，反射要带 NonPublic
        FieldInfo btn = dlg.GetField("DeleteAllButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Check("MailListDialog.DeleteAllButton 字段存在", btn != null);
        Check("DeleteAllButton 是 MirButton", btn != null && btn.FieldType.Name == "MirButton",
            btn == null ? null : btn.FieldType.Name);

        MethodInfo count = dlg.GetMethod("CountForDeleteAll", BindingFlags.Public | BindingFlags.Static);
        Check("MailListDialog.CountForDeleteAll 静态方法存在", count != null);

        MethodInfo norm = dlg.GetMethod("NormalizePage", BindingFlags.Public | BindingFlags.Static);
        Check("MailListDialog.NormalizePage 静态方法存在", norm != null);

        if (count != null) CheckCount(count);
        if (norm != null) CheckNormalize(norm);

        // IL：构造里 557/558/559 三态 + 237/414 坐标 + 提示文字
        ConstructorInfo ctor = dlg.GetConstructor(Type.EmptyTypes);
        byte[] il = ctor?.GetMethodBody()?.GetILAsByteArray();
        Check("构造函数可取到方法体", il != null);

        if (il != null)
        {
            HashSet<int> ints = CollectIntConstants(il);
            Check("按钮 x = 237（紧跟第 6 个按钮）", ints.Contains(237));
            Check("按钮 y = 414（与其他动作按钮同一行）", ints.Contains(414));
            Check("用了删除图标三态 557/558/559",
                ints.Contains(557) && ints.Contains(558) && ints.Contains(559));

            Check("按钮提示文字 = 删除所有邮件", CollectStrings(ctor).Any(s => s == "删除所有邮件"));
        }

        // 全类里确实发得出去 C.DeleteAllMail
        Check("MailListDialog 里有代码 new 了 ClientPackets.DeleteAllMail", MentionsPacket(dlg, "DeleteAllMail"));

        // 源码：Click 绑定、确认框、选中清理、回复按钮复位
        string src = ReadSource(@"Client\MirScenes\Dialogs\MailDialogs.cs");
        Check("源码里绑定了 Click -> DeleteAllMails()",
            Regex.IsMatch(src, @"DeleteAllButton\.Click\s*\+=\s*\(o,\s*e\)\s*=>\s*DeleteAllMails\(\)"));
        Check("确认后才发包（YesButton.Click 里 Enqueue DeleteAllMail）",
            Regex.IsMatch(src, @"YesButton\.Click[\s\S]{0,200}new C\.DeleteAllMail\(\)"));
        Check("UpdateInterface 用 NormalizePage 夹页码", Regex.IsMatch(src, @"NormalizePage\(CurrentPage,\s*GameScene\.User\.Mail\.Count"));
        Check("删完会清掉指向已删邮件的 SelectedMail",
            Regex.IsMatch(src, @"SelectedMail\s*!=\s*null\s*&&\s*!GameScene\.User\.Mail\.Contains\(SelectedMail\)"));

        // 顺序很关键：每次刷新都会新建 ClientMail 实例，先把 SelectedMail 清掉就再也补不回来（选择丢失）
        int rowIdx = src.IndexOf("Rows[i] = new MailItemRow", StringComparison.Ordinal);
        int clearIdx = src.IndexOf("!GameScene.User.Mail.Contains(SelectedMail)", StringComparison.Ordinal);
        Check("SelectedMail 清理排在补行之后（否则每次刷新都丢选中）", rowIdx > 0 && clearIdx > rowIdx,
            $"补行 @{rowIdx}，清理 @{clearIdx}");

        Check("SelectedMail 为空时回复按钮也复位",
            Regex.IsMatch(src, @"else\s*\{[\s\S]{0,300}ReplyButton\.Visible\s*=\s*false"));
    }

    private static void CheckCount(MethodInfo count)
    {
        IList empty = NewMailInfoListForClient();
        CheckStats(count, empty, 0, 0, 0, "空列表");

        IList plain = NewMailInfoListForClient();
        plain.Add(CMail(1, false));
        CheckStats(count, plain, 1, 0, 0, "1 封普通");

        IList mixed = NewMailInfoListForClient();
        mixed.Add(CMail(1, false));
        mixed.Add(CMail(2, true));
        mixed.Add(CMailWithGold(3, 500));
        mixed.Add(CMailWithItem(4));
        mixed.Add(CMail(5, true));
        CheckStats(count, mixed, 3, 2, 2, "5 封（2 锁 / 2 带物品金币）");

        IList onlyLocked = NewMailInfoListForClient();
        onlyLocked.Add(CMail(1, true));
        onlyLocked.Add(CMail(2, true));
        CheckStats(count, onlyLocked, 0, 2, 0, "全锁定");

        IList goldCollected = NewMailInfoListForClient();
        goldCollected.Add(CMailWithGold(1, 100));
        CheckStats(count, goldCollected, 1, 0, 1, "带金币（已领也算带钱）");
    }

    private static void CheckStats(MethodInfo count, IList list, int wantDel, int wantLock, int wantParcel, string tag)
    {
        object[] p = { list, 0, 0, 0 };
        try
        {
            count.Invoke(null, p);
            int del = (int)p[1], locked = (int)p[2], parcel = (int)p[3];
            Check($"{tag}：可删 {wantDel} / 锁定 {wantLock} / 带物品金币 {wantParcel}",
                del == wantDel && locked == wantLock && parcel == wantParcel,
                $"实际 {del}/{locked}/{parcel}");
        }
        catch (Exception ex)
        {
            Check($"{tag}：统计不抛异常", false, ex.InnerException?.Message ?? ex.Message);
        }
    }

    private static void CheckNormalize(MethodInfo norm)
    {
        // (当前页, 邮件数) -> (页数, 页码, 起始下标)
        (int page, int count, int pageCount, int page2, int start, string tag)[] cases =
        {
            (1, 0,   1, 1, 0,  "空邮箱"),
            (1, 5,   1, 1, 0,  "5 封 1 页"),
            (1, 10,  1, 1, 0,  "正好 10 封 1 页"),
            (2, 25,  3, 2, 10, "25 封停在第 2 页"),
            (3, 25,  3, 3, 20, "25 封停在末页"),
            (5, 5,   1, 1, 0,  "★删剩 5 封时从第 5 页夹回第 1 页"),
            (9, 0,   1, 1, 0,  "★删空后从第 9 页夹回第 1 页"),
            (0, 12,  2, 1, 0,  "页码 0 更正为 1"),
            (-3, 12, 2, 1, 0,  "负页码更正为 1"),
        };

        foreach (var c in cases)
        {
            object[] p = { c.page, c.count, 0, 0, 0 };
            try
            {
                norm.Invoke(null, p);
                int pageCount = (int)p[2], page = (int)p[3], start = (int)p[4];
                Check($"{c.tag}：{c.page} 页/{c.count} 封 -> {c.pageCount} 页 第 {c.page2} 页 起 {c.start}",
                    pageCount == c.pageCount && page == c.page2 && start == c.start,
                    $"实际 {pageCount} 页 第 {page} 页 起 {start}");
            }
            catch (Exception ex)
            {
                Check($"{c.tag}：不抛异常", false, ex.InnerException?.Message ?? ex.Message);
            }
        }
    }

    // ─────────────────────────── D. 布局 ───────────────────────────
    private static void CheckLayout()
    {
        string src = ReadSource(@"Client\MirScenes\Dialogs\MailDialogs.cs");

        MatchCollection ms = Regex.Matches(src, @"Location = new Point\((\d+), 414\)");
        List<int> xs = ms.Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(v => v).ToList();

        Check("底部动作按钮共 7 个（原 6 + 删除全部）", xs.Count == 7, "实际 " + xs.Count + "：" + string.Join(",", xs));
        Check("新增按钮在最右边（x 最大 = 237）", xs.Count > 0 && xs[^1] == 237, "最大 x = " + (xs.Count > 0 ? xs[^1] : -1));

        bool grid = true;
        for (int i = 1; i < xs.Count; i++)
            if (xs[i] - xs[i - 1] != 27) grid = false;
        Check("按钮等距 27px，与既有一致（不重叠）", grid, string.Join(",", xs));

        int right = (xs.Count > 0 ? xs[^1] : 0) + 28;
        Check("按钮右边缘 265 不超出面板内边框（≤302）", right <= 302, "右边缘 = " + right);

        Check("面板宽度仍是 312（沿用原图，不拉宽）", Regex.IsMatch(src, @"Size = new Size\(312, 444\)"));
    }

    // ─────────────────────────── 构造/工具 ───────────────────────────
    private static object Mail(ulong id, bool locked)
    {
        // MailInfo 只有 (int recipientIndex, bool canReply) 和 (BinaryReader,...) 两个构造
        object m = Activator.CreateInstance(_mailInfo, new object[] { 0, false });
        _mailInfo.GetField("MailID").SetValue(m, id);
        _mailInfo.GetField("Locked").SetValue(m, locked);
        return m;
    }

    private static object CMail(ulong id, bool locked)
    {
        object m = Activator.CreateInstance(_clientMail);
        _clientMail.GetField("MailID").SetValue(m, id);
        _clientMail.GetField("Locked").SetValue(m, locked);
        return m;
    }

    private static object CMailWithGold(ulong id, uint gold)
    {
        object m = CMail(id, false);
        _clientMail.GetField("Gold").SetValue(m, gold);
        return m;
    }

    private static object CMailWithItem(ulong id)
    {
        object m = CMail(id, false);

        object items = _clientMail.GetField("Items").GetValue(m);
        // UserItem 没有无参构造，跳过构造直接造空壳（只用来让 Items.Count > 0）
        ((IList)items).Add(System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(_userItem));

        return m;
    }

    private static IList NewMailInfoList() => NewListOf(_mailInfo);

    private static IList NewMailInfoListForClient() => NewListOf(_clientMail);

    private static IList NewListOf(Type element)
    {
        Type listType = typeof(List<>).MakeGenericType(element);
        return (IList)Activator.CreateInstance(listType);
    }

    private static int InvokeRemove(MethodInfo m, IList mail)
    {
        try
        {
            return (int)m.Invoke(null, new object[] { mail });
        }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine("  抛异常：" + (ex.InnerException?.Message ?? ex.Message));
            return -999;
        }
    }

    private static bool AllLocked(IList list)
    {
        foreach (object o in list)
            if (!(bool)Field(o, "Locked")) return false;

        return true;
    }

    private static ulong IdOf(object o) => (ulong)Field(o, "MailID");

    private static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

    private static string Arg(string[] args, int i, string fallback) =>
        args.Length > i && !string.IsNullOrWhiteSpace(args[i]) ? args[i] : fallback;

    private static Type FindType(string fullName)
    {
        foreach (Assembly a in new[] { _server, _shared })
        {
            Type t = a?.GetType(fullName, false);
            if (t != null) return t;

            try
            {
                foreach (Type x in a.GetTypes())
                    if (x.FullName == fullName) return x;
            }
            catch { }
        }

        string dir = Path.GetDirectoryName(_server.Location);
        foreach (string extra in new[] { "Server.Library.dll", "Server.Core.dll" })
        {
            string p = Path.Combine(dir ?? "", extra);
            if (!File.Exists(p)) continue;

            try
            {
                Type t = Assembly.LoadFrom(p).GetType(fullName, false);
                if (t != null) return t;
            }
            catch { }
        }

        return null;
    }

    private static string ReadSource(string relative)
    {
        string p = Path.Combine(Root, relative);
        return File.Exists(p) ? File.ReadAllText(p) : string.Empty;
    }

    /// <summary>截出某个方法签名到大括号配平为止的方法体文本。</summary>
    private static string ExtractBody(string src, string signature)
    {
        int at = src.IndexOf(signature, StringComparison.Ordinal);
        if (at < 0) return null;

        int open = src.IndexOf('{', at);
        if (open < 0) return null;

        int depth = 0;
        for (int i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0) return src.Substring(open, i - open + 1);
            }
        }

        return null;
    }

    private static HashSet<int> CollectIntConstants(byte[] il)
    {
        HashSet<int> set = new HashSet<int>();

        for (int i = 0; i < il.Length; i++)
        {
            byte op = il[i];

            if (op == 0x1F && i + 1 < il.Length) { set.Add((sbyte)il[i + 1]); i += 1; }          // ldc.i4.s
            else if (op == 0x20 && i + 4 < il.Length) { set.Add(BitConverter.ToInt32(il, i + 1)); i += 4; } // ldc.i4
        }

        return set;
    }

    private static IEnumerable<string> CollectStrings(ConstructorInfo ctor)
    {
        byte[] il = ctor.GetMethodBody()?.GetILAsByteArray();
        if (il == null) yield break;

        for (int i = 0; i < il.Length; i++)
        {
            if (il[i] != 0x72 || i + 4 >= il.Length) continue;   // ldstr

            string s = null;
            try { s = ctor.Module.ResolveString(BitConverter.ToInt32(il, i + 1)); } catch { }

            if (s != null) yield return s;
        }
    }

    /// <summary>类型（含编译器为 lambda 生成的嵌套类）里是否有任何方法 new 了指定名字的包。</summary>
    private static bool MentionsPacket(Type type, string packetName)
    {
        IEnumerable<MethodBase> methods = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));

        foreach (MethodBase m in methods)
        {
            byte[] il;
            try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch { continue; }
            if (il == null) continue;

            for (int i = 0; i < il.Length; i++)
            {
                if (il[i] != 0x73 || i + 4 >= il.Length) continue;   // newobj

                try
                {
                    MethodBase ctor = m.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1));
                    if (ctor?.DeclaringType?.Name == packetName) return true;
                }
                catch { }
            }
        }

        // 发包代码写在事件 lambda 里，会被编译进 MailListDialog+<>c__DisplayClassX，得进嵌套类再找
        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            if (MentionsPacket(nested, packetName)) return true;

        return false;
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine("── " + title + " ──");
    }

    private static void Check(string name, bool ok, string detail = null)
    {
        if (ok) Console.WriteLine("  [OK] " + name);
        else
        {
            _fail++;
            Console.WriteLine("  [!!] " + name + (string.IsNullOrEmpty(detail) ? "" : "   << " + detail));
        }
    }
}
