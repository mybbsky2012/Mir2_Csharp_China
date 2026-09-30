using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Server;
using Server.MirDatabase;

namespace SpliceMagic
{
    /// <summary>
    /// 把「来源 Server.MirDB」里的技能段（MagicInfoList）整体替换进「目标 Server.MirDB」，
    /// 目标库的地图/物品/怪物/NPC/任务/商城等一切数据保持不变。
    ///
    /// 原理：Server.MirDB 是顺序二进制（见 Envir.SaveDB / LoadDB）：
    ///   头部10个int → 地图段 → 物品段 → 怪物段 → NPC段 → 任务段 → 神龙段 → 技能段 → 商城段 → 攻城段 → 刷怪计时
    /// 本工具复用 Server.dll 里真实的解析类逐段推进流，精确切出技能段字节区间后做拼接。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 2 && args[0] == "--testload")
                {
                    return TestLoad(args[1]);
                }

                // --dump <db> : 列出技能段全部记录（名称/Spell/关键数值）
                if (args.Length >= 2 && args[0] == "--dump")
                {
                    return Dump(args[1]);
                }

                // 从 v117 导出的技能清单（制表符分隔）重建 v110 格式技能段并替换目标库技能段
                if (args.Length >= 2 && args[0] == "--import117")
                {
                    if (args.Length < 4)
                    {
                        Console.WriteLine("用法: SpliceMagic --import117 <技能清单txt> <目标Server.MirDB> <输出文件>");
                        return 1;
                    }
                    return Import117(args[1], args[2], args[3]);
                }

                if (args.Length < 3)
                {
                    Console.WriteLine("用法: SpliceMagic <来源Server.MirDB> <目标Server.MirDB> <输出文件>");
                    return 1;
                }

                string sourcePath = Path.GetFullPath(args[0]);
                string targetPath = Path.GetFullPath(args[1]);
                string outputPath = Path.GetFullPath(args[2]);

                // 目标库与当前源码版本一致，可直接用解析类定位技能段
                var target = LocateMagicSection(targetPath);
                Console.WriteLine($"目标: version={target.Version} 技能数={target.Count} 技能段=[0x{target.Start:X}, 0x{target.End:X}) 文件大小={target.FileSize}");

                // 来源库可能是更新版引擎(v117)写的，前置段无法用当前类解析，
                // 改用锚点法：目标库第一个技能名的字节序列在来源库里全局唯一出现在技能段，
                // 定位后从其前的数量 int 开始按字节兼容的 MagicInfo 格式向后验证解析。
                var source = LocateMagicSectionByAnchor(sourcePath, targetPath, target);
                Console.WriteLine($"来源: 技能数={source.Count} 技能段=[0x{source.Start:X}, 0x{source.End:X}) 文件大小={source.FileSize}");

                if (source.Count == 0)
                {
                    Console.WriteLine("错误: 来源库技能数为 0，拒绝替换。");
                    return 2;
                }

                byte[] targetBytes = File.ReadAllBytes(targetPath);
                byte[] sourceBytes = File.ReadAllBytes(sourcePath);

                using var output = File.Create(outputPath);
                // 目标头部 + 前置各段（地图/物品/怪物/NPC/任务/神龙）
                output.Write(targetBytes, 0, (int)target.Start);
                // 来源的技能段（含数量 int）
                output.Write(sourceBytes, (int)source.Start, (int)(source.End - source.Start));
                // 目标的技能段之后内容（商城/攻城/刷怪计时）
                output.Write(targetBytes, (int)target.End, (int)(targetBytes.Length - target.End));

                Console.WriteLine($"已生成: {outputPath} ({output.Length} 字节)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("异常: " + ex);
                return 3;
            }
        }

        /// <summary>
        /// 按「技能名」合并：两代引擎(v117/v110)的 Spell 编号体系不同，按编号对齐会张冠李戴。
        /// 保留原库每条记录的 Name/Spell/Icon（这是当前客户端实际使用的编号与图标），
        /// 其余数值字段（耗蓝/等级/需求/延迟/威力/倍率）用 v117 清单里同名技能的数据覆盖。
        /// v117 独有而原库没有的技能不导入（其编号在 v110 客户端里没有对应技能，加了也没法用）。
        /// </summary>
        private static int Import117(string listPath, string targetPath, string outputPath)
        {
            // 读取 v117 导出清单：name -> parts
            var sourceByName = new Dictionary<string, string[]>();
            foreach (var line in File.ReadAllLines(listPath))
            {
                if (line.Trim().Length == 0) continue;
                var parts = line.Split('\t');
                if (parts.Length < 20)
                {
                    Console.WriteLine("跳过格式异常行: " + line);
                    continue;
                }
                sourceByName[parts[0]] = parts;
            }

            var target = LocateMagicSection(targetPath);
            Console.WriteLine($"目标: version={target.Version} 技能数={target.Count} 技能段=[0x{target.Start:X}, 0x{target.End:X})");

            // 逐条解析原库技能记录，同名则用 "1" 的数值重建记录
            int updated = 0, untouched = 0;
            var records = new List<byte[]>();
            byte[] targetBytesAll = File.ReadAllBytes(targetPath);
            using (var msT = new MemoryStream(targetBytesAll))
            {
                msT.Position = target.Start;
                using var readerT = new BinaryReader(msT);
                int tCount = readerT.ReadInt32();
                for (int i = 0; i < tCount; i++)
                {
                    long recStart = msT.Position;
                    string name = readerT.ReadString();   // Name
                    int spell = readerT.ReadByte();       // Spell
                    int icon = readerT.ReadByte();        // Icon
                    byte[] rest = readerT.ReadBytes(36);  // BaseCost..MultiplierBonus（36 字节）

                    if (sourceByName.TryGetValue(name, out var p))
                    {
                        using var ms = new MemoryStream();
                        using (var w = new BinaryWriter(ms))
                        {
                            w.Write(name);
                            w.Write((byte)spell);         // 保留原编号（客户端实际使用的编号体系）
                            w.Write((byte)icon);          // 保留原图标（与客户端资源对应）
                            w.Write((byte)int.Parse(p[2]));   // BaseCost
                            w.Write((byte)int.Parse(p[3]));   // LevelCost
                            w.Write((byte)int.Parse(p[5]));   // Level1
                            w.Write((byte)int.Parse(p[6]));   // Level2
                            w.Write((byte)int.Parse(p[7]));   // Level3
                            w.Write((ushort)int.Parse(p[8])); // Need1
                            w.Write((ushort)int.Parse(p[9])); // Need2
                            w.Write((ushort)int.Parse(p[10]));// Need3
                            w.Write((uint)long.Parse(p[11])); // DelayBase
                            w.Write((uint)long.Parse(p[12])); // DelayReduction
                            w.Write((ushort)int.Parse(p[13]));// PowerBase
                            w.Write((ushort)int.Parse(p[14]));// PowerBonus
                            w.Write((ushort)int.Parse(p[15]));// MPowerBase
                            w.Write((ushort)int.Parse(p[16]));// MPowerBonus
                            w.Write((byte)int.Parse(p[17]));  // Range
                            w.Write(float.Parse(p[18], CultureInfo.InvariantCulture)); // MultiplierBase
                            w.Write(float.Parse(p[19], CultureInfo.InvariantCulture)); // MultiplierBonus
                        }
                        records.Add(ms.ToArray());
                        updated++;
                    }
                    else
                    {
                        using var ms = new MemoryStream();
                        using (var w = new BinaryWriter(ms))
                        {
                            w.Write(name);
                            w.Write((byte)spell);
                            w.Write((byte)icon);
                            w.Write(rest);
                        }
                        records.Add(ms.ToArray());
                        untouched++;
                    }
                }
            }

            Console.WriteLine($"合并: {updated} 条用「1」的数值覆盖，{untouched} 条原库独有保持原样");

            byte[] section;
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(records.Count);
                    foreach (var r in records)
                        writer.Write(r);
                }
                section = ms.ToArray();
            }

            byte[] targetBytes = File.ReadAllBytes(targetPath);
            using var output = File.Create(outputPath);
            output.Write(targetBytes, 0, (int)target.Start);
            output.Write(section, 0, section.Length);
            output.Write(targetBytes, (int)target.End, (int)(targetBytes.Length - target.End));

            Console.WriteLine($"已写入 {records.Count} 个技能，输出 {output.Length} 字节 -> {outputPath}");
            return 0;
        }

        /// <summary>列出 DB 技能段全部记录（直接按字节解析技能段，不依赖整体 LoadDB）。</summary>
        private static int Dump(string dbPath)
        {
            var info = LocateMagicSection(dbPath);
            Console.WriteLine($"version={info.Version} 技能数={info.Count}");

            using var stream = File.OpenRead(dbPath);
            stream.Position = info.Start;
            using var reader = new BinaryReader(stream);
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                int spell = reader.ReadByte();
                int baseCost = reader.ReadByte();
                int levelCost = reader.ReadByte();
                int icon = reader.ReadByte();
                int l1 = reader.ReadByte(); int l2 = reader.ReadByte(); int l3 = reader.ReadByte();
                int n1 = reader.ReadUInt16(); int n2 = reader.ReadUInt16(); int n3 = reader.ReadUInt16();
                uint db1 = reader.ReadUInt32(); uint db2 = reader.ReadUInt32();
                int pb = reader.ReadUInt16(); int po = reader.ReadUInt16();
                int mpb = reader.ReadUInt16(); int mpo = reader.ReadUInt16();
                int range = reader.ReadByte();
                float mb = reader.ReadSingle(); float mo = reader.ReadSingle();
                Console.WriteLine($"{spell,4}  {name,-12} lv[{l1}/{l2}/{l3}] need[{n1}/{n2}/{n3}] delay[{db1}/{db2}] pow[{pb}+{po}] mpow[{mpb}+{mpo}] rng={range} mult[{mb}+{mo}]");
            }
            return 0;
        }

        /// <summary>用真实服务端加载逻辑验证 DB 文件能否被当前源码解析。</summary>
        private static int TestLoad(string dbPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "SpliceMagicTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            File.Copy(dbPath, Path.Combine(tempDir, "Server.MirDB"));

            string oldCwd = Environment.CurrentDirectory;
            Environment.CurrentDirectory = tempDir;
            try
            {
                var envir = Server.MirEnvir.Envir.Main;
                bool ok = envir.LoadDB();

                var t = typeof(Server.MirEnvir.Envir);
                foreach (var name in new[] { "Version", "MinVersion", "CustomVersion", "LoadVersion" })
                {
                    var f = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (f != null && f.IsStatic)
                        Console.WriteLine($"{name} = {f.GetValue(null)}");
                }

                while (MessageQueue.Instance.MessageLog.TryDequeue(out var msg))
                    Console.Write("MQ: " + msg);

                Console.WriteLine($"LoadDB = {ok}");
                if (ok)
                    Console.WriteLine($"MagicInfoList.Count = {envir.MagicInfoList.Count}");
                return ok ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine("LoadDB 异常: " + ex.Message);
                return 3;
            }
            finally
            {
                Environment.CurrentDirectory = oldCwd;
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// 锚点法定位来源库的技能段（用于来源库版本比当前源码新、前置段解析不了的情况）。
        /// MagicInfo 记录在两代引擎间字节兼容；技能名只出现在技能段，
        /// 用目标库第一个技能名的完整字节形态（7位长度前缀+UTF8）在来源库里搜首个出现位置。
        /// </summary>
        private static DbInfo LocateMagicSectionByAnchor(string sourcePath, string targetPath, DbInfo target)
        {
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);

            // 目标库第一个技能名的字节形态：[7位长度][UTF8 内容]
            byte[] targetBytes = File.ReadAllBytes(targetPath);
            int nameLen = targetBytes[(int)target.Start + 4];
            if (nameLen > 127) throw new Exception("目标库第一个技能名长度异常");
            byte[] anchor = new byte[1 + nameLen];
            Array.Copy(targetBytes, (int)target.Start + 4, anchor, 0, 1 + nameLen);
            string anchorName = System.Text.Encoding.UTF8.GetString(anchor, 1, nameLen);
            Console.WriteLine($"锚点技能名: \"{anchorName}\"");

            // 全局搜索锚点（技能名不会出现在 DB 其他段；物品/怪物/NPC 名不会与之相同且前后结构不同）
            long anchorPos = -1;
            for (long i = 4; i <= sourceBytes.Length - anchor.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < anchor.Length; j++)
                {
                    if (sourceBytes[i + j] != anchor[j]) { match = false; break; }
                }
                if (match) { anchorPos = i; break; }
            }

            if (anchorPos < 0) throw new Exception("来源库中未找到锚点技能名");

            // 数量 int 位于第一个技能记录前 4 字节
            long start = anchorPos - 4;
            Console.WriteLine($"来源技能段候选起点: 0x{start:X}");

            // 向后按字节兼容格式验证解析全部技能记录
            var info = new DbInfo { Start = start, FileSize = sourceBytes.Length };
            using var stream = new MemoryStream(sourceBytes);
            stream.Position = start;
            using var reader = new BinaryReader(stream);

            info.Count = reader.ReadInt32();
            if (info.Count <= 0 || info.Count > 500) throw new Exception($"技能数量异常: {info.Count}");

            var names = new List<string>();
            for (int i = 0; i < info.Count; i++)
            {
                string name = reader.ReadString();                       // Name
                reader.ReadByte();                                       // Spell
                reader.ReadBytes(3);                                     // BaseCost, LevelCost, Icon
                reader.ReadBytes(3);                                     // Level1-3
                reader.ReadBytes(6);                                     // Need1-3 (u16×3)
                reader.ReadBytes(8);                                     // DelayBase, DelayReduction (u32)
                reader.ReadBytes(8);                                     // Power/MPower (u16×4)
                reader.ReadByte();                                       // Range
                reader.ReadBytes(8);                                     // MultiplierBase/Bonus (float×2)
                if (name.Length == 0 || name.Length > 40) throw new Exception($"第 {i} 个技能名长度异常: {name.Length}");
                names.Add(name);
            }
            info.End = stream.Position;

            Console.WriteLine($"来源技能段验证通过，共 {info.Count} 个技能，前几个: {string.Join(", ", names.Take(6).ToArray())}");
            return info;
        }

        private sealed class DbInfo
        {
            public int Version;
            public int Count;
            public long Start;   // 技能段起始（含数量 int）
            public long End;     // 技能段结束
            public long FileSize;
        }

        /// <summary>按 LoadDB 的读取顺序推进流，返回技能段的字节区间。</summary>
        private static DbInfo LocateMagicSection(string path)
        {
            var info = new DbInfo { FileSize = new FileInfo(path).Length };

            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            int version = reader.ReadInt32();
            int customVersion = reader.ReadInt32();
            info.Version = version;

            // MapInfo/RespawnInfo 等构造函数内部会读静态 Envir.LoadVersion 决定字段分支，
            // 必须先设置成文件里的真实版本，否则解析会错位。
            Server.MirEnvir.Envir.LoadVersion = version;
            Server.MirEnvir.Envir.LoadCustomVersion = customVersion;

            // 头部索引（与 LoadDB 保持一致）
            reader.ReadInt32();   // MapIndex
            reader.ReadInt32();   // ItemIndex
            reader.ReadInt32();   // MonsterIndex
            reader.ReadInt32();   // NPCIndex
            reader.ReadInt32();   // QuestIndex
            if (version >= 63) reader.ReadInt32();   // GameshopIndex
            if (version >= 66) reader.ReadInt32();   // ConquestIndex
            if (version >= 68) reader.ReadInt32();   // RespawnIndex

            Console.WriteLine($"  [{Path.GetFileName(path)}] version={version} custom={customVersion}");

            int count = reader.ReadInt32();
            Console.WriteLine($"  地图数={count}");
            for (int i = 0; i < count; i++)
            {
                long mapStart = stream.Position;
                try
                {
                    var mi = new MapInfo(reader);
                    if (i < 3 || i == count - 1)
                        Console.WriteLine($"    地图[{i}] Index={mi.Index} File=\"{mi.FileName}\" Title=\"{mi.Title}\" 安全区={mi.SafeZones.Count} 刷怪={mi.Respawns.Count} 起点=0x{mapStart:X}");
                }
                catch { Console.WriteLine($"  地图段解析失败于第 {i} 个地图 (起点 0x{mapStart:X}, 失败点 0x{stream.Position:X})"); throw; }
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++) new ItemInfo(reader, version, customVersion);

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++) new MonsterInfo(reader);

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++) new NPCInfo(reader);

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++) new QuestInfo(reader);

            new DragonInfo(reader);

            // 当前位置 = 技能段「数量 int」的起点
            info.Start = stream.Position;
            info.Count = reader.ReadInt32();
            for (int i = 0; i < info.Count; i++) new MagicInfo(reader, version, customVersion);
            info.End = stream.Position;

            return info;
        }
    }
}
