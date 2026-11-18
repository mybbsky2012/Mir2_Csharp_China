using System.IO;

namespace ServerPackets;

public sealed class CanConfirmItemRental : Packet
{
	public override short Index => 262;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
