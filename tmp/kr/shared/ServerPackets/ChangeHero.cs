using System.IO;

namespace ServerPackets;

public sealed class ChangeHero : Packet
{
	public int FromIndex;

	public override short Index => 183;

	protected override void ReadPacket(BinaryReader reader)
	{
		FromIndex = reader.ReadInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(FromIndex);
	}
}
