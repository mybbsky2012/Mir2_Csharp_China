using System.Text;
using Server;
using Server.MirEnvir;

internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        Settings.Load();
        var envir = Envir.Main;
        if (!envir.LoadDB()) { Console.WriteLine("LoadDB fail"); return 2; }

        Console.WriteLine("=== 头盔样本（男）===");
        foreach (var it in envir.ItemInfoList.Where(x => x != null && x.Type == ItemType.头盔)
                     .OrderBy(x => x.Index).Take(14))
            Console.WriteLine($"shape={it.Shape,3} image={it.Image,5} #{it.Index,5} {it.Name}");
        return 0;
    }
}
