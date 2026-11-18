using System.IO;

namespace ServerPackets;

public sealed class ObjectHidden : Packet
{
	public uint ObjectID;

	public bool Hidden;

	public override short Index => 145;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Hidden = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Hidden);
	}
}
