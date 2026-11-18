using System.IO;

namespace ServerPackets;

public sealed class GainHeroExperience : Packet
{
	public uint Amount;

	public override short Index => 84;

	protected override void ReadPacket(BinaryReader reader)
	{
		Amount = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Amount);
	}
}
