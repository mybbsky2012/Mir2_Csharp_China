using System.IO;

namespace ClientPackets;

public sealed class Awakening : Packet
{
	public ulong UniqueID;

	public AwakeType Type;

	public uint PositionIdx;

	public override short Index => 110;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Type = (AwakeType)reader.ReadByte();
		PositionIdx = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write((byte)Type);
		writer.Write(PositionIdx);
	}
}
