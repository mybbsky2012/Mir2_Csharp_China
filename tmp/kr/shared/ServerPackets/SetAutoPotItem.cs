using System.IO;

namespace ServerPackets;

public sealed class SetAutoPotItem : Packet
{
	public MirGridType Grid;

	public int ItemIndex;

	public override short Index => 180;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		ItemIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(ItemIndex);
	}
}
