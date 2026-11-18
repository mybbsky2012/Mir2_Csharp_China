using System.IO;

namespace ServerPackets;

public sealed class Revived : Packet
{
	public override short Index => 134;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
