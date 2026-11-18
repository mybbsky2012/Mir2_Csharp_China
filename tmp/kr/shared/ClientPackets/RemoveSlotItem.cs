using System.IO;

namespace ClientPackets;

public sealed class RemoveSlotItem : Packet
{
	public MirGridType Grid;

	public MirGridType GridTo;

	public ulong UniqueID;

	public int To;

	public ulong FromUniqueID;

	public override short Index => 20;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		GridTo = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		To = reader.ReadInt32();
		FromUniqueID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write((byte)GridTo);
		writer.Write(UniqueID);
		writer.Write(To);
		writer.Write(FromUniqueID);
	}
}
