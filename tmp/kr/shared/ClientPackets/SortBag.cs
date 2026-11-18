using System.IO;

namespace ClientPackets;

public sealed class SortBag : Packet
{
	public override short Index => 145;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
