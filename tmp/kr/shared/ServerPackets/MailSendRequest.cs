using System.IO;

namespace ServerPackets;

public sealed class MailSendRequest : Packet
{
	public override short Index => 231;

	protected override void ReadPacket(BinaryReader reader)
	{
	}

	protected override void WritePacket(BinaryWriter writer)
	{
	}
}
