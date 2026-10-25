using System;
using System.IO;

namespace MapFishingEditor
{
    /// <summary>
    /// Mir2 "Map 2010 Ver 1.0" 地图文件（Map 2010 头，15 字节/格）。
    /// 钓鱼格的唯一判定：该格 light 字节在 100~119，light-100 为掉落组编号。
    /// 文件结构：标题 20 字节，21~22 = Width(与 23~24 的 xor 异或)，23~24 = xor，25~26 = Height。
    /// 数据从 54 起，顺序为 for x { for y }，每格 15 字节：
    ///   +0..3  int32 ^ 0xAA38AA38，bit 0x20000000 = HighWall
    ///   +6..7  int16 ^ xor，bit 0x8000 = LowWall（另一类阻挡地形）
    ///   +8     门序号
    ///   +13    light  ← 钓鱼标记写在这里
    /// </summary>
    internal sealed class MapDoc
    {
        public const int CellSize = 15;
        public const int DataOffset = 54;
        public const int LightOffset = 13;
        public const int HighFlag = 0x20000000;
        public const int LowFlag = 0x8000;
        public const int BackXor = unchecked((int)0xAA38AA38);

        public const int FishMin = 100;
        public const int FishMax = 119;

        public string FilePath;
        public byte[] Raw;
        public int Width;
        public int Height;
        public int Xor;

        public static MapDoc Load(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length < DataOffset + 1)
                throw new InvalidDataException("文件太小，不是有效的地图文件。");

            if (!(raw[0] == 0x10 && raw[2] == 0x61 && raw[7] == 0x31 && raw[14] == 0x31))
                throw new InvalidDataException(
                    "不是「Map 2010 Ver 1.0」格式的地图。\r\n\r\n" +
                    "本工具只支持这一种格式（这种格式每格 15 字节、light 在 +13）。\r\n" +
                    "其他格式的地图（Mir2 老格式 / C# 自定义格式）请勿用本工具修改。");

            var doc = new MapDoc();
            doc.FilePath = path;
            doc.Raw = raw;
            doc.Xor = BitConverter.ToInt16(raw, 23);
            doc.Width = BitConverter.ToInt16(raw, 21) ^ doc.Xor;
            doc.Height = BitConverter.ToInt16(raw, 25) ^ doc.Xor;

            if (doc.Width <= 0 || doc.Height <= 0 || doc.Width > 8000 || doc.Height > 8000)
                throw new InvalidDataException("地图尺寸异常：" + doc.Width + " x " + doc.Height);

            long expect = DataOffset + (long)doc.Width * doc.Height * CellSize;
            if (expect != raw.Length)
                throw new InvalidDataException(
                    "文件大小与尺寸不符。\r\n期望 " + expect + " 字节（" + doc.Width + "x" + doc.Height + "），实际 " + raw.Length + " 字节。");

            return doc;
        }

        public int Offset(int x, int y)
        {
            return DataOffset + (x * Height + y) * CellSize;
        }

        public bool InRange(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public bool IsHigh(int x, int y)
        {
            int o = Offset(x, y);
            return ((BitConverter.ToInt32(Raw, o) ^ BackXor) & HighFlag) != 0;
        }

        public bool IsLow(int x, int y)
        {
            int o = Offset(x, y);
            return ((BitConverter.ToInt16(Raw, o + 6) ^ Xor) & LowFlag) != 0;
        }

        /// <summary>标准水面：HighWall 位且无 LowWall 位 —— 与本图已有钓点同型的地形</summary>
        public bool IsWater(int x, int y)
        {
            return IsHigh(x, y) && !IsLow(x, y);
        }

        public bool IsBlocked(int x, int y)
        {
            return IsHigh(x, y) || IsLow(x, y);
        }

        public byte GetLight(int x, int y)
        {
            return Raw[Offset(x, y) + LightOffset];
        }

        public void SetLight(int x, int y, byte v)
        {
            Raw[Offset(x, y) + LightOffset] = v;
        }

        public bool IsFish(int x, int y)
        {
            byte l = GetLight(x, y);
            return l >= FishMin && l <= FishMax;
        }

        public string TerrainName(int x, int y)
        {
            bool h = IsHigh(x, y), l = IsLow(x, y);
            if (h && !l) return "水面";
            if (!h && l) return "低地形";
            if (h && l) return "水面(低层)";
            return "陆地";
        }

        public long CountFish()
        {
            long n = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    byte l = GetLight(x, y);
                    if (l >= FishMin && l <= FishMax) n++;
                }
            return n;
        }

        /// <summary>保存；首次保存时把原始文件备份成 .bak_fishing</summary>
        public string Save(bool backup)
        {
            string bak = null;
            if (backup && !File.Exists(FilePath + ".bak_fishing"))
            {
                File.Copy(FilePath, FilePath + ".bak_fishing", true);
                bak = FilePath + ".bak_fishing";
            }
            File.WriteAllBytes(FilePath, Raw);
            return bak;
        }

        public static bool HasBackup(string path)
        {
            return File.Exists(path + ".bak_fishing");
        }

        public void RestoreBackup()
        {
            string bak = FilePath + ".bak_fishing";
            if (!File.Exists(bak)) throw new FileNotFoundException("没有找到备份文件：" + bak);
            byte[] raw = File.ReadAllBytes(bak);
            if (raw.Length != Raw.Length)
                throw new InvalidDataException("备份文件大小与当前文件不一致，未还原。");
            Array.Copy(raw, Raw, raw.Length);
        }
    }
}
