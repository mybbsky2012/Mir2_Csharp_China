using System.IO;

namespace ClientPackets;

public sealed class TradeCancel : Packet
{
	public override short Index => 97;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
