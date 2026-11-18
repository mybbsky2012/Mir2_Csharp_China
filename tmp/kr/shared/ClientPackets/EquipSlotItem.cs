using System.IO;

namespace ClientPackets;

public sealed class EquipSlotItem : Packet
{
	public MirGridType Grid;

	public ulong UniqueID;

	public int To;

	public MirGridType GridTo;

	public ulong ToUniqueID;

	public override short Index => 98;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		To = reader.ReadInt32();
		GridTo = (MirGridType)reader.ReadByte();
		ToUniqueID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(UniqueID);
		writer.Write(To);
		writer.Write((byte)GridTo);
		writer.Write(ToUniqueID);
	}
}
