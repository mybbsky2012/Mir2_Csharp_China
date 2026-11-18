using System.IO;

namespace ClientPackets;

public sealed class CollectParcel : Packet
{
	public ulong MailID;

	public override short Index => 116;

	protected override void ReadPacket(BinaryReader reader)
	{
		MailID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MailID);
	}
}
