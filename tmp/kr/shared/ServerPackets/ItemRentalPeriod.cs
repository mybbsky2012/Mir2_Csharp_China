using System.IO;

namespace ServerPackets;

public sealed class ItemRentalPeriod : Packet
{
	public uint Days;

	public override short Index => 255;

	protected override void ReadPacket(BinaryReader reader)
	{
		Days = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Days);
	}
}
