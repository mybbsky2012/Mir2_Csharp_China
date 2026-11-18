using System.IO;

namespace ClientPackets;

public sealed class SplitItem : Packet
{
	public MirGridType Grid;

	public ulong UniqueID;

	public ushort Count;

	public override short Index => 21;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		Count = reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(UniqueID);
		writer.Write(Count);
	}
}
