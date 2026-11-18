using System.IO;

namespace ServerPackets;

public sealed class NewItemInfo : Packet
{
	public ItemInfo Info;

	public override short Index => 32;

	protected override void ReadPacket(BinaryReader reader)
	{
		Info = new ItemInfo(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Info.Save(writer);
	}
}
