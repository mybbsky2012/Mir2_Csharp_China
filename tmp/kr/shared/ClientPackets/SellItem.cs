using System.IO;

namespace ClientPackets;

public sealed class SellItem : Packet
{
	public ulong UniqueID;

	public ushort Count;

	public override short Index => 49;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Count = reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Count);
	}
}
