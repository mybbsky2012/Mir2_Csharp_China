using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class Magic : Packet
{
	public Spell Spell;

	public uint TargetID;

	public Point Target;

	public bool Cast;

	public byte Level;

	public List<uint> SecondaryTargetIDs = new List<uint>();

	public override short Index => 118;

	protected override void ReadPacket(BinaryReader reader)
	{
		Spell = (Spell)reader.ReadByte();
		TargetID = reader.ReadUInt32();
		Target = new Point(reader.ReadInt32(), reader.ReadInt32());
		Cast = reader.ReadBoolean();
		Level = reader.ReadByte();
		int num = reader.ReadInt32();
		SecondaryTargetIDs = new List<uint>();
		for (int i = 0; i < num; i++)
		{
			SecondaryTargetIDs.Add(reader.ReadUInt32());
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Spell);
		writer.Write(TargetID);
		writer.Write(Target.X);
		writer.Write(Target.Y);
		writer.Write(Cast);
		writer.Write(Level);
		List<uint> list = SecondaryTargetIDs ?? new List<uint>();
		writer.Write(list.Count);
		foreach (uint item in list)
		{
			writer.Write(item);
		}
	}
}
