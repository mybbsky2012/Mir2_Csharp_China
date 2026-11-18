using System.IO;

namespace ServerPackets;

public sealed class RetrieveTradeItem : Packet
{
	public int From;

	public int To;

	public bool Success;

	public override short Index => 49;

	protected override void ReadPacket(BinaryReader reader)
	{
		From = reader.ReadInt32();
		To = reader.ReadInt32();
		Success = reader.ReadBoolean();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(From);
		writer.Write(To);
		writer.Write(Success);
	}
}
