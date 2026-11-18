using System.IO;

namespace ClientPackets;

public sealed class ItemRentalLockItem : Packet
{
	public override short Index => 142;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
