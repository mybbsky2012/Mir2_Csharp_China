using System.IO;

namespace ServerPackets;

public sealed class GainedCredit : Packet
{
	public uint Credit;

	public override short Index => 67;

	protected override void ReadPacket(BinaryReader reader)
	{
		Credit = reader.ReadUInt32();
	}

	protected override void WritePacket(BinaryWriter writer)
	{
		writer.Write(Credit);
	}
}
