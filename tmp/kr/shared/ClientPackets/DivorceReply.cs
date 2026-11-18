using System.IO;

namespace ClientPackets;

public sealed class DivorceReply : Packet
{
	public bool AcceptInvite;

	public override short Index => 88;

	protected override void ReadPacket(BinaryReader reader)
	{
		AcceptInvite = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AcceptInvite);
	}
}
