using System.IO;

namespace ClientPackets;

public sealed class DowngradeAwakening : Packet
{
	public ulong UniqueID;

	public override short Index => 112;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
	}
}
