using System.IO;

namespace ServerPackets;

public sealed class UseItem : Packet
{
	public ulong UniqueID;

	public bool Success;

	public MirGridType Grid;

	public override short Index => 50;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Success = reader.ReadBoolean();
		Grid = (MirGridType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Success);
		writer.Write((byte)Grid);
	}
}
