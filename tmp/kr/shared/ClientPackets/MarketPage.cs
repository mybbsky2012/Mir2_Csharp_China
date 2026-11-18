using System.IO;

namespace ClientPackets;

public sealed class MarketPage : Packet
{
	public int Page;

	public override short Index => 70;

	protected override void ReadPacket(BinaryReader reader)
	{
		Page = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Page);
	}
}
