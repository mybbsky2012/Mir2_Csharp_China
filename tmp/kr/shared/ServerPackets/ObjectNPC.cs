using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectNPC : Packet
{
	public uint ObjectID;

	public string Name = string.Empty;

	public Color NameColour;

	public ushort Image;

	public Color Colour;

	public Point Location;

	public MirDirection Direction;

	public List<int> QuestIDs = new List<int>();

	public override short Index => 90;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Name = reader.ReadString();
		NameColour = Color.FromArgb(reader.ReadInt32());
		Image = reader.ReadUInt16();
		Colour = Color.FromArgb(reader.ReadInt32());
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			QuestIDs.Add(reader.ReadInt32());
		}
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Name);
		writer.Write(NameColour.ToArgb());
		writer.Write(Image);
		writer.Write(Colour.ToArgb());
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
		writer.Write(QuestIDs.Count);
		for (int i = 0; i < QuestIDs.Count; i++)
		{
			writer.Write(QuestIDs[i]);
		}
	}
}
