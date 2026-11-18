using System.IO;

namespace ServerPackets;

public sealed class NPCConsign : Packet
{
	public override short Index => 152;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
