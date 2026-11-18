using System.IO;

namespace ServerPackets;

public sealed class NPCCheckRefine : Packet
{
	public override short Index => 105;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
