using System.Drawing;
using System.IO;

namespace ClientPackets;

public sealed class Magic : Packet
{
	public Spell Spell;

	public MirDirection Direction;

	public uint TargetID;

	public Point Location;

	public uint ObjectID;

	public bool SpellTargetLock;

	public override short Index => 55;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Spell = (Spell)reader.ReadByte();
		Direction = (MirDirection)reader.ReadByte();
		TargetID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		SpellTargetLock = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write((byte)Spell);
		writer.Write((byte)Direction);
		writer.Write(TargetID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write(SpellTargetLock);
	}
}
