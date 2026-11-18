using System.IO;

namespace ClientPackets;

public sealed class ConsignItem : Packet
{
	public ulong UniqueID;

	public uint Price;

	public MarketPanelType Type;

	public override short Index => 67;

	protected override void ReadPacket(BinaryReader reader)
	{
		UniqueID = reader.ReadUInt64();
		Price = reader.ReadUInt32();
		Type = (MarketPanelType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(UniqueID);
		writer.Write(Price);
		writer.Write((byte)Type);
	}
}
