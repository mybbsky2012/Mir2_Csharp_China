using System.IO;

namespace ClientPackets;

public sealed class PickUp : Packet
{
	public override short Index => 35;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
