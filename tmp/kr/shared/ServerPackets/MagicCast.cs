using System.IO;

namespace ServerPackets;

public sealed class MagicCast : Packet
{
	public Spell Spell;

	public override short Index => 120;

	protected override void ReadPacket(BinaryReader reader)
	{
		Spell = (Spell)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Spell);
	}
}
