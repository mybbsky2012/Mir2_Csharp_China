using System.IO;

namespace ServerPackets;

public sealed class RepairItem : Packet
{
	public ulong UniqueID;

	public override short Index => 111;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
	}
}
