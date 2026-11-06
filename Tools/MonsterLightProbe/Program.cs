using System.Text;
using Server;
using Server.MirEnvir;

namespace MonsterLightProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        Settings.Load();
        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB fail"); return 2; }

        foreach (var m in envir.MonsterInfoList.Where(m => m != null && (m.Name.Contains("海龙") || m.Name.Contains("水魂鬼") || m.Name.Contains("八爪鱼") || m.Name.Contains("铠甲怪"))))
            Console.WriteLine($"#{m.Index,4} AI={m.AI,-4} Effect={m.Effect}  {m.Name}");
        return 0;
    }
}
