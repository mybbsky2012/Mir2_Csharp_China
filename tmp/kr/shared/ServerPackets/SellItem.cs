using System.IO;

namespace ServerPackets;

public sealed class SellItem : Packet
{
	public ulong UniqueID;

	public ushort Count;

	public bool Success;

	public override short Index => 109;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Count = reader.ReadUInt16();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Count);
		writer.Write(Success);
	}
}
