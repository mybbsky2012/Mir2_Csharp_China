using System.IO;

namespace ClientPackets;

public sealed class AwakeningNeedMaterials : Packet
{
	public ulong UniqueID;

	public AwakeType Type;

	public override short Index => 108;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Type = (AwakeType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write((byte)Type);
	}
}
