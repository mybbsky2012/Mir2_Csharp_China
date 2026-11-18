using System.IO;

namespace ClientPackets;

public sealed class GetRentedItems : Packet
{
	public override short Index => 134;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
