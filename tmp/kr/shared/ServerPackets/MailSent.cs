using System.IO;

namespace ServerPackets;

public sealed class MailSent : Packet
{
	public sbyte Result;

	public override short Index => 232;

	protected override void ReadPacket(BinaryReader reader)
	{
		Result = reader.ReadSByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Result);
	}
}
