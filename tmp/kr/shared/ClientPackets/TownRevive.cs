using System.IO;

namespace ClientPackets;

public sealed class TownRevive : Packet
{
	public override short Index => 65;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
