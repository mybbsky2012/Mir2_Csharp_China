using System.IO;

namespace ServerPackets;

public sealed class RemoveMagic : Packet
{
	public int PlaceId;

	public override short Index => 116;

	protected override void ReadPacket(BinaryReader reader)
	{
		PlaceId = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(PlaceId);
	}
}
