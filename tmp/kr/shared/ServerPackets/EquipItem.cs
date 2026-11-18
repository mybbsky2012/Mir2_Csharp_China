using System.IO;

namespace ServerPackets;

public sealed class EquipItem : Packet
{
	public MirGridType Grid;

	public ulong UniqueID;

	public int To;

	public bool Success;

	public override short Index => 36;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		UniqueID = reader.ReadUInt64();
		To = reader.ReadInt32();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(UniqueID);
		writer.Write(To);
		writer.Write(Success);
	}
}
