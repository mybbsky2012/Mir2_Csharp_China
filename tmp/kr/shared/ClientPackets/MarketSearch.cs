using System.IO;

namespace ClientPackets;

public sealed class MarketSearch : Packet
{
	public string Match = string.Empty;

	public ItemType Type;

	public bool Usermode;

	public short MinShape;

	public short MaxShape = 5000;

	public MarketPanelType MarketType;

	public override short Index => 68;

	protected override void ReadPacket(BinaryReader reader)
	{
		Match = reader.ReadString();
		Type = (ItemType)reader.ReadByte();
		Usermode = reader.ReadBoolean();
		MinShape = reader.ReadInt16();
		MaxShape = reader.ReadInt16();
		MarketType = (MarketPanelType)reader.ReadByte();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Match);
		writer.Write((byte)Type);
		writer.Write(Usermode);
		writer.Write(MinShape);
		writer.Write(MaxShape);
		writer.Write((byte)MarketType);
	}
}
