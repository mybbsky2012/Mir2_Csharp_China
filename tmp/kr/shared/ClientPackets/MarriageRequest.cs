using System.IO;

namespace ClientPackets;

public sealed class MarriageRequest : Packet
{
	public override short Index => 84;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
