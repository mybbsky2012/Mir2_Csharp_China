using System.IO;

namespace ServerPackets;

public sealed class Connected : Packet
{
	public override short Index => 0;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
