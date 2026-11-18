using System.IO;

namespace ServerPackets;

public sealed class MagicDelay : Packet
{
	public uint ObjectID;

	public Spell Spell;

	public long Delay;

	public override short Index => 119;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Spell = (Spell)reader.ReadByte();
		Delay = reader.ReadInt64();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((byte)Spell);
		writer.Write(Delay);
	}
}
