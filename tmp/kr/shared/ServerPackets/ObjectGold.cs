using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class ObjectGold : Packet
{
	public uint ObjectID;

	public uint Gold;

	public Point Location;

	public override short Index => 63;

	protected override void ReadPacket(BinaryReader reader)
	{
		ObjectID = reader.ReadUInt32();
		Gold = reader.ReadUInt32();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ObjectID);
		writer.Write(Gold);
		writer.Write(Location.X);
		writer.Write(Location.Y);
	}
}
