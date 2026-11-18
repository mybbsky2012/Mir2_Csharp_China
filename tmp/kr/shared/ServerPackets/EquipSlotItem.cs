using System.IO;

namespace ServerPackets;

public sealed class EquipSlotItem : Packet
{
	public MirGridType Grid;

	public ulong UniqueID;

	public int To;

	public bool Success;

	public MirGridType GridTo;

	public override short Index => 197;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		To = reader.ReadInt32();
		GridTo = (MirGridType)reader.ReadByte();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(UniqueID);
		writer.Write(To);
		writer.Write((byte)GridTo);
		writer.Write(Success);
	}
}
