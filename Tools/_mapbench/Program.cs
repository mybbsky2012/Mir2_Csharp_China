using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MapBench
{
    // 与 Client/MirObjects/MapCode.cs 的 CellInfo 完全一致的字段布局
    public class CellInfo
    {
        public short BackIndex;
        public int BackImage;
        public short MiddleIndex;
        public int MiddleImage;
        public short FrontIndex;
        public int FrontImage;
        public byte DoorIndex;
        public byte DoorOffset;
        public byte FrontAnimationFrame;
        public byte FrontAnimationTick;
        public byte MiddleAnimationFrame;
        public byte MiddleAnimationTick;
        public short TileAnimationImage;
        public short TileAnimationOffset;
        public byte TileAnimationFrames;
        public byte Light;
        public byte Unknown;
        public List<object> CellObjects;
        public bool FishingCell;
    }

    static class Program
    {
        static CellInfo[,] Load2D(byte[] bytes)
        {
            var span = bytes.AsSpan();
            int off = 21;
            short w = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(off, 2)); off += 2;
            short xor = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(off, 2)); off += 2;
            short h = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(off, 2)); off += 2;

            int Width = w ^ xor;
            int Height = h ^ xor;
            var MapCells = new CellInfo[Width, Height];

            int offSet = 54;
            int mapSize = Width * Height;
            const int cellSize = 15;
            var mapSpan = span.Slice(offSet, mapSize * cellSize);
            const int BackImageXor = unchecked((int)0xAA38AA38);

            for (int i = 0; i < mapSize; i++)
            {
                int x = i / Height;
                int y = i % Height;
                int co = i * cellSize;

                int backImage = BinaryPrimitives.ReadInt32LittleEndian(mapSpan.Slice(co, 4)) ^ BackImageXor;
                co += 4;
                short middleImage = (short)(BinaryPrimitives.ReadInt16LittleEndian(mapSpan.Slice(co, 2)) ^ xor); co += 2;
                short frontImage = (short)(BinaryPrimitives.ReadInt16LittleEndian(mapSpan.Slice(co, 2)) ^ xor); co += 2;
                byte doorIndex = (byte)(mapSpan[co] & 0x7F); co++;
                byte doorOffset = mapSpan[co]; co++;
                byte faf = mapSpan[co]; co++;
                byte fat = mapSpan[co]; co++;
                short frontIndex = (short)(mapSpan[co] + 2); co++;
                byte light = mapSpan[co]; co++;
                byte unknown = mapSpan[co];

                var cell = new CellInfo
                {
                    BackIndex = 0,
                    BackImage = backImage,
                    MiddleIndex = 1,
                    MiddleImage = middleImage,
                    FrontImage = frontImage,
                    DoorIndex = doorIndex,
                    DoorOffset = doorOffset,
                    FrontAnimationFrame = faf,
                    FrontAnimationTick = fat,
                    FrontIndex = frontIndex,
                    Light = light,
                    Unknown = unknown
                };

                if (cell.FrontIndex == 102) cell.FrontIndex = 90;
                if (cell.FrontIndex >= 255) cell.FrontIndex = -1;
                if (cell.Light >= 100 && cell.Light <= 119) cell.FishingCell = true;

                MapCells[x, y] = cell;
            }

            return MapCells;
        }

        static void Main(string[] args)
        {
            string dir = args.Length > 0 ? args[0] : @"D:\龙之传奇\Map";
            string[] files = { "3.map", "5.map", "0.map", "WDragonTown_field.map", "11.map" };

            foreach (string f in files)
            {
                string path = Path.Combine(dir, f);
                if (!File.Exists(path)) { Console.WriteLine("缺失 " + path); continue; }

                // 预热一轮，避免 JIT 计入
                var warm = File.ReadAllBytes(path);
                Load2D(warm);
                warm = null;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();

                long alloc0 = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                byte[] raw = File.ReadAllBytes(path);
                long tRead = sw.ElapsedMilliseconds;
                sw.Restart();
                var cells = Load2D(raw);
                long tParse = sw.ElapsedMilliseconds;
                long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0;

                int w = cells.GetLength(0), h = cells.GetLength(1);
                Console.WriteLine(string.Format(
                    "{0,-24} {1,5}x{2,-5} 文件{3,7:0.0}MB  读盘{4,4}ms  解析{5,4}ms  分配{6,6:0.0}MB",
                    f, w, h, raw.Length / 1048576.0, tRead, tParse, alloc / 1048576.0));

                // 同样尺寸下对象本身占多少：GC.GetTotalMemory 粗略
                cells = null; raw = null;
            }

            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Console.WriteLine("常驻(空) " + (GC.GetTotalMemory(true) / 1048576.0).ToString("0.0") + " MB");
        }
    }
}
