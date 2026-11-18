using System.IO;

namespace ServerPackets;

public sealed class ObjectProjectile : Packet
{
	public Spell Spell;

	public uint Source;

	public uint Destination;

	public override short Index => 123;

	protected override void ReadPacket(BinaryReader reader)
	{
		Spell = (Spell)reader.ReadByte();
		Source = reader.ReadUInt32();
		Destination = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Spell);
		writer.Write(Source);
		writer.Write(Destination);
	}
}
