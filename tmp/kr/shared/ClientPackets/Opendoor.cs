using System.IO;

namespace ClientPackets;

public sealed class Opendoor : Packet
{
	public byte DoorIndex;

	public override short Index => 133;

	protected override void ReadPacket(BinaryReader reader)
	{
		DoorIndex = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(DoorIndex);
	}
}
