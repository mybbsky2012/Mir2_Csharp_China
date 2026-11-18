using System.IO;

namespace ClientPackets;

public sealed class MergeItem : Packet
{
	public MirGridType GridFrom;

	public MirGridType GridTo;

	public ulong IDFrom;

	public ulong IDTo;

	public override short Index => 17;

	protected override void ReadPacket(BinaryReader reader)
	{
		GridFrom = (MirGridType)reader.ReadByte();
		GridTo = (MirGridType)reader.ReadByte();
		IDFrom = reader.ReadUInt64();
		IDTo = reader.ReadUInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)GridFrom);
		writer.Write((byte)GridTo);
		writer.Write(IDFrom);
		writer.Write(IDTo);
	}
}
