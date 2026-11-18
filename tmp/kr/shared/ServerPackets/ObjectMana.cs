using System.IO;

namespace ServerPackets;

public sealed class ObjectMana : Packet
{
	public uint ObjectID;

	public byte Percent;

	public override short Index => 138;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Percent = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Percent);
	}
}
