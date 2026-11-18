using System.IO;

namespace ServerPackets;

public sealed class TransformUpdate : Packet
{
	public long ObjectID;

	public short TransformType;

	public override short Index => 242;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadInt64();
		TransformType = reader.ReadInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(TransformType);
	}
}
