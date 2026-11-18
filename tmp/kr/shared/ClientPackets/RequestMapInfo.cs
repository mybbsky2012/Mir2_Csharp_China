using System.IO;

namespace ClientPackets;

public sealed class RequestMapInfo : Packet
{
	public int MapIndex;

	public override short Index => 36;

	protected override void ReadPacket(BinaryReader reader)
	{
		MapIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MapIndex);
	}
}
