using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectColourChanged : Packet
{
	public uint ObjectID;

	public Color NameColour;

	public override short Index => 81;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		NameColour = Color.FromArgb(reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(NameColour.ToArgb());
	}
}
