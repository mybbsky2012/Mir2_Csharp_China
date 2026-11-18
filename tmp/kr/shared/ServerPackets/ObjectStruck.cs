using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectStruck : Packet
{
	public uint ObjectID;

	public uint AttackerID;

	public Point Location;

	public MirDirection Direction;

	public override short Index => 72;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		AttackerID = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Direction = (MirDirection)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(AttackerID);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Direction);
	}
}
