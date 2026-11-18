using System.IO;

namespace ClientPackets;

public sealed class AwakeningLockedItem : Packet
{
	public ulong UniqueID;

	public bool Locked;

	public override short Index => 109;

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
