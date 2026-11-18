using System.IO;

namespace ClientPackets;

public sealed class DeleteAllMail : Packet
{
	public override short Index => 146;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
