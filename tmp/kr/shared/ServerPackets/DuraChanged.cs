using System.IO;

namespace ServerPackets;

public sealed class DuraChanged : Packet
{
	public ulong UniqueID;

	public ushort CurrentDura;

	public override short Index => 74;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		CurrentDura = reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(CurrentDura);
	}
}
