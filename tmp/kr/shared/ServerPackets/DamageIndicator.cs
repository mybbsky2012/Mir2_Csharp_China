using System.IO;

namespace ServerPackets;

public sealed class DamageIndicator : Packet
{
	public int Damage;

	public DamageType Type;

	public uint ObjectID;

	public override short Index => 73;

	protected override void ReadPacket(BinaryReader reader)
	{
		Damage = reader.ReadInt32();
		Type = (DamageType)reader.ReadByte();
		ObjectID = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Damage);
		writer.Write((byte)Type);
		writer.Write(ObjectID);
	}
}
