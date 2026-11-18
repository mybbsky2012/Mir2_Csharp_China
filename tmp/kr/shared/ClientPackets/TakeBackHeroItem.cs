using System.IO;

namespace ClientPackets;

public sealed class TakeBackHeroItem : Packet
{
	public int From;

	public int To;

	public override short Index => 32;

	protected override void ReadPacket(BinaryReader reader)
	{
		From = reader.ReadInt32();
		To = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(From);
		writer.Write(To);
	}
}
