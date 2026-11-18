using System.IO;

namespace ClientPackets;

public sealed class MoveItem : Packet
{
	public MirGridType Grid;

	public int From;

	public int To;

	public override short Index => 14;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		From = reader.ReadInt32();
		To = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(From);
		writer.Write(To);
	}
}
