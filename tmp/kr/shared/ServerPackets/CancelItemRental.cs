using System.IO;

namespace ServerPackets;

public sealed class CancelItemRental : Packet
{
	public override short Index => 259;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
