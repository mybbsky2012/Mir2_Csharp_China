using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectSpell : Packet
{
	public uint ObjectID;

	public Point Location;

	public Spell Spell;

	public MirDirection Direction;

	public bool Param;

	public override short Index => 147;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Spell = (Spell)reader.ReadByte();
		Direction = (MirDirection)reader.ReadByte();
		Param = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Spell);
		writer.Write((byte)Direction);
		writer.Write(Param);
	}
}
