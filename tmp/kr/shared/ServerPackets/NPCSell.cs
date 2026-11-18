using System.IO;

namespace ServerPackets;

public sealed class NPCSell : Packet
{
	public override short Index => 101;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
