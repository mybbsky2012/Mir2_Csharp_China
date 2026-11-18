using System.IO;

namespace ClientPackets;

public sealed class TradeRequest : Packet
{
	public override short Index => 93;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
