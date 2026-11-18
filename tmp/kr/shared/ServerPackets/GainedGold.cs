using System.IO;

namespace ServerPackets;

public sealed class GainedGold : Packet
{
	public uint Gold;

	public override short Index => 65;

	protected override void ReadPacket(BinaryReader reader)
	{
		Gold = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Gold);
	}
}
