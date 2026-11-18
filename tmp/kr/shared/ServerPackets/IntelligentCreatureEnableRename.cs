using System.IO;

namespace ServerPackets;

public sealed class IntelligentCreatureEnableRename : Packet
{
	public override short Index => 239;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
