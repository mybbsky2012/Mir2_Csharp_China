using System.IO;

namespace ServerPackets;

public sealed class MailCost : Packet
{
	public uint Cost;

	public override short Index => 234;

	protected override void ReadPacket(BinaryReader reader)
	{
		Cost = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Cost);
	}
}
