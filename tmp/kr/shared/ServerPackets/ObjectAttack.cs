using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectAttack : Packet
{
	public uint ObjectID;

	public Point Location;

	public MirDirection Direction;

	public Spell Spell;

	public byte Level;

	public byte Type;

	public override short Index => 70;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
		Spell = (Spell)reader.ReadByte();
		Level = reader.ReadByte();
		Type = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
		writer.Write((byte)Spell);
		writer.Write(Level);
		writer.Write(Type);
	}
}
