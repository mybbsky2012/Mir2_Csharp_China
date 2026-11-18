using System.IO;

namespace ServerPackets;

public sealed class ItemSlotSizeChanged : Packet
{
	public ulong UniqueID;

	public int SlotSize;

	public override short Index => 113;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		SlotSize = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(SlotSize);
	}
}
