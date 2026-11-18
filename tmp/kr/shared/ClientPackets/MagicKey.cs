using System.IO;

namespace ClientPackets;

public sealed class MagicKey : Packet
{
	public Spell Spell;

	public byte Key;

	public byte OldKey;

	public override short Index => 54;

	protected override void ReadPacket(BinaryReader reader)
	{
		Spell = (Spell)reader.ReadByte();
		Key = reader.ReadByte();
		OldKey = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Spell);
		writer.Write(Key);
		writer.Write(OldKey);
	}
}
