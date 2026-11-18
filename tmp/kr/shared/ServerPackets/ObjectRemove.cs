using System.IO;

namespace ServerPackets;

public sealed class ObjectRemove : Packet
{
	public uint ObjectID;

	public override short Index => 26;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
	}
}
