using System.IO;

namespace ServerPackets;

public sealed class ItemRepaired : Packet
{
	public ulong UniqueID;

	public ushort MaxDura;

	public ushort CurrentDura;

	public override short Index => 112;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		MaxDura = reader.ReadUInt16();
		CurrentDura = reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(MaxDura);
		writer.Write(CurrentDura);
	}
}
