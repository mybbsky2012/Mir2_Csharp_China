using System.IO;

namespace ServerPackets;

public sealed class PlayerUpdate : Packet
{
	public uint ObjectID;

	public byte Light;

	public short Weapon;

	public short WeaponEffect;

	public short Armour;

	public byte WingEffect;

	public byte ArmourGrade;

	public byte WeaponGrade;

	public override short Index => 54;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Light = reader.ReadByte();
		Weapon = reader.ReadInt16();
		WeaponEffect = reader.ReadInt16();
		Armour = reader.ReadInt16();
		WingEffect = reader.ReadByte();
		ArmourGrade = reader.ReadByte();
		WeaponGrade = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Light);
		writer.Write(Weapon);
		writer.Write(WeaponEffect);
		writer.Write(Armour);
		writer.Write(WingEffect);
		writer.Write(ArmourGrade);
		writer.Write(WeaponGrade);
	}
}
