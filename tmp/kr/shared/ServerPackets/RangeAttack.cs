using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class RangeAttack : Packet
{
	public uint TargetID;

	public Point Target;

	public Spell Spell;

	public override short Index => 124;

	protected override void ReadPacket(BinaryReader reader)
	{
		TargetID = reader.ReadUInt32();
		Target = new Point(reader.ReadInt32(), reader.ReadInt32());
		Spell = (Spell)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(TargetID);
		writer.Write(Target.X);
		writer.Write(Target.Y);
		writer.Write((byte)Spell);
	}
}
