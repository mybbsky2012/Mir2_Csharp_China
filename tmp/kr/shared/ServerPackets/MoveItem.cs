using System.IO;

namespace ServerPackets;

public sealed class MoveItem : Packet
{
	public MirGridType Grid;

	public int From;

	public int To;

	public bool Success;

	public override short Index => 35;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		From = reader.ReadInt32();
		To = reader.ReadInt32();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(From);
		writer.Write(To);
		writer.Write(Success);
	}
}
