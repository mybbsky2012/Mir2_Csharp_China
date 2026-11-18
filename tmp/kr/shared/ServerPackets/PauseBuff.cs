using System.IO;

namespace ServerPackets;

public sealed class PauseBuff : Packet
{
	public BuffType Type;

	public uint ObjectID;

	public bool Paused;

	public override short Index => 144;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = (BuffType)reader.ReadByte();
		ObjectID = reader.ReadUInt32();
		Paused = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Type);
		writer.Write(ObjectID);
		writer.Write(Paused);
	}
}
