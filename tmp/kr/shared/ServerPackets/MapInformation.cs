using System.IO;

namespace ServerPackets;

public sealed class MapInformation : Packet
{
	public int MapIndex;

	public string FileName = string.Empty;

	public string Title = string.Empty;

	public ushort MiniMap;

	public ushort BigMap;

	public ushort Music;

	public LightSetting Lights;

	public bool Lightning;

	public bool Fire;

	public byte MapDarkLight;

	public WeatherSetting WeatherParticles;

	public override short Index => 17;

	protected override void ReadPacket(BinaryReader reader)
	{
		MapIndex = reader.ReadInt32();
		FileName = reader.ReadString();
		Title = reader.ReadString();
		MiniMap = reader.ReadUInt16();
		BigMap = reader.ReadUInt16();
		Lights = (LightSetting)reader.ReadByte();
		byte num = reader.ReadByte();
		if ((num & 1) == 1)
		{
			Lightning = true;
		}
		if ((num & 2) == 2)
		{
			Fire = true;
		}
		MapDarkLight = reader.ReadByte();
		Music = reader.ReadUInt16();
		WeatherParticles = (WeatherSetting)reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MapIndex);
		writer.Write(FileName);
		writer.Write(Title);
		writer.Write(MiniMap);
		writer.Write(BigMap);
		writer.Write((byte)Lights);
		byte b = 0;
		b = (byte)((uint)b | (Lightning ? 1u : 0u));
		b |= (byte)(Fire ? 2u : 0u);
		writer.Write(b);
		writer.Write(MapDarkLight);
		writer.Write(Music);
		writer.Write((ushort)WeatherParticles);
	}
}
