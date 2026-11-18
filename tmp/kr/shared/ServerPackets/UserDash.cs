using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class UserDash : Packet
{
	public Point Location;

	public MirDirection Direction;

	public override short Index => 148;

	protected override void ReadPacket(BinaryReader reader)
	{
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
	}
}
