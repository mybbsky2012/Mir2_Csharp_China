using System.IO;

namespace ServerPackets;

public sealed class CombineItem : Packet
{
	public MirGridType Grid;

	public ulong IDFrom;

	public ulong IDTo;

	public bool Success;

	public bool Destroy;

	public override short Index => 212;

	protected override void ReadPacket(BinaryReader reader)
	{
		Grid = (MirGridType)reader.ReadByte();
		IDFrom = reader.ReadUInt64();
		IDTo = reader.ReadUInt64();
		Success = reader.ReadBoolean();
		Destroy = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Grid);
		writer.Write(IDFrom);
		writer.Write(IDTo);
		writer.Write(Success);
		writer.Write(Destroy);
	}
}
