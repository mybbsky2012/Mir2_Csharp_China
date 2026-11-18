using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectRangeAttack : Packet
{
	public uint ObjectID;

	public Point Location;

	public MirDirection Direction;

	public uint TargetID;

	public Point Target;

	public byte Type;

	public Spell Spell;

	public byte Level;

	public override short Index => 141;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
		TargetID = reader.ReadUInt32();
		Target = new Point(reader.ReadInt32(), reader.ReadInt32());
		Type = reader.ReadByte();
		Spell = (Spell)reader.ReadByte();
		Level = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
		writer.Write(TargetID);
		writer.Write(Target.X);
		writer.Write(Target.Y);
		writer.Write(Type);
		writer.Write((byte)Spell);
		writer.Write(Level);
	}
}
