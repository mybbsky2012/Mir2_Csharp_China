using System.IO;

namespace ServerPackets;

public sealed class ObjectTeleportOut : Packet
{
	public uint ObjectID;

	public byte Type;

	public override short Index => 97;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Type = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Type);
	}
}
