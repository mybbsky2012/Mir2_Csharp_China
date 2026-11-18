using System.Drawing;
using System.IO;

namespace ClientPackets;

public sealed class IntelligentCreaturePickup : Packet
{
	public bool MouseMode;

	public Point Location = new Point(0, 0);

	public override short Index => 122;

	protected override void ReadPacket(BinaryReader reader)
	{
		MouseMode = reader.ReadBoolean();
		Location.X = reader.ReadInt32();
		Location.Y = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(MouseMode);
		writer.Write(Location.X);
		writer.Write(Location.Y);
	}
}
