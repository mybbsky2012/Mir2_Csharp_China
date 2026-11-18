using System.IO;

namespace ServerPackets;

public sealed class CancelReincarnation : Packet
{
	public override short Index => 205;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
