using System.IO;

namespace ServerPackets;

public sealed class ObjectRevived : Packet
{
	public uint ObjectID;

	public bool Effect;

	public override short Index => 135;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Effect = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Effect);
	}
}
