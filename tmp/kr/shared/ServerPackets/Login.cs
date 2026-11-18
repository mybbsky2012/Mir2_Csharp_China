using System.IO;

namespace ServerPackets;

public sealed class Login : Packet
{
	public byte Result;

	public override short Index => 7;

	protected override void ReadPacket(BinaryReader reader)
	{
		Result = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Result);
	}
}
