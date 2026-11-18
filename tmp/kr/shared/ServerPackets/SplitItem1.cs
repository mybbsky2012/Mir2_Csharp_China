using System.IO;

namespace ServerPackets;

public sealed class SplitItem1 : Packet
{
	public MirGridType Grid;

	public ulong UniqueID;

	public ushort Count;

	public bool Success;

	public override short Index => 43;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		Count = reader.ReadUInt16();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(UniqueID);
		writer.Write(Count);
		writer.Write(Success);
	}
}
