using System.IO;

namespace ClientPackets;

public sealed class DropItem : Packet
{
	public ulong UniqueID;

	public ushort Count;

	public bool HeroInventory;

	public override short Index => 23;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Count = reader.ReadUInt16();
		HeroInventory = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Count);
		writer.Write(HeroInventory);
	}
}
