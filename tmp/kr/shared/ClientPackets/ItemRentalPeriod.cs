using System.IO;

namespace ClientPackets;

public sealed class ItemRentalPeriod : Packet
{
	public uint Days;

	public override short Index => 137;

	protected override void ReadPacket(BinaryReader reader)
	{
		Days = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Days);
	}
}
