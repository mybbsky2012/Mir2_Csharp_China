using System.IO;

namespace ClientPackets;

public sealed class RemoveItem : Packet
{
	public MirGridType Grid;

	public ulong UniqueID;

	public int To;

	public override short Index => 19;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		To = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(UniqueID);
		writer.Write(To);
	}
}
