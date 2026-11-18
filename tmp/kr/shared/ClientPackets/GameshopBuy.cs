using System.IO;

namespace ClientPackets;

public sealed class GameshopBuy : Packet
{
	public int GIndex;

	public byte Quantity;

	public int PType;

	public override short Index => 130;

	protected override void ReadPacket(BinaryReader reader)
	{
		GIndex = reader.ReadInt32();
		Quantity = reader.ReadByte();
		PType = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(GIndex);
		writer.Write(Quantity);
		writer.Write(PType);
	}
}
