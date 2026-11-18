using System.IO;

namespace ClientPackets;

public sealed class DeleteMail : Packet
{
	public ulong MailID;

	public override short Index => 117;

	protected override void ReadPacket(BinaryReader reader)
	{
		MailID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MailID);
	}
}
