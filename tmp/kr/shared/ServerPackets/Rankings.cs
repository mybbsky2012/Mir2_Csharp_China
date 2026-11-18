using System.Collections.Generic;
using System.IO;

namespace ServerPackets;

public sealed class Rankings : Packet
{
	public byte RankType;

	public int MyRank;

	public List<RankCharacterInfo> ListingDetails = new List<RankCharacterInfo>();

	public List<long> Listings = new List<long>();

	public int Count;

	public override short Index => 250;

	public override bool Observable => false;

	protected override void ReadPacket(BinaryReader reader)
	{
		RankType = reader.ReadByte();
		MyRank = reader.ReadInt32();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ListingDetails.Add(new RankCharacterInfo(reader));
		}
		num = reader.ReadInt32();
		for (int j = 0; j < num; j++)
		{
			Listings.Add(reader.ReadInt64());
		}
		Count = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(RankType);
		writer.Write(MyRank);
		writer.Write(ListingDetails.Count);
		for (int i = 0; i < ListingDetails.Count; i++)
		{
			ListingDetails[i].Save(writer);
		}
		writer.Write(Listings.Count);
		for (int j = 0; j < Listings.Count; j++)
		{
			writer.Write(Listings[j]);
		}
		writer.Write(Count);
	}
}
