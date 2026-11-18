using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectTurn : Packet
{
	public uint ObjectID;

	public Point Location;

	public MirDirection Direction;

	public override short Index => 27;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
	}
}
