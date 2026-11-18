using System.IO;

namespace ServerPackets;

public sealed class UnlockHeroAutoPot : Packet
{
	public override short Index => 178;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
