using System.IO;

namespace ServerPackets;

public sealed class NPCDowngrade : Packet
{
	public override short Index => 224;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
