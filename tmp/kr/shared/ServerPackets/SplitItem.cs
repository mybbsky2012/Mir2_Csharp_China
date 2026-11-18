using System.IO;

namespace ServerPackets;

public sealed class SplitItem : Packet
{
	public UserItem Item;

	public MirGridType Grid;

	public override short Index => 42;

	protected override void ReadPacket(BinaryReader reader)
	{
		if (reader.ReadBoolean())
		{
			Item = new UserItem(reader);
		}
		Grid = (MirGridType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Item != null);
		if (Item != null)
		{
			Item.Save(writer);
		}
		writer.Write((byte)Grid);
	}
}
