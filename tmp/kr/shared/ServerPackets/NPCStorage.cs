using System.IO;

namespace ServerPackets;

public sealed class NPCStorage : Packet
{
	public override short Index => 108;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
