using System.IO;

namespace ServerPackets;

public sealed class RequestReincarnation : Packet
{
	public override short Index => 206;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
