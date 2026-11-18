using System.IO;

namespace ServerPackets;

public sealed class NPCReset : Packet
{
	public override short Index => 225;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
