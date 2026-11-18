using System.IO;

namespace ServerPackets;

public sealed class GuildRequestWar : Packet
{
	public override short Index => 173;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
