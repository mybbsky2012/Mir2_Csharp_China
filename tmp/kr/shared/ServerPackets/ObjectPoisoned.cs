using System.IO;

namespace ServerPackets;

public sealed class ObjectPoisoned : Packet
{
	public uint ObjectID;

	public PoisonType Poison;

	public override short Index => 95;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Poison = (PoisonType)reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((ushort)Poison);
	}
}
