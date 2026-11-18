using System.Text;
using Server;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace HumUpBookTest;

/// <summary>
/// 复现「羽化登仙·旧书」接任务/放弃任务的真实服务端行为（只读，不写库）。
///
/// 用法：HumUpBookTest.exe "D:\Mirserver"
///   1) 造一个 60 级角色，背包放一本旧书
///   2) 双击旧书（UseItem）→ 看是否接到《泥色的旧书》
///   3) 放弃任务（AbandonQuest）→ 看任务栏/完成记录
///   4) 再放一本旧书、再双击 → 看服务端回的是哪句话
/// </summary>
internal static class Program
{
    private static Envir _envir;
    private static int _uid = 700001;

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        string dir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
                     ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(dir));
        if (!File.Exists(Envir.DatabasePath)) { Console.WriteLine("找不到 " + Envir.DatabasePath); return 2; }

        Settings.Load();
        _envir = Envir.Main;
        if (!_envir.LoadDB()) { Console.WriteLine("LoadDB 失败"); return 2; }

        ItemInfo book = _envir.ItemInfoList.FirstOrDefault(x => x != null && x.Name == "旧书");
        QuestInfo quest = _envir.QuestInfoList.FirstOrDefault(x => x != null && x.FileName == "humup01");

        if (book == null || quest == null)
        {
            Console.WriteLine($"物品={book != null} 任务={quest != null}");
            return 2;
        }

        Console.WriteLine($"旧书: [{book.Index}] Type={book.Type}({(int)book.Type}) Shape={book.Shape} Stack={book.StackSize}");
        Console.WriteLine($"任务: [{quest.Index}] {quest.FileName} {quest.Name} 类型={quest.Type} 等级≥{quest.RequiredMinLevel}");
        Console.WriteLine();

        var conn = new RecConnection(1) { Account = new AccountInfo { Index = 1, AccountID = "humuptest" } };
        var ch = new CharacterInfo
        {
            Index = 1,
            Name = "旧书测试",
            Class = MirClass.战士,
            Gender = MirGender.男性,
            Level = 60,
            HP = 1000,
            MP = 300,
        };

        var p = new PlayerObject(ch, conn);

        if (p.Info.Inventory == null || p.Info.Inventory.Length < 5)
            p.Info.Inventory = new UserItem[46];

        // ---------- 1. 第一次双击旧书 ----------
        UserItem item1 = Give(p, book);
        conn.Clear();
        Safe(() => p.UseItem(item1.UniqueID));
        Dump("① 双击旧书", conn, p, quest.Index);

        if (p.CurrentQuests.Any(x => x.Index == quest.Index))
        {
            QuestProgressInfo q = p.CurrentQuests.First(x => x.Index == quest.Index);
            Console.WriteLine($"    任务进度: Start={q.StartDateTime:HH:mm:ss} End={q.EndDateTime:HH:mm:ss} Completed={q.Completed}");
        }

        // ---------- 2. 放弃任务 ----------
        conn.Clear();
        Safe(() => p.AbandonQuest(quest.Index));
        Dump("② 放弃任务", conn, p, quest.Index);

        // ---------- 3. 再双击一本旧书 ----------
        UserItem item2 = Give(p, book);
        conn.Clear();
        Safe(() => p.UseItem(item2.UniqueID));
        Dump("③ 再双击旧书", conn, p, quest.Index);

        // ---------- 4. 模拟「第 1 环已交给破关虎」（完成记录里有 179，但没登仙） ----------
        if (!p.CompletedQuests.Contains(quest.Index)) p.CompletedQuests.Add(quest.Index);
        UserItem item3 = Give(p, book);
        conn.Clear();
        Safe(() => p.UseItem(item3.UniqueID));
        Dump("④ 完成记录里有本条 + 再双击（不该说已登仙）", conn, p, quest.Index);

        // ---------- 5. GM 重置：清空羽化任务链 ----------
        conn.Clear();
        int cleared = p.ResetHumUpQuestChain();
        Console.WriteLine($"---- ⑤ @HUMUPRESET 清链 ----\n   清掉 {cleared} 条记录，完成任务表下发 [{string.Join(",", p.CompletedQuests)}]\n");

        UserItem item4 = Give(p, book);
        conn.Clear();
        Safe(() => p.UseItem(item4.UniqueID));
        Dump("⑥ 清链后再双击旧书（应能重新接）", conn, p, quest.Index);

        Console.WriteLine();
        Console.WriteLine("结束（未修改任何文件）");
        return 0;
    }

    private static void Safe(Action a)
    {
        try { a(); }
        catch (Exception ex)
        {
            Console.WriteLine("   [异常] " + ex.GetType().Name + ": " + ex.Message +
                              "  @ " + (ex.StackTrace ?? "").Split('\n').FirstOrDefault(l => l.Contains("Server."))?.Trim());
        }
    }

    private static UserItem Give(PlayerObject p, ItemInfo info)
    {
        for (int i = 0; i < p.Info.Inventory.Length; i++)
        {
            if (p.Info.Inventory[i] != null) continue;

            var it = new UserItem(info)
            {
                UniqueID = (ulong)(++_uid),
                Count = 1,
                MaxDura = info.Durability,
                CurrentDura = info.Durability,
            };
            p.Info.Inventory[i] = it;
            return it;
        }
        throw new InvalidOperationException("背包已满");
    }

    private static void Dump(string title, RecConnection conn, PlayerObject p, int questIndex)
    {
        Console.WriteLine("---- " + title + " ----");

        foreach (Packet pk in conn.Sent.ToArray())
        {
            switch (pk)
            {
                case S.Chat c:
                    Console.WriteLine($"   聊天({c.Type}): {c.Message}");
                    break;
                case S.ChangeQuest cq:
                    Console.WriteLine($"   任务包: Id={cq.Quest?.Id} state={cq.QuestState} Taken={cq.Quest?.Taken} Completed={cq.Quest?.Completed}");
                    break;
                case S.CompleteQuest _:
                    Console.WriteLine($"   完成任务表下发: [{string.Join(",", p.CompletedQuests)}]");
                    break;
                case S.UseItem u:
                    Console.WriteLine($"   使用结果: Success={u.Success}");
                    break;
                case S.DeleteItem d:
                    Console.WriteLine($"   删除物品: id={d.UniqueID} count={d.Count}（扣书）");
                    break;
                default:
                    Console.WriteLine("   " + pk.GetType().Name);
                    break;
            }
        }

        bool inCur = p.CurrentQuests.Any(x => x.Index == questIndex);
        bool inDone = p.CompletedQuests.Contains(questIndex);
        int books = p.Info.Inventory.Count(x => x != null && x.Info.Name == "旧书");

        Console.WriteLine($"   → 任务栏有: {inCur}   完成记录有: {inDone}   背包旧书: {books} 本");
        Console.WriteLine();
    }
}

/// <summary>把服务端下发的包收进列表（不涉及网络）。</summary>
internal sealed class RecConnection : MirConnection
{
    public readonly List<Packet> Sent = new List<Packet>();
    public RecConnection(int sessionID) : base(sessionID, null) { Stage = GameStage.Game; }
    public override void Enqueue(Packet p) { lock (Sent) Sent.Add(p); }
    public override void Process() { }
    public void Clear() => Sent.Clear();
}
