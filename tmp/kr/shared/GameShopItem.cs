using System;
using System.IO;

public class GameShopItem
{
	public int ItemIndex;

	public int GIndex;

	public ItemInfo Info;

	public uint GoldPrice;

	public uint CreditPrice;

	public ushort Count = 1;

	public string Class = "";

	public string Category = "";

	public int Stock;

	public bool iStock;

	public bool Deal;

	public bool TopItem;

	public DateTime Date;

	public bool CanBuyGold;

	public bool CanBuyCredit;

	public GameShopItem()
	{
	}

	public GameShopItem(BinaryReader reader, int version = int.MaxValue, int Customversion = int.MaxValue)
	{
		ItemIndex = reader.ReadInt32();
		GIndex = reader.ReadInt32();
		GoldPrice = reader.ReadUInt32();
		CreditPrice = reader.ReadUInt32();
		if (version <= 84)
		{
			Count = (ushort)reader.ReadUInt32();
		}
		else
		{
			Count = reader.ReadUInt16();
		}
		Class = reader.ReadString();
		Category = reader.ReadString();
		Stock = reader.ReadInt32();
		iStock = reader.ReadBoolean();
		Deal = reader.ReadBoolean();
		TopItem = reader.ReadBoolean();
		Date = DateTime.FromBinary(reader.ReadInt64());
		if (version > 105)
		{
			CanBuyGold = reader.ReadBoolean();
			CanBuyCredit = reader.ReadBoolean();
		}
	}

	public GameShopItem(BinaryReader reader, bool packet = false)
	{
		ItemIndex = reader.ReadInt32();
		GIndex = reader.ReadInt32();
		Info = new ItemInfo(reader);
		GoldPrice = reader.ReadUInt32();
		CreditPrice = reader.ReadUInt32();
		Count = reader.ReadUInt16();
		Class = reader.ReadString();
		Category = reader.ReadString();
		Stock = reader.ReadInt32();
		iStock = reader.ReadBoolean();
		Deal = reader.ReadBoolean();
		TopItem = reader.ReadBoolean();
		Date = DateTime.FromBinary(reader.ReadInt64());
		CanBuyCredit = reader.ReadBoolean();
		CanBuyGold = reader.ReadBoolean();
	}

	public void Save(BinaryWriter writer, bool packet = false)
	{
		writer.Write(ItemIndex);
		writer.Write(GIndex);
		if (packet)
		{
			Info.Save(writer);
		}
		writer.Write(GoldPrice);
		writer.Write(CreditPrice);
		writer.Write(Count);
		writer.Write(Class);
		writer.Write(Category);
		writer.Write(Stock);
		writer.Write(iStock);
		writer.Write(Deal);
		writer.Write(TopItem);
		writer.Write(Date.ToBinary());
		writer.Write(CanBuyCredit);
		writer.Write(CanBuyGold);
	}

	public override string ToString()
	{
		return $"{GIndex}: {Info.Name}";
	}
}
