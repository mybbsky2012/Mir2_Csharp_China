using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class GetRentedItems : Packet
{
	public List<ItemRentalInformation> RentedItems = new List<ItemRentalInformation>();

	public override short Index => 252;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			RentedItems.Add(new ItemRentalInformation(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(RentedItems.Count);
		foreach (ItemRentalInformation rentedItem in RentedItems)
		{
			rentedItem.Save(writer);
		}
	}
}
