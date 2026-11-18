using System.IO;

namespace ServerPackets;

public sealed class MarketFail : Packet
{
	public byte Reason;

	public override short Index => 156;

	protected override void ReadPacket(BinaryReader reader)
	{
		Reason = reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Reason);
	}
}
