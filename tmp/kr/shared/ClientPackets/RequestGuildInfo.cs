using System.IO;

namespace ClientPackets;

public sealed class RequestGuildInfo : Packet
{
	public byte Type;

	public override short Index => 80;

	protected override void ReadPacket(BinaryReader reader)
	{
		Type = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Type);
	}
}
