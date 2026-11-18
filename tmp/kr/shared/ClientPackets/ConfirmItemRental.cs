using System.IO;

namespace ClientPackets;

public sealed class ConfirmItemRental : Packet
{
	public override short Index => 143;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
