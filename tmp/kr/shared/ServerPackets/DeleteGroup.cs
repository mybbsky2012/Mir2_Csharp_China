using System.IO;

namespace ServerPackets;

public sealed class DeleteGroup : Packet
{
	public override short Index => 130;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
