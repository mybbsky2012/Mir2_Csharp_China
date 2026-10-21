using System.Reflection;

namespace RecycleProbe;

/// <summary>
/// 部署后的只读验证：确认客户端与服务端库里的「一键回收」要素都在。
/// 用法: RecycleProbe.exe &lt;客户端目录(E:\micromir2)&gt; &lt;服务端目录(Mir2Server)&gt;
/// </summary>
internal static class Program
{
    private static int _pass, _fail;

    private static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "[通过] " : "[失败] ") + name);
        if (ok) _pass++; else _fail++;
    }

    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        string clientDir = args.Length > 0 ? args[0] : @"E:\micromir2";
        string serverDir = args.Length > 1 ? args[1] : @"E:\BaiduNetdiskDownload\韩Mir2\20260915更新\Mir2Server";

        // ---------- 客户端 ----------
        string clientPath = Path.Combine(clientDir, "Client.dll");
        if (File.Exists(clientPath))
        {
            Assembly client = Assembly.LoadFrom(clientPath);
            Type inv = client.GetType("Client.MirScenes.Dialogs.InventoryDialog");
            Check("InventoryDialog.RecycleLabel 字段存在",
                  inv?.GetField("RecycleLabel") != null);

            // C.Chat 在 Shared.dll（ClientPackets 命名空间）
            string sharedPath = Path.Combine(clientDir, "Shared.dll");
            if (File.Exists(sharedPath))
            {
                Assembly shared = Assembly.LoadFrom(sharedPath);
                Type chat = shared.GetType("ClientPackets.Chat");
                Check("Shared.dll 里 C.Chat 包存在（按钮走 @命令 通道）", chat != null);
            }
            else
            {
                Check("找到 Shared.dll：" + sharedPath, false);
            }
        }
        else
        {
            Check("找到 Client.dll：" + clientPath, false);
        }

        // ---------- 服务端库 ----------
        string libPath = Path.Combine(serverDir, "Server.Library.dll");
        if (File.Exists(libPath))
        {
            Assembly lib = Assembly.LoadFrom(libPath);

            Type rm = lib.GetType("Server.MirObjects.RecycleManager");
            MethodInfo rec = rm?.GetMethod("Recycle", BindingFlags.Public | BindingFlags.Static);
            Check("RecycleManager.Recycle(玩家, 套装名) 存在", rec != null);

            MethodInfo recKw = rm?.GetMethod("RecycleByKeyword", BindingFlags.Public | BindingFlags.Static);
            Check("RecycleManager.RecycleByKeyword(玩家, 关键词) 存在（按名字收）", recKw != null);

            Type at = lib.GetType("Server.MirObjects.ActionType");
            Check("ActionType.Recycle 存在",
                  at != null && Enum.GetNames(at).Contains("Recycle"));
            Check("ActionType.RecycleKey 存在",
                  at != null && Enum.GetNames(at).Contains("RecycleKey"));
        }
        else
        {
            Check("找到 Server.Library.dll：" + libPath, false);
        }

        // ---------- 脚本文件 ----------
        string script = Path.Combine(serverDir, @"Envir\SystemScripts\00Default\特殊消耗品\一键回收令.txt");
        Check("一键回收脚本文件存在", File.Exists(script));

        if (File.Exists(script))
        {
            string[] lines = File.ReadAllLines(script);
            Check("脚本含物品触发页 [@_UseItem(80)]",
                  lines.Any(l => l.TrimStart().StartsWith("[@_UseItem(80)]", StringComparison.OrdinalIgnoreCase)));
            Check("脚本含命令入口页 [@一键回收]",
                  lines.Any(l => l.TrimStart().StartsWith("[@一键回收]", StringComparison.OrdinalIgnoreCase)));
            Check("脚本含 35 个套装确认页(39 含目录)与执行页(35)",
                  lines.Count(l => l.TrimStart().StartsWith("[@回收")) >= 39 &&
                  lines.Count(l => l.TrimStart().StartsWith("[@执行")) >= 35);
            Check("脚本含 RECYCLE 命令调用",
                  lines.Any(l => l.TrimStart().StartsWith("RECYCLE ", StringComparison.OrdinalIgnoreCase)));

            // ---- 非套装（收所有 Set=非套装 的装备）----
            Check("脚本含非套装确认页 [@回收非套装]",
                  lines.Any(l => l.Trim().Equals("[@回收非套装]")));
            Check("脚本含非套装二次确认页 [@回收非套装2]",
                  lines.Any(l => l.Trim().Equals("[@回收非套装2]")));
            Check("脚本含非套装执行页 [@执行非套装]",
                  lines.Any(l => l.Trim().Equals("[@执行非套装]")));
            Check("脚本含 RECYCLE 非套装 调用",
                  lines.Any(l => l.Trim().Equals("RECYCLE 非套装", StringComparison.OrdinalIgnoreCase)));
            Check("主菜单入口指向 [@回收非套装]",
                  lines.Any(l => l.Contains("@回收非套装")));
            Check("已移除沃玛/祖玛/赤月分组",
                  !lines.Any(l => l.Contains("沃玛系列") || l.Contains("祖玛系列") || l.Contains("赤月系列")));
        }

        string useItems = Path.Combine(serverDir, @"Envir\SystemScripts\00Default\UseItems.txt");
        Check("UseItems.txt 已挂 #INSERT",
              File.Exists(useItems) && File.ReadAllLines(useItems).Any(l => l.Contains("一键回收令")));

        string custom = Path.Combine(serverDir, @"Envir\SystemScripts\00Default\CustomCommands.txt");
        Check("CustomCommands.txt 已挂 [@_CUSTOMCOMMAND(一键回收)]",
              File.Exists(custom) && File.ReadAllLines(custom).Any(l => l.Contains("一键回收")));

        Console.WriteLine();
        Console.WriteLine($"结果: {_pass} 通过 / {_fail} 失败");
        return _fail == 0 ? 0 : 1;
    }
}
