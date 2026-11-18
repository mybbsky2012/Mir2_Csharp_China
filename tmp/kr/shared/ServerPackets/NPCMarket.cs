using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class NPCMarket : Packet
{
	public List<ClientAuction> Listings = new List<ClientAuction>();

	public int Pages;

	public bool UserMode;

	public override short Index => 153;

	protected override void ReadPacket(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Listings.Add(new ClientAuction(reader));
		}
		Pages = reader.ReadInt32();
		UserMode = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Listings.Count);
		for (int i = 0; i < Listings.Count; i++)
		{
			Listings[i].Save(writer);
		}
		writer.Write(Pages);
		writer.Write(UserMode);
	}
}
