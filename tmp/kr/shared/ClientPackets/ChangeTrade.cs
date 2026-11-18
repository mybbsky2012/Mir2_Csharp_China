using System.IO;

namespace ClientPackets;

public sealed class ChangeTrade : Packet
{
	public bool AllowTrade;

	public override short Index => 43;

	protected override void ReadPacket(BinaryReader reader)
	{
		AllowTrade = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(AllowTrade);
	}
}
