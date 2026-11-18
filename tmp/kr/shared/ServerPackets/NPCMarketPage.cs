using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class NPCMarketPage : Packet
{
	public List<ClientAuction> Listings = new List<ClientAuction>();

	public override short Index => 154;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Listings.Add(new ClientAuction(reader));
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Listings.Count);
		for (int i = 0; i < Listings.Count; i++)
		{
			Listings[i].Save(writer);
		}
	}
}
