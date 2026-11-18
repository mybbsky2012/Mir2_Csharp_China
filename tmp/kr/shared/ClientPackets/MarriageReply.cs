using System.IO;

namespace ClientPackets;

public sealed class MarriageReply : Packet
{
	public bool AcceptInvite;

	public override short Index => 85;

	protected override void ReadPacket(BinaryReader reader)
	{
		AcceptInvite = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AcceptInvite);
	}
}
