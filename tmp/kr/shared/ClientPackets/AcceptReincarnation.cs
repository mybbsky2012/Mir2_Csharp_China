using System.IO;

namespace ClientPackets;

public sealed class AcceptReincarnation : Packet
{
	public override short Index => 105;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
