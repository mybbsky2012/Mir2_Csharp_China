using System.IO;

namespace ServerPackets;

public sealed class ConsignItem : Packet
{
	public ulong UniqueID;

	public bool Success;

	public override short Index => 155;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Success);
	}
}
