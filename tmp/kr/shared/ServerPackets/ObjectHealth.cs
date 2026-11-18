using System.IO;

namespace ServerPackets;

public sealed class ObjectHealth : Packet
{
	public uint ObjectID;

	public byte Percent;

	public byte Expire;

	public int CurrentHP;

	public int MaxHP;

	public override short Index => 137;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Percent = reader.ReadByte();
		Expire = reader.ReadByte();
		CurrentHP = reader.ReadInt32();
		MaxHP = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Percent);
		writer.Write(Expire);
		writer.Write(CurrentHP);
		writer.Write(MaxHP);
	}
}
