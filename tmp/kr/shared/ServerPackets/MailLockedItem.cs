using System.IO;

namespace ServerPackets;

public sealed class MailLockedItem : Packet
{
	public ulong UniqueID;

	public bool Locked;

	public override short Index => 230;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Locked = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Locked);
	}
}
