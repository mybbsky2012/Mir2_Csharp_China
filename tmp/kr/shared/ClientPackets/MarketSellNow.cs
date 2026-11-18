using System.IO;

namespace ClientPackets;

public sealed class MarketSellNow : Packet
{
	public ulong AuctionID;

	public override short Index => 73;

	protected override void ReadPacket(BinaryReader reader)
	{
		AuctionID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AuctionID);
	}
}
