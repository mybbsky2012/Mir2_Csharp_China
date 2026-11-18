using System.IO;

namespace ServerPackets;

public sealed class RemoveBuff : Packet
{
	public BuffType Type;

	public uint ObjectID;

	public override short Index => 143;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = (BuffType)reader.ReadByte();
		ObjectID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Type);
		writer.Write(ObjectID);
	}
}
