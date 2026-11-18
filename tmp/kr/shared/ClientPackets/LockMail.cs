using System.IO;

namespace ClientPackets;

public sealed class LockMail : Packet
{
	public ulong MailID;

	public bool Lock;

	public override short Index => 118;

	protected override void ReadPacket(BinaryReader reader)
	{
		MailID = reader.ReadUInt64();
		Lock = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MailID);
		writer.Write(Lock);
	}
}
