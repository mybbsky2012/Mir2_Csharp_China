using System.IO;

namespace ServerPackets;

public sealed class RefineItem : Packet
{
	public ulong UniqueID;

	public override short Index => 47;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
	}
}
