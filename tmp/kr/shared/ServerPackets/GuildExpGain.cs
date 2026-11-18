using System.IO;

namespace ServerPackets;

public sealed class GuildExpGain : Packet
{
	public uint Amount;

	public override short Index => 168;

	protected override void ReadPacket(BinaryReader reader)
	{
		Amount = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Amount);
	}
}
