using System.IO;

namespace ServerPackets;

public sealed class DropItem : Packet
{
	public ulong UniqueID;

	public ushort Count;

	public bool HeroItem;

	public bool Success;

	public override short Index => 51;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Count = reader.ReadUInt16();
		HeroItem = reader.ReadBoolean();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Count);
		writer.Write(HeroItem);
		writer.Write(Success);
	}
}
