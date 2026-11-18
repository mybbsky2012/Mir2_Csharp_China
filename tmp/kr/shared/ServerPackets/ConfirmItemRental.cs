using System.IO;

namespace ServerPackets;

public sealed class ConfirmItemRental : Packet
{
	public override short Index => 263;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
