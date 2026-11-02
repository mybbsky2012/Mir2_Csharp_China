using System.Drawing;
using Server.MirEnvir;

namespace Server.MirDatabase
{
    public class MapInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        protected static Envir EditEnvir
        {
            get { return Envir.Edit; }
        }

        public int Index;
        public string FileName = string.Empty, Title = string.Empty;
        public ushort MiniMap, BigMap, Music;
        public LightSetting Light;
        public byte MapDarkLight = 0, MineIndex = 0;

        public bool NoTeleport, NoReconnect, NoRandom, NoEscape, NoRecall, NoDrug, NoPosition, NoFight,
            NoThrowItem, NoDropPlayer, NoDropMonster, NoNames, NoMount, NeedBridle, Fight, NeedHole, Fire, Lightning,
            NoTownTeleport, NoReincarnation;

        public string NoReconnectMap = string.Empty;
        public int FireDamage, LightningDamage;

        public List<SafeZoneInfo> SafeZones = new List<SafeZoneInfo>();
        public List<MovementInfo> Movements = new List<MovementInfo>();
        public List<RespawnInfo> Respawns = new List<RespawnInfo>();
        public List<NPCInfo> NPCs = new List<NPCInfo>();
        public List<MineZone> MineZones = new List<MineZone>();
        public List<Point> ActiveCoords = new List<Point>();
        public WeatherSetting WeatherParticles = WeatherSetting.无效果;

        public MapInfo()
        {

        }

        public MapInfo(BinaryReader reader)
        {
            Index = reader.ReadInt32();
            FileName = reader.ReadString();
            Title = reader.ReadString();
            MiniMap = reader.ReadUInt16();
            Light = (LightSetting)reader.ReadByte();

            BigMap = reader.ReadUInt16();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                SafeZones.Add(new SafeZoneInfo(reader) { Info = this });

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                Respawns.Add(new RespawnInfo(reader, Envir.LoadVersion, Envir.LoadCustomVersion));

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                Movements.Add(new MovementInfo(reader));

            NoTeleport = reader.ReadBoolean();
            NoReconnect = reader.ReadBoolean();
            NoReconnectMap = reader.ReadString();

            NoRandom = reader.ReadBoolean();
            NoEscape = reader.ReadBoolean();
            NoRecall = reader.ReadBoolean();
            NoDrug = reader.ReadBoolean();
            NoPosition = reader.ReadBoolean();
            NoThrowItem = reader.ReadBoolean();
            NoDropPlayer = reader.ReadBoolean();
            NoDropMonster = reader.ReadBoolean();
            NoNames = reader.ReadBoolean();
            Fight = reader.ReadBoolean();
            Fire = reader.ReadBoolean();
            FireDamage = reader.ReadInt32();
            Lightning = reader.ReadBoolean();
            LightningDamage = reader.ReadInt32();
            MapDarkLight = reader.ReadByte();
            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                MineZones.Add(new MineZone(reader));
            MineIndex = reader.ReadByte();
            NoMount = reader.ReadBoolean();
            NeedBridle = reader.ReadBoolean();
            NoFight = reader.ReadBoolean();
            Music = reader.ReadUInt16();

            if (Envir.LoadVersion < 78) return;
            NoTownTeleport = reader.ReadBoolean();
            if (Envir.LoadVersion < 79) return;
            NoReincarnation = reader.ReadBoolean();

            if (Envir.LoadVersion >= 110)
            {
                WeatherParticles = (WeatherSetting)reader.ReadUInt16();
            }
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(FileName);
            writer.Write(Title);
            writer.Write(MiniMap);
            writer.Write((byte)Light);
            writer.Write(BigMap);
            int safeZoneCount = 0;
            for (int i = 0; i < SafeZones.Count; i++)
                if (SafeZones[i] != null) safeZoneCount++;

            writer.Write(safeZoneCount);

            for (int i = 0; i < SafeZones.Count; i++)
                if (SafeZones[i] != null)   // 空行（null）不写入存档
                    SafeZones[i].Save(writer);

            writer.Write(Respawns.Count);
            for (int i = 0; i < Respawns.Count; i++)
                Respawns[i].Save(writer);

            writer.Write(Movements.Count);
            for (int i = 0; i < Movements.Count; i++)
                Movements[i].Save(writer);

            writer.Write(NoTeleport);
            writer.Write(NoReconnect);
            writer.Write(NoReconnectMap);
            writer.Write(NoRandom);
            writer.Write(NoEscape);
            writer.Write(NoRecall);
            writer.Write(NoDrug);
            writer.Write(NoPosition);
            writer.Write(NoThrowItem);
            writer.Write(NoDropPlayer);
            writer.Write(NoDropMonster);
            writer.Write(NoNames);
            writer.Write(Fight);
            writer.Write(Fire);
            writer.Write(FireDamage);
            writer.Write(Lightning);
            writer.Write(LightningDamage);
            writer.Write(MapDarkLight);
            writer.Write(MineZones.Count);
            for (int i = 0; i < MineZones.Count; i++)
                MineZones[i].Save(writer);
            writer.Write(MineIndex);

            writer.Write(NoMount);
            writer.Write(NeedBridle);

            writer.Write(NoFight);

            writer.Write(Music);
            writer.Write(NoTownTeleport);
            writer.Write(NoReincarnation);

            writer.Write((UInt16)WeatherParticles);

        }


        /// <summary>
        /// 造一份"运行服专用"的副本（地图热重载新增地图用）。标量字段全量复制，
        /// 配置列表各复制一份新列表且元素也复制（否则会改到编辑器的对象），
        /// Respawns/NPCs 只复制列表、元素仍共享 —— 这两块由「刷怪重载」「NPC全量重载」按引用/编号严格比对，不能换对象。
        /// </summary>
        public MapInfo Clone()
        {
            MapInfo copy = (MapInfo)MemberwiseClone();

            copy.SafeZones = CloneSafeZones(SafeZones, copy);
            copy.Movements = CloneMovements(Movements);
            copy.MineZones = CloneMineZones(MineZones);
            copy.Respawns = new List<RespawnInfo>(Respawns ?? new List<RespawnInfo>());
            copy.NPCs = new List<NPCInfo>(NPCs ?? new List<NPCInfo>());

            // ActiveCoords 是运行期的"脚本触发坐标"缓存（由 NPC 脚本 ACTIVEPOINT 现填），不随配置复制
            copy.ActiveCoords = new List<Point>();

            return copy;
        }

        /// <summary>
        /// 把 src 的配置覆盖到本实例（老图热重载的"配置刷新"用）。
        /// 故意不动这几样：Index / FileName（身份与地形，热改不了）、Respawns / NPCs（各由独立重载处理）、ActiveCoords（运行期缓存）。
        /// </summary>
        public bool CopyConfigFrom(MapInfo src, out bool zonesChanged, out bool movementsChanged, out bool minesChanged)
        {
            zonesChanged = movementsChanged = minesChanged = false;
            if (src == null) return false;

            bool changed = false;

            changed |= Set(ref Title, src.Title);
            changed |= Set(ref MiniMap, src.MiniMap);
            changed |= Set(ref BigMap, src.BigMap);
            changed |= Set(ref Music, src.Music);
            changed |= Set(ref Light, src.Light);
            changed |= Set(ref MapDarkLight, src.MapDarkLight);
            changed |= Set(ref WeatherParticles, src.WeatherParticles);

            changed |= Set(ref NoTeleport, src.NoTeleport);
            changed |= Set(ref NoReconnect, src.NoReconnect);
            changed |= Set(ref NoReconnectMap, src.NoReconnectMap);
            changed |= Set(ref NoRandom, src.NoRandom);
            changed |= Set(ref NoEscape, src.NoEscape);
            changed |= Set(ref NoRecall, src.NoRecall);
            changed |= Set(ref NoDrug, src.NoDrug);
            changed |= Set(ref NoPosition, src.NoPosition);
            changed |= Set(ref NoFight, src.NoFight);
            changed |= Set(ref NoThrowItem, src.NoThrowItem);
            changed |= Set(ref NoDropPlayer, src.NoDropPlayer);
            changed |= Set(ref NoDropMonster, src.NoDropMonster);
            changed |= Set(ref NoNames, src.NoNames);
            changed |= Set(ref NoMount, src.NoMount);
            changed |= Set(ref NeedBridle, src.NeedBridle);
            changed |= Set(ref Fight, src.Fight);
            changed |= Set(ref NeedHole, src.NeedHole);
            changed |= Set(ref Fire, src.Fire);
            changed |= Set(ref FireDamage, src.FireDamage);
            changed |= Set(ref Lightning, src.Lightning);
            changed |= Set(ref LightningDamage, src.LightningDamage);
            changed |= Set(ref NoTownTeleport, src.NoTownTeleport);
            changed |= Set(ref NoReincarnation, src.NoReincarnation);

            if (!SameSafeZones(SafeZones, src.SafeZones))
            {
                zonesChanged = true;
                changed = true;
                SafeZones = CloneSafeZones(src.SafeZones, this);
            }

            if (!SameMovements(Movements, src.Movements))
            {
                movementsChanged = true;
                changed = true;
                Movements = CloneMovements(src.Movements);
            }

            if (MineIndex != src.MineIndex || !SameMineZones(MineZones, src.MineZones))
            {
                minesChanged = true;
                changed = true;
                MineIndex = src.MineIndex;
                MineZones = CloneMineZones(src.MineZones);
            }

            return changed;
        }

        private static bool Set<T>(ref T field, T value) where T : struct
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;

            field = value;
            return true;
        }

        private static bool Set(ref string field, string value)
        {
            value = value ?? string.Empty;

            if (string.Equals(field ?? string.Empty, value, StringComparison.Ordinal)) return false;

            field = value;
            return true;
        }

        private static List<SafeZoneInfo> CloneSafeZones(List<SafeZoneInfo> src, MapInfo owner)
        {
            List<SafeZoneInfo> list = new List<SafeZoneInfo>();

            if (src == null) return list;

            for (int i = 0; i < src.Count; i++)
            {
                SafeZoneInfo s = src[i];

                // 编辑器数据里安全区可能有空行（null），拷贝时直接丢弃，
                // 否则运行时 GetSafeZone 遍历到 null 元素会抛空引用（分身推人/传送等都会触发）
                if (s == null) continue;

                list.Add(new SafeZoneInfo
                {
                    Location = s.Location,
                    Size = s.Size,
                    StartPoint = s.StartPoint,
                    Info = owner   // 反指必须指向副本自己：出生点/随机传送要靠 szi.Info.Index
                });
            }

            return list;
        }

        private static List<MovementInfo> CloneMovements(List<MovementInfo> src)
        {
            List<MovementInfo> list = new List<MovementInfo>();

            if (src == null) return list;

            for (int i = 0; i < src.Count; i++)
            {
                MovementInfo s = src[i];

                list.Add(s == null ? null : new MovementInfo
                {
                    MapIndex = s.MapIndex,
                    Source = s.Source,
                    Destination = s.Destination,
                    NeedHole = s.NeedHole,
                    NeedMove = s.NeedMove,
                    ShowOnBigMap = s.ShowOnBigMap,
                    ConquestIndex = s.ConquestIndex,
                    Icon = s.Icon
                });
            }

            return list;
        }

        private static List<MineZone> CloneMineZones(List<MineZone> src)
        {
            List<MineZone> list = new List<MineZone>();

            if (src == null) return list;

            for (int i = 0; i < src.Count; i++)
            {
                MineZone s = src[i];

                list.Add(s == null ? null : new MineZone
                {
                    Location = s.Location,
                    Size = s.Size,
                    Mine = s.Mine
                });
            }

            return list;
        }

        private static bool SameSafeZones(List<SafeZoneInfo> a, List<SafeZoneInfo> b)
        {
            int ac = a == null ? 0 : a.Count;
            int bc = b == null ? 0 : b.Count;

            if (ac != bc) return false;

            for (int i = 0; i < ac; i++)
            {
                SafeZoneInfo x = a[i], y = b[i];

                if (x == null || y == null)
                {
                    if (x != y) return false;
                    continue;
                }

                if (x.Location != y.Location || x.Size != y.Size || x.StartPoint != y.StartPoint) return false;
            }

            return true;
        }

        private static bool SameMovements(List<MovementInfo> a, List<MovementInfo> b)
        {
            int ac = a == null ? 0 : a.Count;
            int bc = b == null ? 0 : b.Count;

            if (ac != bc) return false;

            for (int i = 0; i < ac; i++)
            {
                MovementInfo x = a[i], y = b[i];

                if (x == null || y == null)
                {
                    if (x != y) return false;
                    continue;
                }

                if (x.MapIndex != y.MapIndex || x.Source != y.Source || x.Destination != y.Destination) return false;
                if (x.NeedHole != y.NeedHole || x.NeedMove != y.NeedMove || x.ShowOnBigMap != y.ShowOnBigMap) return false;
                if (x.ConquestIndex != y.ConquestIndex || x.Icon != y.Icon) return false;
            }

            return true;
        }

        private static bool SameMineZones(List<MineZone> a, List<MineZone> b)
        {
            int ac = a == null ? 0 : a.Count;
            int bc = b == null ? 0 : b.Count;

            if (ac != bc) return false;

            for (int i = 0; i < ac; i++)
            {
                MineZone x = a[i], y = b[i];

                if (x == null || y == null)
                {
                    if (x != y) return false;
                    continue;
                }

                if (x.Location != y.Location || x.Size != y.Size || x.Mine != y.Mine) return false;
            }

            return true;
        }

        public void CreateMap()
        {
            for (int j = 0; j < Envir.NPCInfoList.Count; j++)
            {
                if (Envir.NPCInfoList[j].MapIndex != Index) continue;

                NPCs.Add(Envir.NPCInfoList[j]);
            }

            Map map = new Map(this);

            if (!map.Load()) return;

            Envir.MapList.Add(map);

            for (int i = 0; i < SafeZones.Count; i++)
            {
                if (SafeZones[i] == null) continue;   // 空行不进出生点
                if (SafeZones[i].StartPoint)
                    Envir.StartPoints.Add(SafeZones[i]);
            }
        }

        public void CreateSafeZone()
        {
            SafeZones.Add(new SafeZoneInfo { Info = this });
        }

        public void CreateRespawnInfo()
        {
            Respawns.Add(new RespawnInfo { RespawnIndex = ++EditEnvir.RespawnIndex });
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Index, Title);
        }

        public void CreateNPCInfo()
        {
            NPCs.Add(new NPCInfo());
        }

        public void CreateMovementInfo()
        {
            Movements.Add(new MovementInfo());
        }

        public static void FromText(string text)
        {
            string[] data = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (data.Length < 8) return;

            MapInfo info = new MapInfo { FileName = data[0], Title = data[1] };


            if (!ushort.TryParse(data[2], out info.MiniMap)) return;

            if (!Enum.TryParse(data[3], out info.Light)) return;
            int sziCount, miCount, riCount, npcCount;

            if (!int.TryParse(data[4], out sziCount)) return;
            if (!int.TryParse(data[5], out miCount)) return;
            if (!int.TryParse(data[6], out riCount)) return;
            if (!int.TryParse(data[7], out npcCount)) return;


            int start = 8;

            for (int i = 0; i < sziCount; i++)
            {
                SafeZoneInfo temp = new SafeZoneInfo { Info = info };
                int x, y;

                if (!int.TryParse(data[start + (i * 4)], out x)) return;
                if (!int.TryParse(data[start + 1 + (i * 4)], out y)) return;
                if (!ushort.TryParse(data[start + 2 + (i * 4)], out temp.Size)) return;
                if (!bool.TryParse(data[start + 3 + (i * 4)], out temp.StartPoint)) return;

                temp.Location = new Point(x, y);
                info.SafeZones.Add(temp);
            }
            start += sziCount * 4;



            for (int i = 0; i < miCount; i++)
            {
                MovementInfo temp = new MovementInfo();
                int x, y;

                if (!int.TryParse(data[start + (i * 5)], out x)) return;
                if (!int.TryParse(data[start + 1 + (i * 5)], out y)) return;
                temp.Source = new Point(x, y);

                if (!int.TryParse(data[start + 2 + (i * 5)], out temp.MapIndex)) return;

                if (!int.TryParse(data[start + 3 + (i * 5)], out x)) return;
                if (!int.TryParse(data[start + 4 + (i * 5)], out y)) return;
                temp.Destination = new Point(x, y);

                info.Movements.Add(temp);
            }
            start += miCount * 5;


            for (int i = 0; i < riCount; i++)
            {
                RespawnInfo temp = new RespawnInfo();
                int x, y;

                if (!int.TryParse(data[start + (i * 7)], out temp.MonsterIndex)) return;
                if (!int.TryParse(data[start + 1 + (i * 7)], out x)) return;
                if (!int.TryParse(data[start + 2 + (i * 7)], out y)) return;

                temp.Location = new Point(x, y);

                if (!ushort.TryParse(data[start + 3 + (i * 7)], out temp.Count)) return;
                if (!ushort.TryParse(data[start + 4 + (i * 7)], out temp.Spread)) return;
                if (!ushort.TryParse(data[start + 5 + (i * 7)], out temp.Delay)) return;
                if (!byte.TryParse(data[start + 6 + (i * 7)], out temp.Direction)) return;
                if (!int.TryParse(data[start + 7 + (i * 7)], out temp.RespawnIndex)) return;
                if (!bool.TryParse(data[start + 8 + (i * 7)], out temp.SaveRespawnTime)) return;
                if (!ushort.TryParse(data[start + 9 + (i * 7)], out temp.RespawnTicks)) return;

                info.Respawns.Add(temp);
            }
            start += riCount * 7;


            for (int i = 0; i < npcCount; i++)
            {
                NPCInfo temp = new NPCInfo { FileName = data[start + (i * 6)], Name = data[start + 1 + (i * 6)] };
                int x, y;

                if (!int.TryParse(data[start + 2 + (i * 6)], out x)) return;
                if (!int.TryParse(data[start + 3 + (i * 6)], out y)) return;

                temp.Location = new Point(x, y);

                if (!ushort.TryParse(data[start + 4 + (i * 6)], out temp.Rate)) return;
                if (!ushort.TryParse(data[start + 5 + (i * 6)], out temp.Image)) return;

                info.NPCs.Add(temp);
            }



            info.Index = ++EditEnvir.MapIndex;
            EditEnvir.MapInfoList.Add(info);
        }
    }
}
