using System.IO;

namespace ClientPackets;

public sealed class Run : Packet
{
	public MirDirection Direction;

	public override short Index => 12;

	protected override void ReadPacket(BinaryReader reader)
	{
		Direction = (MirDirection)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write((byte)Direction);
	}
}
