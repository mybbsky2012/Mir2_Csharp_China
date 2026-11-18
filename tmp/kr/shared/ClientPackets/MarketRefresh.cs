using System.IO;

namespace ClientPackets;

public sealed class MarketRefresh : Packet
{
	public override short Index => 69;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
