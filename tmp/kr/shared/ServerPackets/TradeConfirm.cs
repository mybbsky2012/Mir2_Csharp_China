using System.IO;

namespace ServerPackets;

public sealed class TradeConfirm : Packet
{
	public override short Index => 194;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
