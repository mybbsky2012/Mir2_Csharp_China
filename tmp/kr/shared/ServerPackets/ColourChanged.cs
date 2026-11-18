using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ColourChanged : Packet
{
	public Color NameColour;

	public override short Index => 80;

	protected override void ReadPacket(BinaryReader reader)
	{
		NameColour = Color.FromArgb(reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(NameColour.ToArgb());
	}
}
