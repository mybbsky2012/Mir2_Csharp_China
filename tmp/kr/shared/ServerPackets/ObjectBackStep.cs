using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectBackStep : Packet
{
	public uint ObjectID;

	public Point Location;

	public MirDirection Direction;

	public int Distance;

	public override short Index => 208;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
		Distance = reader.ReadInt16();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
		writer.Write(Distance);
	}
}
