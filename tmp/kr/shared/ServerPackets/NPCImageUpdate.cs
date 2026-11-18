using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class NPCImageUpdate : Packet
{
	public long ObjectID;

	public ushort Image;

	public Color Colour;

	public override short Index => 186;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadInt64();
		Image = reader.ReadUInt16();
		Colour = Color.FromArgb(reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Image);
		writer.Write(Colour.ToArgb());
	}
}
