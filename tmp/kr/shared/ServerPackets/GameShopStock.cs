using System.IO;

namespace ServerPackets;

public sealed class GameShopStock : Packet
{
	public int GIndex;

	public int StockLevel;

	public override short Index => 249;

	protected override void ReadPacket(BinaryReader reader)
	{
		GIndex = reader.ReadInt32();
		StockLevel = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(GIndex);
		writer.Write(StockLevel);
	}
}
