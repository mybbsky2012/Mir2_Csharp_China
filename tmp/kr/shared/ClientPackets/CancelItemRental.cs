using System.IO;

namespace ClientPackets;

public sealed class CancelItemRental : Packet
{
	public override short Index => 140;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
