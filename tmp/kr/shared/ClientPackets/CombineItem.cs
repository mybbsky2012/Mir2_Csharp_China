using System.IO;

namespace ClientPackets;

public sealed class CombineItem : Packet
{
	public MirGridType Grid;

	public ulong IDFrom;

	public ulong IDTo;

	public override short Index => 107;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		IDFrom = reader.ReadUInt64();
		IDTo = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(IDFrom);
		writer.Write(IDTo);
	}
}
