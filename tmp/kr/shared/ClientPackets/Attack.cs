using System.IO;

namespace ClientPackets;

public sealed class Attack : Packet
{
	public MirDirection Direction;

	public Spell Spell;

	public override short Index => 44;

	protected override void ReadPacket(BinaryReader reader)
	{
		Direction = (MirDirection)reader.ReadByte();
		Spell = (Spell)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Direction);
		writer.Write((byte)Spell);
	}
}
