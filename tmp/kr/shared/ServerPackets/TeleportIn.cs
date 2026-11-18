using System.IO;

namespace ServerPackets;

public sealed class TeleportIn : Packet
{
	public override short Index => 99;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
