using System.IO;

namespace ServerPackets;

public sealed class GameShopInfo : Packet
{
	public GameShopItem Item;

	public int StockLevel;

	public bool Reset;

	public override short Index => 248;

	protected override void ReadPacket(BinaryReader reader)
	{
		Item = new GameShopItem(reader, packet: true);
		StockLevel = reader.ReadInt32();
		Reset = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		if (Item == null)
		{
			Item = new GameShopItem
			{
				Info = new ItemInfo
				{
					Name = string.Empty
				}
			};
		}
		Item.Save(writer, packet: true);
		writer.Write(StockLevel);
		writer.Write(Reset);
	}
}
