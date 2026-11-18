using System.IO;

namespace ServerPackets;

public sealed class SpellToggle : Packet
{
	public uint ObjectID;

	public Spell Spell;

	public bool CanUse;

	public override short Index => 136;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Spell = (Spell)reader.ReadByte();
		CanUse = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((byte)Spell);
		writer.Write(CanUse);
	}
}
