using System.IO;

namespace ClientPackets;

public sealed class CancelReincarnation : Packet
{
	public override short Index => 106;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
