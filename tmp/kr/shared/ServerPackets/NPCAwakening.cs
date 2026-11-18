using System.IO;

namespace ServerPackets;

public sealed class NPCAwakening : Packet
{
	public override short Index => 222;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
