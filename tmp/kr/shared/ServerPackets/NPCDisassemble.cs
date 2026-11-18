using System.IO;

namespace ServerPackets;

public sealed class NPCDisassemble : Packet
{
	public override short Index => 223;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
