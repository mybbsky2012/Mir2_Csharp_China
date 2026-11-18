using System.IO;

namespace ServerPackets;

public sealed class MagicLeveled : Packet
{
	public uint ObjectID;

	public Spell Spell;

	public byte Level;

	public ushort Experience;

	public override short Index => 117;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Spell = (Spell)reader.ReadByte();
		Level = reader.ReadByte();
		Experience = reader.ReadUInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((byte)Spell);
		writer.Write(Level);
		writer.Write(Experience);
	}
}
