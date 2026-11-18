using System.IO;

namespace ServerPackets;

public sealed class LoseGold : Packet
{
	public uint Gold;

	public override short Index => 66;

	protected override void ReadPacket(BinaryReader reader)
	{
		Gold = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Gold);
	}
}
