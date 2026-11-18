using System.IO;

namespace ClientPackets;

public sealed class ItemRentalRequest : Packet
{
	public override short Index => 135;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
