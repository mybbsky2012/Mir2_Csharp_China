using System.IO;

namespace ServerPackets;

public sealed class MarketSuccess : Packet
{
	public string Message = string.Empty;

	public override short Index => 157;

	protected override void ReadPacket(BinaryReader reader)
	{
		Message = reader.ReadString();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Message);
	}
}
