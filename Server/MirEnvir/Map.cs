using System.Drawing;
﻿using Server.MirDatabase;
using Server.MirObjects;
using S = ServerPackets;

namespace Server.MirEnvir
{
    public class Map
    {
        private static Envir Envir
        {
            get { return Envir.Main; }
        }

        protected static MessageQueue MessageQueue
        {
            get { return MessageQueue.Instance; }
        }

        public MapInfo Info;

        public int Thread = 0;

        public int Width, Height;
        public Cell[,] Cells;
        public List<Point> WalkableCells;
        //★11-04 阶段二：门格索引从 Door[,] 换成稀疏字典。
        //  原来每张图 new Door[Width, Height]（每格 8 字节），全服 3171 万格 = 242 MB，
        //  其中 99.99% 是 null；真正有门的格子只有几万个。
        //  key = y * Width + x。字段上直接初始化，任何路径都不会有空引用。
        public Dictionary<int, Door> DoorIndex = new Dictionary<int, Door>();
        public List<Door> Doors = new List<Door>();
        public MineSpot[,] Mine;
        public long LightningTime, FireTime;
        public int MonsterCount;

        public List<NPCObject> NPCs = new List<NPCObject>();
        public List<SpellObject> Spells = new List<SpellObject>();
        public List<PlayerObject> Players = new List<PlayerObject>();
        public List<MapRespawn> Respawns = new List<MapRespawn>();
        public List<DelayedAction> ActionList = new List<DelayedAction>();
        public List<HeroObject> Heroes = new List<HeroObject>();

        // 安全区自动生成的那批装饰/回血法术（边框 TrapHexagon + 每 2 秒回血 Healing）的引用。
        // 重载安全区配置时按这个列表精确撤掉，不去扫格子猜 —— 攻城战场特效同样是 Decoration 法术，扫格子会误伤。
        public List<SpellObject> SafeZoneSpells = new List<SpellObject>();

        public List<ConquestObject> Conquest = new List<ConquestObject>();
        public ConquestObject tempConquest;

        public Map(MapInfo info)
        {
            Info = info;
            Thread = Envir.Random.Next(Settings.ThreadLimit);
        }

        public Door AddDoor(byte DoorIndex, Point location)
        {
            DoorIndex = (byte)(DoorIndex & 0x7F);
            for (int i = 0; i < Doors.Count; i++)
                if (Doors[i].index == DoorIndex)
                    return Doors[i];
            Door DoorInfo = new Door() { index = DoorIndex, Location = location };
            Doors.Add(DoorInfo);
            return DoorInfo;
        }

        /// <summary>稀疏门索引的 key：按行优先展开成一维。</summary>
        private int DoorKey(int x, int y)
        {
            return y * Width + x;
        }

        /// <summary>登记门格：AddDoor 负责复用同 index 的 Door 对象，这里负责把「格子 → 门」记进索引。</summary>
        private Door RegisterDoor(byte index, Point location)
        {
            Door door = AddDoor(index, location);
            DoorIndex[DoorKey(location.X, location.Y)] = door;
            return door;
        }
        
        public bool OpenDoor(byte DoorIndex)
        {
            for (int i = 0; i < Doors.Count; i++)
                if (Doors[i].index == DoorIndex)
                {
                    Doors[i].DoorState = DoorState.Open;
                    Doors[i].LastTick = Envir.Time;
                    return true;
                }
            return false;
        }

        public int GetAllMonstersObjectsCount()
        {
            int mobCount = 0;

            foreach (var cell in Cells)
            {
                if (cell.Objects == null || !cell.Objects.Any())
                {
                    continue;
                }

                foreach (var obj in cell.Objects)
                {
                    if (obj == null)
                        continue;

                    if (obj.Race == ObjectType.Monster)
                    {
                        mobCount++;
                    }
                }
            }
            return mobCount;
        }

        private byte FindType(byte[] input)
        {
            //c# custom map format
            if ((input[2] == 0x43) && (input[3] == 0x23))
            {
                return 100;
            }
            //wemade mir3 maps have no title they just start with blank bytes
            if (input[0] == 0)
                return 5;
            //shanda mir3 maps start with title: (C) SNDA, MIR3.
            if ((input[0] == 0x0F) && (input[5] == 0x53) && (input[14] == 0x33))
                return 6;

            //wemades antihack map (laby maps) title start with: Mir2 AntiHack
            if ((input[0] == 0x15) && (input[4] == 0x32) && (input[6] == 0x41) && (input[19] == 0x31))
                return 4;

            //wemades 2010 map format i guess title starts with: Map 2010 Ver 1.0
            if ((input[0] == 0x10) && (input[2] == 0x61) && (input[7] == 0x31) && (input[14] == 0x31))
                return 1;

            //shanda's 2012 format and one of shandas(wemades) older formats share same header info, only difference is the filesize
            if ((input[4] == 0x0F) || (input[4] == 0x03) && (input[18] == 0x0D) && (input[19] == 0x0A))
            {
                int W = input[0] + (input[1] << 8);
                int H = input[2] + (input[3] << 8);
                if (input.Length > (52 + (W * H * 14)))
                    return 3;
                else
                    return 2;
            }

            //3/4 heroes map format (myth/lifcos i guess)
            if ((input[0] == 0x0D) && (input[1] == 0x4C) && (input[7] == 0x20) && (input[11] == 0x6D))
                return 7;
            return 0;
        }

        private void LoadMapCellsv0(byte[] fileBytes)
        {
            int offSet = 0;
            Width = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            Height = BitConverter.ToInt16(fileBytes, offSet);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 52;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 12
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.

                    offSet += 2;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //Can't Fire Over.

                    offSet += 2;

                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //No Floor Tile.

                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };

                    offSet += 4;

                    if (fileBytes[offSet] > 0)
                        RegisterDoor(fileBytes[offSet], new Point(x, y));

                    offSet += 3;

                    byte light = fileBytes[offSet++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);
                }
        }
        
        private void LoadMapCellsv1(byte[] fileBytes)
        {
            int offSet = 21;

            int w = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            int xor = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            int h = BitConverter.ToInt16(fileBytes, offSet);
            Width = w ^ xor;
            Height = h ^ xor;
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 54;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    if (((BitConverter.ToInt32(fileBytes, offSet) ^ 0xAA38AA38) & 0x20000000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.

                    offSet += 6;
                    if (((BitConverter.ToInt16(fileBytes, offSet) ^ xor) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //No Floor Tile.

                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offSet += 2;
                    if (fileBytes[offSet] > 0)
                        RegisterDoor(fileBytes[offSet], new Point(x, y));
                    offSet += 5;

                    byte light = fileBytes[offSet++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);

                    offSet += 1;
                }
        }

        private void LoadMapCellsv2(byte[] fileBytes)
        {
            int offSet = 0;
            Width = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            Height = BitConverter.ToInt16(fileBytes, offSet);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 52;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 14
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.

                    offSet += 2;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //Can't Fire Over.

                    offSet += 2;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //No Floor Tile.

                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };

                    offSet += 2;
                    if (fileBytes[offSet] > 0)
                        RegisterDoor(fileBytes[offSet], new Point(x, y));
                    offSet += 5;

                    byte light = fileBytes[offSet++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);

                    offSet += 2;
                }
        }

        private void LoadMapCellsv3(byte[] fileBytes)
        {
            int offSet = 0;
            Width = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            Height = BitConverter.ToInt16(fileBytes, offSet);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 52;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 36
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.

                    offSet += 2;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //Can't Fire Over.

                    offSet += 2;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //No Floor Tile.

                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offSet += 2;
                    if (fileBytes[offSet] > 0)
                        RegisterDoor(fileBytes[offSet], new Point(x, y));
                    offSet += 12;

                    byte light = fileBytes[offSet++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);

                    offSet += 17;
                }
        }

        private void LoadMapCellsv4(byte[] fileBytes)
        {
            int offSet = 31;
            int w = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            int xor = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            int h = BitConverter.ToInt16(fileBytes, offSet);
            Width = w ^ xor;
            Height = h ^ xor;
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 64;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 12
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.

                    offSet += 2;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //Can't Fire Over.

                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offSet += 4;
                    if (fileBytes[offSet] > 0)
                        RegisterDoor(fileBytes[offSet], new Point(x, y));
                    offSet += 6;
                }
        }

        private void LoadMapCellsv5(byte[] fileBytes)
        {
            int offSet = 22;
            Width = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            Height = BitConverter.ToInt16(fileBytes, offSet);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 28 + (3 * ((Width / 2) + (Width % 2)) * (Height / 2));
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 14
                    if ((fileBytes[offSet] & 0x01) != 1)
                        Cells[x, y] = Cell.HighWall;
                    else if ((fileBytes[offSet] & 0x02) != 2)
                        Cells[x, y] = Cell.LowWall;
                    else
                        Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offSet += 13;

                    byte light = fileBytes[offSet++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);
                }
        }

        private void LoadMapCellsv6(byte[] fileBytes)
        {
            int offSet = 16;
            Width = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 2;
            Height = BitConverter.ToInt16(fileBytes, offSet);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 40;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 20
                    if ((fileBytes[offSet] & 0x01) != 1)
                        Cells[x, y] = Cell.HighWall;
                    else if ((fileBytes[offSet] & 0x02) != 2)
                        Cells[x, y] = Cell.LowWall;
                    else
                        Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offSet += 20;
                }
        }

        private void LoadMapCellsv7(byte[] fileBytes)
        {
            int offSet = 21;
            Width = BitConverter.ToInt16(fileBytes, offSet);
            offSet += 4;
            Height = BitConverter.ToInt16(fileBytes, offSet);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offSet = 54;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {//total 15
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.
                    offSet += 6;
                    if ((BitConverter.ToInt16(fileBytes, offSet) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //Can't Fire Over.
                    //offSet += 2;
                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offSet += 2;
                    if (fileBytes[offSet] > 0)
                        RegisterDoor(fileBytes[offSet], new Point(x, y));
                    offSet += 4;

                    byte light = fileBytes[offSet++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);

                    offSet += 2;
                }
        }

        private void LoadMapCellsV100(byte[] Bytes)
        {
            int offset = 4;
            if ((Bytes[0] != 1) || (Bytes[1] != 0)) return;//only support version 1 atm
            Width = BitConverter.ToInt16(Bytes, offset);
            offset += 2;
            Height = BitConverter.ToInt16(Bytes, offset);
            Cells = new Cell[Width, Height];
            DoorIndex = new Dictionary<int, Door>();

            offset = 8;

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    offset += 2;
                    if ((BitConverter.ToInt32(Bytes, offset) & 0x20000000) != 0)
                        Cells[x, y] = Cell.HighWall; //Can Fire Over.
                    offset += 10;
                    if ((BitConverter.ToInt16(Bytes, offset) & 0x8000) != 0)
                        Cells[x, y] = Cell.LowWall; //Can't Fire Over.

                    if (Cells[x, y] == null) Cells[x, y] = new Cell { Attribute = CellAttribute.Walk };
                    offset += 2;
                    if (Bytes[offset] > 0)
                        RegisterDoor(Bytes[offset], new Point(x, y));
                    offset += 11;

                    byte light = Bytes[offset++];

                    if (light >= 100 && light <= 119)
                        Cells[x, y].FishingAttribute = (sbyte)(light - 100);
                }
                
        }

        public bool Load()
        {
            try
            {
                string fileName = Path.Combine(Settings.MapPath, Info.FileName + ".map");
                if (File.Exists(fileName))
                {
                    byte[] fileBytes = File.ReadAllBytes(fileName);
                    switch(FindType(fileBytes))
                    {
                        case 0:
                            LoadMapCellsv0(fileBytes);
                            break;
                        case 1:
                            LoadMapCellsv1(fileBytes);
                            break;
                        case 2:
                            LoadMapCellsv2(fileBytes);
                            break;
                        case 3:
                            LoadMapCellsv3(fileBytes);
                            break;
                        case 4:
                            LoadMapCellsv4(fileBytes);
                            break;
                        case 5:
                            LoadMapCellsv5(fileBytes);
                            break;
                        case 6:
                            LoadMapCellsv6(fileBytes);
                            break;
                        case 7:
                            LoadMapCellsv7(fileBytes);
                            break;
                        case 100:
                            LoadMapCellsV100(fileBytes);
                            break;
                    }

                    GetWalkableCells();
                    
                    for (int i = 0; i < Info.Respawns.Count; i++)
                    {
                        MapRespawn info = new MapRespawn(Info.Respawns[i]);
                        if (info.Monster == null) continue;
                        info.Map = this;
                        //★11-04 阶段一：不再给每个刷怪点复制一片可走点。
                        //  原来这里对每个刷怪点 Where().ToList() 一份，全服累计 1.42 亿个 Point = 1.44 GB，
                        //  而且也是启动慢的主因（0.map 993 个点 × 32.6 万可走点 = 3.2 亿次比较）。
                        //  取点改由 MapRespawn.GetSpawnPoint 现场按方框采样，语义等价。
                        Respawns.Add(info);

                        if ((info.Info.SaveRespawnTime) && (info.Info.RespawnTicks != 0))
                            Envir.SavedSpawns.Add(info);
                    }

                    for (int i = 0; i < Info.NPCs.Count; i++)
                    {
                        NPCInfo info = Info.NPCs[i];
                        if (!ValidPoint(info.Location)) continue;

                        AddObject(new NPCObject(info) { CurrentMap = this });
                    }

                    for (int i = 0; i < Info.SafeZones.Count; i++)
                    {
                        if (Info.SafeZones[i] == null) continue;   // 空行不挡整张图的加载
                        CreateSafeZone(Info.SafeZones[i]);
                    }

                    CreateMine();

                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }

            MessageQueue.Enqueue("地图加载失败: " + Info.Title);
            MessageQueue.Enqueue("加载地图失败: " + Info.FileName);
            return false;
        }

        public void GetWalkableCells()
        {
            if (WalkableCells == null)
            {
                WalkableCells = new List<Point>();

                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (Cells[x, y].Attribute == CellAttribute.Walk)
                            WalkableCells.Add(new Point(x, y));
            }
        }

        private void CreateSafeZone(SafeZoneInfo info)
        {
            if (Settings.SafeZoneBorder)
            {
                for (int y = info.Location.Y - info.Size; y <= info.Location.Y + info.Size; y++)
                {
                    if (y < 0) continue;
                    if (y >= Height) break;
                    for (int x = info.Location.X - info.Size; x <= info.Location.X + info.Size; x += Math.Abs(y - info.Location.Y) == info.Size ? 1 : info.Size * 2)
                    {
                        if (x < 0) continue;
                        if (x >= Width) break;
                        if (!Cells[x, y].Valid) continue;

                        SpellObject spell = new SpellObject
                        {
                            ExpireTime = long.MaxValue,
                            Spell = Spell.TrapHexagon,
                            TickSpeed = int.MaxValue,
                            CurrentLocation = new Point(x, y),
                            CurrentMap = this,
                            Decoration = true
                        };

                        Cells[x, y].Add(spell);

                        spell.Spawned();

                        SafeZoneSpells.Add(spell);
                    }
                }
            }

            if (Settings.SafeZoneHealing)
            {
                for (int y = info.Location.Y - info.Size; y <= info.Location.Y + info.Size; y++)
                {
                    if (y < 0) continue;
                    if (y >= Height) break;
                    for (int x = info.Location.X - info.Size; x <= info.Location.X + info.Size; x++)
                    {
                        if (x < 0) continue;
                        if (x >= Width) break;
                        if (!Cells[x, y].Valid) continue;

                        SpellObject spell = new SpellObject
                            {
                                ExpireTime = long.MaxValue,
                                Value = 25,
                                TickSpeed = 2000,
                                Spell = Spell.Healing,
                                CurrentLocation = new Point(x, y),
                                CurrentMap = this
                            };

                        Cells[x, y].Add(spell);

                        spell.Spawned();

                        SafeZoneSpells.Add(spell);
                    }
                }
            }


        }

        /// <summary>
        /// 撤掉本图所有安全区自动生成的法术（边框/回血）。老图重载安全区配置前必须先调用，
        /// 否则新边框会叠在旧边框外面，回血法术也会翻倍。
        /// </summary>
        public void ClearSafeZoneSpells()
        {
            for (int i = 0; i < SafeZoneSpells.Count; i++)
            {
                SpellObject spell = SafeZoneSpells[i];
                if (spell == null) continue;

                Point loc = spell.CurrentLocation;

                if (loc.X >= 0 && loc.X < Width && loc.Y >= 0 && loc.Y < Height)
                {
                    Cell cell = Cells[loc.X, loc.Y];
                    if (cell != null && cell.Objects != null) cell.Remove(spell);
                }

                spell.Despawn();
            }

            SafeZoneSpells.Clear();
        }

        /// <summary>
        /// 按当前 Info.SafeZones 重新生成安全区法术（老图重载安全区配置时用）。
        /// </summary>
        public void RebuildSafeZones()
        {
            ClearSafeZoneSpells();

            for (int i = 0; i < Info.SafeZones.Count; i++)
            {
                if (Info.SafeZones[i] == null) continue;   // 编辑器里留的空行不该让重载崩掉
                CreateSafeZone(Info.SafeZones[i]);
            }
        }

        /// <summary>
        /// 按当前 Info 的矿区配置重建 Mine 格子（老图重载矿区配置时用）。
        /// Mine 只在挖矿时按坐标实时读取，重建不影响在场玩家。
        /// </summary>
        public void RebuildMine()
        {
            Mine = null;
            CreateMine();
        }

        private void CreateMine()
        {
            if ((Info.MineIndex == 0) && (Info.MineZones.Count == 0)) return;
            Mine = new MineSpot[Width, Height];
            for (int i = 0; i < Width; i++)
                for (int j = 0; j < Height; j++)
                    Mine[i, j] = new MineSpot();
            if ((Info.MineIndex != 0) && (Settings.MineSetList.Count > Info.MineIndex - 1))
            {
                Settings.MineSetList[Info.MineIndex - 1].SetDrops(Envir.ItemInfoList);
                for (int i = 0; i < Width; i++)
                    for (int j = 0; j < Height; j++)
                        Mine[i,j].Mine = Settings.MineSetList[Info.MineIndex - 1];
            }
            if (Info.MineZones.Count > 0)
            {
                for (int i = 0; i < Info.MineZones.Count; i++)
                {
                    MineZone Zone = Info.MineZones[i];
                    // 越界检查要放在取下标之前：矿区设置编号超过 MineSet 数量时直接跳过，
                    // 否则（尤其是重载地图时）会抛 IndexOutOfRange
                    if (Settings.MineSetList.Count < Zone.Mine) continue;
                    if (Zone.Mine != 0)
                        Settings.MineSetList[Zone.Mine - 1].SetDrops(Envir.ItemInfoList);
                    for (int x =  Zone.Location.X - Zone.Size; x < Zone.Location.X + Zone.Size; x++)
                        for (int y = Zone.Location.Y - Zone.Size; y < Zone.Location.Y + Zone.Size; y++)
                        {
                            if ((x < 0) || (x >= Width) || (y < 0) || (y >= Height)) continue;
                            if (Zone.Mine == 0)
                                Mine[x, y].Mine = null;
                            else
                                Mine[x, y].Mine = Settings.MineSetList[Zone.Mine - 1];
                        }
                }
            }
        }

        public Cell GetCell(Point location)
        {
            return Cells[location.X, location.Y];
        }

        public Cell GetCell(int x, int y)
        {
            return Cells[x, y];
        }

        public bool ValidPoint(Point location)
        {
            return location.X >= 0 && location.X < Width && location.Y >= 0 && location.Y < Height && GetCell(location).Valid;
        }
        public bool ValidPoint(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height && GetCell(x, y).Valid;
        }

        // ==== 20261103 韩服20261001移植：ValidPoint+开门检查 ====
        public bool CanJump(Point location)
        {
            if (!ValidPoint(location)) return false;
            if (!CheckDoorOpen(location)) return false;
            return true;
        }

        public bool CheckDoorOpen(Point location)
        {
            //★11-04 阶段二：字典查不到 = 这一格没有门。顺带对越界坐标也安全（数组版会抛 IndexOutOfRange）。
            if (!DoorIndex.TryGetValue(DoorKey(location.X, location.Y), out Door door)) return true;
            return door.DoorState == DoorState.Open;
        }

        public void Process()
        {
            ProcessRespawns();

            for (int i = 0; i < Doors.Count; i++)
            {
                if ((Doors[i].DoorState == DoorState.Open) && (Doors[i].LastTick + 5000 < Envir.Time))
                {
                    Doors[i].DoorState = 0;

                    Broadcast(new S.Opendoor() { DoorIndex = Doors[i].index, Close = true }, Doors[i].Location);
                }
            }

            if ((Info.Lightning) && Envir.Time > LightningTime)
            {
                LightningTime = Envir.Time + Envir.Random.Next(3000, 15000);
                for (int i = Players.Count - 1; i >= 0; i--)
                {
                    PlayerObject player = Players[i];
                    Point location;
                    if (Envir.Random.Next(4) == 0)
                    {
                        location = player.CurrentLocation;          
                    }
                    else
                        location = new Point(player.CurrentLocation.X - 10 + Envir.Random.Next(20), player.CurrentLocation.Y - 10 + Envir.Random.Next(20));

                    if (!ValidPoint(location)) continue;

                    SpellObject lightning = new SpellObject
                    {
                        Spell = Spell.MapLightning,
                        Value = Envir.Random.Next(Info.LightningDamage),
                        ExpireTime = Envir.Time + (1000),
                        TickSpeed = 500,
                        Caster = null,
                        CurrentLocation = location,
                        CurrentMap = this,
                        Direction = MirDirection.Up
                    };
                    AddObject(lightning);
                    lightning.Spawned();
                }
            }

            if ((Info.Fire) && Envir.Time > FireTime)
            {
                FireTime = Envir.Time + Envir.Random.Next(3000, 15000);
                for (int i = Players.Count - 1; i >= 0; i--)
                {
                    PlayerObject player = Players[i];
                    Point location;
                    if (Envir.Random.Next(4) == 0)
                    {
                        location = player.CurrentLocation;

                    }
                    else
                        location = new Point(player.CurrentLocation.X - 10 + Envir.Random.Next(20), player.CurrentLocation.Y - 10 + Envir.Random.Next(20));

                    if (!ValidPoint(location)) continue;

                    SpellObject lightning = new SpellObject
                    {
                        Spell = Spell.MapLava,
                        Value = Envir.Random.Next(Info.FireDamage),
                        ExpireTime = Envir.Time + (1000),
                        TickSpeed = 500,
                        Caster = null,
                        CurrentLocation = location,
                        CurrentMap = this,
                        Direction = MirDirection.Up
                    };
                    AddObject(lightning);
                    lightning.Spawned();
                }
            }

            for (int i = 0; i < ActionList.Count; i++)
            {
                if (Envir.Time < ActionList[i].Time) continue;
                Process(ActionList[i]);
                ActionList.RemoveAt(i);
            }
        }

        private void ProcessRespawns()
        {
            bool Success = true;
            for (int i = 0; i < Respawns.Count; i++)
            {
                MapRespawn respawn = Respawns[i];
                if ((respawn.Info.RespawnTicks != 0) && (Envir.RespawnTick.CurrentTickcounter < respawn.NextSpawnTick)) continue;
                if ((respawn.Info.RespawnTicks == 0) && (Envir.Time < respawn.RespawnTime)) continue;

                if (respawn.Count < (respawn.Info.Count * Envir.SpawnMultiplier))
                {
                    int count = (respawn.Info.Count * Envir.SpawnMultiplier) - respawn.Count;

                    for (int c = 0; c < count; c++)
                        Success = respawn.Spawn();
                }
                if (Success)
                {
                    respawn.ErrorCount = 0;
                    long delay = Math.Max(1, respawn.Info.Delay - respawn.Info.RandomDelay + Envir.Random.Next(respawn.Info.RandomDelay * 2));
                    respawn.RespawnTime = Envir.Time + (delay * Settings.Minute);
                    if (respawn.Info.RespawnTicks != 0)
                    {
                        respawn.NextSpawnTick = Envir.RespawnTick.CurrentTickcounter + (ulong)respawn.Info.RespawnTicks;
                        if (respawn.NextSpawnTick > long.MaxValue)//since nextspawntick is ulong this simple thing allows an easy way of preventing the counter from overflowing
                            respawn.NextSpawnTick -= long.MaxValue;
                    }
                }
                else
                {
                    respawn.RespawnTime = Envir.Time + 1 * Settings.Minute; // each time it fails to spawn, give it a 1 minute cooldown
                    if (respawn.ErrorCount < 5)
                        respawn.ErrorCount++;
                    else
                    {
                        if (respawn.ErrorCount == 5)
                        {
                            respawn.ErrorCount++;

                            Logger.GetLogger(LogType.Spawn).Info($"Failed to spawn: " +
                                $"mapindex: {respawn.Map.Info.Index}, " +
                                $"mob info: index: {respawn.Info.MonsterIndex}, " +
                                $"spawncoords ({respawn.Info.Location.X}:{respawn.Info.Location.Y}), " +
                                $"range {respawn.Info.Spread}");
                        }

                    }
                }
            }
        }

        public void Process(DelayedAction action)
        {
            switch (action.Type)
            {
                case DelayedType.Magic:
                    CompleteMagic(action.Params);
                    break;
                case DelayedType.Spawn:
                    MapObject obj = (MapObject)action.Params[0];

                    switch(obj.Race)
                    {
                        case ObjectType.Monster:
                            {
                                MonsterObject mob = (MonsterObject)action.Params[0];
                                mob.Spawn(this, (Point)action.Params[1]);
                                if (action.Params.Length > 2) ((MonsterObject)action.Params[2]).SlaveList.Add(mob);
                            }
                            break;
                        case ObjectType.Spell:
                            {
                                SpellObject spell = (SpellObject)action.Params[0];
                                AddObject(spell);
                                spell.Spawned();
                            }
                            break;
                    }
                    break;
            }
        }

         /**
         * return the coordinates of effect coordinates within an n x n square (n should be odd number. i.e. 3x3, 5x5, 7x7)
         * then use GetCell() in Map.cs to retrive real objects
         * default 3x3
         */
        public static List<Point> GetPointsInEffectiveSquare(Point location, int mapWidth, int mapHeight, int squareEdgeLength = 3)
        {
            var pointsWithinTheMap = new List<Point>();
            int fallBackSpread = 1;
            int spread;
            if (squareEdgeLength > 1)
            {
                spread = (int)((squareEdgeLength - 1) / 2);
            }
            else
            {
                spread = fallBackSpread; // 3x3
            }

            for (int y = location.Y - spread; y <= location.Y + spread; y++)
            {
                if (y < 0) continue;
                if (y >= mapHeight) break;

                for (int x = location.X - spread; x <= location.X + spread; x++)
                {
                    if (x < 0) continue;
                    if (x >= mapWidth) break;
                    pointsWithinTheMap.Add(new Point(x, y));
                }
            }
            return pointsWithinTheMap;
        }

        private void CompleteMagic(IList<object> data)
        {
            bool train = false;

            if (data == null || data.Count < 2) return;

            HumanObject player = (HumanObject)data[0];
            UserMagic magic = (UserMagic)data[1];

            if (player == null || player.Info == null || player.Node == null) return;
            if (magic == null || magic.Info == null) return;   // 技能被删/存档异常时别把整张图带崩

            int value, value2;
            Point location;
            Cell cell;
            MirDirection dir;
            MonsterObject monster;
            Point front;
            switch (magic.Spell)
            {

                #region HellFire

                case Spell.HellFire:
                    value = (int)data[2];
                    dir = (MirDirection)data[4];
                    location = Functions.PointMove((Point)data[3], dir, 1);
                    int count = (int)data[5] - 1;

                    if (!ValidPoint(location)) return;

                    if (count > 0)
                    {
                        DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 100, player, magic, value, location, dir, count);
                        ActionList.Add(action);
                    }

                    cell = GetCell(location);

                    if (cell.Objects == null) return;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject target = cell.Objects[i];
                        switch (target.Race)
                        {
                            case ObjectType.Monster:
                            case ObjectType.Player:
                                //Only targets
                                if (target.IsAttackTarget(player))
                                {
                                    if (target.Attacked(player, value, DefenceType.MAC, false) > 0)
                                        train = true;
                                    break;
                                }
                                break;
                        }
                    }
                    break;

                #endregion

                #region SummonSkeleton, SummonShinsu, SummonHolyDeva, ArcherSummons

                case Spell.SummonSkeleton:
                case Spell.SummonShinsu:
                case Spell.SummonHolyDeva:
                case Spell.SummonVampire:
                case Spell.SummonToad:
                case Spell.SummonSnakes:
                    monster = (MonsterObject)data[2];
                    front = (Point)data[3];

                    // ★Master 可能已经是 null：主人换图/掉线/英雄被收回时 MapObject.Process 会把 Master 置空，
                    // 而这条召唤延迟动作还留在队列里 → 直接 monster.Master.Dead 就是 NullReferenceException。
                    if (monster == null || monster.Master == null || monster.Master.Dead) return;

                    // 落点不可走 / 地图不对时 Spawn 返回 false，怪物身上就没有 CurrentMap；
                    // 这种"没落地"的宠物一旦进了 Master.Pets，之后任何 Pet.CurrentMap.Info 都是空引用
                    // （服务端界面查玩家信息、下线存档都会踩到）。落地失败就别收进宠物列表。
                    bool spawned = ValidPoint(front)
                        ? monster.Spawn(this, front)
                        : monster.Spawn(player.CurrentMap ?? this, player.CurrentLocation);

                    if (!spawned)
                        spawned = monster.Spawn(player.CurrentMap ?? this, player.CurrentLocation);

                    if (!spawned) return;

                    monster.Master.Pets.Add(monster);
                    break;
                case Spell.Stonetrap:
                    monster = (MonsterObject)data[2];
                    front = (Point)data[3];

                    if (monster == null || monster.Master == null || monster.Master.Dead) return;

                    // 同上：落地失败（没有 CurrentMap）就别收进 Pets
                    bool trapped = ValidPoint(front)
                        ? monster.Spawn(this, front)
                        : monster.Spawn(player.CurrentMap ?? this, player.CurrentLocation);

                    if (!trapped)
                        trapped = monster.Spawn(player.CurrentMap ?? this, player.CurrentLocation);

                    if (!trapped) return;

                    monster.Master.Pets.Add(monster);
                    break;
                #endregion

                #region FireBang, IceStorm

                case Spell.IceStorm:
                case Spell.FireBang:
                    value = (int)data[2];
                    location = (Point)data[3];

                    for (int y = location.Y - 1; y <= location.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 1; x <= location.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (target.IsAttackTarget(player))
                                        {
                                            if (target.Attacked(player, value, DefenceType.MAC, false) > 0)
                                                train = true;
                                        }
                                        break;
                                }
                            }

                        }

                    }

                    break;

                #endregion

                #region MassHiding

                case Spell.MassHiding:
                    value = (int)data[2];
                    location = (Point)data[3];

                    for (int y = location.Y - 1; y <= location.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 1; x <= location.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];

                                if (target.Node == null) continue;

                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (target.IsFriendlyTarget(player))
                                        {
                                            target.AddBuff(BuffType.隐身术, player, (Settings.Second * value), new Stats());
                                            target.OperateTime = 0;
                                            train = true;
                                        }
                                        break;
                                }
                            }

                        }

                    }

                    break;

                #endregion

                #region SoulShield, BlessedArmour

                case Spell.SoulShield:
                case Spell.BlessedArmour:
                    value = (int)data[2];
                    location = (Point)data[3];
                    BuffType type = magic.Spell == Spell.SoulShield ? BuffType.幽灵盾 : BuffType.神圣战甲术;

                    for (int y = location.Y - 3; y <= location.Y + 3; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 3; x <= location.X + 3; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];

                                if (target.Node == null) continue;

                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                    case ObjectType.Hero:
                                        //Only targets
                                        if (target.IsFriendlyTarget(player))
                                        {
                                            var stats = new Stats
                                            {
                                                [type == BuffType.幽灵盾 ? Stat.MaxMAC : Stat.MaxAC] = target.Level / 7 + 4
                                            };

                                            target.AddBuff(type, player, Settings.Second * value, stats);
                                            target.OperateTime = 0;
                                            train = true;
                                        }
                                        break;
                                }
                            }

                        }

                    }

                    break;

                #endregion

                #region FireWall

                case Spell.FireWall:
                    value = (int)data[2];
                    location = (Point)data[3];

                    player.LevelMagic(magic);

                    if (ValidPoint(location))
                    {
                        cell = GetCell(location);

                        bool cast = true;
                        if (cell.Objects != null)
                            for (int o = 0; o < cell.Objects.Count; o++)
                            {
                                MapObject target = cell.Objects[o];
                                if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != Spell.FireWall) continue;

                                cast = false;
                                break;
                            }

                        if (cast)
                        {
                            SpellObject ob = new SpellObject
                                {
                                    Spell = Spell.FireWall,
                                    Value = value,
                                    ExpireTime = Envir.Time + (10 + value / 2) * 1000,
                                    TickSpeed = 2000,
                                    Caster = player,
                                    CurrentLocation = location,
                                    CurrentMap = this,
                                };
                            AddObject(ob);
                            ob.Spawned();
                        }
                    }

                    dir = MirDirection.Up;
                    for (int i = 0; i < 4; i++)
                    {
                        location = Functions.PointMove((Point)data[3], dir, 1);
                        dir += 2;

                        if (!ValidPoint(location)) continue;

                        cell = GetCell(location);
                        bool cast = true;

                        if (cell.Objects != null)
                            for (int o = 0; o < cell.Objects.Count; o++)
                            {
                                MapObject target = cell.Objects[o];
                                if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != Spell.FireWall) continue;

                                cast = false;
                                break;
                            }

                        if (!cast) continue;

                        SpellObject ob = new SpellObject
                        {
                            Spell = Spell.FireWall,
                            Value = value,
                            ExpireTime = Envir.Time + (10 + value / 2) * 1000,
                            TickSpeed = 2000,
                            Caster = player,
                            CurrentLocation = location,
                            CurrentMap = this,
                        };
                        AddObject(ob);
                        ob.Spawned();
                    }

                    break;

                #endregion

                #region Lightning

                case Spell.Lightning:
                    value = (int)data[2];
                    location = (Point)data[3];
                    dir = (MirDirection)data[4];

                    for (int i = 0; i < 6; i++)
                    {
                        location = Functions.PointMove(location, dir, 1);

                        if (!ValidPoint(location)) continue;

                        cell = GetCell(location);

                        if (cell.Objects == null) continue;

                        for (int o = 0; o < cell.Objects.Count; o++)
                        {
                            MapObject target = cell.Objects[o];
                            if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) continue;

                            if (!target.IsAttackTarget(player)) continue;
                            if (target.Attacked(player, value, DefenceType.MAC, false) > 0)
                                train = true;
                            break;
                        }
                    }

                    break;

                #endregion

                #region HeavenlySword

                case Spell.HeavenlySword:
                    value = (int)data[2];
                    location = (Point)data[3];
                    dir = (MirDirection)data[4];

                    for (int i = 0; i < 3; i++)
                    {
                        location = Functions.PointMove(location, dir, 1);

                        if (!ValidPoint(location)) continue;

                        cell = GetCell(location);

                        if (cell.Objects == null) continue;

                        for (int o = 0; o < cell.Objects.Count; o++)
                        {
                            MapObject target = cell.Objects[o];
                            if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) continue;

                            if (!target.IsAttackTarget(player)) continue;
                            if (target.Attacked(player, value, DefenceType.MAC, false) > 0)
                                train = true;
                            break;
                        }
                    }

                    break;

                #endregion

                #region MassHealing

                case Spell.MassHealing:
                    value = (int)data[2];
                    location = (Point)data[3];

                    for (int y = location.Y - 1; y <= location.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 1; x <= location.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                    case ObjectType.Hero:
                                        //Only targets
                                        if (target.IsFriendlyTarget(player))
                                        {
                                            if (target.Health >= target.MaxHealth) continue;
                                            target.HealAmount = (ushort)Math.Min(ushort.MaxValue, target.HealAmount + value);
                                            target.OperateTime = 0;
                                            train = true;
                                        }
                                        break;
                                }
                            }

                        }

                    }

                    break;

                #endregion

                #region ThunderStorm

                case Spell.ThunderStorm:
                case Spell.FlameField:
                case Spell.NapalmShot:
                //case Spell.StormEscape:
                    value = (int)data[2];
                    location = (Point)data[3];
                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (!target.IsAttackTarget(player)) break;
                                        target.Attacked(player, magic.Spell == Spell.ThunderStorm && !target.Undead ? value / 10 : value, DefenceType.MAC, false);
                                        train = true;
                                        break;
                                }
                            }

                        }
                    }

                    break;

                #endregion

                #region StormEscape

                case Spell.StormEscape:
                    value = (int)data[2];
                    location = (Point)data[3];
                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        if (!target.IsAttackTarget(player)) break;
                                        {
                                            target.ApplyPoison(new Poison { PType = PoisonType.LRParalysis, Duration = magic.Level + 2, TickSpeed = 1000 }, player);
                                            target.OperateTime = 0;
                                        }
                                        break;
                                }
                            }
                            train = true;
                            break;
                        }

                    }

                    break;

                #endregion

                #region StormEscapeRare

                case Spell.StormEscapeRare:
                    value = (int)data[2];
                    location = (Point)data[3];
                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (!target.IsAttackTarget(player)) break;
                                        if (target.Attacked(player, magic.Spell == Spell.ThunderStorm && !target.Undead ? value / 10 : value, DefenceType.MAC, false) <= 0) return;
                                        {
                                            target.ApplyPoison(new Poison { PType = PoisonType.LRParalysis, Duration = magic.Level + 2, TickSpeed = 1000 }, player);
                                            target.OperateTime = 0;
                                        }
                                        break;

                                }
                            }
                            train = true;
                            break;
                        }

                    }

                    break;

                #endregion

                #region MoonMist

                case Spell.MoonMist:
                case Spell.MoonMistRare: //新增：月影雾-秘籍（攻击范围增加 2 倍：半径 2 → 4）

                    value = (int)data[2];
                    location = (Point)data[3];
                    int moonRadius = magic.Spell == Spell.MoonMistRare ? 4 : 2;
                    for (int y = location.Y - moonRadius; y <= location.Y + moonRadius; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - moonRadius; x <= location.X + moonRadius; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (!target.IsAttackTarget(player)) break;

                                        if (target.Attacked(player, magic.Spell == Spell.ThunderStorm && !target.Undead ? value / 10 : value, DefenceType.AC, false) <= 0)
                                        {
                                            if (target.Undead)
                                            {
                                                target.ApplyPoison(new Poison { PType = PoisonType.Stun, Duration = magic.Level + 2, TickSpeed = 1000 }, player);
                                            }
                                            break;
                                        }

                                        train = true;
                                        break;
                                }
                            }

                        }
                    }

                    break;

                #endregion

                #region LionRoar

                case Spell.LionRoar:
                    location = (Point)data[2];

                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                if (target.Race != ObjectType.Monster) continue;
                                //Only targets
                                if (!target.IsAttackTarget(player) || player.Level + 3 < target.Level) continue;
                                target.ApplyPoison(new Poison { PType = PoisonType.LRParalysis, Duration = magic.Level + 2, TickSpeed = 1000 }, player);
                                target.OperateTime = 0;
                                train = true;
                            }

                        }

                    }

                    break;

                #endregion

                #region LionRoarRare

                case Spell.LionRoarRare:
                    location = (Point)data[2];

                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                if (target.Race != ObjectType.Monster) continue;
                                //Only targets
                                if (!target.IsAttackTarget(player) || player.Level + 3 < target.Level) continue;
                                target.ApplyPoison(new Poison { PType = PoisonType.LRParalysis, Duration = magic.Level + 2, TickSpeed = 1000 }, player);
                                target.OperateTime = 0;
                                train = true;
                            }

                        }

                    }

                    break;

                #endregion
                #region PoisonCloud

                case Spell.PoisonCloud:
                    value = (int)data[2];
                    location = (Point)data[3];
                    byte bonusdmg = (byte)data[4];
                    //data[5] = 施放时用掉的毒粉形态（1=灰色毒粉→绿毒掉血、2=黄色毒粉→红毒掉防）。
                    //旧数据（或无该位）时按 0 处理，SpellObject 会走「绿毒」分支，行为与老版本一致。
                    byte poisonShape = data.Count > 5 ? (byte)data[5] : (byte)0;
                    train = true;
                    bool show = true; //★这个 switch 后面的多个 case（HealingCircle 等）共用 show 变量，此处必须保留它的声明

                    //毒雾：群体绿毒，范围 6 格（13×13）
                    const int poisonRadius = 6;

                    //需求：特效要「持续一整片云」，中间不留空白。
                    //云素材（Magic2 1280-1289）单朵张开后约 9×6 格大，所以按 4 格间隔在网格点上各显示一朵，
                    //9 朵云正好互相重叠着盖满整个 13×13 范围；其余格子只负责伤害/中毒，不下发显示。
                    const int cloudStep = 4;

                    for (int y = location.Y - poisonRadius; y <= location.Y + poisonRadius; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - poisonRadius; x <= location.X + poisonRadius; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid) continue;

                            bool cast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != Spell.PoisonCloud) continue;

                                    cast = false;
                                    break;
                                }

                            if (!cast) continue;

                            //只有落在网格点上的格子才显示云，多朵连成一整片
                            bool cloudShow = ((x - location.X) % cloudStep == 0) && ((y - location.Y) % cloudStep == 0);

                            SpellObject ob = new SpellObject
                                {
                                    Spell = Spell.PoisonCloud,
                                    Value = value,
                                    BonusDmg = bonusdmg,
                                    PoisonShape = poisonShape,
                                    ExpireTime = Envir.Time + 6000,
                                    TickSpeed = 1000,
                                    Caster = player,
                                    CurrentLocation = new Point(x, y),
                                    CastLocation = location,
                                    Show = cloudShow,
                                    CurrentMap = this,
                                };

                            AddObject(ob);
                            ob.Spawned();
                        }
                    } 

                    break;

                #endregion

                #region IceThrust

                case Spell.IceThrust:
                    {
                        location = (Point)data[2];
                        MirDirection direction = (MirDirection)data[3];

                        int nearDamage = (int)data[4];
                        int farDamage = (int)data[5];

                        int col = 3;
                        int row = 3;

                        Point[] loc = new Point[col]; //0 = left 1 = center 2 = right
                        loc[0] = Functions.PointMove(location, Functions.PreviousDir(direction), 1);
                        loc[1] = Functions.PointMove(location, direction, 1);
                        loc[2] = Functions.PointMove(location, Functions.NextDir(direction), 1);

                        for (int i = 0; i < col; i++)
                        {
                            Point startPoint = loc[i];
                            for (int j = 0; j < row; j++)
                            {
                                Point hitPoint = Functions.PointMove(startPoint, direction, j);

                                if (!ValidPoint(hitPoint)) continue;

                                cell = GetCell(hitPoint);

                                if (cell.Objects == null) continue;

                                for (int k = 0; k < cell.Objects.Count; k++)
                                {
                                    MapObject target = cell.Objects[k];
                                    switch (target.Race)
                                    {
                                        case ObjectType.Monster:
                                        case ObjectType.Player:
                                            if (target.IsAttackTarget(player))
                                            {
                                                //Only targets
                                                if (target.Attacked(player, j <= 1 ? nearDamage : farDamage, DefenceType.MAC, false) > 0)
                                                {
                                                    if (player.Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 20) <= magic.Level)
                                                    {
                                                        target.ApplyPoison(new Poison
                                                        {
                                                            Owner = player,
                                                            Duration = target.Race == ObjectType.Player ? 4 : 5 + Envir.Random.Next(5),
                                                            PType = PoisonType.Slow,
                                                            TickSpeed = 1000,
                                                        }, player);
                                                        target.OperateTime = 0;
                                                    }

                                                    if (player.Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 40) <= magic.Level)
                                                    {
                                                        target.ApplyPoison(new Poison
                                                        {
                                                            Owner = player,
                                                            Duration = target.Race == ObjectType.Player ? 2 : 5 + Envir.Random.Next(player.Stats[Stat.冰冻伤害]),
                                                            PType = PoisonType.Frozen,
                                                            TickSpeed = 1000,
                                                        }, player);
                                                        target.OperateTime = 0;
                                                    }

                                                    train = true;
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    break;

                #endregion

                #region SlashingBurst

                case Spell.SlashingBurst:
                    value = (int)data[2];
                    location = (Point)data[3];
                    dir = (MirDirection)data[4];
                    count = (int)data[5];

                    for (int i = 0; i < count; i++)
                    {
                        location = Functions.PointMove(location, dir, 1);

                        if (!ValidPoint(location)) continue;

                        cell = GetCell(location);

                        if (cell.Objects == null) continue;

                        for (int o = 0; o < cell.Objects.Count; o++)
                        {
                            MapObject target = cell.Objects[o];
                            if (target.Race != ObjectType.Player && target.Race != ObjectType.Monster) continue;

                            if (!target.IsAttackTarget(player)) continue;
                            if (target.Attacked(player, value, DefenceType.AC, false) > 0)
                                train = true;
                            break;
                        }
                    }
                    break;

                #endregion

                #region DimensionalSword

                case Spell.DimensionalSword:

                    value = (int)data[2];
                    if (value > 0)
                        train = true;
                    break;

                #endregion

                #region DimensionalSwordRare

                case Spell.DimensionalSwordRare:

                    value = (int)data[2];
                    if (value > 0)
                        train = true;
                    break;

                #endregion

                #region Mirroring

                case Spell.Mirroring:
                    monster = (MonsterObject)data[2];
                    front = (Point)data[3];
                    bool finish = (bool)data[4];

                    if (monster == null) return;

                    if (finish)
                    {
                        monster.Die();
                        return;
                    };

                    if (ValidPoint(front))
                        monster.Spawn(this, front);
                    else
                        monster.Spawn(player.CurrentMap ?? this, player.CurrentLocation);
                    break;

                #endregion

                #region Blizzard

                case Spell.Blizzard:
                    value = (int)data[2];
                    location = (Point)data[3];

                    train = true;
                    show = true;

                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid) continue;

                            bool cast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    if (target.Race != ObjectType.Spell || ((SpellObject) target).Spell != Spell.Blizzard) continue;

                                    cast = false;
                                    break;
                                }

                            if (!cast) continue;

                            SpellObject ob = new SpellObject
                                {
                                    Spell = Spell.Blizzard,
                                    Value = value,
                                    ExpireTime = Envir.Time + 3000,
                                    TickSpeed = 440,
                                    Caster = player,
                                    CurrentLocation = new Point(x, y),
                                    CastLocation = location,
                                    Show = show,
                                    CurrentMap = this,
                                    StartTime = Envir.Time + 800,
                                };

                            show = false;

                            AddObject(ob);
                            ob.Spawned();
                        }
                    } 

                    break;

                #endregion

                #region MeteorStrike

                case Spell.MeteorStrike:
                    value = (int)data[2];
                    location = (Point)data[3];

                    train = true;
                    show = true;

                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid) continue;

                            bool cast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != Spell.MeteorStrike) continue;

                                    cast = false;
                                    break;
                                }

                            if (!cast) continue;

                            SpellObject ob = new SpellObject
                            {
                                Spell = Spell.MeteorStrike,
                                Value = value,
                                ExpireTime = Envir.Time + 3000,
                                TickSpeed = 440,
                                Caster = player,
                                CurrentLocation = new Point(x, y),
                                CastLocation = location,
                                Show = show,
                                CurrentMap = this,
                                StartTime = Envir.Time + 800,
                            };

                            show = false;

                            AddObject(ob);
                            ob.Spawned();
                        }
                    }

                    break;

                #endregion

                #region HealingcircleRare

                case Spell.HealingcircleRare:
                    value = (int)data[2];
                    location = (Point)data[3];

                    train = true;
                    show = true;

                    for (int y = location.Y - 3; y <= location.Y + 3; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 3; x <= location.X + 3; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid) continue;

                            bool cast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    switch (target.Race)
                                    {
                                        case ObjectType.Monster:
                                        case ObjectType.Player:
                                        case ObjectType.Hero:

                                            if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != Spell.HealingcircleRare) continue;

                                            cast = false;
                                            break;
                                    }
                                }

                            if (!cast) continue;

                            SpellObject ob = new SpellObject
                            {
                                Spell = Spell.HealingcircleRare,
                                Value = value,
                                ExpireTime = Envir.Time + 20000,
                                TickSpeed = 1200,
                                Caster = player,
                                CurrentLocation = new Point(x, y),
                                CastLocation = location,
                                Show = show,
                                CurrentMap = this,
                                StartTime = Envir.Time + 800,
                            };

                            show = false;

                            AddObject(ob);
                            ob.Spawned();
                        }
                    }
                    break;

                #endregion

                #region TrapHexagon

                case Spell.TrapHexagon:
                    value = (int)data[2];
                    location = (Point)data[3];

                    MonsterObject centerTarget = null;

                    for (int y = location.Y - 1; y <= location.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 1; x <= location.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];

                                if (y == location.Y && x == location.X && target.Race == ObjectType.Monster)
                                {
                                    centerTarget = (MonsterObject)target;
                                }
                                
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                        if (target == null || !target.IsAttackTarget(player) || target.Node == null || target.Level > player.Level + 2) continue;

                                        MonsterObject mobTarget = (MonsterObject)target;

                                        if (centerTarget == null) centerTarget = mobTarget;

                                        mobTarget.ShockTime = Envir.Time + value;
                                        mobTarget.Target = null;
                                        break;
                                }
                            }

                        }
                    }

                    if (centerTarget == null) return;

                    for (byte i = 0; i < 8; i += 2)
                    {
                        Point startpoint = Functions.PointMove(location, (MirDirection)i, 2);
                        for (byte j = 0; j <= 4; j += 4)
                        {
                            MirDirection spawndirection = i == 0 || i == 4 ? MirDirection.Right : MirDirection.Up;
                            Point spawnpoint = Functions.PointMove(startpoint, spawndirection + j, 1);
                            if (spawnpoint.X <= 0 || spawnpoint.X > centerTarget.CurrentMap.Width) continue;
                            if (spawnpoint.Y <= 0 || spawnpoint.Y > centerTarget.CurrentMap.Height) continue;
                            SpellObject ob = new SpellObject
                            {
                                Spell = Spell.TrapHexagon,
                                ExpireTime = Envir.Time + value,
                                TickSpeed = 100,
                                Caster = player,
                                CurrentLocation = spawnpoint,
                                CastLocation = location,
                                CurrentMap = centerTarget.CurrentMap,
                                Target = centerTarget,
                            };

                            centerTarget.CurrentMap.AddObject(ob);
                            ob.Spawned();
                        }
                    }

                    train = true;

                    break;

                #endregion

                #region Curse

                case Spell.Curse:
                    value = (int)data[2];
                    location = (Point)data[3];
                    value2 = (int)data[4];

                    for (int y = location.Y - 3; y <= location.Y + 3; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 3; x <= location.X + 3; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];

                                if (target.Node == null) continue;

                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:

                                        if (Envir.Random.Next(10) >= 4) continue;

                                        //Only targets
                                        if (target.IsAttackTarget(player))
                                        {
                                            target.ApplyPoison(new Poison { PType = PoisonType.Slow, Duration = value, TickSpeed = 1000, Value = value2, Owner = player }, player);

                                            var stats = new Stats
                                            {
                                                [Stat.最大物理攻击数率] = value2 * -1,
                                                [Stat.最大魔法攻击数率] = value2 * -1,
                                                [Stat.最大道术攻击数率] = value2 * -1,
                                                [Stat.攻击速度数率] = target.Race == ObjectType.Player ? value2 * -1 : 0
                                            };

                                            target.AddBuff(BuffType.诅咒术, player, Settings.Second * value, stats);
                                            target.OperateTime = 0;
                                            train = true;
                                        }
                                        break;
                                }
                            }

                        }

                    }

                    break;

                #endregion

                #region ExplosiveTrap

                case Spell.ExplosiveTrap:
                    value = (int)data[2];
                    front = (Point)data[3];
                    int trapID = (int)data[4];

                    if (ValidPoint(front))
                    {
                        cell = GetCell(front);

                        bool cast = true;
                        if (cell.Objects != null)
                            for (int o = 0; o < cell.Objects.Count; o++)
                            {
                                MapObject target = cell.Objects[o];
                                if (target.Race != ObjectType.Spell || (((SpellObject)target).Spell != Spell.FireWall && ((SpellObject)target).Spell != Spell.ExplosiveTrap)) continue;

                                cast = false;
                                break;
                            }

                        if (cast)
                        {
                            player.LevelMagic(magic);

                            System.Drawing.Point[] traps = new Point[3];
                            traps[0] = front;
                            traps[1] = Functions.Left(front, player.Direction);
                            traps[2] = Functions.Right(front, player.Direction);

                            for (int i = 0; i <= 2; i++)
                            {
                                bool skip = false;
                                //for the extra spots make sure to not overlap new traps with old ones since it creates invisible double/tripple /... trap spots
                                if (i > 0)
                                {
                                    cell = GetCell(traps[i]);

                                    if (cell.Objects != null)
                                        for (int o = 0; o < cell.Objects.Count; o++)
                                        {
                                            MapObject target = cell.Objects[o];
                                            if (target.Race != ObjectType.Spell || (((SpellObject)target).Spell != Spell.FireWall && ((SpellObject)target).Spell != Spell.ExplosiveTrap)) continue;

                                            skip = true;
                                            break;
                                        }
                                }
                                if (skip) continue;
                                SpellObject ob = new SpellObject
                                {
                                    Spell = Spell.ExplosiveTrap,
                                    Value = value,
                                    ExpireTime = Envir.Time + (10 + value / 2) * 1000,
                                    TickSpeed = 500,
                                    Caster = player,
                                    CurrentLocation = traps[i],
                                    CurrentMap = this,
                                    ExplosiveTrapID = trapID,
                                    ExplosiveTrapCount = i
                                };

                                AddObject(ob);
                                ob.Spawned();
                            }
                        }
                    }
                    break;

                #endregion

                #region Plague

                case Spell.Plague:
                    // DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC])), location, pType);
                    // (int)data[2] === magic.GetDamage(GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]))
                    // (Point)data[3] === location
                    // (PoisonType)data[4] === pType
                    value = (int)data[2];
                    location = (Point)data[3];

                    // the skill affect a 3x3 square
                    var points= GetPointsInEffectiveSquare(location, Width, Height, 3);
                    foreach (var point in points)
                    {
                        cell = GetCell(point.X, point.Y);

                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject target = cell.Objects[i];
                            switch (target.Race)
                            {
                                case ObjectType.Monster:
                                case ObjectType.Player:
                                    //Only targets
                                    if (target.IsAttackTarget(player))
                                    {

                                        int chance = Envir.Random.Next(15);
                                        PoisonType poison;
                                        if (new int[] { 0, 1, 2 }.Contains(chance)) //3 in 15 chances it'll slow
                                            poison = PoisonType.Slow;
                                        else if (new int[] { 3, 4 }.Contains(chance)) //2 in 15 chances it'll freeze
                                            poison = PoisonType.Frozen;
                                        else if (new int[] { 5, 6, 7, 8, 9 }.Contains(chance)) //5 in 15 chances it'll red/green
                                            // (whatever type of poison player is holding)
                                            poison = (PoisonType)data[4];
                                        else //5 in 15 chances it'll do nothing
                                            poison = PoisonType.None;

                                        int tempValue = 0;

                                        if (poison == PoisonType.Red)
                                        {
                                            tempValue = value / 15 + magic.Level + 1;
                                        }
                                        else
                                        {
                                            tempValue = value + (magic.Level + 1) * 2;
                                        }

                                        if (poison != PoisonType.None)
                                        {
                                            target.ApplyPoison(new Poison { PType = poison, Duration = (2 * (magic.Level + 1)) + (value / 10), TickSpeed = 1000, Value = tempValue, Owner = player }, player, false, true);
                                        }

                                        if (target.Race == ObjectType.Player)
                                        {
                                            PlayerObject tempOb = (PlayerObject)target;

                                            tempOb.ChangeMP(-tempValue);
                                        }

                                        target.Attacked(player, player.Stats[Stat.MaxSC] * 2, DefenceType.MAC, true);

                                        train = true;
                                    }
                                    break;
                            }
                        }
                    }
                    break;

                #endregion

                #region Trap

                case Spell.Trap:
                    value = (int)data[2];
                    //location = (Point)data[3];
                    MapObject originalTarget = (MapObject)data[3];
                    location = originalTarget.CurrentLocation;
                    MonsterObject selectTarget = null;

                    if (!ValidPoint(location)) break;

                    cell = GetCell(location);

                    if (!cell.Valid || cell.Objects == null) break;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject target = cell.Objects[i];
                        if (target.Race == ObjectType.Monster)
                        {
                            selectTarget = (MonsterObject)target;

                            if (selectTarget == null || !selectTarget.IsAttackTarget(player) || selectTarget.Node == null || selectTarget.Level >= player.Level + 2) continue;
                            selectTarget.ShockTime = Envir.Time + value;
                            selectTarget.Target = null;
                            break;
                        }
                    }

                    if (selectTarget == null) return;

                    if (location.X <= 0 || location.X > selectTarget.CurrentMap.Width) break;
                    if (location.Y <= 0 || location.Y > selectTarget.CurrentMap.Height) break;
                    SpellObject spellOb = new SpellObject
                    {
                        Spell = Spell.Trap,
                        ExpireTime = Envir.Time + value,
                        TickSpeed = 100,
                        Caster = player,
                        CurrentLocation = location,
                        CastLocation = location,
                        CurrentMap = selectTarget.CurrentMap,
                        Target = selectTarget,
                    };

                    selectTarget.CurrentMap.AddObject(spellOb);
                    spellOb.Spawned();

                    train = true;
                    break;

                #endregion

                #region OneWithNature

                case Spell.OneWithNature:
                    value = (int)data[2];
                    location = (Point)data[3];

                    bool hasVampBuff = (player.Buffs.Any(ex => ex.Type == BuffType.吸血地闪));
                    bool hasPoisonBuff = (player.Buffs.Any(ex => ex.Type == BuffType.毒魔闪));

                    for (int y = location.Y - 2; y <= location.Y + 2; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 2; x <= location.X + 2; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (!target.IsAttackTarget(player) || target.Dead) break;

                                        //knockback
                                        //int distance = 1 + Math.Max(0, magic.Level - 1) + Envir.Random.Next(2);
                                        //dir = Functions.DirectionFromPoint(location, target.CurrentLocation);
                                        //if(target.Level < player.Level)
                                        //    target.Pushed(player, dir, distance);// <--crashes server somehow?

                                        if (target.Attacked(player, value, DefenceType.MAC, false) <= 0) break;

                                        if (hasVampBuff)//Vampire Effect
                                        {
                                            if (player.VampAmount == 0) player.VampTime = Envir.Time + 1000;
                                            player.VampAmount += (ushort)(value * (magic.Level + 1) * 0.25F);
                                        }
                                        if (hasPoisonBuff)//Poison Effect
                                        {
                                            target.ApplyPoison(new Poison
                                            {
                                                Duration = (value * 2) + (magic.Level + 1) * 7,
                                                Owner = player,
                                                PType = PoisonType.Green,
                                                TickSpeed = 2000,
                                                Value = value / 15 + magic.Level + 1 + Envir.Random.Next(player.Stats[Stat.毒素伤害])
                                            }, player);
                                            target.OperateTime = 0;
                                        }
                                        train = true;
                                        break;
                                }
                            }

                        }
                    }

                    if (hasVampBuff)
                    {
                        //Expire
                        player.AddBuff(BuffType.吸血地闪, player, Settings.Second * 1, new Stats());
                    }
                    if (hasPoisonBuff)
                    {
                        //Expire
                        player.AddBuff(BuffType.毒魔闪, player, Settings.Second * 1, new Stats());
                    }
                    break;

                #endregion

                #region Portal

                case Spell.Portal:                  
                    value = (int)data[2];
                    location = (Point)data[3];
                    value2 = (int)data[4];

                    spellOb = new SpellObject
                    {
                        Spell = Spell.Portal,
                        Value = value2,
                        ExpireTime = Envir.Time + value * 1000,
                        TickSpeed = 2000,
                        Caster = player,
                        CurrentLocation = location,
                        CurrentMap = this,
                    };

                    AddObject(spellOb);
                    spellOb.Spawned();
                    train = true;
                    break;

                #endregion

                #region DelayedExplosion

                case Spell.DelayedExplosion:
                    value = (int)data[2];
                    location = (Point)data[3];

                    for (int y = location.Y - 1; y <= location.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 1; x <= location.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject target = cell.Objects[i];
                                switch (target.Race)
                                {
                                    case ObjectType.Monster:
                                    case ObjectType.Player:
                                        //Only targets
                                        if (target.IsAttackTarget(player))
                                        {
                                            if (target.Attacked(player, value, DefenceType.MAC, false) > 0)
                                                train = false;//wouldnt want to make the skill give twice the points
                                        }
                                        break;
                                }
                            }

                        }

                    }

                    break;

                #endregion

                #region BattleCry
                case Spell.BattleCry:
                    location = (Point)data[2];

                    int startX = Math.Max(location.X - 2, 0);
                    int endX = Math.Min(location.X + 2, Width - 1);
                    int startY = Math.Max(location.Y - 2, 0);
                    int endY = Math.Min(location.Y + 2, Height - 1);

                    int randomValue = Envir.Random.Next(100);

                    for (int y = startY; y <= endY; y++)
                    {
                        for (int x = startX; x <= endX; x++)
                        {
                            cell = GetCell(x, y);
                            if (!cell.Valid || cell.Objects == null || cell.Objects.Count == 0) continue;

                            foreach (var target in cell.Objects)
                            {
                                if (target.Race != ObjectType.Monster) continue;

                                int threshold = magic.Level switch
                                {
                                    0 => 90, // 90% chance of failure (10% success)
                                    1 => 70, // 70% chance of failure (30% success)
                                    2 => 50, // 50% chance of failure (50% success)
                                    3 => 30, // 30% chance of failure (70% success)
                                    _ => 100 // Default case, should not occur
                                };


                                if (randomValue > threshold) continue;


                                if (((MonsterObject)target).Info.CoolEye == 100) continue;
                                target.Target = player;
                                target.OperateTime = 0;

                                train = true;
                            }
                        }
                    }
                    break;

                #endregion
                #region HealingCircle

                case Spell.HealingCircle:
                    value = (int)data[2];
                    location = (Point)data[3];

                    train = true;
                    show = true;

                    for (int y = location.Y - 1; y <= location.Y + 1; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - 1; x <= location.X + 1; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);

                            if (!cell.Valid) continue;

                            bool cast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != Spell.HealingCircle) continue;

                                    cast = false;
                                    break;
                                }

                            if (!cast) continue;

                            SpellObject ob = new SpellObject
                            {
                                Spell = Spell.HealingCircle,
                                Value = value,
                                ExpireTime = Envir.Time + (10000) + (5000 * magic.Level),
                                TickSpeed = 400,
                                Caster = player,
                                CurrentLocation = new Point(x, y),
                                CastLocation = location,
                                Show = show,
                                CurrentMap = this,
                            };

                            show = false;

                            AddObject(ob);
                            ob.Spawned();
                        }
                    }

                    break;

                    #endregion

                #region AbsorbFlame, AbsorbFlameRare（新增：吸魔炎风）

                case Spell.AbsorbFlame:
                case Spell.AbsorbFlameRare:
                    value = (int)data[2];
                    location = (Point)data[3];
                    int flameSeconds = (int)data[4];

                    if (!ValidPoint(location)) break;

                    //吸魔炎风：群体。普通 2 格（5×5），秘籍 6 格（13×13）
                    int flameRadius = magic.Spell == Spell.AbsorbFlameRare ? 6 : 2;

                    //只有落点那一份下发特效（SpellObject.GetInfo 按 Show 过滤），其余格子只做伤害结算
                    bool flameShow = true;

                    for (int y = location.Y - flameRadius; y <= location.Y + flameRadius; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - flameRadius; x <= location.X + flameRadius; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);
                            if (!cell.Valid) continue;

                            bool flameCast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != magic.Spell) continue;

                                    flameCast = false;
                                    break;
                                }

                            if (!flameCast) continue;

                            SpellObject flame = new SpellObject
                            {
                                Spell = magic.Spell,
                                Value = value,
                                ExpireTime = Envir.Time + (flameSeconds * 1000),
                                TickSpeed = 1000,
                                Caster = player,
                                CurrentLocation = new Point(x, y),
                                CastLocation = location,
                                Show = flameShow,
                                CurrentMap = this,
                            };

                            flameShow = false;

                            AddObject(flame);
                            flame.Spawned();
                        }
                    }

                    train = true;
                    break;

                #endregion

                #region SkyThunderShot, SkyThunderShotRare（新增：落雷杀）

                case Spell.SkyThunderShot:
                case Spell.SkyThunderShotRare:
                    value = (int)data[2];
                    location = (Point)data[3];
                    int thunderSeconds = (int)data[4];

                    if (!ValidPoint(location)) break;

                    //落雷杀：群体落雷。普通 4 格（9×9），秘笈 8 格（17×17）
                    int thunderRadius = magic.Spell == Spell.SkyThunderShotRare ? 8 : 4;

                    //只有落点那一份会下发特效（SpellObject.GetInfo 里按 Show 过滤），
                    //其余格子仅用于伤害结算，避免一次铺出上百个循环播放的落雷特效把客户端拖垮
                    bool thunderShow = true;

                    for (int y = location.Y - thunderRadius; y <= location.Y + thunderRadius; y++)
                    {
                        if (y < 0) continue;
                        if (y >= Height) break;

                        for (int x = location.X - thunderRadius; x <= location.X + thunderRadius; x++)
                        {
                            if (x < 0) continue;
                            if (x >= Width) break;

                            cell = GetCell(x, y);
                            if (!cell.Valid) continue;

                            bool thunderCast = true;
                            if (cell.Objects != null)
                                for (int o = 0; o < cell.Objects.Count; o++)
                                {
                                    MapObject target = cell.Objects[o];
                                    if (target.Race != ObjectType.Spell || ((SpellObject)target).Spell != magic.Spell) continue;

                                    thunderCast = false;
                                    break;
                                }

                            if (!thunderCast) continue;

                            SpellObject thunder = new SpellObject
                            {
                                Spell = magic.Spell,
                                Value = value,
                                ExpireTime = Envir.Time + (thunderSeconds * 1000),
                                TickSpeed = 600,
                                Caster = player,
                                CurrentLocation = new Point(x, y),
                                CastLocation = location,
                                Show = thunderShow,
                                CurrentMap = this,
                            };

                            thunderShow = false;

                            AddObject(thunder);
                            thunder.Spawned();
                        }
                    }

                    train = true;
                    break;

                #endregion
        }

            if (train)
                player.LevelMagic(magic);

        }

        public void AddObject(MapObject ob)
        {
            if (ob.Race == ObjectType.Player)
            {
                Players.Add((PlayerObject)ob);
            }

            if (ob.Race == ObjectType.Merchant) NPCs.Add((NPCObject)ob);
            if (ob.Race == ObjectType.Spell) Spells.Add((SpellObject)ob);
            if (ob.Race == ObjectType.Hero) Heroes.Add((HeroObject)ob);

            GetCell(ob.CurrentLocation).Add(ob);
        }

        public void RemoveObject(MapObject ob)
        {
            if (ob.Race == ObjectType.Player) Players.Remove((PlayerObject)ob);
            if (ob.Race == ObjectType.Merchant) NPCs.Remove((NPCObject)ob);
            if (ob.Race == ObjectType.Spell) Spells.Remove((SpellObject)ob);
            if (ob.Race == ObjectType.Hero) Heroes.Remove((HeroObject)ob);

            GetCell(ob.CurrentLocation).Remove(ob);
        }


        public SafeZoneInfo GetSafeZone(Point location)
        {
            for (int i = 0; i < Info.SafeZones.Count; i++)
            {
                SafeZoneInfo szi = Info.SafeZones[i];
                if (szi == null) continue;   // 数据里可能有空行（编辑器留白/旧版重载拷贝），不能让遍历崩掉
                if (Functions.InRange(szi.Location, location, szi.Size))
                    return szi;
            }
            return null;
        }

        public List<SpellObject> GetSpellObjects(Spell spell ,MapObject caster)
        {
            List<SpellObject> spellObjects = new List<SpellObject>();

            for (int i = 0; i < Spells.Count; i++)
            {
                if (spell != Spells[i].Spell) continue;
                if (caster != null && Spells[i].Caster != caster) continue;

                spellObjects.Add(Spells[i]);
            }

            return spellObjects;
        }

        public ConquestObject GetConquest(Point location)
        {
            for (int i = 0; i < Conquest.Count; i++)
            {
                ConquestObject swi = Conquest[i];

                if ((swi.Info.FullMap || Functions.InRange(swi.Info.Location, location, swi.Info.Size)) && swi.WarIsOn)
                    return swi;
            }
            return null;
        }

        //public ConquestObject GetInnerConquest(Map map, Point location)
        //{
        //    for (int i = 0; i < Conquest.Count; i++)
        //    {
        //        ConquestObject swi = Conquest[i];
        //        if (map.Info.Index != swi.Info.MapIndex) continue;

        //        if (Functions.InRange(swi.Info.KingLocation, location, swi.Info.KingSize) && swi.WarIsOn)
        //            return swi;
        //    }
        //    return null;
        //}

        public void Broadcast(Packet p, Point location)
        {
            if (p == null) return;

            for (int i = Players.Count - 1; i >= 0; i--)
            {
                PlayerObject player = Players[i];

                if (player is FakePlayerObject) continue;   // 假人没有客户端，别白扫（见 MapObject.Broadcast）

                if (Functions.InRange(location, player.CurrentLocation, Globals.DataRange))
                    player.Enqueue(p);                   
            }
        }

        public void BroadcastNPC(Packet p, Point location)
        {
            if (p == null) return;

            for (int i = Players.Count - 1; i >= 0; i--)
            {
                PlayerObject player = Players[i];

                if (player is FakePlayerObject) continue;   // 同上

                if (Functions.InRange(location, player.CurrentLocation, Globals.DataRange))
                    player.Enqueue(p);

            }
        }


        public void Broadcast(Packet p, Point location, PlayerObject Player)
        {
            if (p == null) return;

            if (Functions.InRange(location, Player.CurrentLocation, Globals.DataRange))
            {
                Player.Enqueue(p);
            }    
        }
    }
    public class Cell
    {
        public static Cell LowWall { get { return new Cell { Attribute = CellAttribute.LowWall }; } }
        public static Cell HighWall { get { return new Cell { Attribute = CellAttribute.HighWall }; } }

        public bool Valid
        {
            get { return Attribute == CellAttribute.Walk; }
        }

        public List<MapObject> Objects;
        public CellAttribute Attribute;
        public sbyte FishingAttribute = -1;

        public void Add(MapObject mapObject)
        {
            if (Objects == null) Objects = new List<MapObject>();

            Objects.Add(mapObject);
        }
        public void Remove(MapObject mapObject)
        {
            Objects.Remove(mapObject);
            if (Objects.Count == 0) Objects = null;
        }
    }
    public class MapRespawn
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public RespawnInfo Info;
        public MonsterInfo Monster;
        public Map Map;
        public int Count;
        public long RespawnTime;
        public ulong NextSpawnTick;
        public byte ErrorCount = 0;

        public List<RouteInfo> Route;

        /// <summary>方框内没有可走格时的返回值。用 -1 而不是 Point.Empty，避免和真实坐标 (0,0) 混淆。</summary>
        public static readonly Point InvalidSpawnPoint = new Point(-1, -1);

        public MapRespawn(RespawnInfo info)
        {
            Info = info;
            Monster = Envir.GetMonsterInfo(info.MonsterIndex);

            LoadRoutes();
        }
        public bool Spawn()
        {
            MonsterObject ob = MonsterObject.GetMonster(Monster);
            if (ob == null) return true;
            return ob.Spawn(this);
        }

        /// <summary>
        /// 取一个可站立的出生点：先在方框内随机试探 30 次，命中不了再按随机起点整框扫一遍。
        /// 取代原先「每个刷怪点各存一份可走点列表」的做法（那是 1.44 GB 的重复数据）。
        /// 结果与旧实现等价：方框内确实一格可走都没有时返回 InvalidSpawnPoint。
        /// </summary>
        public Point GetSpawnPoint(Map map)
        {
            if (map == null) return InvalidSpawnPoint;

            int cx = Info.Location.X, cy = Info.Location.Y, sp = Info.Spread;

            for (int i = 0; i < 30; i++)
            {
                int x = cx + Envir.Random.Next(-sp, sp + 1);
                int y = cy + Envir.Random.Next(-sp, sp + 1);
                if (CanSpawnAt(map, x, y)) return new Point(x, y);
            }

            // 兜底：整框扫一遍。用随机起点 + 环绕，避免总落在左上角导致怪全堆在一侧。
            int side = sp * 2 + 1;
            int total = side * side;
            int start = Envir.Random.Next(total);

            for (int k = 0; k < total; k++)
            {
                int idx = start + k;
                if (idx >= total) idx -= total;

                int x = cx - sp + (idx % side);
                int y = cy - sp + (idx / side);

                if (CanSpawnAt(map, x, y)) return new Point(x, y);
            }

            return InvalidSpawnPoint;
        }

        private static bool CanSpawnAt(Map map, int x, int y)
        {
            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return false;

            Cell cell = map.Cells[x, y];
            return cell != null && cell.Valid;
        }

        public void LoadRoutes()
        {
            Route = new List<RouteInfo>();

            if (string.IsNullOrEmpty(Info.RoutePath)) return;

            string fileName = Path.Combine(Settings.RoutePath, Info.RoutePath + ".txt");

            if (!File.Exists(fileName)) return;

            List<string> lines = File.ReadAllLines(fileName).ToList();

            foreach (string line in lines)
            {
                RouteInfo info = RouteInfo.FromText(line);

                if (info == null) continue;

                Route.Add(info);
            }
        }
    }
}
