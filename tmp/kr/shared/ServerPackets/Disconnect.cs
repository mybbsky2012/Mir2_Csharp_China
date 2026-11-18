using System.IO;

namespace ServerPackets;

public sealed class Disconnect : Packet
{
	public byte Reason;

	public override short Index => 2;

	protected override void ReadPacket(BinaryReader reader)
	{
		Reason = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Reason);
	}
}
