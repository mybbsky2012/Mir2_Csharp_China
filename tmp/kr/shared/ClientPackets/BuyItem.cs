using System.IO;

namespace ClientPackets;

public sealed class BuyItem : Packet
{
	public ulong ItemIndex;

	public ushort Count;

	public PanelType Type;

	public override short Index => 48;

	protected override void ReadPacket(BinaryReader reader)
	{
		ItemIndex = reader.ReadUInt64();
		Count = reader.ReadUInt16();
		Type = (PanelType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ItemIndex);
		writer.Write(Count);
		writer.Write((byte)Type);
	}
}
