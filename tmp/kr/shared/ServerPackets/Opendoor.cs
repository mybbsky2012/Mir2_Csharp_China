using System.IO;

namespace ServerPackets;

public sealed class Opendoor : Packet
{
	public bool Close;

	public byte DoorIndex;

	public override short Index => 251;

	protected override void ReadPacket(BinaryReader reader)
	{
		DoorIndex = reader.ReadByte();
		Close = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(DoorIndex);
		writer.Write(Close);
	}
}
