using System.IO;

namespace ServerPackets;

public sealed class NewMapInfo : Packet
{
	public int MapIndex;

	public ClientMapInfo Info;

	public override short Index => 18;

	protected override void ReadPacket(BinaryReader reader)
	{
		MapIndex = reader.ReadInt32();
		Info = new ClientMapInfo(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MapIndex);
		Info.Save(writer);
	}
}
