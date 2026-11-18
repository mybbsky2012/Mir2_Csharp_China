using System.Text;
using Server;
using Server.MirEnvir;
using Server.MirDatabase;

namespace LoginDiag;

/// <summary>
/// 只读诊断工具（11-10）：定位「选完人物进不去游戏，卡载入后弹回选人」。
///
/// 服务端 StartGame 会依次判断：
///   1) Settings.AllowStartGame          → 否则 Result=0「服务器维护禁止登录」
///   2) Account == null                  → Result=1「尚未登录」
///   3) 找不到角色                       → Result=2「没有激活角色」
///   4) 当前地图/绑定地图无效            → Result=3「无效地图或没有新手出生点」
/// 客户端收到非 4 的结果都会撤掉载入画面弹框、退回选人界面，正好是用户描述的现象。
///
/// 本工具离线把 1~4 的前置条件全查一遍（含地图文件是否存在、装备引用的物品是否还在），
/// 不改任何数据。
///
/// 用法：把 exe 放到服务端目录（Server.MirDB / Server.MirADB 所在处）运行。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 输出重定向时可能失败 */ }

        if (!File.Exists(Envir.DatabasePath))
        {
            Console.WriteLine($"当前目录下找不到 {Envir.DatabasePath}，请在服务端目录运行。");
            return 2;
        }

        Settings.Load();

        var envir = Envir.Main;
        if (!envir.LoadDB())
        {
            Console.WriteLine("!! LoadDB 失败");
            return 2;
        }

        Console.WriteLine("════════ 基础开关 ════════");
        Console.WriteLine($"AllowStartGame = {Settings.AllowStartGame}   （false → 所有人 Result=0，进不去）");
        Console.WriteLine($"地图信息 {envir.MapInfoList.Count} / 物品 {envir.ItemInfoList.Count} / " +
                          $"怪物 {envir.MonsterInfoList.Count} / NPC {envir.NPCInfoList.Count} / " +
                          $"任务 {envir.QuestInfoList.Count} / 商店 {envir.GameShopList.Count}");

        Console.WriteLine();
        Console.WriteLine("════════ 地图文件存在性 ════════");
        int missingMap = 0;
        foreach (var m in envir.MapInfoList)
        {
            if (m == null) continue;
            string file = Path.Combine(Settings.MapPath, m.FileName + ".map");
            if (!File.Exists(file))
            {
                missingMap++;
                Console.WriteLine($"  缺失: Index={m.Index,-4} {m.FileName,-24} {m.Title}");
            }
        }
        Console.WriteLine(missingMap == 0 ? "  全部地图文件都在。" : $"  共有 {missingMap} 个地图文件缺失。");

        // ★LoadAccounts 里读 Buff 需要 Envir.BuffInfoList（真实服务端在 StartEnvir 里才填），
        //   这里手动补上，否则会抛 NotImplementedException「xxx 尚未实施」。
        envir.BuffInfoList.Clear();
        try
        {
            foreach (var b in BuffInfo.Load()) envir.BuffInfoList.Add(b);
        }
        catch (Exception ex) { Console.WriteLine("!! BuffInfo.Load 失败：" + ex.Message); }

        Console.WriteLine();
        Console.WriteLine("════════ 账号 / 角色 ════════");
        try
        {
            envir.LoadAccounts();
        }
        catch (Exception ex)
        {
            Console.WriteLine("!! LoadAccounts 抛异常：" + ex.Message);
            Console.WriteLine(ex);
            return 3;
        }

        Console.WriteLine($"账号 {envir.AccountList.Count} 个");

        var mapByIndex = envir.MapInfoList.Where(m => m != null)
                                          .GroupBy(m => m.Index)
                                          .ToDictionary(g => g.Key, g => g.First());
        var itemByIndex = envir.ItemInfoList.Where(i => i != null)
                                            .GroupBy(i => i.Index)
                                            .ToDictionary(g => g.Key, g => g.First());

        int problems = 0;
        foreach (var acc in envir.AccountList)
        {
            if (acc == null || acc.Characters == null) continue;

            foreach (var ch in acc.Characters)
            {
                if (ch == null) continue;

                var notes = new List<string>();

                // ① 当前地图
                bool curOk = mapByIndex.TryGetValue(ch.CurrentMapIndex, out MapInfo curMap);
                if (!curOk)
                    notes.Add($"当前地图 Index={ch.CurrentMapIndex} 在表里不存在 → Result=3");
                else if (!File.Exists(Path.Combine(Settings.MapPath, curMap.FileName + ".map")))
                    notes.Add($"当前地图文件缺失 {curMap.FileName} → Result=3");

                // ② 绑定/重生地图
                bool bindOk = mapByIndex.TryGetValue(ch.BindMapIndex, out MapInfo bindMap);
                if (!bindOk)
                    notes.Add($"绑定地图 Index={ch.BindMapIndex} 在表里不存在 → Result=3");
                else if (!File.Exists(Path.Combine(Settings.MapPath, bindMap.FileName + ".map")))
                    notes.Add($"绑定地图文件缺失 {bindMap.FileName} → Result=3");

                // ③ 装备/背包引用的物品是否还在（缺了会在 RefreshStats 里炸）
                int badItems = 0;
                string badSample = "";
                void Scan(UserItem[] arr, string tag)
                {
                    if (arr == null) return;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var it = arr[i];
                        if (it == null) continue;
                        if (!itemByIndex.ContainsKey(it.ItemIndex))
                        {
                            badItems++;
                            if (badSample.Length < 60)
                                badSample += $"{tag}[{i}]#{it.ItemIndex} ";
                        }
                    }
                }
                Scan(ch.Equipment, "装");
                Scan(ch.Inventory, "包");
                Scan(ch.QuestInventory, "任");
                if (badItems > 0)
                    notes.Add($"有 {badItems} 格装备/物品的 ItemIndex 在物品表里找不到（{badSample}）");

                if (notes.Count > 0)
                {
                    problems++;
                    Console.WriteLine($"  ✗ [{acc.AccountID}] {ch.Name} Lv{ch.Level} " +
                                      $"cur=({ch.CurrentMapIndex},{ch.CurrentLocation.X},{ch.CurrentLocation.Y}) " +
                                      $"bind=({ch.BindMapIndex},{ch.BindLocation.X},{ch.BindLocation.Y})");
                    foreach (var n in notes) Console.WriteLine("      - " + n);
                }
                else
                {
                    Console.WriteLine($"  ✓ [{acc.AccountID}] {ch.Name} Lv{ch.Level} " +
                                      $"cur=({ch.CurrentMapIndex},{ch.CurrentLocation.X},{ch.CurrentLocation.Y}) " +
                                      $"bind=({ch.BindMapIndex},{ch.BindLocation.X},{ch.BindLocation.Y})");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"有问题的角色：{problems} 个");
        return problems == 0 ? 0 : 4;
    }
}
