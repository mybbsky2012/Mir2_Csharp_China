using System.IO;

namespace ServerPackets;

public sealed class GuildNameRequest : Packet
{
	public override short Index => 169;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
