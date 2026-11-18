using System.IO;

namespace ServerPackets;

public sealed class NewChatItem : Packet
{
	public UserItem Item;

	public override short Index => 34;

	protected override void ReadPacket(BinaryReader reader)
	{
		Item = new UserItem(reader);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		Item.Save(writer);
	}
}
