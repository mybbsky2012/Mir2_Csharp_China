using System.Drawing;
using System.IO;

namespace ClientPackets;

public sealed class RangeAttack : Packet
{
	public MirDirection Direction;

	public Point Location;

	public uint TargetID;

	public Point TargetLocation;

	public override short Index => 45;

	protected override void ReadPacket(BinaryReader reader)
	{
		Direction = (MirDirection)reader.ReadByte();
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		TargetID = reader.ReadUInt32();
		TargetLocation = new Point(reader.ReadInt32(), reader.ReadInt32());
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Direction);
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write(TargetID);
		writer.Write(TargetLocation.X);
		writer.Write(TargetLocation.Y);
	}
}
