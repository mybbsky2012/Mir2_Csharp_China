using System.Text;

// 只读工具：把一个 .Lib 图库内嵌的「动作帧表」打印出来。
// 用途：核对某个怪物图库到底有没有 近距攻击/远程攻击/施法/石化 这些动作帧段，
//       以及每段从第几帧开始、多少帧、间隔多少 —— 客户端特效分支里的魔法数字全是从这里取的。
// 用法： LibFrameDump.exe <lib路径> [--all]
//   默认只打印动作表 + 图像总数；带 --all 额外打印每个动作的 Effect 段。
// 格式说明（版本3）：int32 version / int32 imageCount / int32 frameSeek / int32[imageCount] 索引表
//                    frameSeek 处：int32 frameCount，随后每条 = byte action + Frame(8×int32 + 2×bool)
internal static class Program
{
    private static int Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        if (args.Length == 0)
        {
            Console.WriteLine("用法：LibFrameDump.exe <lib路径> [--all]");
            return 2;
        }

        string path = args[0];
        bool all = args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase));

        if (!File.Exists(path))
        {
            Console.WriteLine("找不到文件：" + Path.GetFullPath(path));
            return 2;
        }

        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new BinaryReader(fs);

        int version = reader.ReadInt32();
        int imageCount = reader.ReadInt32();

        int frameSeek = 0;
        if (version >= 3) frameSeek = reader.ReadInt32();

        Console.WriteLine($"文件 {path}");
        Console.WriteLine($"  版本 {version}，图像(帧)总数 {imageCount}，文件大小 {new FileInfo(path).Length / 1024 / 1024} MB");

        if (version < 3)
        {
            Console.WriteLine("  版本 < 3，没有内嵌帧表。");
            return 0;
        }

        fs.Seek(frameSeek, SeekOrigin.Begin);
        int frameCount = reader.ReadInt32();
        Console.WriteLine($"  动作帧段 {frameCount} 条");

        if (frameCount <= 0)
        {
            Console.WriteLine("  ！没有帧表");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("  序号 动作              起始  帧数  跨步  间隔  帧范围(含跨步)");

        int prevEnd = -1;
        for (int i = 0; i < frameCount; i++)
        {
            byte action = reader.ReadByte();

            int start = reader.ReadInt32();
            int count = reader.ReadInt32();
            int skip = reader.ReadInt32();
            int interval = reader.ReadInt32();
            int effStart = reader.ReadInt32();
            int effCount = reader.ReadInt32();
            int effSkip = reader.ReadInt32();
            int effInterval = reader.ReadInt32();
            bool reverse = reader.ReadBoolean();
            bool blend = reader.ReadBoolean();

            string name = ActionName(action);

            // 绘制时实际用到的最大图像编号：Start + (Count-1)*(Skip+1)
            int lastImage = count > 0 ? start + (count - 1) * (skip + 1) : start;

            Console.WriteLine(string.Format("  {0,3}  {1,-16}  {2,5}  {3,4}  {4,4}  {5,4}  {6} ~ {7}{8}",
                i, name, start, count, skip, interval, start, lastImage,
                (lastImage != prevEnd + 1 && prevEnd >= 0) ? "   <跨度不连续>" : ""));

            if (all && effCount > 0)
                Console.WriteLine(string.Format("        └ 特效段 start={0} count={1} skip={2} interval={3}{4}{5}",
                    effStart, effCount, effSkip, effInterval,
                    reverse ? " reverse" : "", blend ? " blend" : ""));

            if (lastImage > prevEnd) prevEnd = lastImage;
        }

        Console.WriteLine();
        Console.WriteLine($"  帧表覆盖到图像 {prevEnd}（图像总数 {imageCount}）");
        return 0;
    }

    private static string ActionName(byte a)
    {
        string[] names =
        {
            "站立", "行走", "跑步", "推开", "左冲", "右冲", "冲击失败",
            "站立姿势", "站立姿势2",
            "近距攻击1", "近距攻击2", "近距攻击3", "近距攻击4", "近距攻击5",
            "远程攻击1", "远程攻击2", "远程攻击3", "特殊攻击",
            "被击", "挖矿展示", "施法", "死亡", "死后尸体", "挖后尸骸",
            "石化苏醒", "切换LIB", "石化状态", "召唤初现",
            "复活动作", "坐下", "挖矿",
            "刺客潜行", "刺客冲击", "刺客步刺",
            "弓箭行走", "弓箭奔跑", "弓箭跳跃"
        };

        if (a < names.Length) return names[a];
        return "未知(" + a + ")";
    }
}
