using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class MapChanged : Packet
{
	public int MapIndex;

	public string FileName = string.Empty;

	public string Title = string.Empty;

	public ushort MiniMap;

	public ushort BigMap;

	public ushort Music;

	public LightSetting Lights;

	public Point Location;

	public MirDirection Direction;

	public byte MapDarkLight;

	public WeatherSetting Weather;

	public override short Index => 96;

	protected override void ReadPacket(BinaryReader reader)
	{
		MapIndex = reader.ReadInt32();
		FileName = reader.ReadString();
		Title = reader.ReadString();
		MiniMap = reader.ReadUInt16();
		BigMap = reader.ReadUInt16();
		Lights = (LightSetting)reader.ReadByte();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
		MapDarkLight = reader.ReadByte();
		Music = reader.ReadUInt16();
		Weather = (WeatherSetting)reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MapIndex);
		writer.Write(FileName);
		writer.Write(Title);
		writer.Write(MiniMap);
		writer.Write(BigMap);
		writer.Write((byte)Lights);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
		writer.Write(MapDarkLight);
		writer.Write(Music);
		writer.Write((ushort)Weather);
	}
}
