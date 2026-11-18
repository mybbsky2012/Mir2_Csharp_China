using System.IO;

namespace ClientPackets;

public sealed class MarketBuy : Packet
{
	public ulong AuctionID;

	public uint BidPrice;

	public override short Index => 71;

	protected override void ReadPacket(BinaryReader reader)
	{
		AuctionID = reader.ReadUInt64();
		BidPrice = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AuctionID);
		writer.Write(BidPrice);
	}
}
