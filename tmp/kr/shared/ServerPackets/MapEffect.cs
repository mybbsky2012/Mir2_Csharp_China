using System.Drawing;
using System.IO;

namespace ServerPackets;

public sealed class MapEffect : Packet
{
	public Point Location;

	public SpellEffect Effect;

	public byte Value;

	public override short Index => 139;

	protected override void ReadPacket(BinaryReader reader)
	{
		Location = new Point(reader.ReadInt32(), reader.ReadInt32());
		Effect = (SpellEffect)reader.ReadByte();
		Value = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Location.X);
		writer.Write(Location.Y);
		writer.Write((byte)Effect);
		writer.Write(Value);
	}
}
