using System.IO;

namespace ClientPackets;

public sealed class ItemRentalLockFee : Packet
{
	public override short Index => 141;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
