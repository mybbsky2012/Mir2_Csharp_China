using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class SetCompass : Packet
{
	public Point Location;

	public override short Index => 271;

	protected override void ReadPacket(BinaryReader reader)
	{
		int x = reader.ReadInt32();
		int y = reader.ReadInt32();
		Location = new Point(x, y);
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Location.X);
		writer.Write(Location.Y);
	}
}
