using System.Drawing;
using Server.MirEnvir;

namespace Server.MirDatabase
{
    public class RespawnInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public int MonsterIndex;
        public Point Location;
        public ushort Count, Spread, Delay, RandomDelay;
        public byte Direction;

        public string RoutePath = string.Empty;
        public int RespawnIndex;
        public bool SaveRespawnTime = false;
        public ushort RespawnTicks; //leave 0 if not using this system!

        public RespawnInfo()
        {

        }
        public RespawnInfo(BinaryReader reader, int Version, int Customversion)
        {
            MonsterIndex = reader.ReadInt32();

            Location = new Point(reader.ReadInt32(), reader.ReadInt32());

            Count = reader.ReadUInt16();
            Spread = reader.ReadUInt16();

            Delay = reader.ReadUInt16();
            Direction = reader.ReadByte();

            RoutePath = reader.ReadString();

            if (Version > 67)
            {
                RandomDelay = reader.ReadUInt16();
                RespawnIndex = reader.ReadInt32();
                SaveRespawnTime = reader.ReadBoolean();
                RespawnTicks = reader.ReadUInt16();
            }
            else
            {
                RespawnIndex = ++Envir.RespawnIndex;
            }
        }

        /// <summary>
        /// 把一条刷怪点序列化成 11 段逗号分隔文本（与 <see cref="FromText"/> 严格对称）。
        /// 顺序：MonsterIndex,X,Y,Count,Spread,Delay,Direction,RandomDelay,RespawnIndex,SaveRespawnTime,RespawnTicks
        /// 「地图信息」窗体的「复制 / 粘贴」按钮用的就是这套文本。
        /// </summary>
        public string ToText()
        {
            return string.Join(",",
                MonsterIndex,
                Location.X,
                Location.Y,
                Count,
                Spread,
                Delay,
                Direction,
                RandomDelay,
                RespawnIndex,
                SaveRespawnTime,
                RespawnTicks);
        }

        public static RespawnInfo FromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string[] data = text.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);

            if (data.Length < 7) return null;

            RespawnInfo info = new RespawnInfo();

            int x,y ;

            if (!int.TryParse(data[0], out info.MonsterIndex)) return null;
            if (!int.TryParse(data[1], out x)) return null;
            if (!int.TryParse(data[2], out y)) return null;

            info.Location = new Point(x, y);

            if (!ushort.TryParse(data[3], out info.Count)) return null;
            if (!ushort.TryParse(data[4], out info.Spread)) return null;
            if (!ushort.TryParse(data[5], out info.Delay)) return null;
            if (!byte.TryParse(data[6], out info.Direction)) return null;

            // 后 4 段是可选字段：老格式（含脚本手写的刷怪文本）只写 7 段，
            // 原实现无条件读 data[7]~data[10]，段数不足时会 IndexOutOfRangeException。
            ushort tempUShort;
            int tempInt;
            bool tempBool;

            if (data.Length > 7 && ushort.TryParse(data[7], out tempUShort)) info.RandomDelay = tempUShort;
            if (data.Length > 8 && int.TryParse(data[8], out tempInt)) info.RespawnIndex = tempInt;
            if (data.Length > 9 && bool.TryParse(data[9], out tempBool)) info.SaveRespawnTime = tempBool;
            if (data.Length > 10 && ushort.TryParse(data[10], out tempUShort)) info.RespawnTicks = tempUShort;

            return info;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(MonsterIndex);

            writer.Write(Location.X);
            writer.Write(Location.Y);
            writer.Write(Count);
            writer.Write(Spread);

            writer.Write(Delay);
            writer.Write(Direction);

            writer.Write(RoutePath);

            writer.Write(RandomDelay);
            writer.Write(RespawnIndex);
            writer.Write(SaveRespawnTime);
            writer.Write(RespawnTicks);
        }

        public override string ToString()
        {
            return string.Format("Monster: {0} - {1} - {2} - {3} - {4} - {5} - {6} - {7} - {8} - {9}", MonsterIndex, Functions.PointToString(Location), Count, Spread, Delay, Direction, RandomDelay, RespawnIndex, SaveRespawnTime, RespawnTicks);
        }
    }

    public class RouteInfo
    {
        public Point Location;
        public int Delay;

        public static RouteInfo FromText(string text)
        {
            string[] data = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (data.Length < 2) return null;

            RouteInfo info = new RouteInfo();

            int x, y;

            if (!int.TryParse(data[0], out x)) return null;
            if (!int.TryParse(data[1], out y)) return null;

            info.Location = new Point(x, y);

            if (data.Length <= 2) return info;

            return !int.TryParse(data[2], out info.Delay) ? info : info;
        }
    }
}
