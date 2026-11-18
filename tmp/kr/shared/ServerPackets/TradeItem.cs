using System.IO;

namespace ServerPackets;

public sealed class TradeItem : Packet
{
	public UserItem[] TradeItems;

	public override short Index => 193;

	protected override void ReadPacket(BinaryReader reader)
	{
		TradeItems = new UserItem[reader.ReadInt32()];
		for (int i = 0; i < TradeItems.Length; i++)
		{
			if (reader.ReadBoolean())
			{
				TradeItems[i] = new UserItem(reader);
			}
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(TradeItems.Length);
		for (int i = 0; i < TradeItems.Length; i++)
		{
			UserItem userItem = TradeItems[i];
			writer.Write(userItem != null);
			userItem?.Save(writer);
		}
	}
}
