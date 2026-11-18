using System.IO;

namespace ClientPackets;

public sealed class ChangeHero : Packet
{
	public int ListIndex;

	public override short Index => 64;

	protected override void ReadPacket(BinaryReader reader)
	{
		ListIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(ListIndex);
	}
}
