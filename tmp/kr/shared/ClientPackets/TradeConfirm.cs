using System.IO;

namespace ClientPackets;

public sealed class TradeConfirm : Packet
{
	public bool Locked;

	public override short Index => 96;

	protected override void ReadPacket(BinaryReader reader)
	{
		Locked = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Locked);
	}
}
