using System.IO;

namespace ClientPackets;

public sealed class TradeGold : Packet
{
	public uint Amount;

	public override short Index => 95;

	protected override void ReadPacket(BinaryReader reader)
	{
		Amount = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Amount);
	}
}
