using System.IO;

namespace ServerPackets;

public sealed class ObjectName : Packet
{
	public uint ObjectID;

	public string Name = string.Empty;

	public override short Index => 127;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Name = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Name);
	}
}
