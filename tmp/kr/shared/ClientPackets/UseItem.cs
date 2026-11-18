using System.IO;

namespace ClientPackets;

public sealed class UseItem : Packet
{
	public ulong UniqueID;

	public MirGridType Grid;

	public override short Index => 22;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Grid = (MirGridType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write((byte)Grid);
	}
}
